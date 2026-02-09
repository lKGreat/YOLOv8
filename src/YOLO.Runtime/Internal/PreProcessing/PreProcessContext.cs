namespace YOLO.Runtime.Internal.PreProcessing;

/// <summary>
/// Metadata produced by preprocessing, needed by postprocessing to
/// map detections back to original image coordinates.
/// </summary>
internal readonly record struct PreProcessContext(
    int OriginalWidth,
    int OriginalHeight,
    float Ratio,
    float PadX,
    float PadY,
    int InputSize);
