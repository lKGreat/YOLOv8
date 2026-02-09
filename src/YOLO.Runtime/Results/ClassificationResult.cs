namespace YOLO.Runtime.Results;

/// <summary>
/// A single classification result.
/// </summary>
/// <param name="ClassId">Predicted class index.</param>
/// <param name="Confidence">Classification confidence score (0-1).</param>
/// <param name="ClassName">Optional class name.</param>
public readonly record struct ClassificationResult(
    int ClassId, float Confidence, string? ClassName = null)
{
    public override string ToString() =>
        $"[{ClassName ?? $"#{ClassId}"}] conf={Confidence:P1}";
}
