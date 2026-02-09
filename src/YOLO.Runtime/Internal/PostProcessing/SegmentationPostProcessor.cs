using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Postprocessor for YOLO segmentation models (e.g. yolov8-seg).
/// Output includes detection boxes + mask prototypes.
/// This is a placeholder for future full segmentation support.
/// Currently falls back to detection-only processing.
/// </summary>
internal sealed class SegmentationPostProcessor : IPostProcessor
{
    private readonly NmsPostProcessor _detectionProcessor = new();

    public DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options)
    {
        // For now, delegate to standard NMS detection processing.
        // Full segmentation mask processing can be added here.
        return _detectionProcessor.ProcessDetections(output, shape, context, options);
    }

    public ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape, YoloOptions options)
    {
        return [];
    }
}
