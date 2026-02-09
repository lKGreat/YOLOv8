using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Models;
using YOLO.Core.Modules;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// YOLOv Detection Loss.
/// Combines:
///   - CIoU loss for bounding box regression (weight: 7.5)
///   - Distribution Focal Loss (DFL) for box distribution (weight: 1.5)
///   - Binary Cross-Entropy for classification (weight: 0.5)
///
/// Uses TaskAlignedAssigner for dynamic label assignment.
/// </summary>
public class DetectionLoss
{
    private readonly TaskAlignedAssigner assigner;
    private readonly BboxLoss bboxLoss;
    private readonly DFL dfl;
    private readonly int nc;
    private readonly int regMax;
    private readonly long[] strides;
    private readonly double boxGain;
    private readonly double clsGain;
    private readonly double dflGain;

    public DetectionLoss(int nc, int regMax = 16, long[]? strides = null,
        double boxGain = 7.5, double clsGain = 0.5, double dflGain = 1.5)
    {
        this.nc = nc;
        this.regMax = regMax;
        this.strides = strides ?? ModelConfig.DetectionStrides;
        this.boxGain = boxGain;
        this.clsGain = clsGain;
        this.dflGain = dflGain;

        assigner = new TaskAlignedAssigner(topK: 10, alpha: 0.5, beta: 6.0);
        bboxLoss = new BboxLoss(regMax - 1);
        dfl = new DFL("loss_dfl", regMax);
    }

    /// <summary>
    /// Compute total detection loss.
    /// </summary>
    /// <param name="rawBox">Raw box distribution predictions (B, 4*reg_max, N)</param>
    /// <param name="rawCls">Raw classification logits (B, nc, N)</param>
    /// <param name="featureSizes">Feature map sizes per level</param>
    /// <param name="batchGtLabels">GT class labels (B, maxGT, 1)</param>
    /// <param name="batchGtBboxes">GT bboxes normalized xyxy (B, maxGT, 4)</param>
    /// <param name="batchMaskGT">Valid GT mask (B, maxGT, 1)</param>
    /// <param name="imgSize">Input image size (for denormalization)</param>
    /// <returns>Tuple of (totalLoss, lossItems tensor [box, cls, dfl])</returns>
    public (Tensor totalLoss, Tensor lossItems) Compute(
        Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes,
        Tensor batchGtLabels, Tensor batchGtBboxes, Tensor batchMaskGT,
        long imgSize = 640)
    {
        var device = rawBox.device;
        var dtype = rawBox.dtype;
        long batch = rawBox.shape[0];
        long nAnchors = rawBox.shape[2];

        // Transpose to (B, N, C) format
        var predDist = rawBox.permute(0, 2, 1); // (B, N, 4*reg_max)
        var predClsLogits = rawCls.permute(0, 2, 1); // (B, N, nc)

        // Generate anchor points and stride tensor
        var (anchorPoints, strideTensor) = BboxUtils.MakeAnchors(featureSizes, strides, 0.5, device);

        // Decode box predictions (DFL -> distances -> boxes)
        // predDist: (B, N, 4*reg_max) -> reshape for DFL
        var distForDFL = predDist.permute(0, 2, 1); // (B, 4*reg_max, N)
        var decodedDist = dfl.forward(distForDFL); // (B, 4, N)
        var predBboxes = BboxUtils.Dist2Bbox(decodedDist, anchorPoints, xywh: false); // (B, 4, N) xyxy

        // Transpose to (B, N, 4) and scale by stride
        predBboxes = predBboxes.permute(0, 2, 1); // (B, N, 4)
        var strideExpanded = strideTensor.unsqueeze(0); // (1, N, 1)
        var predBboxesScaled = predBboxes * strideExpanded;

        // Scale GT bboxes to match (they are in normalized coords, scale to pixel)
        var gtBboxesScaled = batchGtBboxes * imgSize;

        // Predicted scores (sigmoid for assignment)
        var predScores = predClsLogits.detach().sigmoid();

        // Task-aligned assignment
        var (targetLabels, targetBboxes, targetScores, fgMask, targetGTIdx) =
            assigner.Assign(predScores, predBboxesScaled.detach(), anchorPoints * strideTensor,
                batchGtLabels, gtBboxesScaled, batchMaskGT);

        var targetScoresSum = torch.max(
            targetScores.sum(),
            torch.tensor(1.0f, device: device));

        // Convert back to stride-relative coordinates for loss
        var targetBboxesStride = targetBboxes / strideExpanded;
        var predBboxesStride = predBboxes;

        // === Classification Loss (BCE) ===
        var clsLoss = functional.binary_cross_entropy_with_logits(
            predClsLogits, targetScores, reduction: Reduction.None)
            .sum() / targetScoresSum;

        // === Box Loss (CIoU + DFL) ===
        Tensor iouLoss, dflLossVal;
        if (fgMask.sum().item<long>() > 0)
        {
            (iouLoss, dflLossVal) = bboxLoss.Compute(
                predDist, predBboxesStride, anchorPoints,
                targetBboxesStride, targetScores, targetScoresSum, fgMask);
        }
        else
        {
            iouLoss = torch.zeros(1, device: device);
            dflLossVal = torch.zeros(1, device: device);
        }

        // Apply loss gains - ensure all are scalar tensors
        var totalBoxLoss = iouLoss.reshape(1) * boxGain;
        var totalClsLoss = clsLoss.reshape(1) * clsGain;
        var totalDflLoss = dflLossVal.reshape(1) * dflGain;

        var totalLoss = (totalBoxLoss + totalClsLoss + totalDflLoss).sum() * batch;

        var lossItems = torch.cat([
            totalBoxLoss.detach(),
            totalClsLoss.detach(),
            totalDflLoss.detach()
        ]);

        return (totalLoss, lossItems);
    }
}
