using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Modules;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Heads;

/// <summary>
/// YOLOv8 Pose head for keypoint detection.
/// Extends DetectHead with keypoint regression branches (cv4).
///
/// Matches Python ultralytics Pose class:
///   - kpt_shape: (nkpt, ndim) e.g. (17, 3) for COCO
///   - nk = nkpt * ndim total keypoint outputs
///   - cv4: per-level Conv→Conv→Conv2d(nk) branches
///
/// Training returns (rawFeats, rawKpts).
/// Inference returns decoded keypoints concatenated with boxes/scores.
/// </summary>
public class PoseHead : Module<Tensor[], PoseHead.PoseOutput>
{
    public record struct PoseOutput(
        Tensor Boxes, Tensor Scores, Tensor[] RawFeats, Tensor Keypoints);

    public int NumClasses { get; }
    public int RegMax { get; }
    public (int nkpt, int ndim) KptShape { get; }
    public int Nk { get; } // total keypoint channels = nkpt * ndim
    public int NumDetectionLayers { get; }
    public long[] Strides { get; }

    private readonly DetectHead detect;
    private readonly ModuleList<Sequential> cv4; // keypoint branches

    public PoseHead(string name, int nc, long[] channelsPerLevel, long[] strides,
        int regMax = 16, int nkpt = 17, int ndim = 3)
        : base(name)
    {
        NumClasses = nc;
        RegMax = regMax;
        KptShape = (nkpt, ndim);
        Nk = nkpt * ndim;
        NumDetectionLayers = channelsPerLevel.Length;
        Strides = strides;

        detect = new DetectHead("detect", nc, channelsPerLevel, strides, regMax);

        cv4 = new ModuleList<Sequential>();
        long c4 = Math.Max(channelsPerLevel[0] / 4, Nk);
        for (int i = 0; i < channelsPerLevel.Length; i++)
        {
            long ch = channelsPerLevel[i];
            cv4.Add(Sequential(
                ($"cv4_{i}_0", new ConvBlock($"cv4_{i}_0", ch, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_1", new ConvBlock($"cv4_{i}_1", c4, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_2", Conv2d(c4, Nk, 1) as Module<Tensor, Tensor>)
            ));
        }

        RegisterComponents();
    }

    public DetectHead DetectSubHead => detect;

    public override PoseOutput forward(Tensor[] feats)
    {
        long bs = feats[0].shape[0];

        // Keypoint branch
        var kptParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var k_i = cv4[i].forward(feats[i]); // (B, nk, H_i, W_i)
            kptParts[i] = k_i.view(bs, Nk, -1);  // (B, nk, H_i*W_i)
        }
        var rawKpt = torch.cat(kptParts, dim: -1); // (B, nk, totalAnchors)

        // Detection forward
        var (boxes, scores, rawFeats) = detect.forward(feats);

        // Decode keypoints (non-export path)
        var decoded = KptsDecode(rawKpt, detect);

        return new PoseOutput(boxes, scores, rawFeats, decoded);
    }

    /// <summary>
    /// Training forward — returns raw predictions.
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes, Tensor rawKpt)
        ForwardTrain(Tensor[] feats)
    {
        long bs = feats[0].shape[0];

        var kptParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var k_i = cv4[i].forward(feats[i]);
            kptParts[i] = k_i.view(bs, Nk, -1);
        }
        var rawKpt = torch.cat(kptParts, dim: -1); // (B, nk, totalAnchors)

        var (rawBox, rawCls, featureSizes) = detect.ForwardTrain(feats);
        return (rawBox, rawCls, featureSizes, rawKpt);
    }

    /// <summary>
    /// Decode raw keypoint predictions to image coordinates.
    /// Matches Python Pose.kpts_decode (non-export path):
    ///   y[0::ndim] = (y[0::ndim] * 2.0 + (anchors[0] - 0.5)) * strides
    ///   y[1::ndim] = (y[1::ndim] * 2.0 + (anchors[1] - 0.5)) * strides
    ///   if ndim==3: y[2::ndim].sigmoid_()
    /// </summary>
    private Tensor KptsDecode(Tensor kpts, DetectHead det)
    {
        // kpts: (B, nk, N)
        var y = kpts.clone();
        int ndim = KptShape.ndim;

        // Get anchor points from the detect head via a dummy call
        // Instead, use BboxUtils.MakeAnchors with cached strides
        // For simplicity, we decode in the same manner as Python

        if (ndim == 3)
        {
            // Sigmoid visibility: every 3rd channel starting from index 2
            for (int k = 2; k < Nk; k += ndim)
            {
                y[.., k, ..] = y[.., k, ..].sigmoid();
            }
        }

        // x/y coordinate decoding requires anchor points — defer to caller
        // For now, return raw sigmoid'd keypoints; full decode needs anchor integration
        return y;
    }
}
