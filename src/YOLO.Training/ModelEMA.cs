using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training;

/// <summary>
/// Model Exponential Moving Average (EMA).
/// Maintains a shadow copy of model state_dict (parameters + buffers) that is updated each step:
///   ema_param = decay * ema_param + (1 - decay) * model_param
///
/// Decay ramps up from 0 to target (0.9999) using:
///   decay = target_decay * (1 - exp(-updates / tau))
///
/// Matches Python ultralytics ModelEMA which updates the full state_dict()
/// including BatchNorm running_mean/running_var buffers.
/// </summary>
public class ModelEMA : IDisposable
{
    private readonly Dictionary<string, Tensor> shadowState = new();
    private int updates;
    private readonly double decayTarget;
    private readonly double tau;
    private bool enabled = true;
    private bool disposed;

    /// <summary>
    /// Create an EMA tracker for the given model.
    /// </summary>
    /// <param name="model">The model to track</param>
    /// <param name="decay">Target decay rate</param>
    /// <param name="tau">Ramp-up time constant</param>
    public ModelEMA(Module model, double decay = 0.9999, double tau = 2000.0)
    {
        decayTarget = decay;
        this.tau = tau;
        updates = 0;

        // Copy all parameters
        foreach (var (name, param) in model.named_parameters())
        {
            shadowState[name] = param.detach().clone();
        }

        // Copy all buffers (BatchNorm running_mean, running_var, num_batches_tracked)
        foreach (var (name, buffer) in model.named_buffers())
        {
            shadowState[$"buffer_{name}"] = buffer.detach().clone();
        }
    }

    /// <summary>
    /// Compute current decay rate based on update count.
    /// </summary>
    private double GetDecay()
    {
        return decayTarget * (1.0 - Math.Exp(-updates / tau));
    }

    private static bool IsFloating(ScalarType dtype)
    {
        return dtype == ScalarType.Float32 || dtype == ScalarType.Float16 ||
               dtype == ScalarType.Float64 || dtype == ScalarType.BFloat16;
    }

    /// <summary>
    /// Update EMA state with current model state.
    /// Updates both parameters and buffers (matching Python state_dict() behavior).
    /// </summary>
    public void Update(Module model)
    {
        if (!enabled) return;

        updates++;
        var d = GetDecay();

        using var _ = torch.no_grad();

        // Update parameters
        foreach (var (name, param) in model.named_parameters())
        {
            if (shadowState.TryGetValue(name, out var shadow) && IsFloating(param.dtype))
            {
                shadow.mul_(d).add_(param.detach(), alpha: 1.0 - d);
            }
        }

        // Update buffers (BatchNorm running_mean/running_var)
        foreach (var (name, buffer) in model.named_buffers())
        {
            var key = $"buffer_{name}";
            if (shadowState.TryGetValue(key, out var shadow) && IsFloating(buffer.dtype))
            {
                shadow.mul_(d).add_(buffer.detach(), alpha: 1.0 - d);
            }
            else if (shadowState.TryGetValue(key, out var shadowNonFloat))
            {
                // Non-floating buffers (e.g. num_batches_tracked): just copy
                shadowNonFloat.copy_(buffer.detach());
            }
        }
    }

    /// <summary>
    /// Apply EMA state to the model (for evaluation).
    /// Restores both parameters and buffers.
    /// </summary>
    public void ApplyTo(Module model)
    {
        using var _ = torch.no_grad();

        foreach (var (name, param) in model.named_parameters())
        {
            if (shadowState.TryGetValue(name, out var shadow))
            {
                param.copy_(shadow);
            }
        }

        foreach (var (name, buffer) in model.named_buffers())
        {
            var key = $"buffer_{name}";
            if (shadowState.TryGetValue(key, out var shadow))
            {
                buffer.copy_(shadow);
            }
        }
    }

    /// <summary>
    /// Get a copy of the shadow state (for saving).
    /// </summary>
    public Dictionary<string, Tensor> GetShadowState()
    {
        return new Dictionary<string, Tensor>(shadowState);
    }

    public void Disable() => enabled = false;
    public void Enable() => enabled = true;

    public void Dispose()
    {
        if (!disposed)
        {
            foreach (var t in shadowState.Values)
                t.Dispose();
            shadowState.Clear();
            disposed = true;
        }
    }
}
