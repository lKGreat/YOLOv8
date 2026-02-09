using Microsoft.ML.OnnxRuntime;
using YOLO.Runtime.Internal.Memory;

namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// ONNX Runtime inference backend. Supports CPU, CUDA, TensorRT, DirectML.
/// Thread-safe for concurrent <see cref="Run"/> calls on the same session.
/// Uses the modern OrtValue API for reduced allocations and GC pressure.
/// </summary>
internal sealed class OnnxBackend : IInferenceBackend
{
    private readonly InferenceSession _session;
    private readonly string[] _inputNames;
    private readonly string[] _outputNames;
    private readonly RunOptions _runOptions;

    // Cached output metadata (populated on first run)
    private long[]? _cachedOutputLongShape;
    private int[]? _cachedOutputShape;
    private int _cachedOutputLen;

    private bool _disposed;

    public string ModelPath { get; }
    public string BackendName => "OnnxRuntime";

    public OnnxBackend(string modelPath, YoloOptions options)
    {
        ModelPath = modelPath;

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        sessionOptions.EnableCpuMemArena = true;
        sessionOptions.EnableMemoryPattern = true;

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
        _inputNames = [_session.InputNames[0]];
        _outputNames = [_session.OutputNames[0]];
        _runOptions = new RunOptions();
    }

    public (BufferHandle data, int[] shape) Run(ReadOnlySpan<float> input, int[] inputShape)
    {
        // Convert input shape to long[] for OrtValue API
        var longShape = Array.ConvertAll(inputShape, i => (long)i);

        // Create OrtValue wrapping the input data
        var inputArray = input.ToArray();
        using var inputOrtValue = OrtValue.CreateTensorValueFromMemory(
            inputArray, longShape);

        // Run inference using the OrtValue API (less GC than NamedOnnxValue path)
        using var results = _session.Run(_runOptions, _inputNames, [inputOrtValue], _outputNames);
        var outputOrtValue = results[0];

        // Extract output shape
        var typeAndShape = outputOrtValue.GetTensorTypeAndShape();
        var outputLongShape = typeAndShape.Shape;
        int[] outputShape;
        int outputLen;

        // Cache the output shape on first run — YOLO models have deterministic output shapes
        if (_cachedOutputShape is not null
            && _cachedOutputLongShape is not null
            && ShapeEquals(_cachedOutputLongShape, outputLongShape))
        {
            outputShape = _cachedOutputShape;
            outputLen = _cachedOutputLen;
        }
        else
        {
            outputShape = Array.ConvertAll(outputLongShape, s => (int)s);
            outputLen = 1;
            foreach (var d in outputShape) outputLen *= d;
            _cachedOutputLongShape = outputLongShape;
            _cachedOutputShape = outputShape;
            _cachedOutputLen = outputLen;
        }

        // Copy output into a pooled buffer
        var outputHandle = TensorBufferPool.Rent(outputLen);
        outputOrtValue.GetTensorDataAsSpan<float>()
            .Slice(0, outputLen)
            .CopyTo(outputHandle.Span);

        return (outputHandle, outputShape);
    }

    public Task<(BufferHandle data, int[] shape)> RunAsync(float[] input, int[] inputShape, CancellationToken ct = default)
    {
        return Task.Run(() => Run(input.AsSpan(), inputShape), ct);
    }

    private static bool ShapeEquals(long[] a, long[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    internal static ExecutionProviderType ResolveProvider(DeviceType device)
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
