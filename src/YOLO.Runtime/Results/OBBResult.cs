namespace YOLO.Runtime.Results;

/// <summary>
/// A single oriented bounding box detection result.
/// Coordinates are in the original image space.
/// </summary>
/// <param name="CenterX">Center X of the rotated box.</param>
/// <param name="CenterY">Center Y of the rotated box.</param>
/// <param name="Width">Width of the rotated box.</param>
/// <param name="Height">Height of the rotated box.</param>
/// <param name="Angle">Rotation angle in radians [0, pi/2).</param>
/// <param name="Confidence">Detection confidence score (0-1).</param>
/// <param name="ClassId">Predicted class index.</param>
/// <param name="ClassName">Optional class name.</param>
public readonly record struct OBBResult(
    float CenterX, float CenterY, float Width, float Height,
    float Angle, float Confidence, int ClassId, string? ClassName = null)
{
    /// <summary>Area of the oriented box.</summary>
    public float Area => Width * Height;

    public override string ToString() =>
        $"[{ClassName ?? $"#{ClassId}"}] center=({CenterX:F0},{CenterY:F0}) " +
        $"size={Width:F0}x{Height:F0} angle={Angle:F2}rad conf={Confidence:P1}";
}
