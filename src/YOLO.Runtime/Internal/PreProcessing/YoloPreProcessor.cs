using System.Runtime.InteropServices;
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
    /// <summary>1/255 as a multiply — faster than per-pixel division.</summary>
    private const float Scale = 1.0f / 255.0f;

    /// <summary>
    /// Preprocess an image from file path.
    /// Uses Mutate (in-place resize) since we own the image exclusively.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(string imagePath, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imagePath);
        return ProcessOwned(image, imgSize);
    }

    /// <summary>
    /// Preprocess an image from raw bytes (e.g. PNG/JPEG encoded).
    /// Uses Mutate (in-place resize) since we own the image exclusively.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(ReadOnlySpan<byte> imageBytes, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        return ProcessOwned(image, imgSize);
    }

    /// <summary>
    /// Preprocess an image from a stream.
    /// Uses Mutate (in-place resize) since we own the image exclusively.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(Stream imageStream, int imgSize)
    {
        using var image = Image.Load<Rgb24>(imageStream);
        return ProcessOwned(image, imgSize);
    }

    /// <summary>
    /// Preprocess a loaded image. The caller retains ownership of the image,
    /// so we must Clone before resizing.
    /// </summary>
    public static (BufferHandle buffer, PreProcessContext context) Process(Image<Rgb24> image, int imgSize)
    {
        var (origW, origH, ratio, newW, newH, padX, padY, left, top) = ComputeLetterbox(image, imgSize);

        // Clone because caller owns the original
        using var resized = image.Clone(ctx => ctx.Resize(newW, newH));

        var handle = FillBuffer(resized, imgSize, newW, newH, left, top);
        var context = new PreProcessContext(origW, origH, ratio, padX, padY, imgSize);
        return (handle, context);
    }

    /// <summary>
    /// Fast path for images we own exclusively — Mutate in-place to avoid a Clone allocation.
    /// </summary>
    private static (BufferHandle buffer, PreProcessContext context) ProcessOwned(Image<Rgb24> image, int imgSize)
    {
        var (origW, origH, ratio, newW, newH, padX, padY, left, top) = ComputeLetterbox(image, imgSize);

        // Mutate in-place — no Clone allocation since we own the image
        image.Mutate(ctx => ctx.Resize(newW, newH));

        var handle = FillBuffer(image, imgSize, newW, newH, left, top);
        var context = new PreProcessContext(origW, origH, ratio, padX, padY, imgSize);
        return (handle, context);
    }

    /// <summary>
    /// Compute letterbox resize parameters.
    /// </summary>
    private static (int origW, int origH, float ratio, int newW, int newH,
                     float padX, float padY, int left, int top)
        ComputeLetterbox(Image<Rgb24> image, int imgSize)
    {
        int origW = image.Width;
        int origH = image.Height;

        float ratio = Math.Min((float)imgSize / origH, (float)imgSize / origW);
        ratio = Math.Min(ratio, 1.0f); // Only scale down, never up

        int newW = (int)Math.Round(origW * ratio);
        int newH = (int)Math.Round(origH * ratio);

        float padX = (imgSize - newW) / 2.0f;
        float padY = (imgSize - newH) / 2.0f;

        int left = (int)Math.Round(padX - 0.1);
        int top = (int)Math.Round(padY - 0.1);

        return (origW, origH, ratio, newW, newH, padX, padY, left, top);
    }

    /// <summary>
    /// Fill the CHW tensor buffer from a (already resized) image.
    /// Uses pre-computed valid copy region to eliminate per-pixel bounds checks,
    /// raw byte access via MemoryMarshal, and multiply instead of divide.
    /// </summary>
    private static BufferHandle FillBuffer(Image<Rgb24> resized, int imgSize, int newW, int newH, int left, int top)
    {
        int bufferSize = 3 * imgSize * imgSize;
        var handle = TensorBufferPool.Rent(bufferSize);
        var dataArray = handle.Array;

        // Fill with gray padding (114/255)
        handle.Span.Fill(114.0f * Scale);

        // Pre-compute the valid copy region — eliminates per-pixel bounds checks
        int startX = Math.Max(0, -left);
        int startY = Math.Max(0, -top);
        int endX = Math.Min(newW, imgSize - left);
        int endY = Math.Min(newH, imgSize - top);
        int copyWidth = endX - startX;

        if (copyWidth <= 0 || startY >= endY)
            return handle;

        // Pre-compute channel plane offsets
        int planeSize = imgSize * imgSize;
        int planeR = 0;
        int planeG = planeSize;
        int planeB = 2 * planeSize;

        resized.ProcessPixelRows(accessor =>
        {
            for (int y = startY; y < endY; y++)
            {
                var row = accessor.GetRowSpan(y);
                // Access raw bytes: [R0, G0, B0, R1, G1, B1, ...]
                var rowBytes = MemoryMarshal.AsBytes(row);

                int dy = y + top;
                int baseOffset = dy * imgSize + left + startX;
                int byteStart = startX * 3;

                for (int i = 0; i < copyWidth; i++)
                {
                    int bIdx = byteStart + i * 3;
                    int outIdx = baseOffset + i;
                    dataArray[planeR + outIdx] = rowBytes[bIdx] * Scale;
                    dataArray[planeG + outIdx] = rowBytes[bIdx + 1] * Scale;
                    dataArray[planeB + outIdx] = rowBytes[bIdx + 2] * Scale;
                }
            }
        });

        return handle;
    }
}
