using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Training;

/// <summary>
/// Model Exponential Moving Average (EMA).
/// Maintains a shadow copy of model parameters that is updated each step:
///   ema_param = decay * ema_param + (1 - decay) * model_param
///
/// Decay ramps up from 0 to target (0.9999) using:
///   decay = target_decay * (1 - exp(-updates / tau))
/// </summary>
public class ModelEMA : IDisposable
{
    private readonly Dictionary<string, Tensor> shadowParams = new();
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
            shadowParams[name] = param.detach().clone();
        }
    }

    /// <summary>
    /// Compute current decay rate based on update count.
    /// </summary>
    private double GetDecay()
    {
        return decayTarget * (1.0 - Math.Exp(-updates / tau));
    }

    /// <summary>
    /// Update EMA parameters with current model parameters.
    /// </summary>
    public void Update(Module model)
    {
        if (!enabled) return;

        updates++;
        var d = GetDecay();

        using var _ = torch.no_grad();
        foreach (var (name, param) in model.named_parameters())
        {
            if (shadowParams.TryGetValue(name, out var shadow) &&
                param.dtype == ScalarType.Float32 || param.dtype == ScalarType.Float16 || param.dtype == ScalarType.Float64)
            {
                shadow.mul_(d).add_(param.detach(), alpha: 1.0 - d);
            }
        }
    }

    /// <summary>
    /// Apply EMA parameters to the model (for evaluation).
    /// </summary>
    public void ApplyTo(Module model)
    {
        using var _ = torch.no_grad();
        foreach (var (name, param) in model.named_parameters())
        {
            if (shadowParams.TryGetValue(name, out var shadow))
            {
                param.copy_(shadow);
            }
        }
    }

    /// <summary>
    /// Get a copy of the shadow parameters (for saving).
    /// </summary>
    public Dictionary<string, Tensor> GetShadowParams()
    {
        return new Dictionary<string, Tensor>(shadowParams);
    }

    public void Disable() => enabled = false;
    public void Enable() => enabled = true;

    public void Dispose()
    {
        if (!disposed)
        {
            foreach (var t in shadowParams.Values)
                t.Dispose();
            shadowParams.Clear();
            disposed = true;
        }
    }
}
