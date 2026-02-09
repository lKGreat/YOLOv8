using TorchSharp;
using YOLO.Core.Utils;
using static TorchSharp.torch;

namespace YOLO.Inference.PostProcess;

/// <summary>
/// Non-Maximum Suppression for object detection.
/// Filters overlapping detections based on confidence and IoU thresholds.
/// </summary>
public static class NMS
{
    /// <summary>
    /// Perform Non-Maximum Suppression on detection results.
    /// </summary>
    /// <param name="prediction">Model output (B, nc+4, N) where 4=xywh, nc=class scores</param>
    /// <param name="confThres">Confidence threshold</param>
    /// <param name="iouThres">IoU threshold for NMS</param>
    /// <param name="maxDet">Maximum detections per image</param>
    /// <param name="maxNms">Maximum boxes for NMS</param>
    /// <param name="agnostic">Class-agnostic NMS</param>
    /// <returns>List of tensors per batch image: (numDets, 6) as [x1,y1,x2,y2,conf,cls]</returns>
    public static List<Tensor> NonMaxSuppression(
        Tensor boxes, Tensor scores,
        double confThres = 0.25,
        double iouThres = 0.45,
        int maxDet = 300,
        int maxNms = 30000,
        bool agnostic = false)
    {
        // boxes: (B, 4, N) in xywh, scores: (B, nc, N)
        long batch = boxes.shape[0];
        long nc = scores.shape[1];

        var results = new List<Tensor>();

        for (long bi = 0; bi < batch; bi++)
        {
            var batchBoxes = boxes[bi]; // (4, N)
            var batchScores = scores[bi]; // (nc, N)

            // Transpose to (N, 4) and (N, nc)
            batchBoxes = batchBoxes.T; // (N, 4)
            batchScores = batchScores.T; // (N, nc)

            // Convert xywh to xyxy
            var xyxyBoxes = BboxUtils.Xywh2Xyxy(batchBoxes);

            // Get max class score and class index
            var (maxScores, maxClasses) = batchScores.max(dim: 1); // each (N,)

            // Filter by confidence
            var confMask = maxScores > confThres;
            var filteredBoxes = xyxyBoxes[confMask]; // (M, 4)
            var filteredScores = maxScores[confMask]; // (M,)
            var filteredClasses = maxClasses[confMask].to(ScalarType.Float32); // (M,)

            if (filteredBoxes.shape[0] == 0)
            {
                results.Add(torch.zeros(0, 6, dtype: ScalarType.Float32));
                continue;
            }

            // Limit candidates
            if (filteredBoxes.shape[0] > maxNms)
            {
                var (_, sortIdx) = filteredScores.sort(descending: true);
                sortIdx = sortIdx[..maxNms];
                filteredBoxes = filteredBoxes[sortIdx];
                filteredScores = filteredScores[sortIdx];
                filteredClasses = filteredClasses[sortIdx];
            }

            // Apply NMS per class (class offset trick)
            double maxWH = 7680;
            var classOffset = filteredClasses * (agnostic ? 0 : maxWH);
            var offsetBoxes = filteredBoxes + classOffset.unsqueeze(1);

            // Greedy NMS
            var keepIndices = GreedyNMS(offsetBoxes, filteredScores, iouThres);

            // Limit detections
            if (keepIndices.Count > maxDet)
                keepIndices = keepIndices.GetRange(0, maxDet);

            if (keepIndices.Count > 0)
            {
                var keepIdx = torch.tensor(keepIndices.ToArray(), dtype: ScalarType.Int64);
                var finalBoxes = filteredBoxes[keepIdx]; // (K, 4)
                var finalScores = filteredScores[keepIdx].unsqueeze(1); // (K, 1)
                var finalClasses = filteredClasses[keepIdx].unsqueeze(1); // (K, 1)

                results.Add(torch.cat([finalBoxes, finalScores, finalClasses], dim: 1));
            }
            else
            {
                results.Add(torch.zeros(0, 6, dtype: ScalarType.Float32));
            }
        }

        return results;
    }

    /// <summary>
    /// Greedy NMS: iteratively select the highest confidence box
    /// and suppress all boxes with IoU > threshold.
    /// </summary>
    private static List<int> GreedyNMS(Tensor boxes, Tensor scores, double iouThreshold)
    {
        var (_, order) = scores.sort(descending: true);
        var keep = new List<int>();

        var boxesData = boxes.data<float>().ToArray();
        var orderData = order.data<long>().ToArray();
        long n = boxes.shape[0];

        var suppressed = new bool[n];

        for (int i = 0; i < orderData.Length; i++)
        {
            int idx = (int)orderData[i];
            if (suppressed[idx]) continue;

            keep.Add(idx);

            float x1i = boxesData[idx * 4 + 0];
            float y1i = boxesData[idx * 4 + 1];
            float x2i = boxesData[idx * 4 + 2];
            float y2i = boxesData[idx * 4 + 3];
            float areaI = (x2i - x1i) * (y2i - y1i);

            for (int j = i + 1; j < orderData.Length; j++)
            {
                int jdx = (int)orderData[j];
                if (suppressed[jdx]) continue;

                float x1j = boxesData[jdx * 4 + 0];
                float y1j = boxesData[jdx * 4 + 1];
                float x2j = boxesData[jdx * 4 + 2];
                float y2j = boxesData[jdx * 4 + 3];

                float interX1 = Math.Max(x1i, x1j);
                float interY1 = Math.Max(y1i, y1j);
                float interX2 = Math.Min(x2i, x2j);
                float interY2 = Math.Min(y2i, y2j);

                float inter = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
                float areaJ = (x2j - x1j) * (y2j - y1j);
                float iou = inter / (areaI + areaJ - inter + 1e-7f);

                if (iou > iouThreshold)
                    suppressed[jdx] = true;
            }
        }

        return keep;
    }
}
