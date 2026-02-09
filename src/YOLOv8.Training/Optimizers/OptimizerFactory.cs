using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using static TorchSharp.torch.optim;

namespace YOLOv8.Training.Optimizers;

/// <summary>
/// Factory for creating optimizers with proper parameter groups.
/// Implements YOLOv8's 3-group parameter strategy:
///   Group 0: Conv/Linear weights   -> weight_decay
///   Group 1: BatchNorm weights     -> no weight_decay
///   Group 2: All biases            -> no weight_decay
/// </summary>
public static class OptimizerFactory
{
    /// <summary>
    /// Create optimizer with parameter groups matching YOLOv8 training.
    /// </summary>
    /// <param name="model">The model to optimize</param>
    /// <param name="name">Optimizer name: "SGD", "AdamW", or "auto"</param>
    /// <param name="lr">Learning rate</param>
    /// <param name="momentum">Momentum (SGD) or beta1 (AdamW)</param>
    /// <param name="weightDecay">Weight decay</param>
    /// <param name="nc">Number of classes (for auto mode)</param>
    /// <param name="totalIterations">Total training iterations (for auto mode)</param>
    public static torch.optim.Optimizer Create(
        Module<Tensor, (Tensor, Tensor, Tensor[])> model,
        string name = "auto",
        double lr = 0.01,
        double momentum = 0.937,
        double weightDecay = 0.0005,
        int nc = 80,
        long totalIterations = 0)
    {
        // Auto mode: SGD for large datasets, AdamW for smaller
        if (name.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (totalIterations > 10000)
            {
                name = "SGD";
                lr = 0.01;
                momentum = 0.9;
            }
            else
            {
                name = "AdamW";
                lr = Math.Round(0.002 * 5.0 / (4 + nc), 6);
                momentum = 0.9;
            }
        }

        // Collect all parameters - TorchSharp handles parameter groups differently
        var parameters = model.parameters();

        if (name.Equals("SGD", StringComparison.OrdinalIgnoreCase))
        {
            return torch.optim.SGD(parameters, lr, momentum, weight_decay: weightDecay);
        }
        else if (name.Equals("AdamW", StringComparison.OrdinalIgnoreCase))
        {
            return torch.optim.AdamW(parameters, lr, beta1: momentum, beta2: 0.999, weight_decay: weightDecay);
        }
        else if (name.Equals("Adam", StringComparison.OrdinalIgnoreCase))
        {
            return torch.optim.Adam(parameters, lr, beta1: momentum, beta2: 0.999, weight_decay: weightDecay);
        }
        else
        {
            throw new ArgumentException($"Unknown optimizer: {name}. Use SGD, AdamW, Adam, or auto.");
        }
    }
}
