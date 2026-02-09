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

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0], out int classId)) continue;
            if (!float.TryParse(parts[1], out float cx)) continue;
            if (!float.TryParse(parts[2], out float cy)) continue;
            if (!float.TryParse(parts[3], out float w)) continue;
            if (!float.TryParse(parts[4], out float h)) continue;

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
                    if (float.TryParse(parts[i], out float val))
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
}
