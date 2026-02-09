using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Postprocessor for YOLOv8/v9/v11 style models that output (B, 4+nc, N) format
/// and require Non-Maximum Suppression.
/// Also used as the default postprocessor for unknown versions.
/// </summary>
internal sealed class NmsPostProcessor : IPostProcessor
{
    public DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options)
    {
        // Expected shape: (1, 4+nc, N) or (B, 4+nc, N)
        // For ONNX: shape might be (1, 84, 8400) for 80 classes
        // For TorchSharp: same format from combined boxes+scores

        if (shape.Length != 3)
            return [];

        int channels = shape[1]; // 4 + nc
        int numAnchors = shape[2]; // N
        int nc = channels - 4;

        if (nc <= 0)
            return [];

        float confThreshold = options.Confidence;
        float iouThreshold = options.IoU;
        int maxDet = options.MaxDetections;
        string[]? classNames = options.ClassNames;

        // Collect candidates: transpose from (4+nc, N) to iterate by anchor
        var candidates = new List<(float x1, float y1, float x2, float y2, float conf, int cls)>();

        for (int i = 0; i < numAnchors; i++)
        {
            // Find best class score
            float bestScore = float.MinValue;
            int bestClass = 0;

            for (int c = 0; c < nc; c++)
            {
                float score = output[(4 + c) * numAnchors + i];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore < confThreshold)
                continue;

            // Get box in xywh format
            float cx = output[0 * numAnchors + i];
            float cy = output[1 * numAnchors + i];
            float w = output[2 * numAnchors + i];
            float h = output[3 * numAnchors + i];

            // Convert xywh -> xyxy
            float x1 = cx - w / 2f;
            float y1 = cy - h / 2f;
            float x2 = cx + w / 2f;
            float y2 = cy + h / 2f;

            candidates.Add((x1, y1, x2, y2, bestScore, bestClass));
        }

        if (candidates.Count == 0)
            return [];

        // Sort by confidence descending
        candidates.Sort((a, b) => b.conf.CompareTo(a.conf));

        // Limit candidates
        if (candidates.Count > 30000)
            candidates = candidates.GetRange(0, 30000);

        // Greedy NMS with class offset trick
        const float maxWH = 7680f;
        var kept = GreedyNms(candidates, iouThreshold, maxDet, maxWH);

        // Map back to original coordinates
        var results = new DetectionResult[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        {
            var (x1, y1, x2, y2, conf, cls) = kept[i];

            // Remove padding and scale back to original image
            x1 = (x1 - context.PadX) / context.Ratio;
            y1 = (y1 - context.PadY) / context.Ratio;
            x2 = (x2 - context.PadX) / context.Ratio;
            y2 = (y2 - context.PadY) / context.Ratio;

            // Clip to image bounds
            x1 = Math.Clamp(x1, 0, context.OriginalWidth);
            y1 = Math.Clamp(y1, 0, context.OriginalHeight);
            x2 = Math.Clamp(x2, 0, context.OriginalWidth);
            y2 = Math.Clamp(y2, 0, context.OriginalHeight);

            string? className = classNames is not null && cls < classNames.Length
                ? classNames[cls] : null;

            results[i] = new DetectionResult(x1, y1, x2, y2, conf, cls, className);
        }

        return results;
    }

    public ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape, YoloOptions options)
    {
        // Not applicable for detection models
        return [];
    }

    /// <summary>
    /// Greedy NMS with class offset trick (per-class suppression).
    /// </summary>
    private static List<(float x1, float y1, float x2, float y2, float conf, int cls)> GreedyNms(
        List<(float x1, float y1, float x2, float y2, float conf, int cls)> candidates,
        float iouThreshold, int maxDet, float maxWH)
    {
        var kept = new List<(float x1, float y1, float x2, float y2, float conf, int cls)>();
        var suppressed = new bool[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            if (suppressed[i]) continue;

            var a = candidates[i];
            kept.Add(a);

            if (kept.Count >= maxDet)
                break;

            // Apply class offset for per-class NMS
            float ax1 = a.x1 + a.cls * maxWH;
            float ay1 = a.y1 + a.cls * maxWH;
            float ax2 = a.x2 + a.cls * maxWH;
            float ay2 = a.y2 + a.cls * maxWH;
            float areaA = (ax2 - ax1) * (ay2 - ay1);

            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (suppressed[j]) continue;

                var b = candidates[j];
                float bx1 = b.x1 + b.cls * maxWH;
                float by1 = b.y1 + b.cls * maxWH;
                float bx2 = b.x2 + b.cls * maxWH;
                float by2 = b.y2 + b.cls * maxWH;

                // Compute IoU
                float interX1 = Math.Max(ax1, bx1);
                float interY1 = Math.Max(ay1, by1);
                float interX2 = Math.Min(ax2, bx2);
                float interY2 = Math.Min(ay2, by2);

                float inter = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
                float areaB = (bx2 - bx1) * (by2 - by1);
                float iou = inter / (areaA + areaB - inter + 1e-7f);

                if (iou > iouThreshold)
                    suppressed[j] = true;
            }
        }

        return kept;
    }
}
