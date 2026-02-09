using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.Visualization;

/// <summary>
/// Draws detection results (bounding boxes, labels, confidence) on images
/// using SixLabors.ImageSharp.Drawing (cross-platform, no System.Drawing dependency).
/// </summary>
internal static class ImageDrawer
{
    // YOLO-style color palette (20 colors, cycling)
    private static readonly Color[] Palette =
    [
        Color.ParseHex("FF3838"), Color.ParseHex("FF9D97"),
        Color.ParseHex("FF701F"), Color.ParseHex("FFB21D"),
        Color.ParseHex("CFD231"), Color.ParseHex("48F90A"),
        Color.ParseHex("92CC17"), Color.ParseHex("3DDB86"),
        Color.ParseHex("1A9334"), Color.ParseHex("00D4BB"),
        Color.ParseHex("2C99A8"), Color.ParseHex("00C2FF"),
        Color.ParseHex("344593"), Color.ParseHex("6473FF"),
        Color.ParseHex("0018EC"), Color.ParseHex("8438FF"),
        Color.ParseHex("520085"), Color.ParseHex("CB38FF"),
        Color.ParseHex("FF95C8"), Color.ParseHex("FF37C7")
    ];

    // Lazy font initialization
    private static readonly Lazy<Font> DefaultFont = new(() =>
    {
        // Try to find a system font, fall back to a built-in collection
        if (SystemFonts.TryGet("Arial", out var family) ||
            SystemFonts.TryGet("Segoe UI", out family) ||
            SystemFonts.TryGet("DejaVu Sans", out family) ||
            SystemFonts.TryGet("Liberation Sans", out family))
        {
            return family.CreateFont(14, FontStyle.Bold);
        }

        // Use the first available system font
        var families = SystemFonts.Collection.Families.ToArray();
        if (families.Length > 0)
            return families[0].CreateFont(14, FontStyle.Bold);

        // If no fonts at all, this will throw -- caller should handle
        throw new InvalidOperationException(
            "No system fonts found. Install at least one TrueType font on the system.");
    });

    /// <summary>
    /// Draw detection results on a clone of the input image.
    /// The input image is not modified; a new annotated image is returned.
    /// </summary>
    /// <param name="image">The original image (not modified).</param>
    /// <param name="detections">Detection results to draw.</param>
    /// <param name="classNames">Optional class name array for labels.</param>
    /// <returns>A new image with annotations drawn.</returns>
    public static Image<Rgb24> Draw(
        Image<Rgb24> image,
        DetectionResult[] detections,
        string[]? classNames)
    {
        var annotated = image.Clone();

        if (detections.Length == 0)
            return annotated;

        var font = DefaultFont.Value;
        var smallFont = new Font(font.Family, Math.Max(10, font.Size * 0.8f), FontStyle.Bold);

        annotated.Mutate(ctx =>
        {
            foreach (var det in detections)
            {
                var color = Palette[det.ClassId % Palette.Length];
                var penColor = color;
                float lineWidth = Math.Max(2f, Math.Min(image.Width, image.Height) / 300f);

                // Draw bounding box
                var rect = new RectangleF(det.X1, det.Y1, det.Width, det.Height);
                ctx.Draw(penColor, lineWidth, rect);

                // Build label text
                string label;
                if (det.ClassName is not null)
                    label = $"{det.ClassName} {det.Confidence:P0}";
                else if (classNames is not null && det.ClassId < classNames.Length)
                    label = $"{classNames[det.ClassId]} {det.Confidence:P0}";
                else
                    label = $"#{det.ClassId} {det.Confidence:P0}";

                // Measure label
                var textOptions = new TextOptions(smallFont);
                var textSize = TextMeasurer.MeasureSize(label, textOptions);

                // Draw label background
                float labelY = Math.Max(det.Y1 - textSize.Height - 4, 0);
                var bgRect = new RectangleF(det.X1, labelY, textSize.Width + 6, textSize.Height + 4);
                ctx.Fill(color, bgRect);

                // Draw label text in white
                ctx.DrawText(label, smallFont, Color.White,
                    new PointF(det.X1 + 3, labelY + 2));
            }
        });

        return annotated;
    }
}
