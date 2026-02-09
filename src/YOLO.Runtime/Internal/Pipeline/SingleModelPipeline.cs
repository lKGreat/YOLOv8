using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YOLO.Runtime.Internal.Backend;
using YOLO.Runtime.Internal.Logging;
using YOLO.Runtime.Internal.PostProcessing;
using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.Pipeline;

/// <summary>
/// Internal orchestrator for a single model: preprocess -> infer -> postprocess.
/// Wraps timing and logging for each stage.
/// </summary>
internal sealed class SingleModelPipeline : IDisposable
{
    private readonly IInferenceBackend _backend;
    private readonly IPostProcessor _postProcessor;
    private readonly YoloOptions _options;
    private readonly string _modelName;
    private readonly int[] _inputShape; // cached — never changes after construction
    private bool _disposed;

    public SingleModelPipeline(string modelPath, YoloOptions options)
    {
        _options = options;
        _modelName = Path.GetFileName(modelPath);
        _backend = BackendFactory.Create(modelPath, options);
        _inputShape = [1, 3, options.ImgSize, options.ImgSize];

        // Create postprocessor based on version
        _postProcessor = PostProcessorFactory.Create(options.ModelVersion);
    }

    /// <summary>
    /// Run full inference pipeline on a file path.
    /// </summary>
    public DetectionResult[] Detect(string imagePath)
    {
        var totalSw = Stopwatch.StartNew();

        // Preprocess
        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(imagePath, _options.ImgSize);
        preSw.Stop();

        try
        {
            // Infer
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                // Postprocess
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessDetections(
                    outputHandle.Span, outputShape, context, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Run full inference pipeline on raw image bytes.
    /// </summary>
    public DetectionResult[] Detect(ReadOnlySpan<byte> imageBytes)
    {
        var totalSw = Stopwatch.StartNew();

        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(imageBytes, _options.ImgSize);
        preSw.Stop();

        try
        {
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessDetections(
                    outputHandle.Span, outputShape, context, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Run full inference pipeline on a Stream.
    /// </summary>
    public DetectionResult[] Detect(Stream imageStream)
    {
        var totalSw = Stopwatch.StartNew();

        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(imageStream, _options.ImgSize);
        preSw.Stop();

        try
        {
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessDetections(
                    outputHandle.Span, outputShape, context, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Run inference on a pre-loaded Image. Caller retains ownership of the image.
    /// </summary>
    public DetectionResult[] Detect(Image<Rgb24> image)
    {
        var totalSw = Stopwatch.StartNew();

        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(image, _options.ImgSize);
        preSw.Stop();

        try
        {
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessDetections(
                    outputHandle.Span, outputShape, context, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Run inference asynchronously on a file path.
    /// </summary>
    public Task<DetectionResult[]> DetectAsync(string imagePath, CancellationToken ct = default)
    {
        return Task.Run(() => Detect(imagePath), ct);
    }

    /// <summary>
    /// Run inference asynchronously on raw image bytes.
    /// </summary>
    public Task<DetectionResult[]> DetectAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        return Task.Run(() => Detect(imageBytes.AsSpan()), ct);
    }

    /// <summary>
    /// Run inference asynchronously on a pre-loaded image.
    /// </summary>
    public Task<DetectionResult[]> DetectAsync(Image<Rgb24> image, CancellationToken ct = default)
    {
        return Task.Run(() => Detect(image), ct);
    }

    /// <summary>
    /// Run classification on a file path.
    /// </summary>
    public ClassificationResult[] Classify(string imagePath)
    {
        var totalSw = Stopwatch.StartNew();

        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(imagePath, _options.ImgSize);
        preSw.Stop();

        try
        {
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessClassifications(
                    outputHandle.Span, outputShape, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Run classification on a pre-loaded image.
    /// </summary>
    public ClassificationResult[] Classify(Image<Rgb24> image)
    {
        var totalSw = Stopwatch.StartNew();

        var preSw = Stopwatch.StartNew();
        var (buffer, context) = YoloPreProcessor.Process(image, _options.ImgSize);
        preSw.Stop();

        try
        {
            var inferSw = Stopwatch.StartNew();
            var (outputHandle, outputShape) = _backend.Run(buffer.Span, _inputShape);
            inferSw.Stop();

            try
            {
                var postSw = Stopwatch.StartNew();
                var results = _postProcessor.ProcessClassifications(
                    outputHandle.Span, outputShape, _options);
                postSw.Stop();

                totalSw.Stop();
                InferenceLogger.LogTiming(_modelName, preSw.Elapsed, inferSw.Elapsed, postSw.Elapsed, totalSw.Elapsed);

                return results;
            }
            finally
            {
                outputHandle.Dispose();
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// The backend name for logging.
    /// </summary>
    public string ModelName => _modelName;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backend.Dispose();
    }
}
