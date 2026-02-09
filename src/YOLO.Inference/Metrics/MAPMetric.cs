namespace YOLO.Inference.Metrics;

/// <summary>
/// Mean Average Precision (mAP) metric for object detection evaluation.
/// Computes mAP@0.5 and mAP@0.5:0.95 following COCO evaluation protocol.
/// </summary>
public class MAPMetric
{
    private readonly double[] iouThresholds;
    private readonly int numClasses;

    // Per-image accumulator: (className, confidence, isTP[per IoU threshold])
    private readonly List<(int classId, double confidence, bool[] tp)> predictions = new();
    private readonly int[] gtCounts; // number of GT boxes per class

    public MAPMetric(int numClasses, double[]? iouThresholds = null)
    {
        this.numClasses = numClasses;
        this.iouThresholds = iouThresholds ??
            Enumerable.Range(0, 10).Select(i => 0.5 + i * 0.05).ToArray();
        gtCounts = new int[numClasses];
    }

    /// <summary>
    /// Add predictions and ground truths for one image.
    /// </summary>
    /// <param name="predBoxes">Predicted boxes (M, 4) xyxy</param>
    /// <param name="predScores">Predicted confidence scores (M,)</param>
    /// <param name="predClasses">Predicted class IDs (M,)</param>
    /// <param name="gtBoxes">Ground truth boxes (N, 4) xyxy</param>
    /// <param name="gtClasses">Ground truth class IDs (N,)</param>
    public void Update(
        float[,] predBoxes, float[] predScores, int[] predClasses,
        float[,] gtBoxes, int[] gtClasses)
    {
        int numGT = gtClasses.Length;
        int numPred = predClasses.Length;

        // Count GTs per class
        foreach (var cls in gtClasses)
        {
            if (cls >= 0 && cls < numClasses)
                gtCounts[cls]++;
        }

        if (numPred == 0) return;

        // Compute IoU matrix (numPred x numGT)
        var iouMatrix = new float[numPred, numGT];
        for (int i = 0; i < numPred; i++)
        {
            for (int j = 0; j < numGT; j++)
            {
                iouMatrix[i, j] = ComputeIoU(
                    predBoxes[i, 0], predBoxes[i, 1], predBoxes[i, 2], predBoxes[i, 3],
                    gtBoxes[j, 0], gtBoxes[j, 1], gtBoxes[j, 2], gtBoxes[j, 3]);
            }
        }

        // Match predictions to GTs for each IoU threshold
        for (int p = 0; p < numPred; p++)
        {
            var tp = new bool[iouThresholds.Length];

            for (int t = 0; t < iouThresholds.Length; t++)
            {
                double thresh = iouThresholds[t];
                int bestGT = -1;
                double bestIoU = thresh;

                for (int g = 0; g < numGT; g++)
                {
                    if (predClasses[p] != gtClasses[g]) continue;
                    if (iouMatrix[p, g] > bestIoU)
                    {
                        bestIoU = iouMatrix[p, g];
                        bestGT = g;
                    }
                }

                tp[t] = bestGT >= 0;
            }

            predictions.Add((predClasses[p], predScores[p], tp));
        }
    }

    /// <summary>
    /// Compute mAP metrics.
    /// </summary>
    /// <returns>Tuple of (mAP@0.5, mAP@0.5:0.95, per-class AP@0.5)</returns>
    public (double map50, double map5095, double[] perClassAP50) Compute()
    {
        var ap50 = new double[numClasses];
        var ap = new double[numClasses];

        for (int c = 0; c < numClasses; c++)
        {
            if (gtCounts[c] == 0) continue;

            // Get predictions for this class, sorted by confidence (descending)
            var classPreds = predictions
                .Where(p => p.classId == c)
                .OrderByDescending(p => p.confidence)
                .ToList();

            if (classPreds.Count == 0) continue;

            for (int t = 0; t < iouThresholds.Length; t++)
            {
                var tpCum = new double[classPreds.Count];
                var fpCum = new double[classPreds.Count];

                double tpCount = 0, fpCount = 0;
                for (int i = 0; i < classPreds.Count; i++)
                {
                    if (classPreds[i].tp[t])
                        tpCount++;
                    else
                        fpCount++;

                    tpCum[i] = tpCount;
                    fpCum[i] = fpCount;
                }

                // Precision and Recall curves
                var precision = new double[classPreds.Count];
                var recall = new double[classPreds.Count];

                for (int i = 0; i < classPreds.Count; i++)
                {
                    precision[i] = tpCum[i] / (tpCum[i] + fpCum[i]);
                    recall[i] = tpCum[i] / gtCounts[c];
                }

                // Compute AP via 101-point interpolation
                double classAP = ComputeAP(recall, precision);

                if (t == 0) // IoU = 0.5
                    ap50[c] = classAP;

                ap[c] += classAP;
            }

            ap[c] /= iouThresholds.Length;
        }

        // Compute mean over classes with GT
        int numActiveClasses = gtCounts.Count(c => c > 0);
        double map50 = numActiveClasses > 0 ? ap50.Sum() / numActiveClasses : 0;
        double map5095 = numActiveClasses > 0 ? ap.Sum() / numActiveClasses : 0;

        return (map50, map5095, ap50);
    }

    /// <summary>
    /// Compute AP using 101-point interpolation (COCO style).
    /// </summary>
    private static double ComputeAP(double[] recall, double[] precision)
    {
        if (recall.Length == 0) return 0;

        // Prepend (0, 1) and append (1, 0) sentinel values
        var mrec = new double[recall.Length + 2];
        var mpre = new double[precision.Length + 2];

        mrec[0] = 0;
        mrec[^1] = 1;
        mpre[0] = 1;
        mpre[^1] = 0;

        Array.Copy(recall, 0, mrec, 1, recall.Length);
        Array.Copy(precision, 0, mpre, 1, precision.Length);

        // Make precision monotonically decreasing (envelope)
        for (int i = mpre.Length - 2; i >= 0; i--)
            mpre[i] = Math.Max(mpre[i], mpre[i + 1]);

        // 101-point interpolation
        double ap = 0;
        var recallPoints = Enumerable.Range(0, 101).Select(i => i / 100.0).ToArray();

        foreach (var r in recallPoints)
        {
            // Find first index where mrec >= r
            double p = 0;
            for (int i = 0; i < mrec.Length; i++)
            {
                if (mrec[i] >= r)
                {
                    p = mpre[i];
                    break;
                }
            }
            ap += p;
        }

        return ap / 101.0;
    }

    /// <summary>
    /// Compute IoU between two boxes in xyxy format.
    /// </summary>
    private static float ComputeIoU(float x1a, float y1a, float x2a, float y2a,
                                     float x1b, float y1b, float x2b, float y2b)
    {
        float interX1 = Math.Max(x1a, x1b);
        float interY1 = Math.Max(y1a, y1b);
        float interX2 = Math.Min(x2a, x2b);
        float interY2 = Math.Min(y2a, y2b);

        float inter = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
        float areaA = (x2a - x1a) * (y2a - y1a);
        float areaB = (x2b - x1b) * (y2b - y1b);

        return inter / (areaA + areaB - inter + 1e-7f);
    }

    /// <summary>
    /// Reset the metric accumulator.
    /// </summary>
    public void Reset()
    {
        predictions.Clear();
        Array.Clear(gtCounts);
    }
}
