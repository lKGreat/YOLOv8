using System.Text.Json.Serialization;

namespace YOLO.WinForms.Models;

/// <summary>
/// A single rectangle annotation in YOLO normalized format.
/// Coordinates are center-x, center-y, width, height all in [0, 1].
/// </summary>
public class RectAnnotation
{
    /// <summary>Class index (0-based).</summary>
    [JsonPropertyName("classId")]
    public int ClassId { get; set; }

    /// <summary>Center X (normalized 0-1).</summary>
    [JsonPropertyName("cx")]
    public double CX { get; set; }

    /// <summary>Center Y (normalized 0-1).</summary>
    [JsonPropertyName("cy")]
    public double CY { get; set; }

    /// <summary>Width (normalized 0-1).</summary>
    [JsonPropertyName("w")]
    public double W { get; set; }

    /// <summary>Height (normalized 0-1).</summary>
    [JsonPropertyName("h")]
    public double H { get; set; }

    /// <summary>
    /// Create a deep copy.
    /// </summary>
    public RectAnnotation Clone() => new()
    {
        ClassId = ClassId,
        CX = CX,
        CY = CY,
        W = W,
        H = H
    };

    /// <summary>
    /// Convert to YOLO label line: "classId cx cy w h".
    /// </summary>
    public string ToYoloLine() =>
        $"{ClassId} {CX:F6} {CY:F6} {W:F6} {H:F6}";

    /// <summary>
    /// Get the bounding box in pixel coordinates (x1, y1, x2, y2).
    /// </summary>
    public (double X1, double Y1, double X2, double Y2) ToPixelRect(double imgW, double imgH)
    {
        double x1 = (CX - W / 2) * imgW;
        double y1 = (CY - H / 2) * imgH;
        double x2 = (CX + W / 2) * imgW;
        double y2 = (CY + H / 2) * imgH;
        return (x1, y1, x2, y2);
    }

    /// <summary>
    /// Create from pixel coordinates (x1, y1, x2, y2) and image dimensions.
    /// </summary>
    public static RectAnnotation FromPixelRect(
        double x1, double y1, double x2, double y2,
        double imgW, double imgH, int classId)
    {
        // Ensure x1 < x2, y1 < y2
        if (x1 > x2) (x1, x2) = (x2, x1);
        if (y1 > y2) (y1, y2) = (y2, y1);

        return new RectAnnotation
        {
            ClassId = classId,
            CX = ((x1 + x2) / 2) / imgW,
            CY = ((y1 + y2) / 2) / imgH,
            W = (x2 - x1) / imgW,
            H = (y2 - y1) / imgH
        };
    }
}
