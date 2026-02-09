using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using YOLO.Runtime.Internal.Memory;

namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// ONNX Runtime inference backend. Supports CPU, CUDA, TensorRT, DirectML.
/// Thread-safe for concurrent <see cref="Run"/> calls on the same session.
/// </summary>
internal sealed class OnnxBackend : IInferenceBackend
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private bool _disposed;

    public string ModelPath { get; }
    public string BackendName => "OnnxRuntime";

    public OnnxBackend(string modelPath, YoloOptions options)
    {
        ModelPath = modelPath;

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        if (options.InterOpThreads > 0)
            sessionOptions.InterOpNumThreads = options.InterOpThreads;
        if (options.IntraOpThreads > 0)
            sessionOptions.IntraOpNumThreads = options.IntraOpThreads;

        // Determine execution provider
        var provider = options.ExecutionProvider ?? ResolveProvider(options.Device);

        switch (provider)
        {
            case ExecutionProviderType.CUDA:
                sessionOptions.AppendExecutionProvider_CUDA(options.DeviceId);
                break;
            case ExecutionProviderType.TensorRT:
                sessionOptions.AppendExecutionProvider_Tensorrt(options.DeviceId);
                sessionOptions.AppendExecutionProvider_CUDA(options.DeviceId); // fallback
                break;
            case ExecutionProviderType.DirectML:
                sessionOptions.AppendExecutionProvider_DML(options.DeviceId);
                break;
            case ExecutionProviderType.CPU:
            default:
                // CPU is always available as fallback
                break;
        }

        _session = new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputNames[0];
    }

    public (BufferHandle data, int[] shape) Run(ReadOnlySpan<float> input, int[] inputShape)
    {
        // DenseTensor accepts ReadOnlySpan<int> directly — no int[]→long[]→int[] roundtrip
        var inputArray = input.ToArray();
        var tensor = new DenseTensor<float>(inputArray, inputShape);

        // Single-element array is cheaper than List<T> (no resizing, no wrapper)
        var inputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };

        using var results = _session.Run(inputs);
        var output = results[0];
        var outputTensor = output.AsTensor<float>();
        var outputShape = outputTensor.Dimensions.ToArray();

        // Copy output into a pooled buffer (avoids per-call allocation of ~2.7MB)
        int outputLen = 1;
        foreach (var d in outputShape) outputLen *= d;
        var outputHandle = TensorBufferPool.Rent(outputLen);
        if (outputTensor is DenseTensor<float> dense)
            dense.Buffer.Span.Slice(0, outputLen).CopyTo(outputHandle.Span);
        else
            outputTensor.ToArray().AsSpan().CopyTo(outputHandle.Span);

        return (outputHandle, outputShape);
    }

    public Task<(BufferHandle data, int[] shape)> RunAsync(float[] input, int[] inputShape, CancellationToken ct = default)
    {
        return Task.Run(() => Run(input.AsSpan(), inputShape), ct);
    }

    private static ExecutionProviderType ResolveProvider(DeviceType device)
    {
        return device switch
        {
            DeviceType.Gpu => ExecutionProviderType.CUDA,
            DeviceType.Cpu => ExecutionProviderType.CPU,
            DeviceType.Auto => TryGpu() ? ExecutionProviderType.CUDA : ExecutionProviderType.CPU,
            _ => ExecutionProviderType.CPU
        };
    }

    private static bool TryGpu()
    {
        try
        {
            var providers = OrtEnv.Instance().GetAvailableProviders();
            return providers.Contains("CUDAExecutionProvider");
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}
