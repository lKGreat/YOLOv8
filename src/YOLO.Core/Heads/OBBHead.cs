using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Modules;
using YOLO.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Heads;

/// <summary>
/// YOLOv8 OBB (Oriented Bounding Box) head for rotated object detection.
/// Extends DetectHead with an angle regression branch (cv4).
///
/// Based on official Ultralytics OBB implementation:
///   - cv4: per-level Conv→Conv→Conv2d(ne=1) branches for angle prediction
///   - ne = 1 (single angle value per anchor)
///   - angle is in radians [0, pi/2) for rotation
///
/// Training returns (rawFeats, rawAngle).
/// Inference returns boxes+scores with angle appended.
/// </summary>
public class OBBHead : Module<Tensor[], OBBHead.OBBOutput>
{
    public record struct OBBOutput(
        Tensor Boxes, Tensor Scores, Tensor[] RawFeats, Tensor Angles);

    public int NumClasses { get; }
    public int RegMax { get; }
    public int Ne { get; } = 1; // number of extra channels (angle)
    public int NumDetectionLayers { get; }
    public long[] Strides { get; }

    private readonly DetectHead detect;
    private readonly ModuleList<Sequential> cv4; // angle branches

    public OBBHead(string name, int nc, long[] channelsPerLevel, long[] strides,
        int regMax = 16, int ne = 1)
        : base(name)
    {
        NumClasses = nc;
        RegMax = regMax;
        Ne = ne;
        NumDetectionLayers = channelsPerLevel.Length;
        Strides = strides;

        detect = new DetectHead("detect", nc, channelsPerLevel, strides, regMax);

        cv4 = new ModuleList<Sequential>();
        long c4 = Math.Max(channelsPerLevel[0] / 4, ne);
        for (int i = 0; i < channelsPerLevel.Length; i++)
        {
            long ch = channelsPerLevel[i];
            cv4.Add(Sequential(
                ($"cv4_{i}_0", new ConvBlock($"cv4_{i}_0", ch, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_1", new ConvBlock($"cv4_{i}_1", c4, c4, k: 3) as Module<Tensor, Tensor>),
                ($"cv4_{i}_2", Conv2d(c4, ne, 1) as Module<Tensor, Tensor>)
            ));
        }

        RegisterComponents();
    }

    public DetectHead DetectSubHead => detect;

    public override OBBOutput forward(Tensor[] feats)
    {
        long bs = feats[0].shape[0];

        // Angle branch
        var angleParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var a_i = cv4[i].forward(feats[i]); // (B, ne, H_i, W_i)
            angleParts[i] = a_i.view(bs, Ne, -1);  // (B, ne, H_i*W_i)
        }
        var angles = torch.cat(angleParts, dim: -1); // (B, ne, totalAnchors)

        // Detection forward
        var (boxes, scores, rawFeats) = detect.forward(feats);

        return new OBBOutput(boxes, scores, rawFeats, angles);
    }

    /// <summary>
    /// Training forward — returns raw predictions.
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes, Tensor rawAngle)
        ForwardTrain(Tensor[] feats)
    {
        long bs = feats[0].shape[0];

        var angleParts = new Tensor[feats.Length];
        for (int i = 0; i < feats.Length; i++)
        {
            var a_i = cv4[i].forward(feats[i]);
            angleParts[i] = a_i.view(bs, Ne, -1);
        }
        var rawAngle = torch.cat(angleParts, dim: -1);

        var (rawBox, rawCls, featureSizes) = detect.ForwardTrain(feats);
        return (rawBox, rawCls, featureSizes, rawAngle);
    }
}
