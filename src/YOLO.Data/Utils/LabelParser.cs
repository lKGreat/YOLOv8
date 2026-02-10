using System.Globalization;

namespace YOLO.Data.Utils;

/// <summary>
/// Parses YOLO-format label files.
/// Each line: class_id x_center y_center width height (normalized 0-1)
/// </summary>
public static class LabelParser
{
    /// <summary>
    /// Parse a YOLO label text file.
    /// </summary>
    /// <param name="labelPath">Path to the .txt label file</param>
    /// <returns>List of BboxInstance with normalized coordinates</returns>
    public static List<BboxInstance> ParseYOLOLabel(string labelPath)
    {
        var instances = new List<BboxInstance>();

        if (!File.Exists(labelPath))
            return instances;

        foreach (var line in File.ReadAllLines(labelPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Split on any whitespace (space/tab), not just a single space.
            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            // Use InvariantCulture to ensure '.' is always the decimal separator
            if (!int.TryParse(parts[0], out int classId)) continue;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx)) continue;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy)) continue;
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w)) continue;
            if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float h)) continue;

            // Validity checks: reject degenerate / invalid boxes
            if (classId < 0) continue;          // negative class ID
            if (w <= 0 || h <= 0) continue;     // zero-area or negative size
            if (cx < 0 || cx > 1 || cy < 0 || cy > 1) continue;  // center outside [0,1]
            if (w > 1 || h > 1) continue;       // box larger than image

            var instance = new BboxInstance
            {
                ClassId = classId,
                Bbox = [cx, cy, w, h]
            };

            // Optional: parse polygon segments (for segmentation)
            if (parts.Length > 5)
            {
                var segPoints = new List<float>();
                for (int i = 5; i < parts.Length; i++)
                {
                    if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                        segPoints.Add(val);
                }
                if (segPoints.Count >= 6) // at least 3 points
                    instance.Segments = [segPoints.ToArray()];
            }

            instances.Add(instance);
        }

        return instances;
    }

    /// <summary>
    /// Get the label path corresponding to an image path.
    /// Replaces /images/ with /labels/ and changes extension to .txt.
    /// </summary>
    public static string ImageToLabelPath(string imagePath)
    {
        // Normalize path separators to handle mixed / and \ on Windows
        var normalized = imagePath.Replace('/', Path.DirectorySeparatorChar)
                                   .Replace('\\', Path.DirectorySeparatorChar);
        var sep = Path.DirectorySeparatorChar;
        var labelPath = normalized.Replace($"{sep}images{sep}", $"{sep}labels{sep}");

        return Path.ChangeExtension(labelPath, ".txt");
    }

    /// <summary>
    /// Resolve label path for an image using common YOLO layouts.
    /// Tries multiple candidates and returns the first existing path.
    /// If none exists, returns the default YOLO mapping path.
    /// </summary>
    public static string ResolveLabelPath(string imagePath)
    {
        var normalized = imagePath.Replace('/', Path.DirectorySeparatorChar)
                                  .Replace('\\', Path.DirectorySeparatorChar);
        var sep = Path.DirectorySeparatorChar;
        var baseName = Path.GetFileNameWithoutExtension(normalized) + ".txt";
        var imageDir = Path.GetDirectoryName(normalized) ?? string.Empty;

        var candidates = new List<string>
        {
            // Standard YOLO layout: images/... -> labels/...
            ImageToLabelPath(normalized),
            // Same directory as image
            Path.Combine(imageDir, baseName),
            // Common alternative: imgs/... -> labels/...
            Path.ChangeExtension(normalized.Replace($"{sep}imgs{sep}", $"{sep}labels{sep}"), ".txt"),
            // VOC-like alternative: JPEGImages/... -> labels/...
            Path.ChangeExtension(normalized.Replace($"{sep}JPEGImages{sep}", $"{sep}labels{sep}"), ".txt")
        };

        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return p;
        }

        // Fall back to default mapping for diagnostics
        return ImageToLabelPath(normalized);
    }
}
