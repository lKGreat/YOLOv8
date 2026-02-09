using System.Buffers;
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

        // Apply softmax to get probabilities (uses ArrayPool for intermediate buffer)
        var probsArray = ArrayPool<float>.Shared.Rent(numClasses);
        try
        {
            Softmax(output, numClasses, probsArray);

            // Sort indices by probability descending — no LINQ, no boxing
            var indicesArray = ArrayPool<int>.Shared.Rent(numClasses);
            try
            {
                for (int i = 0; i < numClasses; i++) indicesArray[i] = i;
                var indicesSpan = indicesArray.AsSpan(0, numClasses);
                var probsCapture = probsArray; // local for lambda
                indicesSpan.Sort((a, b) => probsCapture[b].CompareTo(probsCapture[a]));

                // Collect results above threshold
                var results = new List<ClassificationResult>();
                for (int k = 0; k < numClasses; k++)
                {
                    int idx = indicesSpan[k];
                    if (probsArray[idx] < options.Confidence)
                        break;

                    string? className = classNames is not null && idx < classNames.Length
                        ? classNames[idx] : null;

                    results.Add(new ClassificationResult(idx, probsArray[idx], className));
                }

                return results.ToArray();
            }
            finally
            {
                ArrayPool<int>.Shared.Return(indicesArray);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(probsArray);
        }
    }

    /// <summary>
    /// In-place softmax into a pre-allocated buffer.
    /// </summary>
    private static void Softmax(ReadOnlySpan<float> logits, int count, float[] result)
    {
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
            float invSum = 1.0f / sum;
            for (int i = 0; i < count; i++)
                result[i] *= invSum;
        }
    }
}
