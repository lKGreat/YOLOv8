using NLog;

namespace YOLO.Runtime.Internal.Logging;

/// <summary>
/// NLog-based structured logger for inference timing diagnostics.
/// Logs preprocessing, inference, postprocessing, and total elapsed times.
/// </summary>
internal static class InferenceLogger
{
    private static readonly Logger Log = LogManager.GetLogger("YOLO.Runtime");

    /// <summary>
    /// Log timing for a single inference call.
    /// </summary>
    public static void LogTiming(
        string modelName,
        TimeSpan preprocess,
        TimeSpan inference,
        TimeSpan postprocess,
        TimeSpan total)
    {
        Log.Info(
            "[{Model}] Pre={PreMs:F1}ms | Infer={InferMs:F1}ms | Post={PostMs:F1}ms | Total={TotalMs:F1}ms",
            modelName,
            preprocess.TotalMilliseconds,
            inference.TotalMilliseconds,
            postprocess.TotalMilliseconds,
            total.TotalMilliseconds);
    }

    /// <summary>
    /// Log timing for a batch inference call.
    /// </summary>
    public static void LogBatchTiming(
        string modelName,
        int batchSize,
        TimeSpan total)
    {
        Log.Info(
            "[{Model}] Batch={BatchSize} | Total={TotalMs:F1}ms | Avg={AvgMs:F1}ms/image",
            modelName,
            batchSize,
            total.TotalMilliseconds,
            total.TotalMilliseconds / Math.Max(batchSize, 1));
    }

    /// <summary>
    /// Log PDF inference timing.
    /// </summary>
    public static void LogPdfTiming(
        string modelName,
        int pageCount,
        TimeSpan renderTime,
        TimeSpan inferenceTime,
        TimeSpan total)
    {
        Log.Info(
            "[{Model}] PDF pages={Pages} | Render={RenderMs:F1}ms | Infer={InferMs:F1}ms | Total={TotalMs:F1}ms",
            modelName,
            pageCount,
            renderTime.TotalMilliseconds,
            inferenceTime.TotalMilliseconds,
            total.TotalMilliseconds);
    }

    /// <summary>
    /// Log an error during inference.
    /// </summary>
    public static void LogError(string modelName, Exception ex)
    {
        Log.Error(ex, "[{Model}] Inference error: {Message}", modelName, ex.Message);
    }
}
