namespace YOLO.Runtime.Results;

/// <summary>
/// A single instance segmentation result: detection + mask.
/// </summary>
/// <param name="Detection">The bounding box detection.</param>
/// <param name="MaskWidth">Width of the mask data.</param>
/// <param name="MaskHeight">Height of the mask data.</param>
/// <param name="MaskData">Binary mask data (row-major, 1=foreground, 0=background).</param>
public readonly record struct SegmentationResult(
    DetectionResult Detection, int MaskWidth, int MaskHeight, float[] MaskData)
{
    public override string ToString() =>
        $"{Detection} mask={MaskWidth}x{MaskHeight}";
}
