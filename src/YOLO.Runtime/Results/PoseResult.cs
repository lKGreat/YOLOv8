namespace YOLO.Runtime.Results;

/// <summary>
/// A single pose estimation result: detection + keypoints.
/// </summary>
/// <param name="Detection">The bounding box detection (person).</param>
/// <param name="Keypoints">Keypoint array: each entry is (x, y, confidence).</param>
public readonly record struct PoseResult(
    DetectionResult Detection, Keypoint[] Keypoints)
{
    /// <summary>
    /// Number of keypoints.
    /// </summary>
    public int NumKeypoints => Keypoints?.Length ?? 0;

    public override string ToString() =>
        $"{Detection} kpts={NumKeypoints}";
}

/// <summary>
/// A single keypoint with position and confidence.
/// </summary>
/// <param name="X">X coordinate in original image space.</param>
/// <param name="Y">Y coordinate in original image space.</param>
/// <param name="Confidence">Visibility/confidence score (0-1).</param>
public readonly record struct Keypoint(float X, float Y, float Confidence)
{
    /// <summary>
    /// Whether this keypoint is visible (confidence > threshold).
    /// </summary>
    public bool IsVisible(float threshold = 0.5f) => Confidence > threshold;

    public override string ToString() => $"({X:F1},{Y:F1},{Confidence:F2})";
}
