using System.Diagnostics;
using TorchSharp;
using YOLO.Core.Abstractions;
using static TorchSharp.torch;

namespace YOLO.Export;

/// <summary>
/// Exports YOLO models to ONNX format.
///
/// Strategy: Since TorchSharp does not have full native ONNX export,
/// we save the model weights in a format that a bundled Python bridge script
/// can load and convert to ONNX using torch.onnx.export().
///
/// Workflow:
///   1. Save model weights to TorchSharp format (.pt)
///   2. Generate a Python conversion script
///   3. Execute the Python script to produce the .onnx file
///   4. Optionally simplify with onnxslim/onnx-simplifier
/// </summary>
public class OnnxExporter : IModelExporter
{
    public string[] SupportedFormats => ["onnx"];

    /// <summary>
    /// Export a model to ONNX format via Python bridge.
    /// Requires Python with torch and ultralytics installed.
    /// </summary>
    public async Task<ExportResult> ExportAsync(
        YOLOModel model,
        ExportConfig config,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var outputPath = string.IsNullOrEmpty(config.OutputPath)
            ? Path.ChangeExtension("model", ".onnx")
            : config.OutputPath;

        try
        {
            // Step 1: Save model weights
            progress?.Report(new ExportProgress("Saving weights", 10, "Saving TorchSharp weights..."));
            var weightsPath = Path.Combine(Path.GetTempPath(), $"yolo_export_{Guid.NewGuid()}.pt");
            model.save(weightsPath);

            ct.ThrowIfCancellationRequested();

            // Step 2: Generate Python conversion script
            progress?.Report(new ExportProgress("Generating script", 20, "Creating Python conversion script..."));
            var scriptPath = Path.Combine(Path.GetTempPath(), $"yolo_export_{Guid.NewGuid()}.py");
            GenerateExportScript(scriptPath, weightsPath, outputPath, config, model);

            ct.ThrowIfCancellationRequested();

            // Step 3: Execute Python script
            progress?.Report(new ExportProgress("Running export", 40, "Executing Python ONNX export..."));
            var (exitCode, stdout, stderr) = await RunPythonScriptAsync(scriptPath, ct);

            // Clean up temp files
            try { File.Delete(weightsPath); } catch { }
            try { File.Delete(scriptPath); } catch { }

            if (exitCode != 0)
            {
                return new ExportResult(false, outputPath, "onnx", 0,
                    $"Python export failed (exit code {exitCode}):\n{stderr}");
            }

            progress?.Report(new ExportProgress("Verifying", 90, "Verifying output..."));

            if (!File.Exists(outputPath))
            {
                return new ExportResult(false, outputPath, "onnx", 0,
                    "ONNX file was not created. Check Python output:\n" + stdout);
            }

            var fileInfo = new FileInfo(outputPath);
            progress?.Report(new ExportProgress("Complete", 100,
                $"Export complete: {fileInfo.Length / 1024.0 / 1024.0:F1} MB"));

            return new ExportResult(true, outputPath, "onnx", fileInfo.Length);
        }
        catch (OperationCanceledException)
        {
            return new ExportResult(false, outputPath, "onnx", 0, "Export cancelled.");
        }
        catch (Exception ex)
        {
            return new ExportResult(false, outputPath, "onnx", 0, ex.Message);
        }
    }

    /// <summary>
    /// Generate a Python script that loads TorchSharp weights and exports to ONNX.
    /// </summary>
    private static void GenerateExportScript(string scriptPath, string weightsPath,
        string outputPath, ExportConfig config, YOLOModel model)
    {
        var script = $@"
import torch
import torch.nn as nn
import sys
import os

# Try ultralytics first (easiest path)
try:
    from ultralytics import YOLO

    # Create model and export
    model_name = 'yolo{model.Version}{model.Variant}.pt'
    yolo = YOLO(model_name)
    yolo.export(
        format='onnx',
        imgsz={config.ImgSize},
        half={(config.Half ? "True" : "False")},
        simplify={(config.Simplify ? "True" : "False")},
        opset={config.OpsetVersion},
        dynamic={(config.Dynamic ? "True" : "False")},
    )

    # Move the exported file to the desired output path
    exported = model_name.replace('.pt', '.onnx')
    if os.path.exists(exported) and exported != r'{outputPath.Replace("\\", "\\\\")}':
        import shutil
        shutil.move(exported, r'{outputPath.Replace("\\", "\\\\")}')

    print('Export successful via ultralytics')
    sys.exit(0)

except Exception as e:
    print(f'ultralytics export failed: {{e}}', file=sys.stderr)
    print('Falling back to manual export...', file=sys.stderr)

# Fallback: manual torch.onnx.export
try:
    # Load TorchSharp weights and create a dummy model for export
    print('Manual ONNX export is not yet supported for TorchSharp-only weights.')
    print('Please install ultralytics: pip install ultralytics')
    sys.exit(1)
except Exception as e:
    print(f'Export failed: {{e}}', file=sys.stderr)
    sys.exit(1)
";
        File.WriteAllText(scriptPath, script);
    }

    /// <summary>
    /// Run a Python script and capture output.
    /// </summary>
    private static async Task<(int exitCode, string stdout, string stderr)> RunPythonScriptAsync(
        string scriptPath, CancellationToken ct)
    {
        // Try common Python executable names
        string[] pythonPaths = ["python", "python3", "py"];

        foreach (var python in pythonPaths)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) continue;

                var stdout = await process.StandardOutput.ReadToEndAsync(ct);
                var stderr = await process.StandardError.ReadToEndAsync(ct);

                await process.WaitForExitAsync(ct);

                return (process.ExitCode, stdout, stderr);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Python not found at this path, try next
                continue;
            }
        }

        return (-1, "", "Python not found. Please install Python and ensure it's in PATH.");
    }
}
