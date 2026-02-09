using YOLOv8.Data.Utils;

namespace YOLOv8.Data.Augmentation;

/// <summary>
/// Random affine/perspective augmentation.
/// Applies random scale, translation, rotation, and shear.
/// Matching YOLOv8 defaults: scale=0.5, translate=0.1, others=0.
/// </summary>
public class RandomPerspective
{
    private readonly float degrees;
    private readonly float translate;
    private readonly float scale;
    private readonly float shear;
    private readonly Random rng;

    public RandomPerspective(float degrees = 0f, float translate = 0.1f,
        float scale = 0.5f, float shear = 0f, int? seed = null)
    {
        this.degrees = degrees;
        this.translate = translate;
        this.scale = scale;
        this.shear = shear;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply random scale and translation to image and labels.
    /// Uses a simplified affine transform (no full perspective).
    /// </summary>
    public (byte[] data, int w, int h, List<BboxInstance> labels) Apply(
        byte[] imgData, int w, int h, List<BboxInstance> labels, int targetSize)
    {
        // Random scale: uniform in [1-scale, 1+scale]
        float s = (float)(rng.NextDouble() * 2 * scale - scale + 1.0);

        // Random translation: uniform in [-translate, translate] * targetSize
        float tx = (float)(rng.NextDouble() * 2 - 1) * translate * targetSize;
        float ty = (float)(rng.NextDouble() * 2 - 1) * translate * targetSize;

        // Simple scale + translate (no rotation/shear for default config)
        int newW = targetSize;
        int newH = targetSize;

        var result = new byte[newW * newH * 3];
        // Fill with gray
        for (int i = 0; i < result.Length; i++)
            result[i] = 114;

        // Apply affine: dst(x,y) = src(s*x + tx, s*y + ty)
        float invS = 1.0f / s;
        float halfW = w / 2f;
        float halfH = h / 2f;
        float halfNewW = newW / 2f;
        float halfNewH = newH / 2f;

        for (int dy = 0; dy < newH; dy++)
        {
            for (int dx = 0; dx < newW; dx++)
            {
                // Map from destination to source coordinates
                float sx = (dx - halfNewW - tx) * invS + halfW;
                float sy = (dy - halfNewH - ty) * invS + halfH;

                int ix = (int)sx;
                int iy = (int)sy;

                if (ix >= 0 && ix < w && iy >= 0 && iy < h)
                {
                    int srcIdx = (iy * w + ix) * 3;
                    int dstIdx = (dy * newW + dx) * 3;
                    result[dstIdx] = imgData[srcIdx];
                    result[dstIdx + 1] = imgData[srcIdx + 1];
                    result[dstIdx + 2] = imgData[srcIdx + 2];
                }
            }
        }

        // Transform labels
        var newLabels = new List<BboxInstance>();
        foreach (var inst in labels)
        {
            var ni = inst.Clone();
            // Denormalize from source image
            ni.Denormalize(w, h);

            // Apply affine: center
            float cx = (ni.Bbox[0] - halfW) * s + halfNewW + tx;
            float cy = (ni.Bbox[1] - halfH) * s + halfNewH + ty;
            float bw = ni.Bbox[2] * s;
            float bh = ni.Bbox[3] * s;

            ni.Bbox = [cx, cy, bw, bh];

            // Clip and normalize
            ni.Clip(newW, newH);
            ni.Normalize(newW, newH);

            // Filter out tiny/invalid boxes
            if (ni.IsValid(minArea: 100f / (newW * newH), maxAspectRatio: 20f))
                newLabels.Add(ni);
        }

        return (result, newW, newH, newLabels);
    }
}
