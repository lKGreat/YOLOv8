namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// Auto-selects the appropriate inference backend based on model file extension.
/// </summary>
internal static class BackendFactory
{
    /// <summary>
    /// Create an inference backend for the given model file.
    /// </summary>
    /// <param name="modelPath">Path to the model file (.onnx, .pt, .bin).</param>
    /// <param name="options">Inference options.</param>
    /// <returns>The appropriate backend instance.</returns>
    public static IInferenceBackend Create(string modelPath, YoloOptions options)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}", modelPath);

        var ext = Path.GetExtension(modelPath).ToLowerInvariant();
        return ext switch
        {
            ".onnx" => new OnnxBackend(modelPath, options),
            ".pt" or ".bin" => new TorchSharpBackend(modelPath, options),
            _ => throw new NotSupportedException(
                $"Unsupported model format '{ext}'. Supported: .onnx, .pt, .bin")
        };
    }
}
