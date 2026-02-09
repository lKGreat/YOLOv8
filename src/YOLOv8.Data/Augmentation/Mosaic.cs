using YOLOv8.Data.Utils;

namespace YOLOv8.Data.Augmentation;

/// <summary>
/// Mosaic augmentation: combines 4 images into a 2x2 grid.
/// Random center point for the grid, fills empty areas with gray (114).
/// </summary>
public class Mosaic
{
    private readonly int imgSize;
    private readonly double probability;
    private readonly Random rng;

    public Mosaic(int imgSize = 640, double probability = 1.0, int? seed = null)
    {
        this.imgSize = imgSize;
        this.probability = probability;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply 4-image mosaic augmentation.
    /// </summary>
    /// <param name="images">4 images as (data, width, height) tuples</param>
    /// <param name="labels">4 sets of labels</param>
    /// <returns>Combined mosaic image and merged labels</returns>
    public (byte[] data, int w, int h, List<BboxInstance> labels) Apply(
        (byte[] data, int w, int h)[] images,
        List<BboxInstance>[] labels)
    {
        if (rng.NextDouble() > probability)
        {
            // No mosaic - return first image
            return (images[0].data, images[0].w, images[0].h, labels[0]);
        }

        int s = imgSize;
        int mosaicW = s * 2;
        int mosaicH = s * 2;

        // Random center
        int yc = rng.Next(s / 2, s * 3 / 2);
        int xc = rng.Next(s / 2, s * 3 / 2);

        var canvas = new byte[mosaicW * mosaicH * 3];
        // Fill with gray (114)
        for (int i = 0; i < canvas.Length; i++)
            canvas[i] = 114;

        var mergedLabels = new List<BboxInstance>();

        for (int i = 0; i < 4; i++)
        {
            var (imgData, iw, ih) = images[i];

            // Resize image to fit mosaic tile
            int tileW, tileH;
            int x1a, y1a, x2a, y2a; // canvas area
            int x1b, y1b, x2b, y2b; // image area

            if (i == 0) // top-left
            {
                tileW = xc; tileH = yc;
                x1a = Math.Max(xc - iw, 0); y1a = Math.Max(yc - ih, 0);
                x2a = xc; y2a = yc;
                x1b = iw - (x2a - x1a); y1b = ih - (y2a - y1a);
                x2b = iw; y2b = ih;
            }
            else if (i == 1) // top-right
            {
                x1a = xc; y1a = Math.Max(yc - ih, 0);
                x2a = Math.Min(xc + iw, mosaicW); y2a = yc;
                x1b = 0; y1b = ih - (y2a - y1a);
                x2b = Math.Min(iw, x2a - x1a); y2b = ih;
            }
            else if (i == 2) // bottom-left
            {
                x1a = Math.Max(xc - iw, 0); y1a = yc;
                x2a = xc; y2a = Math.Min(yc + ih, mosaicH);
                x1b = iw - (x2a - x1a); y1b = 0;
                x2b = iw; y2b = Math.Min(ih, y2a - y1a);
            }
            else // bottom-right
            {
                x1a = xc; y1a = yc;
                x2a = Math.Min(xc + iw, mosaicW); y2a = Math.Min(yc + ih, mosaicH);
                x1b = 0; y1b = 0;
                x2b = Math.Min(iw, x2a - x1a); y2b = Math.Min(ih, y2a - y1a);
            }

            // Copy pixels from source image to canvas
            int copyW = Math.Min(x2a - x1a, x2b - x1b);
            int copyH = Math.Min(y2a - y1a, y2b - y1b);

            for (int y = 0; y < copyH; y++)
            {
                for (int x = 0; x < copyW; x++)
                {
                    int srcIdx = ((y1b + y) * iw + (x1b + x)) * 3;
                    int dstIdx = ((y1a + y) * mosaicW + (x1a + x)) * 3;
                    if (srcIdx + 2 < imgData.Length && dstIdx + 2 < canvas.Length)
                    {
                        canvas[dstIdx] = imgData[srcIdx];
                        canvas[dstIdx + 1] = imgData[srcIdx + 1];
                        canvas[dstIdx + 2] = imgData[srcIdx + 2];
                    }
                }
            }

            // Transform labels
            float padW = x1a - x1b;
            float padH = y1a - y1b;

            foreach (var inst in labels[i])
            {
                var newInst = inst.Clone();
                // Denormalize from original image
                newInst.Denormalize(iw, ih);
                // Add mosaic offset
                newInst.AddPadding(padW, padH);
                // Clip to canvas
                newInst.Clip(mosaicW, mosaicH);
                // Normalize to mosaic size
                newInst.Normalize(mosaicW, mosaicH);

                if (newInst.IsValid(minArea: 10.0f / (mosaicW * mosaicH)))
                    mergedLabels.Add(newInst);
            }
        }

        return (canvas, mosaicW, mosaicH, mergedLabels);
    }
}
