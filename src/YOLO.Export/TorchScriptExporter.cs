using TorchSharp;
using YOLO.Core.Abstractions;
using static TorchSharp.torch;

namespace YOLO.Export;

/// <summary>
/// Exports YOLO models to TorchScript format using TorchSharp's native save.
/// This produces a .pt file that can be loaded by both TorchSharp and Python PyTorch.
/// </summary>
public class TorchScriptExporter : IModelExporter
{
    public string[] SupportedFormats => ["torchscript", "pt"];

    public async Task<ExportResult> ExportAsync(
        YOLOModel model,
        ExportConfig config,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var outputPath = string.IsNullOrEmpty(config.OutputPath)
            ? "model_exported.pt"
            : config.OutputPath;

        try
        {
            progress?.Report(new ExportProgress("Preparing", 10, "Preparing model for export..."));

            ct.ThrowIfCancellationRequested();

            // Set model to eval mode
            model.eval();

            progress?.Report(new ExportProgress("Saving", 50, "Saving TorchSharp model..."));

            // Save in TorchSharp native format
            await Task.Run(() => model.save(outputPath), ct);

            ct.ThrowIfCancellationRequested();

            if (!File.Exists(outputPath))
            {
                return new ExportResult(false, outputPath, "torchscript", 0,
                    "Model file was not created.");
            }

            var fileInfo = new FileInfo(outputPath);
            progress?.Report(new ExportProgress("Complete", 100,
                $"Export complete: {fileInfo.Length / 1024.0 / 1024.0:F1} MB"));

            return new ExportResult(true, outputPath, "torchscript", fileInfo.Length);
        }
        catch (OperationCanceledException)
        {
            return new ExportResult(false, outputPath, "torchscript", 0, "Export cancelled.");
        }
        catch (Exception ex)
        {
            return new ExportResult(false, outputPath, "torchscript", 0, ex.Message);
        }
    }
}
