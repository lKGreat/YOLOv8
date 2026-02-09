using YOLO.Core.Abstractions;
using YOLO.Export;

namespace YOLO.WinForms.Services;

/// <summary>
/// Service that wraps model export for background execution in WinForms.
/// </summary>
public class ExportService
{
    private CancellationTokenSource? _cts;

    public event EventHandler<ExportResult>? ExportCompleted;
    public event EventHandler<string>? ExportFailed;
    public event EventHandler<string>? LogMessage;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Export a model asynchronously.
    /// </summary>
    public async Task<ExportResult?> ExportAsync(
        YOLOModel model,
        ExportConfig config,
        IProgress<ExportProgress>? progress = null)
    {
        _cts = new CancellationTokenSource();

        try
        {
            LogMessage?.Invoke(this, $"Starting export to {config.Format}...");
            LogMessage?.Invoke(this, $"Output: {config.OutputPath}");

            var exporter = ExporterFactory.Create(config.Format);
            var result = await exporter.ExportAsync(model, config, progress, _cts.Token);

            if (result.Success)
            {
                LogMessage?.Invoke(this, $"Export successful: {result.OutputPath} ({result.FileSizeBytes / 1024.0 / 1024.0:F1} MB)");
                ExportCompleted?.Invoke(this, result);
            }
            else
            {
                LogMessage?.Invoke(this, $"Export failed: {result.ErrorMessage}");
                ExportFailed?.Invoke(this, result.ErrorMessage ?? "Unknown error");
            }

            return result;
        }
        catch (Exception ex)
        {
            var msg = $"Export failed: {ex.Message}";
            LogMessage?.Invoke(this, msg);
            ExportFailed?.Invoke(this, msg);
            return null;
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void Reset()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
