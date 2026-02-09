using YOLO.Data.Utils;

namespace YOLO.Data.Augmentation;

/// <summary>
/// Random horizontal/vertical flip augmentation.
/// Default: horizontal flip with p=0.5, vertical flip disabled.
/// </summary>
public class RandomFlip
{
    private readonly double horizontalProb;
    private readonly double verticalProb;
    private readonly Random rng;

    public RandomFlip(double horizontalProb = 0.5, double verticalProb = 0.0, int? seed = null)
    {
        this.horizontalProb = horizontalProb;
        this.verticalProb = verticalProb;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply random flip to image and labels.
    /// </summary>
    public void Apply(byte[] data, int w, int h, List<BboxInstance>? labels = null)
    {
        // Horizontal flip
        if (rng.NextDouble() < horizontalProb)
        {
            FlipHorizontal(data, w, h);
            if (labels != null)
            {
                foreach (var inst in labels)
                    inst.FlipLR(1.0f); // Labels are normalized
            }
        }

        // Vertical flip
        if (rng.NextDouble() < verticalProb)
        {
            FlipVertical(data, w, h);
            if (labels != null)
            {
                foreach (var inst in labels)
                    inst.FlipUD(1.0f);
            }
        }
    }

    private static void FlipHorizontal(byte[] data, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w / 2; x++)
            {
                int left = (y * w + x) * 3;
                int right = (y * w + (w - 1 - x)) * 3;

                // Swap pixels
                for (int c = 0; c < 3; c++)
                    (data[left + c], data[right + c]) = (data[right + c], data[left + c]);
            }
        }
    }

    private static void FlipVertical(byte[] data, int w, int h)
    {
        int rowBytes = w * 3;
        var temp = new byte[rowBytes];

        for (int y = 0; y < h / 2; y++)
        {
            int topOffset = y * rowBytes;
            int bottomOffset = (h - 1 - y) * rowBytes;

            Array.Copy(data, topOffset, temp, 0, rowBytes);
            Array.Copy(data, bottomOffset, data, topOffset, rowBytes);
            Array.Copy(temp, 0, data, bottomOffset, rowBytes);
        }
    }
}
