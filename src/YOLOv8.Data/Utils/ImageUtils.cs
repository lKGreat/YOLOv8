using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TorchSharp;
using static TorchSharp.torch;

namespace YOLOv8.Data.Utils;

/// <summary>
/// Image I/O and conversion utilities using SixLabors.ImageSharp.
/// </summary>
public static class ImageUtils
{
    /// <summary>
    /// Load an image from disk as a float32 tensor in CHW format (0-255 range).
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>Tensor of shape (3, H, W) in RGB order, values 0-255</returns>
    public static (Tensor image, int width, int height) LoadImage(string path)
    {
        using var img = Image.Load<Rgb24>(path);
        int w = img.Width;
        int h = img.Height;

        var data = new float[3 * h * w];
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var pixel = row[x];
                    data[0 * h * w + y * w + x] = pixel.R; // R channel
                    data[1 * h * w + y * w + x] = pixel.G; // G channel
                    data[2 * h * w + y * w + x] = pixel.B; // B channel
                }
            }
        });

        var tensor = torch.tensor(data, dtype: ScalarType.Float32).reshape(3, h, w);
        return (tensor, w, h);
    }

    /// <summary>
    /// Load an image as a raw byte array in HWC BGR format (for augmentation).
    /// </summary>
    public static (byte[] data, int width, int height) LoadImageHWC(string path)
    {
        using var img = Image.Load<Rgb24>(path);
        int w = img.Width;
        int h = img.Height;

        var data = new byte[h * w * 3];
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var pixel = row[x];
                    int idx = (y * w + x) * 3;
                    data[idx + 0] = pixel.R;
                    data[idx + 1] = pixel.G;
                    data[idx + 2] = pixel.B;
                }
            }
        });

        return (data, w, h);
    }

    /// <summary>
    /// Convert HWC byte array to CHW float tensor (0-255).
    /// </summary>
    public static Tensor HWCToTensor(byte[] data, int width, int height)
    {
        var floats = new float[3 * height * width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = (y * width + x) * 3;
                floats[0 * height * width + y * width + x] = data[srcIdx + 0]; // R
                floats[1 * height * width + y * width + x] = data[srcIdx + 1]; // G
                floats[2 * height * width + y * width + x] = data[srcIdx + 2]; // B
            }
        }
        return torch.tensor(floats, dtype: ScalarType.Float32).reshape(3, height, width);
    }

    /// <summary>
    /// Resize an image maintaining byte array format.
    /// </summary>
    public static (byte[] data, int newW, int newH) Resize(byte[] data, int w, int h, int targetW, int targetH)
    {
        using var img = Image.LoadPixelData<Rgb24>(data, w, h);
        img.Mutate(ctx => ctx.Resize(targetW, targetH));
        var result = new byte[targetW * targetH * 3];
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < targetH; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < targetW; x++)
                {
                    var pixel = row[x];
                    int idx = (y * targetW + x) * 3;
                    result[idx + 0] = pixel.R;
                    result[idx + 1] = pixel.G;
                    result[idx + 2] = pixel.B;
                }
            }
        });
        return (result, targetW, targetH);
    }
}
