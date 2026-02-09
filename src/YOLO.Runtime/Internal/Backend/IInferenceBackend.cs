namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// Internal abstraction for running a model forward pass.
/// Implementations: <see cref="OnnxBackend"/> (.onnx) and <see cref="TorchSharpBackend"/> (.pt).
/// </summary>
internal interface IInferenceBackend : IDisposable
{
    /// <summary>
    /// Run inference on the given input buffer.
    /// </summary>
    /// <param name="input">Preprocessed input tensor data (CHW, normalized).</param>
    /// <param name="inputShape">Shape of the input tensor, e.g. [1, 3, 640, 640].</param>
    /// <returns>Raw output tensor data and its shape.</returns>
    (float[] data, int[] shape) Run(ReadOnlySpan<float> input, int[] inputShape);

    /// <summary>
    /// Run inference asynchronously (offloads to ThreadPool for CPU-bound backends).
    /// </summary>
    Task<(float[] data, int[] shape)> RunAsync(float[] input, int[] inputShape, CancellationToken ct = default);

    /// <summary>
    /// The model file path this backend was loaded from.
    /// </summary>
    string ModelPath { get; }

    /// <summary>
    /// Friendly name for logging.
    /// </summary>
    string BackendName { get; }
}
