using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;

namespace YOLOv8.Training.Schedulers;

/// <summary>
/// Learning rate scheduler with warmup support.
/// Supports linear decay and cosine annealing, matching YOLOv8 training.
/// Includes both LR warmup and momentum warmup matching Python ultralytics.
/// </summary>
public class WarmupLRScheduler
{
    private readonly torch.optim.Optimizer optimizer;
    private readonly int totalEpochs;
    private readonly double lrf; // final LR factor
    private readonly bool useCosine;

    // Warmup parameters
    private readonly double warmupEpochs;
    private readonly double warmupBiasLr;
    private readonly double warmupMomentum;
    private readonly double baseMomentum;
    private readonly int warmupIterations;

    // Store initial LRs
    private readonly double[] initialLRs;

    public WarmupLRScheduler(
        torch.optim.Optimizer optimizer,
        int totalEpochs,
        int iterationsPerEpoch,
        double lrf = 0.01,
        bool useCosine = false,
        double warmupEpochs = 3.0,
        double warmupBiasLr = 0.1,
        double warmupMomentum = 0.8,
        double baseMomentum = 0.937)
    {
        this.optimizer = optimizer;
        this.totalEpochs = totalEpochs;
        this.lrf = lrf;
        this.useCosine = useCosine;
        this.warmupEpochs = warmupEpochs;
        this.warmupBiasLr = warmupBiasLr;
        this.warmupMomentum = warmupMomentum;
        this.baseMomentum = baseMomentum;

        warmupIterations = Math.Max((int)Math.Round(warmupEpochs * iterationsPerEpoch), 100);

        // Store initial learning rates
        var groups = optimizer.ParamGroups.ToArray();
        initialLRs = new double[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            initialLRs[i] = groups[i].LearningRate;
        }
    }

    /// <summary>
    /// Compute the LR factor for a given epoch using linear or cosine schedule.
    /// </summary>
    public double GetLRFactor(int epoch)
    {
        if (useCosine)
        {
            // Cosine annealing: (1 - cos(x * pi / epochs)) / 2 * (lrf - 1) + 1
            return ((1.0 - Math.Cos(epoch * Math.PI / totalEpochs)) / 2.0) * (lrf - 1.0) + 1.0;
        }
        else
        {
            // Linear decay from 1.0 to lrf
            return (1.0 - (double)epoch / totalEpochs) * (1.0 - lrf) + lrf;
        }
    }

    /// <summary>
    /// Update learning rates and momentum at the start of each iteration.
    /// Handles warmup for the first few epochs (both LR and momentum).
    /// </summary>
    /// <param name="epoch">Current epoch (0-based)</param>
    /// <param name="iteration">Current global iteration</param>
    public void Step(int epoch, int iteration)
    {
        var groups = optimizer.ParamGroups.ToArray();

        if (iteration <= warmupIterations)
        {
            // Warmup phase: linearly ramp LR and momentum
            double xi0 = 0;
            double xi1 = warmupIterations;

            for (int j = 0; j < groups.Length; j++)
            {
                // LR warmup: bias group (last group, g2) starts at warmupBiasLr, others start at 0
                double startLR = (j == groups.Length - 1) ? warmupBiasLr : 0.0;
                double endLR = initialLRs[j] * GetLRFactor(epoch);
                double newLR = Interpolate(iteration, xi0, xi1, startLR, endLR);
                groups[j].LearningRate = newLR;

                // Momentum warmup: ramp from warmupMomentum to baseMomentum
                double newMomentum = Interpolate(iteration, xi0, xi1, warmupMomentum, baseMomentum);
                SetMomentum(groups[j], newMomentum);
            }
        }
        else
        {
            // Normal phase: apply LR schedule
            double factor = GetLRFactor(epoch);
            for (int j = 0; j < groups.Length; j++)
            {
                groups[j].LearningRate = initialLRs[j] * factor;
            }
        }
    }

    /// <summary>
    /// Set momentum on optimizer param group (handles SGD momentum and Adam beta1).
    /// </summary>
    private static void SetMomentum(object group, double momentum)
    {
        // TorchSharp param groups have specific Options for each optimizer type
        // Use pattern matching to handle SGD momentum and Adam/AdamW beta1
        switch (group)
        {
            case SGD.ParamGroup sgdGroup:
                sgdGroup.Options.momentum = momentum;
                break;
            case AdamW.ParamGroup adamwGroup:
                adamwGroup.Options.beta1 = momentum;
                break;
        }
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    private static double Interpolate(double x, double x0, double x1, double y0, double y1)
    {
        if (x <= x0) return y0;
        if (x >= x1) return y1;
        return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
    }
}
