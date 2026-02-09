namespace YOLOv8.Data.Utils;

/// <summary>
/// Represents a single detection label instance with bounding box and optional segments.
/// Coordinates can be in normalized (0-1) or absolute pixel format.
/// </summary>
public class BboxInstance
{
    /// <summary>Class index (0-based)</summary>
    public int ClassId { get; set; }

    /// <summary>Bounding box in xywh format (center x, center y, width, height)</summary>
    public float[] Bbox { get; set; } = new float[4];

    /// <summary>Bounding box in xyxy format (x1, y1, x2, y2)</summary>
    public float[] BboxXyxy
    {
        get
        {
            float cx = Bbox[0], cy = Bbox[1], w = Bbox[2], h = Bbox[3];
            return [cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2];
        }
    }

    /// <summary>Optional polygon segments for segmentation</summary>
    public float[][]? Segments { get; set; }

    public BboxInstance Clone() => new()
    {
        ClassId = ClassId,
        Bbox = (float[])Bbox.Clone(),
        Segments = Segments?.Select(s => (float[])s.Clone()).ToArray()
    };

    /// <summary>Convert normalized coords to absolute pixels.</summary>
    public void Denormalize(float imgW, float imgH)
    {
        Bbox[0] *= imgW;
        Bbox[1] *= imgH;
        Bbox[2] *= imgW;
        Bbox[3] *= imgH;
    }

    /// <summary>Convert absolute pixels to normalized coords.</summary>
    public void Normalize(float imgW, float imgH)
    {
        Bbox[0] /= imgW;
        Bbox[1] /= imgH;
        Bbox[2] /= imgW;
        Bbox[3] /= imgH;
    }

    /// <summary>Add offset (for mosaic padding).</summary>
    public void AddPadding(float padX, float padY)
    {
        Bbox[0] += padX;
        Bbox[1] += padY;
    }

    /// <summary>Clip to image boundaries (xyxy format internally).</summary>
    public void Clip(float imgW, float imgH)
    {
        var xyxy = BboxXyxy;
        xyxy[0] = Math.Clamp(xyxy[0], 0, imgW);
        xyxy[1] = Math.Clamp(xyxy[1], 0, imgH);
        xyxy[2] = Math.Clamp(xyxy[2], 0, imgW);
        xyxy[3] = Math.Clamp(xyxy[3], 0, imgH);

        // Convert back to xywh
        Bbox[0] = (xyxy[0] + xyxy[2]) / 2;
        Bbox[1] = (xyxy[1] + xyxy[3]) / 2;
        Bbox[2] = xyxy[2] - xyxy[0];
        Bbox[3] = xyxy[3] - xyxy[1];
    }

    /// <summary>Flip horizontally.</summary>
    public void FlipLR(float imgW)
    {
        Bbox[0] = imgW - Bbox[0];
    }

    /// <summary>Flip vertically.</summary>
    public void FlipUD(float imgH)
    {
        Bbox[1] = imgH - Bbox[1];
    }

    /// <summary>Check if the box has valid area.</summary>
    public bool IsValid(float minArea = 1.0f, float maxAspectRatio = 100.0f)
    {
        float area = Bbox[2] * Bbox[3];
        if (area < minArea) return false;
        float ar = Math.Max(Bbox[2], Bbox[3]) / (Math.Min(Bbox[2], Bbox[3]) + 1e-6f);
        return ar < maxAspectRatio;
    }
}

/// <summary>
/// A labeled sample containing an image path and its detection labels.
/// </summary>
public class LabeledSample
{
    public string ImagePath { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<BboxInstance> Instances { get; set; } = new();

    public LabeledSample Clone() => new()
    {
        ImagePath = ImagePath,
        ImageWidth = ImageWidth,
        ImageHeight = ImageHeight,
        Instances = Instances.Select(i => i.Clone()).ToList()
    };
}
