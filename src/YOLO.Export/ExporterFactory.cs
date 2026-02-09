using YOLO.Core.Abstractions;

namespace YOLO.Export;

/// <summary>
/// Factory for creating model exporters by format name.
/// </summary>
public static class ExporterFactory
{
    /// <summary>
    /// Create an exporter for the specified format.
    /// </summary>
    /// <param name="format">Format name: "onnx", "torchscript", "pt"</param>
    public static IModelExporter Create(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "onnx" => new OnnxExporter(),
            "torchscript" or "pt" => new TorchScriptExporter(),
            _ => throw new ArgumentException(
                $"Unknown export format '{format}'. Supported: onnx, torchscript, pt")
        };
    }

    /// <summary>
    /// Get all supported export format names.
    /// </summary>
    public static string[] SupportedFormats => ["onnx", "torchscript", "pt"];
}
