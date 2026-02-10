using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Modules;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Heads;

/// <summary>
/// YOLOv8 Segment head for instance segmentation.
/// Extends DetectHead with mask prototype generation and mask coefficient branches.
///
/// Matches Python ultralytics Segment class:
///   - proto: Proto(ch[0], npr, nm)  — generates nm prototype masks from P3
///   - cv4: per-level mask coefficient branches, each Conv→Conv→Conv2d(nm)
///
/// Training returns (rawFeats, maskCoeffs, protos).
/// Inference returns concatenated [boxes, scores, maskCoeffs] + protos.
/// </summary>
public class SegmentHead : Module<Tensor[], SegmentHead.SegmentOutput>
{
    public record struct SegmentOutput(
        Tensor Boxes, Tensor Scores, Tensor[] RawFeats,
        Tensor MaskCoeffs, Tensor Protos);

    public int NumClasses { get; }
    public int RegMax { get; }
    public int NumMasks { get; }
    public int NumDetectionLayers { get; }
    public long[] Strides { get; }

    // Reuse detect head internals
    private readonly DetectHead detect;
    private readonly Proto proto;
    private readonly ModuleList<Sequential> cv4; // mask coefficient branches

    public SegmentHead(string name, int nc, long[] channelsPerLevel, long[] strides,
        int regMax = 16, int nm = 32, int npr = 256)
        : base(name)
    {
        NumClasses = nc;
        RegMax = regMax;
        NumMasks = nm;
        NumDetectionLayers = channelsPerLevel.Length;
        Strides = strides;

        detect = new DetectHead("detect", nc, channelsPerLevel, strides, regMax);
        proto = new Proto("proto", channelsPerLevel[0], npr, nm);

        cv4 = new ModuleList<Sequential>();
        long c4 = Math.Max(channelsPerLevel[0] / 4, nm);
        for (int i = 0; i < channelsPerLevel.Length; i++)
        {
            long ch = channelsPerLevel[i];
            cv4.Add(Sequential(
                ($"cv4_{i}_0", new ConvBlock($"cv4_{i}_0", ch, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_1", new ConvBlock($"cv4_{i}_1", c4, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_2", Conv2d(c4, nm, 1) as Module<Tensor, Tensor>)
            ));
        }

        RegisterComponents();
    }

    /// <summary>
    /// Access the underlying DetectHead for weight loading or inspection.
    /// </summary>
    public DetectHead DetectSubHead => detect;

    public override SegmentOutput forward(Tensor[] feats)
    {
        // Generate prototypes from P3 (first feature level)
        var p = proto.forward(feats[0]); // (B, nm, H_proto, W_proto)
        long bs = p.shape[0];

        // Compute mask coefficients per level and concatenate
        var mcParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var mc_i = cv4[i].forward(feats[i]); // (B, nm, H_i, W_i)
            mcParts[i] = mc_i.view(bs, NumMasks, -1);   // (B, nm, H_i*W_i)
        }
        var mc = torch.cat(mcParts, dim: 2); // (B, nm, totalAnchors)

        // Run detection forward
        var (boxes, scores, rawFeats) = detect.forward(feats);

        return new SegmentOutput(boxes, scores, rawFeats, mc, p);
    }

    /// <summary>
    /// Training forward — returns raw predictions for loss computation.
    /// Returns (rawBox, rawCls, featureSizes, maskCoeffs, protos).
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes, Tensor maskCoeffs, Tensor protos)
        ForwardTrain(Tensor[] feats)
    {
        var p = proto.forward(feats[0]);
        long bs = p.shape[0];

        var mcParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var mc_i = cv4[i].forward(feats[i]);
            mcParts[i] = mc_i.view(bs, NumMasks, -1);
        }
        var mc = torch.cat(mcParts, dim: 2);

        var (rawBox, rawCls, featureSizes) = detect.ForwardTrain(feats);
        return (rawBox, rawCls, featureSizes, mc, p);
    }
}
