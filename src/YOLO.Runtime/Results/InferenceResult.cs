namespace YOLO.Runtime.Results;

/// <summary>
/// Unified result wrapper for chain inference output.
/// Holds the result of a single step in a multi-model chain.
/// </summary>
public sealed class InferenceResult
{
    /// <summary>Name/identifier of the model step that produced this result.</summary>
    public string ModelName { get; }

    /// <summary>Detection results (non-null when the step is a detection model).</summary>
    public DetectionResult[]? Detections { get; init; }

    /// <summary>Classification results (non-null when the step is a classification model).</summary>
    public ClassificationResult[]? Classifications { get; init; }

    /// <summary>Segmentation results (non-null when the step is a segmentation model).</summary>
    public SegmentationResult[]? Segmentations { get; init; }

    public InferenceResult(string modelName)
    {
        ModelName = modelName;
    }

    /// <summary>Whether this result contains detections.</summary>
    public bool HasDetections => Detections is { Length: > 0 };

    /// <summary>Whether this result contains classifications.</summary>
    public bool HasClassifications => Classifications is { Length: > 0 };

    /// <summary>Whether this result contains segmentations.</summary>
    public bool HasSegmentations => Segmentations is { Length: > 0 };

    public override string ToString()
    {
        if (HasDetections) return $"[{ModelName}] {Detections!.Length} detections";
        if (HasClassifications) return $"[{ModelName}] {Classifications!.Length} classifications";
        if (HasSegmentations) return $"[{ModelName}] {Segmentations!.Length} segmentations";
        return $"[{ModelName}] empty";
    }
}
