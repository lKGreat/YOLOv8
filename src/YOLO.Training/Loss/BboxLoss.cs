using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using YOLO.Core.Utils;

namespace YOLO.Training.Loss;

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
            var targetLTRB = BboxUtils.Bbox2Dist(anchorPoints, targetBboxes, regMax);
            var fgTargetLTRB = targetLTRB[fgMask]; // (numFG, 4)
            var fgPredDist = predDist[fgMask]; // (numFG, 4*(reg_max+1))

            // DFLoss expects 2D target (numFG, 4) to return per-anchor loss (numFG, 1)
            dflLoss = DFLoss(
                fgPredDist.view(-1, regMax + 1), // (numFG*4, reg_max+1) -- flattened for cross_entropy
                fgTargetLTRB                      // (numFG, 4) -- keep 2D for correct per-anchor mean
            );
            // dflLoss: (numFG, 1), weight: (numFG,) -> weighted sum
            dflLoss = (dflLoss * weight.unsqueeze(-1)).sum() / targetScoresSum;
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
    /// <param name="predDist">Predicted distribution logits (numFG*4, reg_max+1)</param>
    /// <param name="target">Target continuous LTRB values (numFG, 4)</param>
    /// <returns>DFL loss per anchor (numFG, 1) -- mean over 4 LTRB directions</returns>
    private Tensor DFLoss(Tensor predDist, Tensor target)
    {
        // target: (numFG, 4), tl/tr/wl/wr all (numFG, 4)
        var tl = target.to(ScalarType.Int64);       // floor
        var tr = tl + 1;                             // ceil
        var wl = tr.to(ScalarType.Float32) - target; // weight for floor
        var wr = 1.0f - wl;                          // weight for ceil

        // cross_entropy needs 1D target, so flatten to (numFG*4,), then reshape back to (numFG, 4)
        var lossL = functional.cross_entropy(predDist, tl.view(-1), reduction: Reduction.None)
            .view(tl.shape) * wl;   // (numFG, 4)
        var lossR = functional.cross_entropy(predDist, tr.clamp_max(regMax).view(-1), reduction: Reduction.None)
            .view(tr.shape) * wr;   // (numFG, 4)

        // Mean over the 4 LTRB directions -> (numFG, 1) per-anchor DFL loss
        return (lossL + lossR).mean(new long[] { -1 }, keepdim: true);
    }
}
