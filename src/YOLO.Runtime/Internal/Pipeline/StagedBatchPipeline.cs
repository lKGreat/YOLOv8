using System.Threading.Channels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YOLO.Runtime.Internal.Backend;
using YOLO.Runtime.Internal.Logging;
using YOLO.Runtime.Internal.Memory;
using YOLO.Runtime.Internal.PostProcessing;
using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.Pipeline;

/// <summary>
/// High-throughput three-stage batch inference pipeline.
/// <para>
/// Stage 1 (Preprocess): N workers load images and produce CHW tensors in parallel.<br/>
/// Stage 2 (Inference):  M workers run inference on separate ONNX sessions in parallel.<br/>
/// Stage 3 (Postprocess): N workers run NMS / coordinate mapping in parallel.
/// </para>
/// Stages are connected by bounded <see cref="Channel{T}"/> so they overlap:
/// while image K is being inferred, image K+1 is being preprocessed,
/// and image K-1 is being postprocessed.
/// </summary>
internal sealed class StagedBatchPipeline : IDisposable
{
    private readonly InferenceSessionPool _sessionPool;
    private readonly IPostProcessor _postProcessor;
    private readonly YoloOptions _options;
    private readonly int _preprocessWorkers;
    private readonly string _modelName;
    private bool _disposed;

    /// <summary>
    /// Create a staged pipeline backed by a multi-session pool.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model.</param>
    /// <param name="options">Inference options.</param>
    /// <param name="sessionCount">Number of parallel ONNX sessions.</param>
    /// <param name="preprocessWorkers">Number of preprocess worker tasks.</param>
    public StagedBatchPipeline(
        string modelPath,
        YoloOptions options,
        int sessionCount,
        int preprocessWorkers)
    {
        _options = options;
        _modelName = Path.GetFileName(modelPath);
        _sessionPool = new InferenceSessionPool(modelPath, options, sessionCount);
        _postProcessor = PostProcessorFactory.Create(options.ModelVersion);
        _preprocessWorkers = Math.Max(1, preprocessWorkers);
    }

    // ── Item types flowing between stages ────────────────────────────────

    private readonly record struct PreprocessedItem(
        int Index,
        BufferHandle Buffer,
        PreProcessContext Context);

    private readonly record struct InferredItem(
        int Index,
        BufferHandle OutputData,
        int[] OutputShape,
        PreProcessContext Context);

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Run detection on a batch of image file paths using the three-stage pipeline.
    /// </summary>
    public async Task<DetectionResult[][]> DetectBatchAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken ct = default)
    {
        int count = imagePaths.Count;
        var results = new DetectionResult[count][];

        // Bounded channels: limit memory by allowing at most sessionCount items in flight
        int capacity = Math.Max(_sessionPool.SessionCount * 2, 4);
        var preCh = Channel.CreateBounded<PreprocessedItem>(
            new BoundedChannelOptions(capacity) { SingleReader = false });
        var inferCh = Channel.CreateBounded<InferredItem>(
            new BoundedChannelOptions(capacity) { SingleReader = false });

        // Launch all three stages concurrently
        var preTask = RunPreprocessStage(imagePaths, preCh.Writer, ct);
        var inferTask = RunInferenceStage(preCh.Reader, inferCh.Writer, ct);
        var postTask = RunPostprocessStage(inferCh.Reader, results, ct);

        await Task.WhenAll(preTask, inferTask, postTask);
        return results;
    }

    /// <summary>
    /// Run detection on a batch of pre-loaded images using the three-stage pipeline.
    /// Caller retains ownership of the images (they are not disposed).
    /// </summary>
    public async Task<DetectionResult[][]> DetectBatchAsync(
        IReadOnlyList<Image<Rgb24>> images,
        CancellationToken ct = default)
    {
        int count = images.Count;
        var results = new DetectionResult[count][];

        int capacity = Math.Max(_sessionPool.SessionCount * 2, 4);
        var preCh = Channel.CreateBounded<PreprocessedItem>(
            new BoundedChannelOptions(capacity) { SingleReader = false });
        var inferCh = Channel.CreateBounded<InferredItem>(
            new BoundedChannelOptions(capacity) { SingleReader = false });

        var preTask = RunPreprocessStageImages(images, preCh.Writer, ct);
        var inferTask = RunInferenceStage(preCh.Reader, inferCh.Writer, ct);
        var postTask = RunPostprocessStage(inferCh.Reader, results, ct);

        await Task.WhenAll(preTask, inferTask, postTask);
        return results;
    }

    // ── Stage 1: Preprocess (N workers) ──────────────────────────────────

    private async Task RunPreprocessStage(
        IReadOnlyList<string> imagePaths,
        ChannelWriter<PreprocessedItem> writer,
        CancellationToken ct)
    {
        try
        {
            // Partition work across N workers
            int count = imagePaths.Count;
            var indexChannel = Channel.CreateUnbounded<int>(
                new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

            // Feed indices
            for (int i = 0; i < count; i++)
                indexChannel.Writer.TryWrite(i);
            indexChannel.Writer.Complete();

            // Launch N parallel workers
            var workers = new Task[_preprocessWorkers];
            for (int w = 0; w < _preprocessWorkers; w++)
            {
                workers[w] = Task.Run(async () =>
                {
                    await foreach (var idx in indexChannel.Reader.ReadAllAsync(ct))
                    {
                        try
                        {
                            var (buffer, context) = YoloPreProcessor.Process(
                                imagePaths[idx], _options.ImgSize);
                            await writer.WriteAsync(new PreprocessedItem(idx, buffer, context), ct);
                        }
                        catch (Exception ex)
                        {
                            InferenceLogger.LogError(_modelName, ex);
                            // Write a sentinel with empty buffer — postprocess will handle it
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workers);
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task RunPreprocessStageImages(
        IReadOnlyList<Image<Rgb24>> images,
        ChannelWriter<PreprocessedItem> writer,
        CancellationToken ct)
    {
        try
        {
            int count = images.Count;
            var indexChannel = Channel.CreateUnbounded<int>(
                new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

            for (int i = 0; i < count; i++)
                indexChannel.Writer.TryWrite(i);
            indexChannel.Writer.Complete();

            var workers = new Task[_preprocessWorkers];
            for (int w = 0; w < _preprocessWorkers; w++)
            {
                workers[w] = Task.Run(async () =>
                {
                    await foreach (var idx in indexChannel.Reader.ReadAllAsync(ct))
                    {
                        try
                        {
                            var (buffer, context) = YoloPreProcessor.Process(
                                images[idx], _options.ImgSize);
                            await writer.WriteAsync(new PreprocessedItem(idx, buffer, context), ct);
                        }
                        catch (Exception ex)
                        {
                            InferenceLogger.LogError(_modelName, ex);
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workers);
        }
        finally
        {
            writer.Complete();
        }
    }

    // ── Stage 2: Inference (M workers, one per session) ──────────────────

    private async Task RunInferenceStage(
        ChannelReader<PreprocessedItem> reader,
        ChannelWriter<InferredItem> writer,
        CancellationToken ct)
    {
        try
        {
            // Launch M inference workers (one per session in the pool)
            int inferWorkers = _sessionPool.SessionCount;
            var workers = new Task[inferWorkers];

            for (int w = 0; w < inferWorkers; w++)
            {
                workers[w] = Task.Run(async () =>
                {
                    await foreach (var item in reader.ReadAllAsync(ct))
                    {
                        try
                        {
                            var (outputHandle, outputShape) = _sessionPool.Run(
                                item.Buffer.Span, _sessionPool.InputShape);

                            await writer.WriteAsync(
                                new InferredItem(item.Index, outputHandle, outputShape, item.Context), ct);
                        }
                        catch (Exception ex)
                        {
                            InferenceLogger.LogError(_modelName, ex);
                        }
                        finally
                        {
                            item.Buffer.Dispose();
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workers);
        }
        finally
        {
            writer.Complete();
        }
    }

    // ── Stage 3: Postprocess (concurrent, lightweight) ───────────────────

    private async Task RunPostprocessStage(
        ChannelReader<InferredItem> reader,
        DetectionResult[][] results,
        CancellationToken ct)
    {
        // Postprocessing is lightweight (NMS + coordinate mapping),
        // so we use a small number of workers.
        int postWorkers = Math.Max(1, _preprocessWorkers / 2);
        var workers = new Task[postWorkers];

        for (int w = 0; w < postWorkers; w++)
        {
            workers[w] = Task.Run(async () =>
            {
                await foreach (var item in reader.ReadAllAsync(ct))
                {
                    try
                    {
                        var detections = _postProcessor.ProcessDetections(
                            item.OutputData.Span, item.OutputShape, item.Context, _options);
                        results[item.Index] = detections;
                    }
                    catch (Exception ex)
                    {
                        InferenceLogger.LogError(_modelName, ex);
                        results[item.Index] = [];
                    }
                    finally
                    {
                        item.OutputData.Dispose();
                    }
                }
            }, ct);
        }

        await Task.WhenAll(workers);

        // Fill any gaps (failed preprocessing items that never reached postprocess)
        for (int i = 0; i < results.Length; i++)
            results[i] ??= [];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionPool.Dispose();
    }
}
