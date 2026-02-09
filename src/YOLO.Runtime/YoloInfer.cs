using System.Diagnostics;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using YOLO.Runtime.Internal.Logging;
using YOLO.Runtime.Internal.Pipeline;
using YOLO.Runtime.Internal.Visualization;
using YOLO.Runtime.Results;

namespace YOLO.Runtime;

/// <summary>
/// The main entry point for YOLO inference.
/// Supports .onnx (ONNX Runtime) and .pt (TorchSharp) models with auto-detection.
/// <para>
/// <b>Simplest usage:</b>
/// <code>
/// var yolo = new YoloInfer("yolov8n.onnx");
/// DetectionResult[] results = yolo.Detect("image.jpg");
/// </code>
/// </para>
/// </summary>
public sealed class YoloInfer : IDisposable
{
    private readonly SingleModelPipeline _pipeline;
    private readonly YoloOptions _options;
    private readonly string _modelPath;
    private bool _disposed;

    /// <summary>
    /// Create a YOLO inference engine from a model file.
    /// Backend is auto-selected: .onnx -> ONNX Runtime, .pt/.bin -> TorchSharp.
    /// </summary>
    /// <param name="modelPath">Path to .onnx or .pt model file.</param>
    /// <param name="options">Optional configuration. All fields have sensible defaults.</param>
    public YoloInfer(string modelPath, YoloOptions? options = null)
    {
        _modelPath = modelPath;
        _options = options ?? new YoloOptions();
        _pipeline = new SingleModelPipeline(modelPath, _options);
    }

    #region Detect (synchronous)

    /// <summary>
    /// Run detection on an image file.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <returns>Array of detection results in original image coordinates.</returns>
    public DetectionResult[] Detect(string imagePath)
    {
        return _pipeline.Detect(imagePath);
    }

    /// <summary>
    /// Run detection on raw image bytes (e.g. PNG/JPEG encoded).
    /// </summary>
    /// <param name="imageBytes">Encoded image bytes.</param>
    /// <returns>Array of detection results.</returns>
    public DetectionResult[] Detect(byte[] imageBytes)
    {
        return _pipeline.Detect(imageBytes.AsSpan());
    }

    /// <summary>
    /// Run detection on an image stream.
    /// </summary>
    /// <param name="imageStream">Stream containing encoded image data.</param>
    /// <returns>Array of detection results.</returns>
    public DetectionResult[] Detect(Stream imageStream)
    {
        return _pipeline.Detect(imageStream);
    }

    #endregion

    #region Detect (async)

    /// <summary>
    /// Run detection asynchronously on an image file.
    /// </summary>
    public Task<DetectionResult[]> DetectAsync(string imagePath, CancellationToken ct = default)
    {
        return _pipeline.DetectAsync(imagePath, ct);
    }

    /// <summary>
    /// Run detection asynchronously on raw image bytes.
    /// </summary>
    public Task<DetectionResult[]> DetectAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        return _pipeline.DetectAsync(imageBytes, ct);
    }

    #endregion

    #region Batch

    /// <summary>
    /// Run detection on a batch of image files with parallel processing.
    /// </summary>
    /// <param name="imagePaths">Array of image file paths.</param>
    /// <returns>Array of detection result arrays (one per image).</returns>
    public DetectionResult[][] DetectBatch(string[] imagePaths)
    {
        return DetectBatchAsync(imagePaths).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Run detection asynchronously on a batch of image files with parallel processing.
    /// </summary>
    public async Task<DetectionResult[][]> DetectBatchAsync(
        string[] imagePaths, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        var results = new DetectionResult[imagePaths.Length][];
        var semaphore = new SemaphoreSlim(_options.MaxParallelism);

        var tasks = imagePaths.Select(async (path, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                results[index] = await _pipeline.DetectAsync(path, ct);
            }
            catch (Exception ex)
            {
                InferenceLogger.LogError(_pipeline.ModelName, ex);
                results[index] = [];
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        totalSw.Stop();
        InferenceLogger.LogBatchTiming(_pipeline.ModelName, imagePaths.Length, totalSw.Elapsed);

        return results;
    }

    #endregion

    #region DetectAndDraw

    /// <summary>
    /// Run detection and draw results on the original image.
    /// Returns PNG-encoded bytes of the annotated image.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <returns>PNG bytes of the annotated image.</returns>
    public byte[] DetectAndDraw(string imagePath)
    {
        var detections = _pipeline.Detect(imagePath);
        using var image = Image.Load<Rgb24>(imagePath);
        using var annotated = ImageDrawer.Draw(image, detections, _options.ClassNames);
        return ImageToBytes(annotated);
    }

    /// <summary>
    /// Run detection and draw results on the original image from byte array.
    /// </summary>
    public byte[] DetectAndDraw(byte[] imageBytes)
    {
        var detections = _pipeline.Detect(imageBytes.AsSpan());
        using var image = Image.Load<Rgb24>(imageBytes);
        using var annotated = ImageDrawer.Draw(image, detections, _options.ClassNames);
        return ImageToBytes(annotated);
    }

    /// <summary>
    /// Run detection and return both structured results and annotated image bytes.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <returns>Tuple of (detections, annotated PNG bytes).</returns>
    public (DetectionResult[] Detections, byte[] AnnotatedImage) DetectAndDrawDetailed(string imagePath)
    {
        var detections = _pipeline.Detect(imagePath);
        using var image = Image.Load<Rgb24>(imagePath);
        using var annotated = ImageDrawer.Draw(image, detections, _options.ClassNames);
        return (detections, ImageToBytes(annotated));
    }

    /// <summary>
    /// Run detection and draw results asynchronously.
    /// </summary>
    public Task<byte[]> DetectAndDrawAsync(string imagePath, CancellationToken ct = default)
    {
        return Task.Run(() => DetectAndDraw(imagePath), ct);
    }

    /// <summary>
    /// Run detection and draw results asynchronously, returning both.
    /// </summary>
    public Task<(DetectionResult[] Detections, byte[] AnnotatedImage)> DetectAndDrawDetailedAsync(
        string imagePath, CancellationToken ct = default)
    {
        return Task.Run(() => DetectAndDrawDetailed(imagePath), ct);
    }

    #endregion

    #region PDF

    /// <summary>
    /// Run detection on all pages of a PDF file.
    /// Pages are rendered to images and processed in parallel.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <returns>Array of results, one per page.</returns>
    public PageResult[] DetectPdf(string pdfPath)
    {
        return DetectPdfAsync(pdfPath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Run detection on all pages of a PDF file and save annotated PDF.
    /// The original PDF content is preserved — only detection boxes are overlaid.
    /// </summary>
    /// <param name="pdfPath">Path to the input PDF file.</param>
    /// <param name="outputPath">Path to save the annotated PDF file.</param>
    /// <returns>Array of results, one per page.</returns>
    public PageResult[] DetectPdfAndSave(string pdfPath, string outputPath)
    {
        var results = DetectPdfAsync(pdfPath).GetAwaiter().GetResult();

        // Open the original PDF and draw detection boxes on it
        PdfHelper.OverlayDetectionsOnPdf(pdfPath, outputPath, results, _options);

        return results;
    }

    /// <summary>
    /// Run detection on all pages of a PDF file asynchronously.
    /// </summary>
    public async Task<PageResult[]> DetectPdfAsync(string pdfPath, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        // Render PDF pages to images
        var renderSw = Stopwatch.StartNew();
        var pageImages = PdfHelper.RenderPages(pdfPath, _options.PdfDpi);
        renderSw.Stop();

        // Parallel inference on each page
        var inferSw = Stopwatch.StartNew();
        var results = new PageResult[pageImages.Count];
        var semaphore = new SemaphoreSlim(_options.MaxParallelism);

        var tasks = pageImages.Select(async (img, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var detections = await _pipeline.DetectAsync(img, ct);

                byte[]? annotated = null;
                if (_options.DrawPdfResults)
                {
                    using var drawn = ImageDrawer.Draw(img, detections, _options.ClassNames);
                    annotated = ImageToBytes(drawn);
                }

                results[index] = new PageResult(index, detections, annotated);
            }
            catch (Exception ex)
            {
                InferenceLogger.LogError(_pipeline.ModelName, ex);
                results[index] = new PageResult(index, []);
            }
            finally
            {
                semaphore.Release();
                img.Dispose();
            }
        });

        await Task.WhenAll(tasks);
        inferSw.Stop();

        totalSw.Stop();
        InferenceLogger.LogPdfTiming(
            _pipeline.ModelName, pageImages.Count,
            renderSw.Elapsed, inferSw.Elapsed, totalSw.Elapsed);

        return results;
    }

    #endregion

    #region Classification

    /// <summary>
    /// Run classification on an image file.
    /// Use this when the loaded model is a classification model (e.g. yolov8n-cls.onnx).
    /// </summary>
    public ClassificationResult[] Classify(string imagePath)
    {
        return _pipeline.Classify(imagePath);
    }

    #endregion

    /// <summary>
    /// The model file name for logging/display.
    /// </summary>
    public string ModelName => _pipeline.ModelName;

    private static byte[] ImageToBytes(Image<Rgb24> image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipeline.Dispose();
    }
}

/// <summary>
/// Internal helper for PDF page rendering and annotation.
/// </summary>
#pragma warning disable CA1416 // Platform compatibility (PDFium is Windows/Linux/macOS)
internal static class PdfHelper
{
    // YOLO-style color palette (matches ImageDrawer)
    private static readonly XColor[] Palette =
    [
        XColor.FromArgb(0xFF, 0x38, 0x38), XColor.FromArgb(0xFF, 0x9D, 0x97),
        XColor.FromArgb(0xFF, 0x70, 0x1F), XColor.FromArgb(0xFF, 0xB2, 0x1D),
        XColor.FromArgb(0xCF, 0xD2, 0x31), XColor.FromArgb(0x48, 0xF9, 0x0A),
        XColor.FromArgb(0x92, 0xCC, 0x17), XColor.FromArgb(0x3D, 0xDB, 0x86),
        XColor.FromArgb(0x1A, 0x93, 0x34), XColor.FromArgb(0x00, 0xD4, 0xBB),
        XColor.FromArgb(0x2C, 0x99, 0xA8), XColor.FromArgb(0x00, 0xC2, 0xFF),
        XColor.FromArgb(0x34, 0x45, 0x93), XColor.FromArgb(0x64, 0x73, 0xFF),
        XColor.FromArgb(0x00, 0x18, 0xEC), XColor.FromArgb(0x84, 0x38, 0xFF),
        XColor.FromArgb(0x52, 0x00, 0x85), XColor.FromArgb(0xCB, 0x38, 0xFF),
        XColor.FromArgb(0xFF, 0x95, 0xC8), XColor.FromArgb(0xFF, 0x37, 0xC7)
    ];

    /// <summary>
    /// Render PDF pages to images for detection.
    /// </summary>
    public static List<Image<Rgb24>> RenderPages(string pdfPath, int dpi = 200)
    {
        var pages = new List<Image<Rgb24>>();

        int pageCount;
        using (var pdfStream = File.OpenRead(pdfPath))
        {
            pageCount = PDFtoImage.Conversion.GetPageCount(pdfStream);
        }

        var renderOptions = new PDFtoImage.RenderOptions { Dpi = dpi };

        for (int i = 0; i < pageCount; i++)
        {
            using var pdfStream = File.OpenRead(pdfPath);
            using var bitmap = PDFtoImage.Conversion.ToImage(pdfStream, page: new Index(i), options: renderOptions);

            using var skImage = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var encoded = skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(encoded.ToArray());
            var image = Image.Load<Rgb24>(ms);
            pages.Add(image);
        }

        return pages;
    }

    /// <summary>
    /// Open the original PDF, draw detection boxes directly on each page, save as a new file.
    /// The original PDF content (text, vector graphics, images) is fully preserved.
    /// Only bounding boxes + labels are overlaid.
    /// </summary>
    public static void OverlayDetectionsOnPdf(
        string sourcePdfPath,
        string outputPath,
        PageResult[] results,
        YoloOptions options)
    {
        int dpi = options.PdfDpi;
        string[]? classNames = options.ClassNames;

        // Open original PDF
        using var document = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Modify);

        foreach (var result in results)
        {
            if (result.Detections.Length == 0)
                continue;

            if (result.PageIndex < 0 || result.PageIndex >= document.PageCount)
                continue;

            var page = document.Pages[result.PageIndex];

            // PDF page size in points (1 point = 1/72 inch)
            double pageWidthPt = page.Width.Point;
            double pageHeightPt = page.Height.Point;

            // Rendered image size in pixels = page_points * dpi / 72
            // So to convert detection pixel coords → PDF points: pixel * 72 / dpi
            double scale = 72.0 / dpi;

            // Draw on top of existing content
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            double lineWidth = Math.Max(1.0, Math.Min(pageWidthPt, pageHeightPt) / 300.0);
            double fontSize = Math.Max(7.0, Math.Min(pageWidthPt, pageHeightPt) / 60.0);
            var font = new XFont("Arial", fontSize, XFontStyle.Bold);

            foreach (var det in result.Detections)
            {
                var color = Palette[det.ClassId % Palette.Length];

                // Convert pixel coordinates to PDF points
                double x1 = det.X1 * scale;
                double y1 = det.Y1 * scale;
                double x2 = det.X2 * scale;
                double y2 = det.Y2 * scale;
                double w = x2 - x1;
                double h = y2 - y1;

                // Draw bounding box — stroke only, 3px border, no fill
                double borderPt = 3.0 * scale; // 3 pixels converted to points
                var pen = new XPen(color, borderPt);
                gfx.DrawRectangle(pen, x1, y1, w, h);

                // Build label
                string label;
                if (det.ClassName is not null)
                    label = $"{det.ClassName} {det.Confidence:P0}";
                else if (classNames is not null && det.ClassId < classNames.Length)
                    label = $"{classNames[det.ClassId]} {det.Confidence:P0}";
                else
                    label = $"#{det.ClassId} {det.Confidence:P0}";

                // Label background (small solid tag above the box)
                var textSize = gfx.MeasureString(label, font);
                double labelY = Math.Max(y1 - textSize.Height - 2, 0);
                var bgRect = new XRect(x1, labelY, textSize.Width + 6, textSize.Height + 3);
                gfx.DrawRectangle(new XSolidBrush(color), bgRect);

                // Label text
                gfx.DrawString(label, font, XBrushes.White,
                    new XPoint(x1 + 3, labelY + textSize.Height - 1));
            }
        }

        // Save modified PDF to output path
        document.Save(outputPath);
    }
}
