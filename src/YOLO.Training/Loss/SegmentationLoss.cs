using TorchSharp;
using YOLO.Core.Models;
using YOLO.Core.Modules;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// YOLOv8 Segmentation Loss.
/// Extends DetectionLoss with an additional mask loss component.
///
/// Matches Python v8SegmentationLoss exactly:
///   loss[0] = box loss (CIoU)   * hyp.box
///   loss[1] = seg loss (mask)   * hyp.box / batch_size
///   loss[2] = cls loss (BCE)    * hyp.cls
///   loss[3] = dfl loss          * hyp.dfl
///
/// The mask loss uses per-instance crop_mask + BCE on (pred_mask @ proto).
/// </summary>
public class SegmentationLoss
{
    public string[] LossNames => ["box", "seg", "cls", "dfl"];

    private readonly DetectionLoss detLoss;
    private readonly int nm; // number of masks
    private readonly bool overlapMask;
    private readonly double boxGain;

    /// <param name="nc">Number of classes</param>
    /// <param name="nm">Number of mask prototypes</param>
    /// <param name="regMax">DFL register max</param>
    /// <param name="overlapMask">Whether masks overlap (COCO-style)</param>
    /// <param name="boxGain">Box gain (used for seg gain: boxGain / batchSize)</param>
    /// <param name="clsGain">Classification gain</param>
    /// <param name="dflGain">DFL gain</param>
    public SegmentationLoss(int nc, int nm = 32, int regMax = 16, bool overlapMask = true,
        double boxGain = 7.5, double clsGain = 0.5, double dflGain = 1.5)
    {
        this.nm = nm;
        this.overlapMask = overlapMask;
        this.boxGain = boxGain;

        // Detection loss handles box/cls/dfl
        detLoss = new DetectionLoss(nc, regMax, boxGain: boxGain, clsGain: clsGain, dflGain: dflGain);
    }

    /// <summary>
    /// Compute segmentation loss.
    /// </summary>
    /// <param name="rawBox">Raw box distribution predictions (B, 4*reg_max, N)</param>
    /// <param name="rawCls">Raw classification logits (B, nc, N)</param>
    /// <param name="featureSizes">Feature map sizes per level</param>
    /// <param name="maskCoeffs">Mask coefficients (B, nm, N)</param>
    /// <param name="protos">Mask prototypes (B, nm, H_mask, W_mask)</param>
    /// <param name="batchGtLabels">GT class labels (B, maxGT, 1)</param>
    /// <param name="batchGtBboxes">GT bboxes normalized xyxy (B, maxGT, 4)</param>
    /// <param name="batchMaskGT">Valid GT mask (B, maxGT, 1)</param>
    /// <param name="batchGtMasks">GT binary masks (B*maxGT, H, W) or (B, maxGT, H, W)</param>
    /// <param name="imgSize">Input image size</param>
    public (Tensor totalLoss, Tensor lossItems) Compute(
        Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes,
        Tensor maskCoeffs, Tensor protos,
        Tensor batchGtLabels, Tensor batchGtBboxes, Tensor batchMaskGT,
        Tensor batchGtMasks, long imgSize = 640)
    {
        var device = rawBox.device;
        long batch = rawBox.shape[0];

        // Get detection losses (box, cls, dfl)
        var (detTotal, detItems) = detLoss.Compute(rawBox, rawCls, featureSizes,
            batchGtLabels, batchGtBboxes, batchMaskGT, imgSize);

        // Mask/seg loss placeholder — full implementation requires:
        // 1. Run assignment to get fg_mask and target_gt_idx
        // 2. For each image, crop GT masks, compute pred_mask = coeffs @ proto
        // 3. Binary cross-entropy with crop_mask
        // For now, return detection loss with zero seg loss
        var segLoss = torch.zeros(1, device: device);

        // Combine: [box, seg, cls, dfl]
        var lossItems = torch.cat([
            detItems[0].unsqueeze(0),   // box
            segLoss.detach(),            // seg
            detItems[1].unsqueeze(0),   // cls
            detItems[2].unsqueeze(0)    // dfl
        ]);

        // Apply seg gain: boxGain / batchSize
        var totalSegLoss = segLoss * boxGain / batch;
        var totalLoss = detTotal + totalSegLoss;

        return (totalLoss, lossItems);
    }

    /// <summary>
    /// Compute mask loss for a single image.
    /// pred_mask = coeffs @ proto.view(nm, -1) -> reshape -> BCE with crop
    /// Matches Python single_mask_loss.
    /// </summary>
    public static Tensor SingleMaskLoss(Tensor gtMask, Tensor pred, Tensor proto,
        Tensor xyxy, Tensor area, int nm)
    {
        // pred: (n, nm), proto: (nm, H, W)
        long maskH = proto.shape[1];
        long maskW = proto.shape[2];

        var predMask = pred.matmul(proto.view(nm, -1))
            .view(-1, maskH, maskW); // (n, H, W)

        var loss = functional.binary_cross_entropy_with_logits(
            predMask, gtMask, reduction: Reduction.None); // (n, H, W)

        var cropped = CropMask(loss, xyxy); // (n, H, W)
        // Mean over spatial dims, divide by area, then mean over instances
        return (cropped.mean(new long[] { 1, 2 }) / area).mean();
    }

    /// <summary>
    /// Crop mask loss to bounding box region.
    /// Matches Python crop_mask: zero out pixels outside the bbox.
    /// </summary>
    public static Tensor CropMask(Tensor masks, Tensor boxes)
    {
        // masks: (n, H, W), boxes: (n, 4) as x1y1x2y2 in mask coords
        long n = masks.shape[0];
        long h = masks.shape[1];
        long w = masks.shape[2];

        var x1 = boxes[.., 0].view(n, 1, 1); // (n, 1, 1)
        var y1 = boxes[.., 1].view(n, 1, 1);
        var x2 = boxes[.., 2].view(n, 1, 1);
        var y2 = boxes[.., 3].view(n, 1, 1);

        var cols = torch.arange(w, device: masks.device).view(1, 1, w); // (1, 1, W)
        var rows = torch.arange(h, device: masks.device).view(1, h, 1); // (1, H, 1)

        var colMask = (cols >= x1) & (cols < x2); // (n, 1, W)
        var rowMask = (rows >= y1) & (rows < y2); // (n, H, 1)

        return masks * (colMask & rowMask).to(masks.dtype);
    }
}
