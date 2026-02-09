using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YOLO.Runtime.Internal.Memory;

namespace YOLO.Runtime.Internal.PreProcessing;

/// <summary>
/// YOLO image preprocessor: letterbox resize + normalize to [0,1] in CHW format.
/// Writes directly into a rented buffer for zero-allocation inference.
/// </summary>
internal static class YoloPreProcessor
{
    /// <summary>
    /// Preprocess an image from file path.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(string imagePath, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imagePath);
        return Process(image, imgSize);
    }

    /// <summary>
    /// Preprocess an image from raw bytes (e.g. PNG/JPEG encoded).
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(ReadOnlySpan<byte> imageBytes, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        return Process(image, imgSize);
    }

    /// <summary>
    /// Preprocess an image from a stream.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(Stream imageStream, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imageStream);
        return Process(image, imgSize);
    }

    /// <summary>
    /// Preprocess a loaded image. The caller retains ownership of the image.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(Image<Rgb24> image, int imgSize)
    {
        int origW = image.Width;
        int origH = image.Height;

        // Compute letterbox parameters
        float ratio = Math.Min((float)imgSize / origH, (float)imgSize / origW);
        ratio = Math.Min(ratio, 1.0f); // Only scale down, never up

        int newW = (int)Math.Round(origW * ratio);
        int newH = (int)Math.Round(origH * ratio);

        float padX = (imgSize - newW) / 2.0f;
        float padY = (imgSize - newH) / 2.0f;

        int left = (int)Math.Round(padX - 0.1);
        int top = (int)Math.Round(padY - 0.1);

        // Rent a buffer for 3 x imgSize x imgSize
        int bufferSize = 3 * imgSize * imgSize;
        var handle = TensorBufferPool.Rent(bufferSize);
        var dataArray = handle.Array; // Use the underlying array (not Span) for lambda capture

        // Fill with gray padding (114/255)
        float gray = 114.0f / 255.0f;
        handle.Span.Fill(gray);

        // Resize the image
        using var resized = image.Clone(ctx => ctx.Resize(newW, newH));

        // Copy resized image pixels into CHW buffer, normalized to [0,1]
        int imgSizeCapture = imgSize; // local copy for lambda
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < newH && (y + top) < imgSizeCapture; y++)
            {
                var row = accessor.GetRowSpan(y);
                int dy = y + top;
                if (dy < 0) continue;

                for (int x = 0; x < newW && (x + left) < imgSizeCapture; x++)
                {
                    int dx = x + left;
                    if (dx < 0) continue;

                    var pixel = row[x];
                    dataArray[0 * imgSizeCapture * imgSizeCapture + dy * imgSizeCapture + dx] = pixel.R / 255.0f;
                    dataArray[1 * imgSizeCapture * imgSizeCapture + dy * imgSizeCapture + dx] = pixel.G / 255.0f;
                    dataArray[2 * imgSizeCapture * imgSizeCapture + dy * imgSizeCapture + dx] = pixel.B / 255.0f;
                }
            }
        });

        var context = new PreProcessContext(origW, origH, ratio, padX, padY, imgSize);
        return (handle, context);
    }
}
