namespace YOLO.Runtime.Internal.PostProcessing;

/// <summary>
/// Factory for creating the appropriate postprocessor based on model version and output shape.
/// </summary>
internal static class PostProcessorFactory
{
    private static readonly Dictionary<string, Func<IPostProcessor>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["v8"] = () => new NmsPostProcessor(),
        ["v9"] = () => new NmsPostProcessor(),
        ["v10"] = () => new V10PostProcessor(),
        ["v11"] = () => new NmsPostProcessor(),
        ["v12"] = () => new NmsPostProcessor(),
        ["cls"] = () => new ClassificationPostProcessor(),
        ["seg"] = () => new SegmentationPostProcessor(),
    };

    /// <summary>
    /// Create a postprocessor for the given model version.
    /// </summary>
    /// <param name="version">Model version string (e.g. "v8", "v10", "cls").</param>
    /// <returns>The appropriate postprocessor.</returns>
    public static IPostProcessor Create(string? version)
    {
        if (version is not null && Registry.TryGetValue(version, out var factory))
            return factory();

        // Default to NMS-based processor (works for v8/v9/v11+)
        return new NmsPostProcessor();
    }

    /// <summary>
    /// Auto-detect the appropriate postprocessor from output shape.
    /// </summary>
    /// <param name="shape">Model output shape.</param>
    /// <param name="version">Optional version hint.</param>
    /// <returns>The appropriate postprocessor.</returns>
    public static IPostProcessor CreateFromShape(int[] shape, string? version)
    {
        // If version is explicitly set, use it
        if (version is not null)
            return Create(version);

        // Auto-detect from shape
        if (shape.Length == 3)
        {
            // (1, 300, 6) -> v10 end-to-end
            if (shape[2] == 6 && shape[1] <= 300)
                return new V10PostProcessor();

            // (1, 4+nc, N) -> standard NMS (v8/v9/v11)
            return new NmsPostProcessor();
        }

        if (shape.Length == 2)
        {
            // (1, num_classes) -> classification
            return new ClassificationPostProcessor();
        }

        return new NmsPostProcessor();
    }

    /// <summary>
    /// Register a custom postprocessor for a model version.
    /// </summary>
    public static void Register(string version, Func<IPostProcessor> factory)
    {
        Registry[version] = factory;
    }
}
