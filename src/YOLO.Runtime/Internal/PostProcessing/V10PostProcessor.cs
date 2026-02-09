using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Postprocessor for YOLOv10 models with end-to-end detection (no NMS needed).
/// Output format: (1, 300, 6) where 6 = [x1, y1, x2, y2, confidence, class_id].
/// </summary>
internal sealed class V10PostProcessor : IPostProcessor
{
    public DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options)
    {
        // Expected shape: (1, 300, 6) or (B, maxDet, 6)
        if (shape.Length != 3 || shape[2] != 6)
            return [];

        int numDets = shape[1];
        float confThreshold = options.Confidence;
        string[]? classNames = options.ClassNames;

        var results = new List<DetectionResult>(Math.Min(numDets, options.MaxDetections));

        for (int i = 0; i < numDets; i++)
        {
            int offset = i * 6;
            float x1 = output[offset + 0];
            float y1 = output[offset + 1];
            float x2 = output[offset + 2];
            float y2 = output[offset + 3];
            float conf = output[offset + 4];
            int cls = (int)output[offset + 5];

            if (conf < confThreshold)
                continue;

            // Map back to original coordinates
            x1 = (x1 - context.PadX) / context.Ratio;
            y1 = (y1 - context.PadY) / context.Ratio;
            x2 = (x2 - context.PadX) / context.Ratio;
            y2 = (y2 - context.PadY) / context.Ratio;

            // Clip to image bounds
            x1 = Math.Clamp(x1, 0, context.OriginalWidth);
            y1 = Math.Clamp(y1, 0, context.OriginalHeight);
            x2 = Math.Clamp(x2, 0, context.OriginalWidth);
            y2 = Math.Clamp(y2, 0, context.OriginalHeight);

            // Skip degenerate boxes
            if (x2 <= x1 || y2 <= y1)
                continue;

            string? className = classNames is not null && cls < classNames.Length
                ? classNames[cls] : null;

            results.Add(new DetectionResult(x1, y1, x2, y2, conf, cls, className));
        }

        return results.ToArray();
    }

    public ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape, YoloOptions options)
    {
        return [];
    }
}
