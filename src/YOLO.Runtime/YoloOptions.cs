namespace YOLO.Runtime;

/// <summary>
/// Device type for inference execution.
/// </summary>
public enum DeviceType
{
    /// <summary>Automatically select GPU if available, otherwise CPU.</summary>
    Auto,
    /// <summary>Force CPU execution.</summary>
    Cpu,
    /// <summary>Force GPU execution.</summary>
    Gpu
}

/// <summary>
/// Specific execution provider for ONNX Runtime backend.
/// </summary>
public enum ExecutionProviderType
{
    /// <summary>CPU execution provider (default fallback).</summary>
    CPU,
    /// <summary>NVIDIA CUDA execution provider.</summary>
    CUDA,
    /// <summary>NVIDIA TensorRT execution provider.</summary>
    TensorRT,
    /// <summary>DirectML execution provider (Windows GPU).</summary>
    DirectML
}

/// <summary>
/// Configuration options for YOLO inference.
/// All properties have sensible defaults -- only override what you need.
/// </summary>
public sealed class YoloOptions
{
    /// <summary>
    /// Device type for inference. Default: Auto (GPU if available).
    /// </summary>
    public DeviceType Device { get; set; } = DeviceType.Auto;

    /// <summary>
    /// GPU device ID when using GPU. Default: 0.
    /// </summary>
    public int DeviceId { get; set; }

    /// <summary>
    /// Specific ONNX execution provider. Only used for .onnx models.
    /// When null, derived from <see cref="Device"/>.
    /// </summary>
    public ExecutionProviderType? ExecutionProvider { get; set; }

    /// <summary>
    /// Confidence threshold for detection filtering. Default: 0.25.
    /// </summary>
    public float Confidence { get; set; } = 0.25f;

    /// <summary>
    /// IoU threshold for Non-Maximum Suppression. Default: 0.45.
    /// </summary>
    public float IoU { get; set; } = 0.45f;

    /// <summary>
    /// Input image size (square). Default: 640.
    /// </summary>
    public int ImgSize { get; set; } = 640;

    /// <summary>
    /// Maximum number of detections per image. Default: 300.
    /// </summary>
    public int MaxDetections { get; set; } = 300;

    /// <summary>
    /// Model version string (e.g. "v8", "v9", "v10").
    /// When null, auto-detected from ONNX metadata or defaults to "v8".
    /// </summary>
    public string? ModelVersion { get; set; }

    /// <summary>
    /// Model variant (e.g. "n", "s", "m", "l", "x"). Used for .pt loading.
    /// Default: "n".
    /// </summary>
    public string ModelVariant { get; set; } = "n";

    /// <summary>
    /// Number of classes. Used for .pt loading. Default: 80 (COCO).
    /// </summary>
    public int NumClasses { get; set; } = 80;

    /// <summary>
    /// Enable half precision (FP16) inference. Default: false.
    /// </summary>
    public bool HalfPrecision { get; set; }

    /// <summary>
    /// Maximum degree of parallelism for batch / PDF inference.
    /// Default: Environment.ProcessorCount.
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Optional class names for labeling detections. Index = ClassId.
    /// </summary>
    public string[]? ClassNames { get; set; }

    /// <summary>
    /// Whether to draw annotated results on PDF pages. Default: false.
    /// </summary>
    public bool DrawPdfResults { get; set; }

    /// <summary>
    /// PDF rendering DPI. Default: 200.
    /// </summary>
    public int PdfDpi { get; set; } = 200;

    /// <summary>
    /// Number of inter-op threads for ONNX Runtime. Default: 0 (auto).
    /// </summary>
    public int InterOpThreads { get; set; }

    /// <summary>
    /// Number of intra-op threads for ONNX Runtime. Default: 0 (auto).
    /// </summary>
    public int IntraOpThreads { get; set; }

    /// <summary>
    /// Number of ONNX inference sessions for CPU-parallel inference.
    /// Each session is pinned to a subset of CPU cores (IntraOp threads = ProcessorCount / sessions).
    /// <para>
    /// Default: 0 (auto-detect). Auto chooses 1 for GPU; for CPU: ProcessorCount / 4, clamped to [1, 8].
    /// </para>
    /// Only takes effect for batch/PDF inference via the staged pipeline.
    /// </summary>
    public int InferenceSessions { get; set; }

    /// <summary>
    /// Number of preprocess worker tasks for the staged batch pipeline.
    /// <para>
    /// Default: 0 (auto-detect: ProcessorCount / 2, clamped to [1, 16]).
    /// </para>
    /// Only takes effect for batch/PDF inference via the staged pipeline.
    /// </summary>
    public int PreprocessWorkers { get; set; }

    /// <summary>
    /// Minimum batch size to trigger the staged pipeline.
    /// Batches smaller than this threshold use the simple semaphore-based approach.
    /// Default: 4.
    /// </summary>
    public int StagedPipelineThreshold { get; set; } = 4;

    // ── Helper: resolve auto-detect values ──────────────────────────────

    /// <summary>
    /// Resolve the effective number of inference sessions.
    /// </summary>
    internal int ResolveInferenceSessions()
    {
        if (InferenceSessions > 0) return InferenceSessions;

        // GPU backends handle their own parallelism — 1 session is sufficient
        var provider = ExecutionProvider ?? ResolveEffectiveProvider();
        if (provider is ExecutionProviderType.CUDA or ExecutionProviderType.TensorRT or ExecutionProviderType.DirectML)
            return 1;

        // CPU: divide cores into groups of ~4
        return Math.Clamp(Environment.ProcessorCount / 4, 1, 8);
    }

    /// <summary>
    /// Resolve the effective number of preprocess workers.
    /// </summary>
    internal int ResolvePreprocessWorkers()
    {
        if (PreprocessWorkers > 0) return PreprocessWorkers;
        return Math.Clamp(Environment.ProcessorCount / 2, 1, 16);
    }

    private ExecutionProviderType ResolveEffectiveProvider()
    {
        return Device switch
        {
            DeviceType.Gpu => ExecutionProviderType.CUDA,
            DeviceType.Cpu => ExecutionProviderType.CPU,
            DeviceType.Auto => ExecutionProviderType.CPU, // safe default for auto-detection
            _ => ExecutionProviderType.CPU
        };
    }
}
