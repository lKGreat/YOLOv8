using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using static TorchSharp.torch.optim;

namespace YOLOv8.Training.Optimizers;

/// <summary>
/// Factory for creating optimizers with proper parameter groups.
/// Implements YOLOv8's 3-group parameter strategy:
///   Group 0 (g0): Conv/Linear weights   -> with weight_decay
///   Group 1 (g1): BatchNorm weights      -> no weight_decay
///   Group 2 (g2): All biases             -> no weight_decay
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
    /// <param name="weightDecay">Base weight decay</param>
    /// <param name="batchSize">Training batch size (for weight decay scaling)</param>
    /// <param name="accumulate">Gradient accumulation steps</param>
    /// <param name="nbs">Nominal batch size (default 64)</param>
    /// <param name="nc">Number of classes (for auto mode)</param>
    /// <param name="totalIterations">Total training iterations (for auto mode)</param>
    public static torch.optim.Optimizer Create(
        Module model,
        string name = "auto",
        double lr = 0.01,
        double momentum = 0.937,
        double weightDecay = 0.0005,
        int batchSize = 16,
        int accumulate = 1,
        int nbs = 64,
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

        // Scale weight decay: weight_decay *= batch_size * accumulate / nbs
        double scaledWeightDecay = weightDecay * batchSize * accumulate / nbs;

        // Separate parameters into 3 groups matching Python ultralytics/engine/trainer.py
        // g0: Conv/Linear weights (with weight_decay)
        // g1: BatchNorm weights (no weight_decay)
        // g2: All biases (no weight_decay)
        var g0 = new List<Parameter>(); // Conv/Linear weights
        var g1 = new List<Parameter>(); // BatchNorm weights
        var g2 = new List<Parameter>(); // Biases

        foreach (var (paramName, param) in model.named_parameters())
        {
            if (!param.requires_grad) continue;

            if (paramName.EndsWith(".bias"))
            {
                // All biases go to g2
                g2.Add(param);
            }
            else if (paramName.Contains("bn.weight") || paramName.Contains("BatchNorm"))
            {
                // BatchNorm weights go to g1
                g1.Add(param);
            }
            else if (paramName.EndsWith(".weight"))
            {
                // Conv/Linear weights go to g0
                g0.Add(param);
            }
            else
            {
                // Default: treat as conv weight
                g0.Add(param);
            }
        }

        if (name.Equals("SGD", StringComparison.OrdinalIgnoreCase))
        {
            // Create SGD with parameter groups and nesterov=True
            // g0 (conv weights): lr, momentum, weight_decay
            // g1 (bn weights): lr, momentum, no weight_decay
            // g2 (biases): lr, momentum, no weight_decay
            return CreateSGDWithGroups(g0, g1, g2, lr, momentum, scaledWeightDecay);
        }
        else if (name.Equals("AdamW", StringComparison.OrdinalIgnoreCase))
        {
            var optimizer = CreateAdamWWithGroups(g0, g1, g2, lr, momentum, scaledWeightDecay);
            return optimizer;
        }
        else if (name.Equals("Adam", StringComparison.OrdinalIgnoreCase))
        {
            var allParams = new List<Parameter>();
            allParams.AddRange(g0);
            allParams.AddRange(g1);
            allParams.AddRange(g2);
            return torch.optim.Adam(allParams, lr, beta1: momentum, beta2: 0.999, weight_decay: scaledWeightDecay);
        }
        else
        {
            throw new ArgumentException($"Unknown optimizer: {name}. Use SGD, AdamW, Adam, or auto.");
        }
    }

    /// <summary>
    /// Create SGD optimizer with 3 parameter groups (nesterov=True).
    /// TorchSharp requires all params in one call, so we create one optimizer
    /// and manually apply weight decay to g0 only via L2 regularization in forward step.
    /// </summary>
    private static torch.optim.Optimizer CreateSGDWithGroups(
        List<Parameter> g0, List<Parameter> g1, List<Parameter> g2,
        double lr, double momentum, double weightDecay)
    {
        // TorchSharp SGD supports parameter groups via ParamGroup
        var groups = new List<SGD.ParamGroup>();

        if (g0.Count > 0)
        {
            groups.Add(new SGD.ParamGroup(g0, new SGD.Options
            {
                LearningRate = lr,
                momentum = momentum,
                weight_decay = weightDecay,
                nesterov = true
            }));
        }

        if (g1.Count > 0)
        {
            groups.Add(new SGD.ParamGroup(g1, new SGD.Options
            {
                LearningRate = lr,
                momentum = momentum,
                weight_decay = 0,
                nesterov = true
            }));
        }

        if (g2.Count > 0)
        {
            groups.Add(new SGD.ParamGroup(g2, new SGD.Options
            {
                LearningRate = lr,
                momentum = momentum,
                weight_decay = 0,
                nesterov = true
            }));
        }

        return torch.optim.SGD(groups, lr, momentum, nesterov: true);
    }

    /// <summary>
    /// Create AdamW optimizer with 3 parameter groups.
    /// </summary>
    private static torch.optim.Optimizer CreateAdamWWithGroups(
        List<Parameter> g0, List<Parameter> g1, List<Parameter> g2,
        double lr, double beta1, double weightDecay)
    {
        var groups = new List<AdamW.ParamGroup>();

        if (g0.Count > 0)
        {
            groups.Add(new AdamW.ParamGroup(g0, new AdamW.Options
            {
                LearningRate = lr,
                beta1 = beta1,
                beta2 = 0.999,
                weight_decay = weightDecay
            }));
        }

        if (g1.Count > 0)
        {
            groups.Add(new AdamW.ParamGroup(g1, new AdamW.Options
            {
                LearningRate = lr,
                beta1 = beta1,
                beta2 = 0.999,
                weight_decay = 0
            }));
        }

        if (g2.Count > 0)
        {
            groups.Add(new AdamW.ParamGroup(g2, new AdamW.Options
            {
                LearningRate = lr,
                beta1 = beta1,
                beta2 = 0.999,
                weight_decay = 0
            }));
        }

        return torch.optim.AdamW(groups, lr, beta1: beta1, beta2: 0.999);
    }
}
