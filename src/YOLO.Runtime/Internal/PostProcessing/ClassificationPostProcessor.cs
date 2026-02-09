using YOLO.Runtime.Internal.PreProcessing;
using YOLO.Runtime.Results;

namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Postprocessor for YOLO classification models.
/// Output format: (1, num_classes) -- class logits/probabilities.
/// </summary>
internal sealed class ClassificationPostProcessor : IPostProcessor
{
    public DetectionResult[] ProcessDetections(
        ReadOnlySpan<float> output, int[] shape,
        PreProcessContext context, YoloOptions options)
    {
        // Classification models don't produce detections
        return [];
    }

    public ClassificationResult[] ProcessClassifications(
        ReadOnlySpan<float> output, int[] shape, YoloOptions options)
    {
        // Expected shape: (1, num_classes)
        if (shape.Length < 2)
            return [];

        int numClasses = shape[^1];
        string[]? classNames = options.ClassNames;

        // Apply softmax to get probabilities
        var probs = Softmax(output, numClasses);

        // Sort by probability descending, return top results above threshold
        var results = new List<ClassificationResult>();
        var indices = Enumerable.Range(0, numClasses)
            .OrderByDescending(i => probs[i])
            .ToArray();

        foreach (var idx in indices)
        {
            if (probs[idx] < options.Confidence)
                break;

            string? className = classNames is not null && idx < classNames.Length
                ? classNames[idx] : null;

            results.Add(new ClassificationResult(idx, probs[idx], className));
        }

        return results.ToArray();
    }

    private static float[] Softmax(ReadOnlySpan<float> logits, int count)
    {
        var result = new float[count];
        float max = float.MinValue;

        for (int i = 0; i < count; i++)
            if (logits[i] > max) max = logits[i];

        float sum = 0;
        for (int i = 0; i < count; i++)
        {
            result[i] = MathF.Exp(logits[i] - max);
            sum += result[i];
        }

        if (sum > 0)
        {
            for (int i = 0; i < count; i++)
                result[i] /= sum;
        }

        return result;
    }
}
