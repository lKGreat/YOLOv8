using System.Diagnostics;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using YOLO.MLNet.Data;

namespace YOLO.MLNet.Evaluation;

/// <summary>
/// 目标检测评估结果。
/// </summary>
public record MLNetEvalResult
{
    /// <summary>mAP@0.5</summary>
    public double Map50 { get; init; }

    /// <summary>mAP@0.5:0.95</summary>
    public double Map5095 { get; init; }

    /// <summary>每类 AP@0.5</summary>
    public double[] PerClassAP50 { get; init; } = [];

    /// <summary>总精确率</summary>
    public double Precision { get; init; }

    /// <summary>总召回率</summary>
    public double Recall { get; init; }

    /// <summary>总检测数</summary>
    public int TotalDetections { get; init; }

    /// <summary>总真值框数</summary>
    public int TotalGroundTruth { get; init; }

    /// <summary>平均推理时间 (ms/image)</summary>
    public double AvgInferenceMs { get; init; }

    /// <summary>类别名称</summary>
    public string[] ClassNames { get; init; } = [];

    /// <summary>评估图像数量</summary>
    public int ImageCount { get; init; }
}

/// <summary>
/// ML.NET 目标检测评估器。
///
/// 支持验证和测试评估, 计算:
///   - mAP@0.5 / mAP@0.5:0.95
///   - Per-class AP
///   - Precision / Recall
///   - 推理速度
/// </summary>
public static class MLNetEvaluator
{
    /// <summary>
    /// 使用训练好的 transformer 评估验证/测试数据。
    /// </summary>
    public static MLNetEvalResult Evaluate(
        MLContext mlContext,
        ITransformer transformer,
        IDataView evalData,
        string[]? classNames = null)
    {
        var sw = Stopwatch.StartNew();

        // 用模型对评估数据做推理
        var predictions = transformer.Transform(evalData);
        sw.Stop();

        // 获取预测结果和真值
        var columnsNeeded = new[]
        {
            predictions.Schema["PredictedLabel"],
            predictions.Schema["PredictedBoundingBoxes"],
            predictions.Schema["Score"],
            predictions.Schema["Label"],
            predictions.Schema["BoundingBoxes"]
        };
        var predCursor = predictions.GetRowCursor(columnsNeeded);

        var predLabelGetter = predCursor.GetGetter<VBuffer<uint>>(predictions.Schema["PredictedLabel"]);
        var predBoxGetter = predCursor.GetGetter<VBuffer<float>>(predictions.Schema["PredictedBoundingBoxes"]);
        var scoreGetter = predCursor.GetGetter<VBuffer<float>>(predictions.Schema["Score"]);
        var gtLabelGetter = predCursor.GetGetter<VBuffer<uint>>(predictions.Schema["Label"]);
        var gtBoxGetter = predCursor.GetGetter<VBuffer<float>>(predictions.Schema["BoundingBoxes"]);

        // 收集所有图像的预测和真值
        var allPredictions = new List<DetectionPrediction>();
        var allGroundTruths = new List<DetectionGroundTruth>();
        int imageCount = 0;
        int totalPreds = 0;
        int totalGTs = 0;

        while (predCursor.MoveNext())
        {
            int imgIdx = imageCount++;

            VBuffer<uint> predLabels = default;
            predLabelGetter(ref predLabels);

            VBuffer<float> predBoxes = default;
            predBoxGetter(ref predBoxes);

            VBuffer<float> scores = default;
            scoreGetter(ref scores);

            VBuffer<uint> gtLabels = default;
            gtLabelGetter(ref gtLabels);

            VBuffer<float> gtBoxes = default;
            gtBoxGetter(ref gtBoxes);

            // 解析预测
            var pLabels = predLabels.DenseValues().ToArray();
            var pBoxes = predBoxes.DenseValues().ToArray();
            var pScores = scores.DenseValues().ToArray();

            for (int i = 0; i < pLabels.Length; i++)
            {
                if (i < pScores.Length)
                {
                    int boxIdx = i * 4;
                    if (boxIdx + 3 < pBoxes.Length)
                    {
                        allPredictions.Add(new DetectionPrediction
                        {
                            ImageIndex = imgIdx,
                            ClassId = (int)pLabels[i] - 1, // 转回 0-based
                            Confidence = pScores[i],
                            X0 = pBoxes[boxIdx],
                            Y0 = pBoxes[boxIdx + 1],
                            X1 = pBoxes[boxIdx + 2],
                            Y1 = pBoxes[boxIdx + 3]
                        });
                        totalPreds++;
                    }
                }
            }

            // 解析真值
            var gLabels = gtLabels.DenseValues().ToArray();
            var gBoxes = gtBoxes.DenseValues().ToArray();

            for (int i = 0; i < gLabels.Length; i++)
            {
                int boxIdx = i * 4;
                if (boxIdx + 3 < gBoxes.Length)
                {
                    allGroundTruths.Add(new DetectionGroundTruth
                    {
                        ImageIndex = imgIdx,
                        ClassId = (int)gLabels[i] - 1, // 转回 0-based
                        X0 = gBoxes[boxIdx],
                        Y0 = gBoxes[boxIdx + 1],
                        X1 = gBoxes[boxIdx + 2],
                        Y1 = gBoxes[boxIdx + 3]
                    });
                    totalGTs++;
                }
            }
        }

        // 计算 mAP
        int numClasses = Math.Max(
            allPredictions.Count > 0 ? allPredictions.Max(p => p.ClassId) + 1 : 0,
            allGroundTruths.Count > 0 ? allGroundTruths.Max(g => g.ClassId) + 1 : 0);

        if (classNames != null)
            numClasses = Math.Max(numClasses, classNames.Length);

        var (map50, map5095, perClassAP50) = ComputeMAP(
            allPredictions, allGroundTruths, numClasses);

        // 计算 Precision / Recall
        var (precision, recall) = ComputePrecisionRecall(
            allPredictions, allGroundTruths, iouThreshold: 0.5);

        double avgMs = imageCount > 0 ? sw.Elapsed.TotalMilliseconds / imageCount : 0;

        return new MLNetEvalResult
        {
            Map50 = map50,
            Map5095 = map5095,
            PerClassAP50 = perClassAP50,
            Precision = precision,
            Recall = recall,
            TotalDetections = totalPreds,
            TotalGroundTruth = totalGTs,
            AvgInferenceMs = avgMs,
            ClassNames = classNames ?? [],
            ImageCount = imageCount
        };
    }

    /// <summary>
    /// 计算 mAP@0.5 和 mAP@0.5:0.95。
    /// </summary>
    private static (double map50, double map5095, double[] perClassAP50) ComputeMAP(
        List<DetectionPrediction> predictions,
        List<DetectionGroundTruth> groundTruths,
        int numClasses)
    {
        if (groundTruths.Count == 0)
            return (0, 0, new double[numClasses]);

        // IoU thresholds: 0.5, 0.55, ..., 0.95
        double[] iouThresholds = [0.5, 0.55, 0.6, 0.65, 0.7, 0.75, 0.8, 0.85, 0.9, 0.95];
        var perClassAP50 = new double[numClasses];
        var perClassAPAll = new double[numClasses];

        // 按置信度排序
        var sortedPreds = predictions.OrderByDescending(p => p.Confidence).ToList();

        for (int c = 0; c < numClasses; c++)
        {
            var classPreds = sortedPreds.Where(p => p.ClassId == c).ToList();
            var classGTs = groundTruths.Where(g => g.ClassId == c).ToList();

            if (classGTs.Count == 0)
            {
                perClassAP50[c] = 0;
                continue;
            }

            // 计算每个 IoU 阈值的 AP
            double[] aps = new double[iouThresholds.Length];
            for (int t = 0; t < iouThresholds.Length; t++)
            {
                aps[t] = ComputeAP(classPreds, classGTs, iouThresholds[t]);
            }

            perClassAP50[c] = aps[0];
            perClassAPAll[c] = aps.Average();
        }

        // 只对有 GT 的类计算 mAP
        var classesWithGT = Enumerable.Range(0, numClasses)
            .Where(c => groundTruths.Any(g => g.ClassId == c))
            .ToList();

        double map50 = classesWithGT.Count > 0
            ? classesWithGT.Average(c => perClassAP50[c])
            : 0;

        double map5095 = classesWithGT.Count > 0
            ? classesWithGT.Average(c => perClassAPAll[c])
            : 0;

        return (map50, map5095, perClassAP50);
    }

    /// <summary>
    /// 计算单个类别在指定 IoU 阈值下的 AP (Average Precision)。
    /// 使用 101-point 插值法。
    /// </summary>
    private static double ComputeAP(
        List<DetectionPrediction> predictions,
        List<DetectionGroundTruth> groundTruths,
        double iouThreshold)
    {
        int nGT = groundTruths.Count;
        if (nGT == 0) return 0;

        // 按图像分组 GT
        var gtByImage = groundTruths.GroupBy(g => g.ImageIndex)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 跟踪已匹配的 GT
        var matched = new HashSet<(int imgIdx, int gtIdx)>();

        var tp = new List<int>(); // 1 = true positive, 0 = false positive
        var conf = new List<float>();

        foreach (var pred in predictions)
        {
            conf.Add(pred.Confidence);

            if (!gtByImage.TryGetValue(pred.ImageIndex, out var imgGTs))
            {
                tp.Add(0);
                continue;
            }

            // 找最大 IoU 的 GT
            double bestIoU = 0;
            int bestGTIdx = -1;
            for (int j = 0; j < imgGTs.Count; j++)
            {
                double iou = ComputeIoU(
                    pred.X0, pred.Y0, pred.X1, pred.Y1,
                    imgGTs[j].X0, imgGTs[j].Y0, imgGTs[j].X1, imgGTs[j].Y1);

                if (iou > bestIoU)
                {
                    bestIoU = iou;
                    bestGTIdx = j;
                }
            }

            if (bestIoU >= iouThreshold && bestGTIdx >= 0 &&
                !matched.Contains((pred.ImageIndex, bestGTIdx)))
            {
                tp.Add(1);
                matched.Add((pred.ImageIndex, bestGTIdx));
            }
            else
            {
                tp.Add(0);
            }
        }

        // 计算 precision-recall 曲线
        var cumTP = new double[tp.Count];
        var cumFP = new double[tp.Count];
        double runTP = 0, runFP = 0;

        for (int i = 0; i < tp.Count; i++)
        {
            runTP += tp[i];
            runFP += 1 - tp[i];
            cumTP[i] = runTP;
            cumFP[i] = runFP;
        }

        var precision = new double[tp.Count];
        var recall = new double[tp.Count];
        for (int i = 0; i < tp.Count; i++)
        {
            precision[i] = cumTP[i] / (cumTP[i] + cumFP[i]);
            recall[i] = cumTP[i] / nGT;
        }

        // 101-point 插值 AP
        double ap = 0;
        for (double r = 0; r <= 1.0; r += 0.01)
        {
            double maxP = 0;
            for (int i = 0; i < recall.Length; i++)
            {
                if (recall[i] >= r && precision[i] > maxP)
                    maxP = precision[i];
            }
            ap += maxP;
        }
        ap /= 101.0;

        return ap;
    }

    /// <summary>
    /// 计算整体 Precision 和 Recall。
    /// </summary>
    private static (double precision, double recall) ComputePrecisionRecall(
        List<DetectionPrediction> predictions,
        List<DetectionGroundTruth> groundTruths,
        double iouThreshold)
    {
        if (groundTruths.Count == 0)
            return (0, 0);

        var gtByImage = groundTruths.GroupBy(g => g.ImageIndex)
            .ToDictionary(g => g.Key, g => g.ToList());

        var matched = new HashSet<(int, int)>();
        int truePos = 0;
        int falsePos = 0;

        var sortedPreds = predictions.OrderByDescending(p => p.Confidence);

        foreach (var pred in sortedPreds)
        {
            if (!gtByImage.TryGetValue(pred.ImageIndex, out var imgGTs))
            {
                falsePos++;
                continue;
            }

            double bestIoU = 0;
            int bestIdx = -1;
            for (int j = 0; j < imgGTs.Count; j++)
            {
                if (imgGTs[j].ClassId != pred.ClassId) continue;

                double iou = ComputeIoU(
                    pred.X0, pred.Y0, pred.X1, pred.Y1,
                    imgGTs[j].X0, imgGTs[j].Y0, imgGTs[j].X1, imgGTs[j].Y1);

                if (iou > bestIoU)
                {
                    bestIoU = iou;
                    bestIdx = j;
                }
            }

            if (bestIoU >= iouThreshold && bestIdx >= 0 &&
                !matched.Contains((pred.ImageIndex, bestIdx)))
            {
                truePos++;
                matched.Add((pred.ImageIndex, bestIdx));
            }
            else
            {
                falsePos++;
            }
        }

        double precision = (truePos + falsePos) > 0 ? (double)truePos / (truePos + falsePos) : 0;
        double recall = groundTruths.Count > 0 ? (double)truePos / groundTruths.Count : 0;

        return (precision, recall);
    }

    /// <summary>
    /// 计算 IoU。
    /// </summary>
    private static double ComputeIoU(
        float x0a, float y0a, float x1a, float y1a,
        float x0b, float y0b, float x1b, float y1b)
    {
        float interX0 = Math.Max(x0a, x0b);
        float interY0 = Math.Max(y0a, y0b);
        float interX1 = Math.Min(x1a, x1b);
        float interY1 = Math.Min(y1a, y1b);

        float interW = Math.Max(0, interX1 - interX0);
        float interH = Math.Max(0, interY1 - interY0);
        float interArea = interW * interH;

        float areaA = (x1a - x0a) * (y1a - y0a);
        float areaB = (x1b - x0b) * (y1b - y0b);
        float unionArea = areaA + areaB - interArea;

        return unionArea > 0 ? interArea / unionArea : 0;
    }

    /// <summary>
    /// 生成评估报告字符串。
    /// </summary>
    public static string GenerateReport(MLNetEvalResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine("         ML.NET 目标检测评估报告");
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  评估图像:   {result.ImageCount}");
        sb.AppendLine($"  总检测数:   {result.TotalDetections}");
        sb.AppendLine($"  总真值框:   {result.TotalGroundTruth}");
        sb.AppendLine();
        sb.AppendLine("  ── 核心指标 ─────────────────────────────────");
        sb.AppendLine($"  mAP@0.5:       {result.Map50:F4} ({result.Map50 * 100:F1}%)");
        sb.AppendLine($"  mAP@0.5:0.95:  {result.Map5095:F4} ({result.Map5095 * 100:F1}%)");
        sb.AppendLine($"  Precision:     {result.Precision:F4} ({result.Precision * 100:F1}%)");
        sb.AppendLine($"  Recall:        {result.Recall:F4} ({result.Recall * 100:F1}%)");
        sb.AppendLine($"  推理速度:      {result.AvgInferenceMs:F1} ms/image");

        if (result.PerClassAP50.Length > 0 && result.ClassNames.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  ── Per-Class AP@0.5 ─────────────────────────");
            int maxLen = result.ClassNames.Max(n => n.Length);
            for (int i = 0; i < result.PerClassAP50.Length && i < result.ClassNames.Length; i++)
            {
                double ap = result.PerClassAP50[i];
                string bar = new string('|', (int)(ap * 30));
                string pad = result.ClassNames[i].PadRight(maxLen);
                string grade = ap >= 0.75 ? "Excellent" : ap >= 0.5 ? "Good" : ap >= 0.3 ? "Fair" : "Poor";
                sb.AppendLine($"  {pad}  {ap:F4} ({ap * 100:F1}%)  {bar}  [{grade}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════");

        return sb.ToString();
    }

    // ── 内部数据结构 ────────────────────────────────

    private struct DetectionPrediction
    {
        public int ImageIndex;
        public int ClassId;
        public float Confidence;
        public float X0, Y0, X1, Y1;
    }

    private struct DetectionGroundTruth
    {
        public int ImageIndex;
        public int ClassId;
        public float X0, Y0, X1, Y1;
    }
}
