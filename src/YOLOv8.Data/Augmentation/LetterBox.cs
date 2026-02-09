using YOLOv8.Data.Utils;

namespace YOLOv8.Data.Augmentation;

/// <summary>
/// LetterBox resize: resizes image maintaining aspect ratio and pads to target size.
/// Padding color is gray (114, 114, 114).
/// </summary>
public class LetterBox
{
    private readonly int targetW;
    private readonly int targetH;
    private readonly bool scaleUp;
    private readonly bool center;
    private readonly int stride;

    public LetterBox(int targetSize = 640, bool scaleUp = true, bool center = true, int stride = 32)
    {
        targetW = targetSize;
        targetH = targetSize;
        this.scaleUp = scaleUp;
        this.center = center;
        this.stride = stride;
    }

    /// <summary>
    /// Apply letterbox resize to image data.
    /// </summary>
    /// <param name="imgData">Input image bytes (HWC RGB)</param>
    /// <param name="w">Image width</param>
    /// <param name="h">Image height</param>
    /// <param name="labels">Optional labels to transform</param>
    /// <returns>Resized and padded image data with updated dimensions</returns>
    public (byte[] data, int newW, int newH, float padX, float padY, float ratio) Apply(
        byte[] imgData, int w, int h, List<BboxInstance>? labels = null)
    {
        // Compute scale ratio
        double r = Math.Min((double)targetH / h, (double)targetW / w);
        if (!scaleUp)
            r = Math.Min(r, 1.0);

        // Compute new unpadded size
        int newUnpadW = (int)Math.Round(w * r);
        int newUnpadH = (int)Math.Round(h * r);

        // Compute padding
        double dw = targetW - newUnpadW;
        double dh = targetH - newUnpadH;

        float padX, padY;
        if (center)
        {
            padX = (float)(dw / 2.0);
            padY = (float)(dh / 2.0);
        }
        else
        {
            padX = 0;
            padY = 0;
        }

        int top = (int)Math.Round(padY - 0.1);
        int bottom = (int)Math.Round(padY + 0.1);
        if (!center) bottom = (int)dh;
        int left = (int)Math.Round(padX - 0.1);
        int right = (int)Math.Round(padX + 0.1);
        if (!center) right = (int)dw;

        // Resize if needed
        byte[] resized;
        if (newUnpadW != w || newUnpadH != h)
        {
            (resized, _, _) = ImageUtils.Resize(imgData, w, h, newUnpadW, newUnpadH);
        }
        else
        {
            resized = imgData;
        }

        // Add padding (gray = 114)
        int finalW = newUnpadW + left + right;
        int finalH = newUnpadH + top + bottom;

        // Ensure exact target size
        finalW = targetW;
        finalH = targetH;

        var padded = new byte[finalW * finalH * 3];
        // Fill with gray
        for (int i = 0; i < padded.Length; i++)
            padded[i] = 114;

        // Copy resized image into padded canvas
        for (int y = 0; y < newUnpadH && (y + top) < finalH; y++)
        {
            for (int x = 0; x < newUnpadW && (x + left) < finalW; x++)
            {
                int srcIdx = (y * newUnpadW + x) * 3;
                int dstIdx = ((y + top) * finalW + (x + left)) * 3;
                padded[dstIdx + 0] = resized[srcIdx + 0];
                padded[dstIdx + 1] = resized[srcIdx + 1];
                padded[dstIdx + 2] = resized[srcIdx + 2];
            }
        }

        // Update labels if provided (transform from original coords to letterboxed coords)
        if (labels != null)
        {
            foreach (var inst in labels)
            {
                // Labels are in normalized xywh, denormalize to original size
                inst.Denormalize(w, h);
                // Scale
                inst.Bbox[0] = (float)(inst.Bbox[0] * r + left);
                inst.Bbox[1] = (float)(inst.Bbox[1] * r + top);
                inst.Bbox[2] = (float)(inst.Bbox[2] * r);
                inst.Bbox[3] = (float)(inst.Bbox[3] * r);
                // Normalize to new size
                inst.Normalize(finalW, finalH);
            }
        }

        return (padded, finalW, finalH, padX, padY, (float)r);
    }
}
