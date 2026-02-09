namespace YOLO.Runtime.Results;

/// <summary>
/// A single object detection result with bounding box, confidence, and class.
/// Coordinates are in the original image space (pixels).
/// </summary>
/// <param name="X1">Left edge of bounding box.</param>
/// <param name="Y1">Top edge of bounding box.</param>
/// <param name="X2">Right edge of bounding box.</param>
/// <param name="Y2">Bottom edge of bounding box.</param>
/// <param name="Confidence">Detection confidence score (0-1).</param>
/// <param name="ClassId">Predicted class index.</param>
/// <param name="ClassName">Optional class name (populated when ClassNames provided in options).</param>
public readonly record struct DetectionResult(
    float X1, float Y1, float X2, float Y2,
    float Confidence, int ClassId, string? ClassName = null)
{
    /// <summary>Width of the bounding box.</summary>
    public float Width => X2 - X1;

    /// <summary>Height of the bounding box.</summary>
    public float Height => Y2 - Y1;

    /// <summary>Area of the bounding box.</summary>
    public float Area => Width * Height;

    /// <summary>Center X coordinate.</summary>
    public float CenterX => (X1 + X2) / 2f;

    /// <summary>Center Y coordinate.</summary>
    public float CenterY => (Y1 + Y2) / 2f;

    public override string ToString() =>
        $"[{ClassName ?? $"#{ClassId}"}] ({X1:F0},{Y1:F0})-({X2:F0},{Y2:F0}) conf={Confidence:P1}";
}
