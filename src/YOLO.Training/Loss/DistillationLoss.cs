using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// Knowledge distillation loss for YOLOv detection.
///
/// Supports three modes:
///   - "logit":   KL divergence on classification logits + MSE on box distribution logits
///   - "feature": MSE between teacher and student neck feature maps (with 1x1 Conv adaptation)
///   - "both":    Logit + Feature distillation combined
///
/// The teacher model is frozen and runs in eval mode. Both teacher and student receive
/// the same input images, so anchor positions (N) are identical.
/// </summary>
public class DistillationLoss : Module<DistillationLoss.DistillInput, (Tensor loss, Tensor lossItem)>
{
    /// <summary>
    /// Input container for distillation loss computation.
    /// </summary>
    public record DistillInput(
        Tensor StudentRawBox,      // (B, 4*reg_max, N)
        Tensor StudentRawCls,      // (B, nc, N)
        Tensor TeacherRawBox,      // (B, 4*reg_max, N)
        Tensor TeacherRawCls,      // (B, nc, N)
        Tensor[]? StudentFeatures, // neck features [P3, P4, P5] or null
        Tensor[]? TeacherFeatures  // neck features [P3, P4, P5] or null
    );

    private readonly double temperature;
    private readonly string mode; // "logit", "feature", "both"
    private readonly double clsKdWeight;
    private readonly double boxKdWeight;
    private readonly double featKdWeight;

    // Feature adaptation layers: 1x1 Conv2d to match student channels to teacher channels
    private readonly ModuleList<Conv2d>? adapters;

    public DistillationLoss(
        string name,
        double temperature = 20.0,
        string mode = "logit",
        double clsKdWeight = 1.0,
        double boxKdWeight = 1.0,
        double featKdWeight = 1.0,
        long[]? studentChannels = null,
        long[]? teacherChannels = null,
        Device? device = null)
        : base(name)
    {
        this.temperature = temperature;
        this.mode = mode.ToLowerInvariant();
        this.clsKdWeight = clsKdWeight;
        this.boxKdWeight = boxKdWeight;
        this.featKdWeight = featKdWeight;

        // Create feature adaptation layers if needed (for "feature" or "both" mode)
        if ((this.mode == "feature" || this.mode == "both") &&
            studentChannels != null && teacherChannels != null)
        {
            adapters = new ModuleList<Conv2d>();
            for (int i = 0; i < studentChannels.Length; i++)
            {
                if (studentChannels[i] != teacherChannels[i])
                {
                    // 1x1 Conv to project student channels to teacher channel space
                    adapters.Add(Conv2d(studentChannels[i], teacherChannels[i], 1, bias: false));
                }
                else
                {
                    // Identity: no adaptation needed, but we register a dummy 1x1 conv
                    // to keep indices aligned. We could use nn.Identity but Conv2d is simpler here.
                    adapters.Add(Conv2d(studentChannels[i], teacherChannels[i], 1, bias: false));
                    // Initialize as identity
                    using (torch.no_grad())
                    {
                        var w = adapters[^1].weight!;
                        w.zero_();
                        long minCh = Math.Min(studentChannels[i], teacherChannels[i]);
                        for (long c = 0; c < minCh; c++)
                            w[c, c, 0, 0] = 1.0f;
                    }
                }
            }

            RegisterComponents();

            if (device is not null && device.type != DeviceType.CPU)
                this.to(device);
        }
        else
        {
            RegisterComponents();
        }
    }

    public override (Tensor loss, Tensor lossItem) forward(DistillInput input)
    {
        return Compute(
            input.StudentRawBox, input.StudentRawCls,
            input.TeacherRawBox, input.TeacherRawCls,
            input.StudentFeatures, input.TeacherFeatures);
    }

    /// <summary>
    /// Compute distillation loss.
    /// </summary>
    /// <param name="studentRawBox">Student raw box distributions (B, 4*reg_max, N)</param>
    /// <param name="studentRawCls">Student raw classification logits (B, nc, N)</param>
    /// <param name="teacherRawBox">Teacher raw box distributions (B, 4*reg_max, N)</param>
    /// <param name="teacherRawCls">Teacher raw classification logits (B, nc, N)</param>
    /// <param name="studentFeatures">Optional student neck features [P3, P4, P5]</param>
    /// <param name="teacherFeatures">Optional teacher neck features [P3, P4, P5]</param>
    /// <returns>Tuple of (distillLoss scalar, lossItem detached scalar)</returns>
    public (Tensor loss, Tensor lossItem) Compute(
        Tensor studentRawBox, Tensor studentRawCls,
        Tensor teacherRawBox, Tensor teacherRawCls,
        Tensor[]? studentFeatures = null,
        Tensor[]? teacherFeatures = null)
    {
        var device = studentRawBox.device;
        var totalLoss = torch.zeros(1, device: device);

        // === Logit-based distillation ===
        if (mode == "logit" || mode == "both")
        {
            var logitLoss = ComputeLogitLoss(
                studentRawBox, studentRawCls,
                teacherRawBox, teacherRawCls);
            totalLoss = totalLoss + logitLoss;
        }

        // === Feature-based distillation ===
        if ((mode == "feature" || mode == "both") &&
            studentFeatures != null && teacherFeatures != null && adapters != null)
        {
            var featLoss = ComputeFeatureLoss(studentFeatures, teacherFeatures);
            totalLoss = totalLoss + featLoss;
        }

        return (totalLoss.sum(), totalLoss.detach());
    }

    /// <summary>
    /// Compute logit-level distillation loss:
    ///   - Classification: KL divergence with temperature scaling
    ///   - Box distribution: MSE on raw box logits (foreground-masked by teacher confidence)
    /// </summary>
    private Tensor ComputeLogitLoss(
        Tensor studentRawBox, Tensor studentRawCls,
        Tensor teacherRawBox, Tensor teacherRawCls)
    {
        var device = studentRawBox.device;
        long batch = studentRawBox.shape[0];

        // --- Classification KL divergence ---
        // Transpose to (B, N, nc) for softmax over class dimension
        var sCls = studentRawCls.permute(0, 2, 1); // (B, N, nc)
        var tCls = teacherRawCls.permute(0, 2, 1); // (B, N, nc)

        // Apply temperature scaling
        var sLogProb = functional.log_softmax(sCls / temperature, dim: -1);
        var tProb = functional.softmax(tCls / temperature, dim: -1);

        // KL divergence: sum over classes, mean over batch and anchors
        // KL(P||Q) = sum(P * (log(P) - log(Q)))
        var klDiv = functional.kl_div(sLogProb, tProb, reduction: nn.Reduction.Sum)
            / (batch * sCls.shape[1]);

        // Scale by T^2 (standard knowledge distillation scaling)
        var clsKdLoss = klDiv * (temperature * temperature) * clsKdWeight;

        // --- Box distribution MSE ---
        // Only apply to anchors where teacher is confident
        // Use teacher's max class score as confidence indicator
        var teacherMaxScores = teacherRawCls.permute(0, 2, 1).sigmoid().max(dim: -1).values; // (B, N)
        var confThreshold = 0.1;
        var fgMask = teacherMaxScores > confThreshold; // (B, N)

        Tensor boxKdLoss;
        long fgCount = fgMask.sum().item<long>();

        if (fgCount > 0)
        {
            // Transpose to (B, N, 4*reg_max) for per-anchor loss
            var sBox = studentRawBox.permute(0, 2, 1); // (B, N, 4*reg_max)
            var tBox = teacherRawBox.permute(0, 2, 1); // (B, N, 4*reg_max)

            // Expand mask to match box dims
            var fgMaskExpanded = fgMask.unsqueeze(-1).expand_as(sBox); // (B, N, 4*reg_max)

            // MSE only on foreground anchors
            var diff = (sBox - tBox).pow(2);
            var maskedDiff = diff * fgMaskExpanded.to(diff.dtype);
            boxKdLoss = maskedDiff.sum() / Math.Max(fgCount, 1) * boxKdWeight;
        }
        else
        {
            boxKdLoss = torch.zeros(1, device: device);
        }

        return (clsKdLoss + boxKdLoss).reshape(1);
    }

    /// <summary>
    /// Compute feature-level distillation loss:
    ///   - MSE between adapted student features and teacher features at each neck level (P3/P4/P5)
    /// </summary>
    private Tensor ComputeFeatureLoss(Tensor[] studentFeatures, Tensor[] teacherFeatures)
    {
        var device = studentFeatures[0].device;
        var totalFeatLoss = torch.zeros(1, device: device);
        int numLevels = Math.Min(studentFeatures.Length, teacherFeatures.Length);

        for (int i = 0; i < numLevels; i++)
        {
            var sFeat = studentFeatures[i]; // (B, Cs, H, W)
            var tFeat = teacherFeatures[i]; // (B, Ct, H, W)

            // Adapt student channels to match teacher if needed
            if (adapters != null && i < adapters.Count)
            {
                sFeat = adapters[i].forward(sFeat); // (B, Ct, H, W)
            }

            // Normalize features before MSE (stabilizes training)
            var sNorm = functional.normalize(sFeat.flatten(2), dim: -1); // (B, C, H*W)
            var tNorm = functional.normalize(tFeat.flatten(2), dim: -1); // (B, C, H*W)

            // MSE loss
            var mse = functional.mse_loss(sNorm, tNorm, reduction: Reduction.Mean);
            totalFeatLoss = totalFeatLoss + mse;
        }

        return totalFeatLoss * featKdWeight;
    }
}
