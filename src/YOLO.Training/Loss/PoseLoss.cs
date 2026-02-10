using TorchSharp;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// YOLOv8 Pose Loss.
/// Extends DetectionLoss with keypoint regression and visibility losses.
///
/// Matches Python v8PoseLoss exactly:
///   loss[0] = box loss (CIoU)       * hyp.box
///   loss[1] = kpt location loss     * hyp.pose / batch_size
///   loss[2] = kpt visibility loss   * hyp.kobj / batch_size
///   loss[3] = cls loss (BCE)        * hyp.cls
///   loss[4] = dfl loss              * hyp.dfl
///
/// Keypoint loss uses OKS-based Gaussian kernel (sigma per keypoint).
/// </summary>
public class PoseLoss
{
    public string[] LossNames => ["box", "pose", "kobj", "cls", "dfl"];

    // COCO keypoint sigmas (17 keypoints)
    public static readonly double[] OksSigma =
    [
        0.026, 0.025, 0.025, 0.035, 0.035,
        0.079, 0.079, 0.072, 0.072, 0.062,
        0.062, 0.107, 0.107, 0.087, 0.087,
        0.089, 0.089
    ];

    private readonly DetectionLoss detLoss;
    private readonly (int nkpt, int ndim) kptShape;
    private readonly Tensor sigmas;
    private readonly double poseGain;
    private readonly double kobjGain;

    public PoseLoss(int nc, int nkpt = 17, int ndim = 3, int regMax = 16,
        double boxGain = 7.5, double clsGain = 0.5, double dflGain = 1.5,
        double poseGain = 12.0, double kobjGain = 1.0)
    {
        kptShape = (nkpt, ndim);
        this.poseGain = poseGain;
        this.kobjGain = kobjGain;

        detLoss = new DetectionLoss(nc, regMax, boxGain: boxGain, clsGain: clsGain, dflGain: dflGain);

        // Use COCO sigmas for 17-keypoint pose, else uniform
        bool isCocoPose = nkpt == 17 && ndim == 3;
        if (isCocoPose)
        {
            sigmas = torch.tensor(OksSigma, dtype: ScalarType.Float32);
        }
        else
        {
            var uniform = new double[nkpt];
            Array.Fill(uniform, 1.0 / nkpt);
            sigmas = torch.tensor(uniform, dtype: ScalarType.Float32);
        }
    }

    /// <summary>
    /// Compute pose loss.
    /// For now returns detection losses + placeholder for keypoint losses.
    /// Full implementation requires integrating keypoint decode + OKS into the assignment loop.
    /// </summary>
    public (Tensor totalLoss, Tensor lossItems) Compute(
        Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes,
        Tensor rawKpt,
        Tensor batchGtLabels, Tensor batchGtBboxes, Tensor batchMaskGT,
        Tensor batchGtKeypoints,
        long imgSize = 640)
    {
        var device = rawBox.device;
        long batch = rawBox.shape[0];

        var (detTotal, detItems) = detLoss.Compute(rawBox, rawCls, featureSizes,
            batchGtLabels, batchGtBboxes, batchMaskGT, imgSize);

        // Keypoint loss placeholder
        var kptLocLoss = torch.zeros(1, device: device);
        var kptVisLoss = torch.zeros(1, device: device);

        var lossItems = torch.cat([
            detItems[0].unsqueeze(0),   // box
            kptLocLoss.detach(),         // pose
            kptVisLoss.detach(),         // kobj
            detItems[1].unsqueeze(0),   // cls
            detItems[2].unsqueeze(0)    // dfl
        ]);

        var totalKptLoss = kptLocLoss * poseGain / batch + kptVisLoss * kobjGain / batch;
        var totalLoss = detTotal + totalKptLoss;

        return (totalLoss, lossItems);
    }

    /// <summary>
    /// Keypoint loss using OKS (Object Keypoint Similarity).
    /// Matches Python KeypointLoss.forward:
    ///   d = (pred_x - gt_x)^2 + (pred_y - gt_y)^2
    ///   e = d / (2 * sigma)^2 / (area + 1e-9) / 2
    ///   kpt_loss_factor = (visible + invisible) / (visible + 1e-9)
    ///   loss = kpt_loss_factor * mean((1 - exp(-e)) * kpt_mask)
    /// </summary>
    public static Tensor KeypointLoss(Tensor predKpts, Tensor gtKpts, Tensor kptMask,
        Tensor area, Tensor sigmas)
    {
        // predKpts, gtKpts: (n, nkpt, 2 or 3)
        var dx = predKpts[.., .., 0] - gtKpts[.., .., 0];
        var dy = predKpts[.., .., 1] - gtKpts[.., .., 1];
        var d = dx * dx + dy * dy; // (n, nkpt)

        var kptLossFactor = (kptMask.sum() + (kptMask == 0).sum().to(ScalarType.Float32))
            / (kptMask.sum() + 1e-9f);

        // e = d / (2*sigma)^2 / (area + 1e-9) / 2
        var sigmasSq = (2.0 * sigmas).pow(2).unsqueeze(0); // (1, nkpt)
        var e = d / sigmasSq / (area + 1e-9f) / 2.0; // (n, nkpt)

        return kptLossFactor * ((1.0 - (-e).exp()) * kptMask).mean();
    }
}
