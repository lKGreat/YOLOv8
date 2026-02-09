using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using YOLOv8.Core.Utils;

namespace YOLOv8.Training.Loss;

/// <summary>
/// Bounding box loss combining CIoU and DFL (Distribution Focal Loss).
/// </summary>
public class BboxLoss
{
    private readonly int regMax;

    public BboxLoss(int regMax = 16)
    {
        this.regMax = regMax;
    }

    /// <summary>
    /// Compute bbox loss (CIoU + DFL).
    /// </summary>
    /// <param name="predDist">Distribution predictions (B, N, 4*reg_max)</param>
    /// <param name="predBboxes">Decoded predicted bboxes (B, N, 4) xyxy</param>
    /// <param name="anchorPoints">Anchor points (N, 2)</param>
    /// <param name="targetBboxes">Target bboxes (B, N, 4) xyxy</param>
    /// <param name="targetScores">Target scores for weighting (B, N, nc)</param>
    /// <param name="targetScoresSum">Sum of target scores (scalar)</param>
    /// <param name="fgMask">Foreground mask (B, N) bool</param>
    /// <returns>Tuple of (CIoU loss, DFL loss)</returns>
    public (Tensor iouLoss, Tensor dflLoss) Compute(
        Tensor predDist, Tensor predBboxes, Tensor anchorPoints,
        Tensor targetBboxes, Tensor targetScores, Tensor targetScoresSum,
        Tensor fgMask)
    {
        // Weight by target score sum per anchor
        var weight = targetScores.sum(-1)[fgMask]; // foreground weights

        // CIoU loss
        var iou = IoUUtils.CIoU(
            predBboxes[fgMask], // (numFG, 4)
            targetBboxes[fgMask]  // (numFG, 4)
        );
        var iouLoss = ((1.0 - iou) * weight).sum() / targetScoresSum;

        // DFL loss
        Tensor dflLoss;
        if (regMax > 1)
        {
            // Convert target bboxes to LTRB distances from anchor points
            var targetLTRB = BboxUtils.Bbox2Dist(anchorPoints, targetBboxes, regMax - 1);
            var fgTargetLTRB = targetLTRB[fgMask]; // (numFG, 4)
            var fgPredDist = predDist[fgMask]; // (numFG, 4*reg_max)

            dflLoss = DFLoss(
                fgPredDist.view(-1, regMax), // (numFG*4, reg_max)
                fgTargetLTRB.view(-1)        // (numFG*4)
            );
            dflLoss = (dflLoss * weight.unsqueeze(-1).expand_as(fgTargetLTRB)).sum()
                       / targetScoresSum;
        }
        else
        {
            dflLoss = torch.zeros(1, device: predDist.device);
        }

        return (iouLoss, dflLoss);
    }

    /// <summary>
    /// Distribution Focal Loss.
    /// Computes weighted cross-entropy between the two adjacent bins of the target.
    /// </summary>
    /// <param name="predDist">Predicted distribution logits (M, reg_max)</param>
    /// <param name="target">Target continuous values (M,)</param>
    /// <returns>DFL loss per element (M,)</returns>
    private Tensor DFLoss(Tensor predDist, Tensor target)
    {
        var tl = target.to(ScalarType.Int64);       // floor
        var tr = tl + 1;                             // ceil
        var wl = tr.to(ScalarType.Float32) - target; // weight for floor
        var wr = 1.0f - wl;                          // weight for ceil

        var lossL = functional.cross_entropy(predDist, tl.view(-1), reduction: Reduction.None)
            .view(tl.shape) * wl;
        var lossR = functional.cross_entropy(predDist, tr.clamp_max(regMax - 1).view(-1), reduction: Reduction.None)
            .view(tr.shape) * wr;

        return (lossL + lossR).mean(-1, keepdim: true);
    }
}
