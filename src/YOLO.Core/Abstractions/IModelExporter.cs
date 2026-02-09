namespace YOLO.Core.Abstractions;

/// <summary>
/// Result of a model export operation.
/// </summary>
public record ExportResult(
    bool Success,
    string OutputPath,
    string Format,
    long FileSizeBytes,
    string? ErrorMessage = null
);

/// <summary>
/// Progress information for export operations.
/// </summary>
public record ExportProgress(
    string Stage,
    int PercentComplete,
    string? Message = null
);

/// <summary>
/// Configuration for model export.
/// </summary>
public record ExportConfig
{
    /// <summary>Export format: "onnx", "torchscript"</summary>
    public string Format { get; init; } = "onnx";
    /// <summary>Output file path</summary>
    public string OutputPath { get; init; } = "";
    /// <summary>Input image size</summary>
    public int ImgSize { get; init; } = 640;
    /// <summary>Use FP16 half precision</summary>
    public bool Half { get; init; } = false;
    /// <summary>Simplify the ONNX model</summary>
    public bool Simplify { get; init; } = true;
    /// <summary>ONNX opset version</summary>
    public int OpsetVersion { get; init; } = 17;
    /// <summary>Enable dynamic batch size</summary>
    public bool Dynamic { get; init; } = false;
}

/// <summary>
/// Interface for model export (ONNX, TorchScript, etc.).
/// </summary>
public interface IModelExporter
{
    /// <summary>
    /// Export a model to the specified format.
    /// </summary>
    Task<ExportResult> ExportAsync(
        YOLOModel model,
        ExportConfig config,
        IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Formats supported by this exporter.
    /// </summary>
    string[] SupportedFormats { get; }
}
