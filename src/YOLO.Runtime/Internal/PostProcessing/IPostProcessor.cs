using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Internal interface for postprocessing raw model output into typed results.
/// </summary>
internal interface IPostProcessor
{
    /// <summary>
    /// Process raw model output into detection results.
    /// </summary>
    /// <param name="output">Raw model output tensor data (flat).</param>
    /// <param name="shape">Shape of the output tensor.</param>
    /// <param name="context">Preprocessing context for coordinate mapping.</param>
    /// <param name="options">Inference options (thresholds, class names, etc.).</param>
    /// <returns>Array of detection results in original image coordinates.</returns>
    DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options);

    /// <summary>
    /// Process raw model output into classification results.
    /// </summary>
    ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape,
        YoloOptions options);
}
