using TorchSharp;
using TorchSharp.Modules;
using YOLOv8.Core.Modules;
using YOLOv8.Core.Utils;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Heads;

/// <summary>
/// YOLOv8 decoupled detection head.
/// Each feature map level has separate box regression and classification branches.
///
/// Box branch outputs 4 * reg_max channels (DFL distribution).
/// Class branch outputs nc channels (one per class).
///
/// During inference, DFL converts distributions to distances, which are then
/// converted to bounding boxes via dist2bbox.
/// </summary>
public class DetectHead : Module<Tensor[], (Tensor boxes, Tensor scores, Tensor[] rawFeats)>
{
    public int NumClasses { get; }
    public int RegMax { get; }
    public int NumDetectionLayers { get; }
    public long[] Strides { get; }

    private readonly int numOutputs; // nc + reg_max * 4
    private readonly ModuleList<Sequential> cv2; // box regression branches
    private readonly ModuleList<Sequential> cv3; // classification branches
    private readonly DFL dfl;

    // Cached anchor data (lazily initialized)
    private Tensor? _anchorPoints;
    private Tensor? _strideTensor;

    public DetectHead(string name, int nc, long[] channelsPerLevel, long[] strides, int regMax = 16)
        : base(name)
    {
        NumClasses = nc;
        RegMax = regMax;
        NumDetectionLayers = channelsPerLevel.Length;
        Strides = strides;
        numOutputs = nc + regMax * 4;

        cv2 = new ModuleList<Sequential>();
        cv3 = new ModuleList<Sequential>();

        for (int i = 0; i < channelsPerLevel.Length; i++)
        {
            long ch = channelsPerLevel[i];

            // Box branch: c2 = max(16, ch//4, reg_max * 4)
            long c2 = Math.Max(16, Math.Max(ch / 4, regMax * 4));
            cv2.Add(Sequential(
                ($"cv2_{i}_0", new ConvBlock($"cv2_{i}_0", ch, c2, k: 3, s: 1) as Module<Tensor, Tensor>),
                ($"cv2_{i}_1", new ConvBlock($"cv2_{i}_1", c2, c2, k: 3, s: 1) as Module<Tensor, Tensor>),
                ($"cv2_{i}_2", Conv2d(c2, 4 * regMax, 1) as Module<Tensor, Tensor>)
            ));

            // Class branch: c3 = max(ch, min(nc, 100))
            long c3 = Math.Max(ch, Math.Min(nc, 100));
            cv3.Add(Sequential(
                ($"cv3_{i}_0", new ConvBlock($"cv3_{i}_0", ch, c3, k: 3, s: 1) as Module<Tensor, Tensor>),
                ($"cv3_{i}_1", new ConvBlock($"cv3_{i}_1", c3, c3, k: 3, s: 1) as Module<Tensor, Tensor>),
                ($"cv3_{i}_2", Conv2d(c3, nc, 1) as Module<Tensor, Tensor>)
            ));
        }

        dfl = new DFL($"{name}_dfl", regMax);

        RegisterComponents();
    }

    /// <summary>
    /// Get or create cached anchor points for the given feature map sizes.
    /// </summary>
    private (Tensor anchorPoints, Tensor strideTensor) GetAnchors(
        (long h, long w)[] featureSizes, Device device)
    {
        long totalAnchors = featureSizes.Sum(fs => fs.h * fs.w);

        if (_anchorPoints is null || _anchorPoints.shape[0] != totalAnchors ||
            _anchorPoints.device != device)
        {
            (_anchorPoints, _strideTensor) = BboxUtils.MakeAnchors(featureSizes, Strides, 0.5, device);
        }

        return (_anchorPoints, _strideTensor!);
    }

    public override (Tensor boxes, Tensor scores, Tensor[] rawFeats) forward(Tensor[] feats)
    {
        var rawOutputs = new Tensor[feats.Length];
        var featureSizes = new (long h, long w)[feats.Length];

        for (int i = 0; i < feats.Length; i++)
        {
            var boxOut = cv2[i].forward(feats[i]);   // (B, 4*reg_max, H, W)
            var clsOut = cv3[i].forward(feats[i]);    // (B, nc, H, W)

            var b = boxOut.shape[0];
            var h = boxOut.shape[2];
            var w = boxOut.shape[3];
            featureSizes[i] = (h, w);

            // Flatten spatial dims: (B, C, H*W)
            var boxFlat = boxOut.view(b, 4 * RegMax, h * w);
            var clsFlat = clsOut.view(b, NumClasses, h * w);

            rawOutputs[i] = torch.cat([boxFlat, clsFlat], dim: 1); // (B, 4*reg_max + nc, H*W)
        }

        // Concatenate all levels: (B, 4*reg_max + nc, totalAnchors)
        var xCat = torch.cat(rawOutputs, dim: 2);
        var device = xCat.device;

        // Split box and class predictions
        var splits = xCat.split([4 * RegMax, NumClasses], dim: 1);
        var boxPred = splits[0]; // (B, 4*reg_max, N)
        var clsPred = splits[1]; // (B, nc, N)

        // Decode boxes: DFL -> dist2bbox -> scale by stride
        var (anchorPoints, strideTensor) = GetAnchors(featureSizes, device);

        var distPred = dfl.forward(boxPred); // (B, 4, N)
        var boxes = BboxUtils.Dist2Bbox(distPred, anchorPoints, xywh: true);
        boxes = boxes * strideTensor.T.unsqueeze(0); // scale by stride

        // Sigmoid on class scores
        var scores = clsPred.sigmoid();

        return (boxes, scores, [boxPred]);
    }

    /// <summary>
    /// Forward pass for training - returns raw features needed for loss computation.
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes) ForwardTrain(Tensor[] feats)
    {
        var boxOutputs = new List<Tensor>();
        var clsOutputs = new List<Tensor>();
        var featureSizes = new (long h, long w)[feats.Length];

        for (int i = 0; i < feats.Length; i++)
        {
            var boxOut = cv2[i].forward(feats[i]);   // (B, 4*reg_max, H, W)
            var clsOut = cv3[i].forward(feats[i]);    // (B, nc, H, W)

            var b = boxOut.shape[0];
            var h = boxOut.shape[2];
            var w = boxOut.shape[3];
            featureSizes[i] = (h, w);

            boxOutputs.Add(boxOut.view(b, 4 * RegMax, h * w));
            clsOutputs.Add(clsOut.view(b, NumClasses, h * w));
        }

        var rawBox = torch.cat(boxOutputs.ToArray(), dim: 2); // (B, 4*reg_max, N)
        var rawCls = torch.cat(clsOutputs.ToArray(), dim: 2); // (B, nc, N)

        return (rawBox, rawCls, featureSizes);
    }
}
