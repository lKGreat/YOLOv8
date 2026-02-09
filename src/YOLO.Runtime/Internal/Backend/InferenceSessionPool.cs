using Microsoft.ML.OnnxRuntime;
using YOLO.Runtime.Internal.Memory;

namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// Pool of ONNX <see cref="InferenceSession"/> instances for CPU-parallel inference.
/// Each session is configured with a subset of CPU threads (IntraOp) and spinning disabled
/// so that multiple sessions can run truly in parallel on different CPU cores.
/// <para>
/// For GPU inference a single session is sufficient (the GPU handles parallelism internally),
/// so this pool should only be used with CPU execution.
/// </para>
/// </summary>
internal sealed class InferenceSessionPool : IDisposable
{
    private readonly InferenceSession[] _sessions;
    private readonly SemaphoreSlim _semaphore;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly int[] _inputShape;
    private bool _disposed;

    /// <summary>Number of sessions in the pool.</summary>
    public int SessionCount => _sessions.Length;

    public InferenceSessionPool(string modelPath, YoloOptions options, int sessionCount)
    {
        sessionCount = Math.Max(1, sessionCount);
        _sessions = new InferenceSession[sessionCount];

        int totalCores = Environment.ProcessorCount;
        int coresPerSession = Math.Max(1, totalCores / sessionCount);

        for (int i = 0; i < sessionCount; i++)
        {
            var so = new SessionOptions();
            so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            so.EnableCpuMemArena = true;
            so.EnableMemoryPattern = true;

            // Each session uses only coresPerSession intra-op threads,
            // allowing multiple sessions to occupy different CPU cores.
            so.IntraOpNumThreads = coresPerSession;
            so.InterOpNumThreads = 1;

            // Disable thread spinning to avoid CPU contention between sessions.
            // Without this, idle sessions spin-wait and waste CPU cycles that
            // active sessions need.
            so.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            so.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");

            // Apply user-specified execution provider (should be CPU for this pool)
            var provider = options.ExecutionProvider ?? OnnxBackend.ResolveProvider(options.Device);
            switch (provider)
            {
                case ExecutionProviderType.CUDA:
                    so.AppendExecutionProvider_CUDA(options.DeviceId);
                    break;
                case ExecutionProviderType.TensorRT:
                    so.AppendExecutionProvider_Tensorrt(options.DeviceId);
                    so.AppendExecutionProvider_CUDA(options.DeviceId);
                    break;
                case ExecutionProviderType.DirectML:
                    so.AppendExecutionProvider_DML(options.DeviceId);
                    break;
                case ExecutionProviderType.CPU:
                default:
                    break;
            }

            _sessions[i] = new InferenceSession(modelPath, so);
        }

        _inputName = _sessions[0].InputNames[0];
        _outputName = _sessions[0].OutputNames[0];
        _inputShape = [1, 3, options.ImgSize, options.ImgSize];
        _semaphore = new SemaphoreSlim(sessionCount, sessionCount);
    }

    /// <summary>
    /// Acquire a session from the pool and run inference.
    /// Thread-safe — blocks if all sessions are busy.
    /// </summary>
    public (BufferHandle data, int[] shape) Run(ReadOnlySpan<float> input, int[] inputShape)
    {
        _semaphore.Wait();
        try
        {
            // Find a free session index via simple round-robin with the semaphore.
            // Because we use a counting semaphore, at most N callers are inside this block.
            var session = AcquireSession();
            try
            {
                return RunOnSession(session, input, inputShape);
            }
            finally
            {
                ReleaseSession(session);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Acquire a session and run inference asynchronously.
    /// </summary>
    public async Task<(BufferHandle data, int[] shape)> RunAsync(
        float[] input, int[] inputShape, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var session = AcquireSession();
            try
            {
                // Offload CPU-bound inference to the thread pool
                return await Task.Run(() => RunOnSession(session, input.AsSpan(), inputShape), ct);
            }
            finally
            {
                ReleaseSession(session);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>The cached input shape.</summary>
    public int[] InputShape => _inputShape;

    // ── Session acquisition ──────────────────────────────────────────────

    // Simple lock-free session allocation: each session has an in-use flag.
    private readonly int[] _sessionInUse = new int[64]; // padded, indexed by session id

    private InferenceSession AcquireSession()
    {
        // Try to find a free session
        for (int i = 0; i < _sessions.Length; i++)
        {
            if (Interlocked.CompareExchange(ref _sessionInUse[i], 1, 0) == 0)
                return _sessions[i];
        }
        // Fallback: shouldn't happen since semaphore limits access, but use session 0
        return _sessions[0];
    }

    private void ReleaseSession(InferenceSession session)
    {
        for (int i = 0; i < _sessions.Length; i++)
        {
            if (ReferenceEquals(_sessions[i], session))
            {
                Interlocked.Exchange(ref _sessionInUse[i], 0);
                return;
            }
        }
    }

    // ── Inference on a specific session ──────────────────────────────────

    private static (BufferHandle data, int[] shape) RunOnSession(
        InferenceSession session, ReadOnlySpan<float> input, int[] inputShape)
    {
        var inputArray = input.ToArray();
        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(inputArray, inputShape);

        var inputs = new Microsoft.ML.OnnxRuntime.NamedOnnxValue[]
        {
            Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(
                session.InputNames[0], tensor)
        };

        using var results = session.Run(inputs);
        var output = results[0];
        var outputTensor = output.AsTensor<float>();
        var outputShape = outputTensor.Dimensions.ToArray();

        int outputLen = 1;
        foreach (var d in outputShape) outputLen *= d;
        var outputHandle = TensorBufferPool.Rent(outputLen);

        if (outputTensor is Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float> dense)
            dense.Buffer.Span.Slice(0, outputLen).CopyTo(outputHandle.Span);
        else
            outputTensor.ToArray().AsSpan().CopyTo(outputHandle.Span);

        return (outputHandle, outputShape);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
        foreach (var session in _sessions)
            session.Dispose();
    }
}
