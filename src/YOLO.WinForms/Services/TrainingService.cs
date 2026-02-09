using YOLO.Training;

namespace YOLO.WinForms.Services;

/// <summary>
/// Progress info for training.
/// </summary>
public record TrainingProgress(
    string Stage,
    int PercentComplete,
    string? Message = null
);

/// <summary>
/// Service that wraps the Trainer for background execution in WinForms.
/// Provides async execution with cancellation, progress reporting,
/// and real-time per-epoch metrics via the EpochCompleted event.
/// </summary>
public class TrainingService
{
    private CancellationTokenSource? _cts;

    /// <summary>Fired after each epoch with live metrics (box/cls/dfl loss, mAP, LR, etc.).</summary>
    public event EventHandler<EpochMetrics>? EpochCompleted;

    /// <summary>Fired when the entire training run finishes successfully.</summary>
    public event EventHandler<TrainResult>? TrainingCompleted;

    /// <summary>Fired when training fails with an exception.</summary>
    public event EventHandler<string>? TrainingFailed;

    /// <summary>General log messages.</summary>
    public event EventHandler<string>? LogMessage;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Start training asynchronously in a background thread.
    /// Per-epoch metrics are reported via the <see cref="EpochCompleted"/> event.
    /// </summary>
    public Task<TrainResult?> TrainAsync(
        TrainConfig config,
        string trainDataDir,
        string? valDataDir,
        string[]? classNames = null,
        IProgress<TrainingProgress>? progress = null)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(new TrainingProgress("Initializing", 0, "Creating model and datasets..."));
                LogMessage?.Invoke(this, $"Starting training: {config.ModelVersion}{config.ModelVariant}");
                LogMessage?.Invoke(this, $"Epochs: {config.Epochs}, Batch: {config.BatchSize}, ImgSize: {config.ImgSize}");

                var trainer = new Trainer(config);

                // Pass per-epoch callback so UI can update charts in real-time
                var result = trainer.Train(trainDataDir, valDataDir, classNames,
                    onEpochCompleted: metrics =>
                    {
                        // Forward to event subscribers (TrainingPanel updates MetricsChart here)
                        EpochCompleted?.Invoke(this, metrics);

                        // Also report progress percentage
                        int pct = (int)(100.0 * metrics.Epoch / metrics.TotalEpochs);
                        progress?.Report(new TrainingProgress("Training",
                            pct, $"Epoch {metrics.Epoch}/{metrics.TotalEpochs}"));

                        // Check cancellation between epochs
                        ct.ThrowIfCancellationRequested();
                    });

                progress?.Report(new TrainingProgress("Complete", 100, "Training finished."));
                TrainingCompleted?.Invoke(this, result);

                return result;
            }
            catch (OperationCanceledException)
            {
                LogMessage?.Invoke(this, "Training cancelled by user.");
                return null;
            }
            catch (Exception ex)
            {
                var msg = $"Training failed: {ex.Message}";
                LogMessage?.Invoke(this, msg);
                TrainingFailed?.Invoke(this, msg);
                return null;
            }
        }, ct);
    }

    /// <summary>
    /// Cancel the current training run.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
        LogMessage?.Invoke(this, "Cancellation requested...");
    }

    /// <summary>
    /// Dispose of the cancellation token source.
    /// </summary>
    public void Reset()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
