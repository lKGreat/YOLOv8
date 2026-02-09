using System.Runtime.CompilerServices;
using System.Threading.Channels;
using YOLO.Runtime.Internal.Logging;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.Pipeline;

/// <summary>
/// High-throughput parallel inference pipeline using <see cref="Channel{T}"/>.
/// Supports bounded concurrency via <see cref="SemaphoreSlim"/> to prevent GPU OOM.
/// </summary>
internal sealed class ParallelPipeline : IAsyncDisposable
{
    private readonly SingleModelPipeline _pipeline;
    private readonly int _maxParallelism;

    public ParallelPipeline(SingleModelPipeline pipeline, int maxParallelism)
    {
        _pipeline = pipeline;
        _maxParallelism = Math.Max(1, maxParallelism);
    }

    /// <summary>
    /// Process a batch of image paths in parallel, yielding results as they complete.
    /// Results are returned in the original order.
    /// </summary>
    public async IAsyncEnumerable<(int Index, DetectionResult[] Detections)> ProcessAsync(
        IReadOnlyList<string> imagePaths,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var results = new DetectionResult[imagePaths.Count][];
        var semaphore = new SemaphoreSlim(_maxParallelism);

        // Channel for completed indices
        var completionChannel = Channel.CreateBounded<int>(
            new BoundedChannelOptions(_maxParallelism * 2)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        var processingTask = Task.Run(async () =>
        {
            var tasks = new List<Task>();

            for (int i = 0; i < imagePaths.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await semaphore.WaitAsync(ct);

                int idx = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        results[idx] = await _pipeline.DetectAsync(imagePaths[idx], ct);
                    }
                    catch (Exception ex)
                    {
                        InferenceLogger.LogError(_pipeline.ModelName, ex);
                        results[idx] = [];
                    }
                    finally
                    {
                        semaphore.Release();
                        await completionChannel.Writer.WriteAsync(idx, ct);
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);
            completionChannel.Writer.Complete();
        }, ct);

        // Yield results in completion order
        var yielded = new bool[imagePaths.Count];
        int nextToYield = 0;

        await foreach (var completedIdx in completionChannel.Reader.ReadAllAsync(ct))
        {
            yielded[completedIdx] = true;

            // Yield all consecutive completed results from nextToYield
            while (nextToYield < imagePaths.Count && yielded[nextToYield])
            {
                yield return (nextToYield, results[nextToYield]);
                nextToYield++;
            }
        }

        await processingTask; // Propagate any exceptions
    }

    public ValueTask DisposeAsync()
    {
        // Pipeline is owned by YoloInfer, not us
        return ValueTask.CompletedTask;
    }
}
