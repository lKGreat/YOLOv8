using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Heads;
using YOLO.Core.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Models;

/// <summary>
/// YOLOv8 segmentation model.
/// Shares the same backbone and neck as YOLOv8Model but replaces
/// DetectHead with SegmentHead (detection + mask prototype + mask coefficients).
///
/// Matches Python SegmentationModel(DetectionModel) structure.
/// </summary>
public class YOLOv8SegModel : Module<Tensor, SegmentHead.SegmentOutput>
{
    public string Version => "v8";
    public string Variant { get; }
    public int NumClasses { get; }
    public int NumMasks { get; }
    public long[] FeatureChannels { get; }
    public long[] Strides => ModelConfig.DetectionStrides;

    // Backbone
    private readonly ConvBlock b0, b1, b3, b5, b7;
    private readonly C2f b2, b4, b6, b8;
    private readonly SPPF b9;

    // Neck
    private readonly Upsample neck_up1, neck_up2;
    private readonly C2f n_c2f1, n_c2f2, n_c2f3, n_c2f4;
    private readonly ConvBlock n_down1, n_down2;

    // Segment Head
    private readonly SegmentHead segHead;

    public SegmentHead Head => segHead;

    public YOLOv8SegModel(string name, int nc = 80, string variant = "n", int nm = 32,
        int npr = 256, Device? device = null) : base(name)
    {
        NumClasses = nc;
        Variant = variant;
        NumMasks = nm;

        if (!ModelConfig.Scales.TryGetValue(variant, out var scale))
            throw new ArgumentException($"Unknown variant '{variant}'. Use n/s/m/l/x.");

        var (depth, width, maxCh) = (scale.Depth, scale.Width, scale.MaxChannels);

        long ch0 = ModelConfig.ScaleWidth(64, width, maxCh);
        long ch1 = ModelConfig.ScaleWidth(128, width, maxCh);
        long ch2 = ModelConfig.ScaleWidth(256, width, maxCh);
        long ch3 = ModelConfig.ScaleWidth(512, width, maxCh);
        long ch4 = ModelConfig.ScaleWidth(1024, width, maxCh);

        FeatureChannels = [ch2, ch3, ch4];

        int d0 = ModelConfig.ScaleDepth(3, depth);
        int d1 = ModelConfig.ScaleDepth(6, depth);
        int d2 = ModelConfig.ScaleDepth(6, depth);
        int d3 = ModelConfig.ScaleDepth(3, depth);
        int nd0 = ModelConfig.ScaleDepth(3, depth);
        int nd1 = ModelConfig.ScaleDepth(3, depth);
        int nd2 = ModelConfig.ScaleDepth(3, depth);

        // Backbone (identical to detect)
        b0 = new ConvBlock("b0", 3, ch0, k: 3, s: 2);
        b1 = new ConvBlock("b1", ch0, ch1, k: 3, s: 2);
        b2 = new C2f("b2", ch1, ch1, n: d0, shortcut: true);
        b3 = new ConvBlock("b3", ch1, ch2, k: 3, s: 2);
        b4 = new C2f("b4", ch2, ch2, n: d1, shortcut: true);
        b5 = new ConvBlock("b5", ch2, ch3, k: 3, s: 2);
        b6 = new C2f("b6", ch3, ch3, n: d2, shortcut: true);
        b7 = new ConvBlock("b7", ch3, ch4, k: 3, s: 2);
        b8 = new C2f("b8", ch4, ch4, n: d3, shortcut: true);
        b9 = new SPPF("b9", ch4, ch4, k: 5);

        // Neck
        neck_up1 = Upsample(scale_factor: [2.0, 2.0], mode: UpsampleMode.Nearest);
        n_c2f1 = new C2f("n_c2f1", ch4 + ch3, ch3, n: nd0, shortcut: false);
        neck_up2 = Upsample(scale_factor: [2.0, 2.0], mode: UpsampleMode.Nearest);
        n_c2f2 = new C2f("n_c2f2", ch3 + ch2, ch2, n: nd1, shortcut: false);
        n_down1 = new ConvBlock("n_down1", ch2, ch2, k: 3, s: 2);
        n_c2f3 = new C2f("n_c2f3", ch2 + ch3, ch3, n: nd2, shortcut: false);
        n_down2 = new ConvBlock("n_down2", ch3, ch3, k: 3, s: 2);
        n_c2f4 = new C2f("n_c2f4", ch3 + ch4, ch4, n: nd2, shortcut: false);

        // Segment head: uses P3 channel for Proto
        // Python Segment.proto uses ch[0] = smallest output from neck = ch2
        segHead = new SegmentHead("segment", nc,
            channelsPerLevel: [ch2, ch3, ch4],
            strides: ModelConfig.DetectionStrides,
            regMax: 16, nm: nm, npr: npr);

        RegisterComponents();

        if (device is not null && device.type != DeviceType.CPU)
            this.to(device);
    }

    private (Tensor fpn_p3, Tensor pan_p4, Tensor pan_p5) ForwardBackboneNeck(Tensor x)
    {
        var p1 = b0.forward(x);
        var p2 = b1.forward(p1);
        p2 = b2.forward(p2);
        var p3 = b3.forward(p2);
        p3 = b4.forward(p3);
        var p4 = b5.forward(p3);
        p4 = b6.forward(p4);
        var p5 = b7.forward(p4);
        p5 = b8.forward(p5);
        p5 = b9.forward(p5);

        var up1 = neck_up1.forward(p5);
        var cat1 = torch.cat([up1, p4], dim: 1);
        var fpn_p4 = n_c2f1.forward(cat1);
        var up2 = neck_up2.forward(fpn_p4);
        var cat2 = torch.cat([up2, p3], dim: 1);
        var fpn_p3 = n_c2f2.forward(cat2);

        var down1 = n_down1.forward(fpn_p3);
        var cat3 = torch.cat([down1, fpn_p4], dim: 1);
        var pan_p4 = n_c2f3.forward(cat3);
        var down2 = n_down2.forward(pan_p4);
        var cat4 = torch.cat([down2, p5], dim: 1);
        var pan_p5 = n_c2f4.forward(cat4);

        return (fpn_p3, pan_p4, pan_p5);
    }

    public override SegmentHead.SegmentOutput forward(Tensor x)
    {
        var (fpn_p3, pan_p4, pan_p5) = ForwardBackboneNeck(x);
        return segHead.forward([fpn_p3, pan_p4, pan_p5]);
    }

    /// <summary>
    /// Training forward returning all raw outputs for loss computation.
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes, Tensor maskCoeffs, Tensor protos)
        ForwardTrain(Tensor x)
    {
        var (fpn_p3, pan_p4, pan_p5) = ForwardBackboneNeck(x);
        return segHead.ForwardTrain([fpn_p3, pan_p4, pan_p5]);
    }
}
