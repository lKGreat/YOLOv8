namespace YOLO.Runtime.Results;

/// <summary>
/// Inference result for a single PDF page.
/// </summary>
/// <param name="PageIndex">Zero-based page index in the PDF.</param>
/// <param name="Detections">Detection results for this page.</param>
/// <param name="AnnotatedImage">Optional PNG bytes of the annotated page image.</param>
public readonly record struct PageResult(
    int PageIndex, DetectionResult[] Detections, byte[]? AnnotatedImage = null)
{
    /// <summary>Number of detections on this page.</summary>
    public int Count => Detections.Length;

    public override string ToString() =>
        $"Page {PageIndex}: {Detections.Length} detections";
}
