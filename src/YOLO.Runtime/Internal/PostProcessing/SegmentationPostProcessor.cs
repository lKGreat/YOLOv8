using System.Buffers;
using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Postprocessor for YOLOv8 segmentation models.
/// Output format: (1, 4+nc+nm, N) detection tensor + (1, nm, mask_h, mask_w) proto tensor.
///
/// Process:
///   1. Run standard NMS on the detection part (4+nc channels)
///   2. For each kept detection, extract nm mask coefficients
///   3. Compute mask = sigmoid(coeffs @ proto) per instance
///   4. Crop mask to bbox, resize to original image
///
/// Matches Python ultralytics postprocess logic for segment inference.
/// </summary>
internal sealed class SegmentationPostProcessor : IPostProcessor
{
    private readonly NmsPostProcessor _detectionProcessor = new();

    /// <summary>
    /// Number of mask prototypes (default 32 for YOLOv8-seg).
    /// </summary>
    public int NumMasks { get; init; } = 32;

    public DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options)
    {
        // For combined output without separate proto, fall back to detection
        return _detectionProcessor.ProcessDetections(output, shape, context, options);
    }

    /// <summary>
    /// Process segmentation output with separate proto tensor.
    /// </summary>
    /// <param name="detOutput">Detection output: (1, 4+nc+nm, N)</param>
    /// <param name="detShape">Shape of detection output</param>
    /// <param name="protoOutput">Proto output: (1, nm, mask_h, mask_w)</param>
    /// <param name="protoShape">Shape of proto output</param>
    /// <param name="context">Preprocessing context</param>
    /// <param name="options">Inference options</param>
    public SegmentationResult[] ProcessSegmentations(
        ReadOnlySpan<float> detOutput, int[] detShape,
        ReadOnlySpan<float> protoOutput, int[] protoShape,
        PreProcessContext context, YoloOptions options)
    {
        if (detShape.Length != 3 || protoShape.Length != 4)
            return [];

        int channels = detShape[1]; // 4 + nc + nm
        int numAnchors = detShape[2];
        int nc = channels - 4 - NumMasks;

        if (nc <= 0)
            return [];

        int maskH = protoShape[2];
        int maskW = protoShape[3];

        float confThreshold = options.Confidence;
        float iouThreshold = options.IoU;

        // Step 1: Extract detection candidates with mask coefficients
        var candidates = new List<(float x1, float y1, float x2, float y2,
            float conf, int cls, float[] maskCoeffs)>(Math.Min(numAnchors, 2048));

        for (int i = 0; i < numAnchors; i++)
        {
            float bestScore = float.MinValue;
            int bestClass = 0;

            for (int c = 0; c < nc; c++)
            {
                float score = detOutput[(4 + c) * numAnchors + i];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore < confThreshold)
                continue;

            float cx = detOutput[0 * numAnchors + i];
            float cy = detOutput[1 * numAnchors + i];
            float w = detOutput[2 * numAnchors + i];
            float h = detOutput[3 * numAnchors + i];

            float x1 = cx - w * 0.5f;
            float y1 = cy - h * 0.5f;
            float x2 = cx + w * 0.5f;
            float y2 = cy + h * 0.5f;

            // Extract mask coefficients
            var coeffs = new float[NumMasks];
            for (int m = 0; m < NumMasks; m++)
                coeffs[m] = detOutput[(4 + nc + m) * numAnchors + i];

            candidates.Add((x1, y1, x2, y2, bestScore, bestClass, coeffs));
        }

        if (candidates.Count == 0)
            return [];

        // Step 2: NMS on detections
        candidates.Sort((a, b) => b.conf.CompareTo(a.conf));
        var kept = GreedyNmsWithMask(candidates, iouThreshold, options.MaxDetections);

        // Step 3: Generate masks
        var results = new SegmentationResult[kept.Count];
        for (int k = 0; k < kept.Count; k++)
        {
            var (x1, y1, x2, y2, conf, cls, coeffs) = kept[k];

            // Compute mask: sigmoid(coeffs @ proto_flat)
            var mask = new float[maskH * maskW];
            for (int pixel = 0; pixel < maskH * maskW; pixel++)
            {
                float sum = 0;
                for (int m = 0; m < NumMasks; m++)
                    sum += coeffs[m] * protoOutput[m * maskH * maskW + pixel];

                // Sigmoid
                mask[pixel] = 1.0f / (1.0f + MathF.Exp(-sum));
            }

            // Crop mask to bbox in model coordinates
            // InputSize is the square dimension of the model input (e.g. 640)
            float bx1 = Math.Max(0, x1 / context.InputSize * maskW);
            float by1 = Math.Max(0, y1 / context.InputSize * maskH);
            float bx2 = Math.Min(maskW, x2 / context.InputSize * maskW);
            float by2 = Math.Min(maskH, y2 / context.InputSize * maskH);

            for (int row = 0; row < maskH; row++)
            {
                for (int col = 0; col < maskW; col++)
                {
                    if (row < by1 || row >= by2 || col < bx1 || col >= bx2)
                        mask[row * maskW + col] = 0;
                    else
                        mask[row * maskW + col] = mask[row * maskW + col] > 0.5f ? 1.0f : 0.0f;
                }
            }

            // Map detection coordinates back to original image
            float dx1 = (x1 - context.PadX) / context.Ratio;
            float dy1 = (y1 - context.PadY) / context.Ratio;
            float dx2 = (x2 - context.PadX) / context.Ratio;
            float dy2 = (y2 - context.PadY) / context.Ratio;

            dx1 = Math.Clamp(dx1, 0, context.OriginalWidth);
            dy1 = Math.Clamp(dy1, 0, context.OriginalHeight);
            dx2 = Math.Clamp(dx2, 0, context.OriginalWidth);
            dy2 = Math.Clamp(dy2, 0, context.OriginalHeight);

            string? className = options.ClassNames is not null && cls < options.ClassNames.Length
                ? options.ClassNames[cls] : null;

            var det = new DetectionResult(dx1, dy1, dx2, dy2, conf, cls, className);
            results[k] = new SegmentationResult(det, maskW, maskH, mask);
        }

        return results;
    }

    public ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape, YoloOptions options)
    {
        return [];
    }

    private static List<(float x1, float y1, float x2, float y2, float conf, int cls, float[] coeffs)>
        GreedyNmsWithMask(
            List<(float x1, float y1, float x2, float y2, float conf, int cls, float[] coeffs)> candidates,
            float iouThreshold, int maxDet)
    {
        const float maxWH = 7680f;
        var kept = new List<(float, float, float, float, float, int, float[])>(
            Math.Min(candidates.Count, maxDet));

        var suppressed = ArrayPool<bool>.Shared.Rent(candidates.Count);
        Array.Clear(suppressed, 0, candidates.Count);

        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (suppressed[i]) continue;

                var a = candidates[i];
                kept.Add(a);
                if (kept.Count >= maxDet) break;

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
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(suppressed);
        }

        return kept;
    }
}
