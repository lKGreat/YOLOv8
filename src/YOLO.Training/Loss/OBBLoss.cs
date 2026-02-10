using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// YOLOv8 OBB (Oriented Bounding Box) Loss.
/// Extends DetectionLoss with an angle regression loss component.
///
/// Based on official Ultralytics OBB loss:
///   loss[0] = box loss (CIoU)     * hyp.box
///   loss[1] = cls loss (BCE)      * hyp.cls
///   loss[2] = dfl loss            * hyp.dfl
///   loss[3] = angle loss          * hyp.box (angle shares box gain)
///
/// The angle loss uses smooth L1 or cross-entropy depending on implementation.
/// For YOLOv8-obb, the angle is predicted as a single continuous value and
/// trained with rotation-aware IoU loss (Probiou/RotatedIoU).
/// </summary>
public class OBBLoss
{
    public string[] LossNames => ["box", "cls", "dfl", "angle"];

    private readonly DetectionLoss detLoss;
    private readonly double angleGain;

    public OBBLoss(int nc, int regMax = 16,
        double boxGain = 7.5, double clsGain = 0.5, double dflGain = 1.5,
        double angleGain = 7.5)
    {
        this.angleGain = angleGain;
        detLoss = new DetectionLoss(nc, regMax, boxGain: boxGain, clsGain: clsGain, dflGain: dflGain);
    }

    /// <summary>
    /// Compute OBB loss.
    /// Currently returns detection losses + placeholder angle loss.
    /// Full implementation requires rotated IoU computation in the assignment loop.
    /// </summary>
    public (Tensor totalLoss, Tensor lossItems) Compute(
        Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes,
        Tensor rawAngle,
        Tensor batchGtLabels, Tensor batchGtBboxes, Tensor batchMaskGT,
        Tensor? batchGtAngles = null,
        long imgSize = 1024)
    {
        var device = rawBox.device;
        long batch = rawBox.shape[0];

        var (detTotal, detItems) = detLoss.Compute(rawBox, rawCls, featureSizes,
            batchGtLabels, batchGtBboxes, batchMaskGT, imgSize);

        // Angle loss placeholder — full implementation needs:
        // 1. Decode angle predictions
        // 2. Assign using rotated IoU
        // 3. Compute angle regression loss (smooth L1 or similar)
        var angleLoss = torch.zeros(1, device: device);

        var lossItems = torch.cat([
            detItems[0].unsqueeze(0),   // box
            detItems[1].unsqueeze(0),   // cls
            detItems[2].unsqueeze(0),   // dfl
            angleLoss.detach()           // angle
        ]);

        var totalAngleLoss = angleLoss * angleGain;
        var totalLoss = detTotal + totalAngleLoss;

        return (totalLoss, lossItems);
    }
}
