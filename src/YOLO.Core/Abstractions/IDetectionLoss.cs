using TorchSharp;
using static TorchSharp.torch;

namespace YOLO.Core.Abstractions;

/// <summary>
/// Interface for detection loss computation.
/// Different YOLO versions use different loss strategies:
///   - v8: CIoU + DFL + BCE with TaskAlignedAssigner
///   - v9: Similar to v8 with auxiliary branch loss
///   - v10: Dual assignment loss (one-to-one + one-to-many)
/// </summary>
public interface IDetectionLoss
{
    /// <summary>
    /// Compute the total detection loss.
    /// </summary>
    /// <param name="rawBox">Raw box distribution predictions (B, 4*reg_max, N)</param>
    /// <param name="rawCls">Raw classification logits (B, nc, N)</param>
    /// <param name="featureSizes">Feature map sizes per level</param>
    /// <param name="batchGtLabels">GT class labels (B, maxGT, 1)</param>
    /// <param name="batchGtBboxes">GT bboxes normalized (B, maxGT, 4)</param>
    /// <param name="batchMaskGT">Valid GT mask (B, maxGT, 1)</param>
    /// <param name="imgSize">Input image size (for denormalization)</param>
    /// <returns>Tuple of (totalLoss, lossItems tensor per component)</returns>
    (Tensor totalLoss, Tensor lossItems) Compute(
        Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes,
        Tensor batchGtLabels, Tensor batchGtBboxes, Tensor batchMaskGT,
        long imgSize = 640);

    /// <summary>
    /// Names of each loss component for logging.
    /// e.g. ["box", "cls", "dfl"] for v8.
    /// </summary>
    string[] LossNames { get; }
}
