using YOLO.Training;

namespace YOLO.WinForms.Services;

/// <summary>
/// Event args for epoch completion with metrics.
/// </summary>
public class EpochCompletedEventArgs : EventArgs
{
    public int Epoch { get; init; }
    public int TotalEpochs { get; init; }
    public double BoxLoss { get; init; }
    public double ClsLoss { get; init; }
    public double DflLoss { get; init; }
    public double Map50 { get; init; }
    public double Map5095 { get; init; }
    public double Fitness { get; init; }
    public double LearningRate { get; init; }
}

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
/// Provides async execution with cancellation and progress reporting.
/// </summary>
public class TrainingService
{
    private CancellationTokenSource? _cts;

    public event EventHandler<TrainResult>? TrainingCompleted;
    public event EventHandler<string>? TrainingFailed;
    public event EventHandler<string>? LogMessage;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Start training asynchronously in a background thread.
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
                var result = trainer.Train(trainDataDir, valDataDir, classNames);

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
