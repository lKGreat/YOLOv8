using TorchSharp;
using YOLO.Core.Models;
using static TorchSharp.torch;

namespace YOLO.Core.Abstractions;

/// <summary>
/// Registration entry for a YOLO model version.
/// </summary>
public record ModelRegistration(
    /// <summary>Factory function: (numClasses, variant, device) -> YOLOModel</summary>
    Func<int, string, Device?, YOLOModel> ModelFactory,
    /// <summary>Supported variant names, e.g. ["n","s","m","l","x"]</summary>
    string[] SupportedVariants,
    /// <summary>Scale definitions per variant</summary>
    Dictionary<string, ModelScale> Scales,
    /// <summary>
    /// Optional loss factory: (numClasses, boxGain, clsGain, dflGain) -> IDetectionLoss.
    /// Registered separately to avoid circular project dependencies.
    /// </summary>
    Func<int, double, double, double, IDetectionLoss>? LossFactory = null
);

/// <summary>
/// Central registry for YOLO model versions.
/// Each version (v8, v9, v10, ...) registers itself here.
/// The Trainer, Predictor, and GUI use this registry to create models by version string.
/// </summary>
public static class ModelRegistry
{
    private static readonly Dictionary<string, ModelRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register a model version with its factory and metadata.
    /// </summary>
    /// <param name="version">Version key, e.g. "v8", "v9", "v10"</param>
    /// <param name="registration">Registration details</param>
    public static void Register(string version, ModelRegistration registration)
    {
        _registrations[version] = registration;
    }

    /// <summary>
    /// Register (or update) a loss factory for an existing model version.
    /// Called from the Training layer to avoid circular project dependencies.
    /// </summary>
    public static void RegisterLoss(string version, Func<int, double, double, double, IDetectionLoss> lossFactory)
    {
        if (!_registrations.TryGetValue(version, out var reg))
            throw new ArgumentException(
                $"Cannot register loss for unknown model version '{version}'. Register the model first.");

        _registrations[version] = reg with { LossFactory = lossFactory };
    }

    /// <summary>
    /// Create a model instance by version and variant.
    /// </summary>
    public static YOLOModel Create(string version, int nc, string variant, Device? device = null)
    {
        if (!_registrations.TryGetValue(version, out var reg))
            throw new ArgumentException(
                $"Unknown model version '{version}'. Available: {string.Join(", ", _registrations.Keys)}");

        if (!reg.SupportedVariants.Contains(variant, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Variant '{variant}' not supported for {version}. Available: {string.Join(", ", reg.SupportedVariants)}");

        return reg.ModelFactory(nc, variant, device);
    }

    /// <summary>
    /// Create a detection loss instance by version.
    /// </summary>
    public static IDetectionLoss CreateLoss(string version, int nc,
        double boxGain = 7.5, double clsGain = 0.5, double dflGain = 1.5)
    {
        if (!_registrations.TryGetValue(version, out var reg))
            throw new ArgumentException(
                $"Unknown model version '{version}'. Available: {string.Join(", ", _registrations.Keys)}");

        if (reg.LossFactory == null)
            throw new InvalidOperationException(
                $"No loss factory registered for version '{version}'. " +
                $"Call ModelRegistry.RegisterLoss() first.");

        return reg.LossFactory(nc, boxGain, clsGain, dflGain);
    }

    /// <summary>
    /// Get all registered model version keys.
    /// </summary>
    public static string[] GetVersions() => _registrations.Keys.ToArray();

    /// <summary>
    /// Get supported variants for a model version.
    /// </summary>
    public static string[] GetVariants(string version)
    {
        if (!_registrations.TryGetValue(version, out var reg))
            return [];
        return reg.SupportedVariants;
    }

    /// <summary>
    /// Get the model scale for a specific version and variant.
    /// </summary>
    public static ModelScale GetScale(string version, string variant)
    {
        if (!_registrations.TryGetValue(version, out var reg))
            throw new ArgumentException($"Unknown model version '{version}'.");

        if (!reg.Scales.TryGetValue(variant, out var scale))
            throw new ArgumentException($"Unknown variant '{variant}' for version '{version}'.");

        return scale;
    }

    /// <summary>
    /// Check if a version is registered.
    /// </summary>
    public static bool IsRegistered(string version) => _registrations.ContainsKey(version);

    /// <summary>
    /// Check if a version has a loss factory registered.
    /// </summary>
    public static bool HasLossFactory(string version) =>
        _registrations.TryGetValue(version, out var reg) && reg.LossFactory != null;

    /// <summary>
    /// Ensure the static constructor of the given type has run, triggering registration.
    /// Call this to ensure a model version's assembly is loaded.
    /// </summary>
    public static void EnsureLoaded(Type modelType)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(modelType.TypeHandle);
    }
}
