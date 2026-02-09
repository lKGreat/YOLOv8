using TorchSharp;
using TorchSharp.Modules;
using YOLOv8.Core.Heads;
using YOLOv8.Core.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Models;

/// <summary>
/// Complete YOLOv8 detection model.
///
/// Architecture:
///   Backbone (CSPDarknet):
///     0: Conv(3→ch0, k=3, s=2)           P1/2
///     1: Conv(ch0→ch1, k=3, s=2)          P2/4
///     2: C2f(ch1, n=d0, shortcut=True)     P2
///     3: Conv(ch1→ch2, k=3, s=2)          P3/8
///     4: C2f(ch2, n=d1, shortcut=True)     P3
///     5: Conv(ch2→ch3, k=3, s=2)          P4/16
///     6: C2f(ch3, n=d2, shortcut=True)     P4
///     7: Conv(ch3→ch4, k=3, s=2)          P5/32
///     8: C2f(ch4, n=d3, shortcut=True)     P5
///     9: SPPF(ch4, k=5)                    P5
///
///   Neck (PANet):
///     10: Upsample(2x)
///     11: Concat(P4)
///     12: C2f(ch3, n=nd0)                 FPN P4
///     13: Upsample(2x)
///     14: Concat(P3)
///     15: C2f(ch2, n=nd1)                 FPN P3 → small objects
///     16: Conv(ch2→ch2, k=3, s=2)         PAN downsample
///     17: Concat(FPN P4)
///     18: C2f(ch3, n=nd2)                 PAN P4 → medium objects
///     19: Conv(ch3→ch3, k=3, s=2)         PAN downsample
///     20: Concat(SPPF out)
///     21: C2f(ch4, n=nd2)                 PAN P5 → large objects
///
///   Head:
///     22: Detect(P3_out, P4_out, P5_out)
/// </summary>
public class YOLOv8Model : Module<Tensor, (Tensor boxes, Tensor scores, Tensor[] rawFeats)>
{
    // Backbone layers
    private readonly ConvBlock backbone0;
    private readonly ConvBlock backbone1;
    private readonly C2f backbone2;
    private readonly ConvBlock backbone3;
    private readonly C2f backbone4;
    private readonly ConvBlock backbone5;
    private readonly C2f backbone6;
    private readonly ConvBlock backbone7;
    private readonly C2f backbone8;
    private readonly SPPF backbone9;

    // Neck layers
    private readonly Upsample neck_up1;
    private readonly C2f neck_c2f1;
    private readonly Upsample neck_up2;
    private readonly C2f neck_c2f2;
    private readonly ConvBlock neck_down1;
    private readonly C2f neck_c2f3;
    private readonly ConvBlock neck_down2;
    private readonly C2f neck_c2f4;

    // Detection head
    private readonly DetectHead detect;

    // Scaled channel sizes for external access
    public long Ch2 { get; }
    public long Ch3 { get; }
    public long Ch4 { get; }
    public int NumClasses { get; }
    public string Variant { get; }

    public DetectHead Head => detect;

    public YOLOv8Model(string name, int nc = 80, string variant = "n", Device? device = null)
        : base(name)
    {
        NumClasses = nc;
        Variant = variant;

        if (!ModelConfig.Scales.TryGetValue(variant, out var scale))
            throw new ArgumentException($"Unknown variant '{variant}'. Use n/s/m/l/x.");

        var (depth, width, maxCh) = (scale.Depth, scale.Width, scale.MaxChannels);

        // Scale channels
        long ch0 = ModelConfig.ScaleWidth(64, width, maxCh);
        long ch1 = ModelConfig.ScaleWidth(128, width, maxCh);
        long ch2 = ModelConfig.ScaleWidth(256, width, maxCh);
        long ch3 = ModelConfig.ScaleWidth(512, width, maxCh);
        long ch4 = ModelConfig.ScaleWidth(1024, width, maxCh);

        Ch2 = ch2; Ch3 = ch3; Ch4 = ch4;

        // Scale depths
        int d0 = ModelConfig.ScaleDepth(3, depth);
        int d1 = ModelConfig.ScaleDepth(6, depth);
        int d2 = ModelConfig.ScaleDepth(6, depth);
        int d3 = ModelConfig.ScaleDepth(3, depth);
        int nd0 = ModelConfig.ScaleDepth(3, depth);
        int nd1 = ModelConfig.ScaleDepth(3, depth);
        int nd2 = ModelConfig.ScaleDepth(3, depth);

        // === Backbone ===
        backbone0 = new ConvBlock("b0", 3, ch0, k: 3, s: 2);           // P1/2
        backbone1 = new ConvBlock("b1", ch0, ch1, k: 3, s: 2);         // P2/4
        backbone2 = new C2f("b2", ch1, ch1, n: d0, shortcut: true);    // P2
        backbone3 = new ConvBlock("b3", ch1, ch2, k: 3, s: 2);         // P3/8
        backbone4 = new C2f("b4", ch2, ch2, n: d1, shortcut: true);    // P3
        backbone5 = new ConvBlock("b5", ch2, ch3, k: 3, s: 2);         // P4/16
        backbone6 = new C2f("b6", ch3, ch3, n: d2, shortcut: true);    // P4
        backbone7 = new ConvBlock("b7", ch3, ch4, k: 3, s: 2);         // P5/32
        backbone8 = new C2f("b8", ch4, ch4, n: d3, shortcut: true);    // P5
        backbone9 = new SPPF("b9", ch4, ch4, k: 5);                    // SPPF

        // === Neck (PANet FPN) ===
        neck_up1 = Upsample(scale_factor: [2.0, 2.0], mode: UpsampleMode.Nearest);
        // After upsample P5 + concat P4: ch4 + ch3 channels
        neck_c2f1 = new C2f("n_c2f1", ch4 + ch3, ch3, n: nd0, shortcut: false);

        neck_up2 = Upsample(scale_factor: [2.0, 2.0], mode: UpsampleMode.Nearest);
        // After upsample + concat P3: ch3 + ch2 channels
        neck_c2f2 = new C2f("n_c2f2", ch3 + ch2, ch2, n: nd1, shortcut: false);

        // PAN bottom-up
        neck_down1 = new ConvBlock("n_down1", ch2, ch2, k: 3, s: 2);
        // After downsample + concat FPN P4: ch2 + ch3 channels
        neck_c2f3 = new C2f("n_c2f3", ch2 + ch3, ch3, n: nd2, shortcut: false);

        neck_down2 = new ConvBlock("n_down2", ch3, ch3, k: 3, s: 2);
        // After downsample + concat SPPF: ch3 + ch4 channels
        neck_c2f4 = new C2f("n_c2f4", ch3 + ch4, ch4, n: nd2, shortcut: false);

        // === Detection Head ===
        detect = new DetectHead("detect", nc,
            channelsPerLevel: [ch2, ch3, ch4],
            strides: ModelConfig.DetectionStrides,
            regMax: 16);

        RegisterComponents();

        if (device is not null && device.type != DeviceType.CPU)
            this.to(device);
    }

    public override (Tensor boxes, Tensor scores, Tensor[] rawFeats) forward(Tensor x)
    {
        // === Backbone ===
        var p1 = backbone0.forward(x);
        var p2 = backbone1.forward(p1);
        p2 = backbone2.forward(p2);
        var p3 = backbone3.forward(p2);
        p3 = backbone4.forward(p3);       // P3 output for skip connection
        var p4 = backbone5.forward(p3);
        p4 = backbone6.forward(p4);       // P4 output for skip connection
        var p5 = backbone7.forward(p4);
        p5 = backbone8.forward(p5);
        p5 = backbone9.forward(p5);       // SPPF output (P5)

        // === Neck: Top-down (FPN) ===
        var up1 = neck_up1.forward(p5);
        var cat1 = torch.cat([up1, p4], dim: 1);
        var fpn_p4 = neck_c2f1.forward(cat1);

        var up2 = neck_up2.forward(fpn_p4);
        var cat2 = torch.cat([up2, p3], dim: 1);
        var fpn_p3 = neck_c2f2.forward(cat2);  // Small object features

        // === Neck: Bottom-up (PAN) ===
        var down1 = neck_down1.forward(fpn_p3);
        var cat3 = torch.cat([down1, fpn_p4], dim: 1);
        var pan_p4 = neck_c2f3.forward(cat3);  // Medium object features

        var down2 = neck_down2.forward(pan_p4);
        var cat4 = torch.cat([down2, p5], dim: 1);
        var pan_p5 = neck_c2f4.forward(cat4);  // Large object features

        // === Detection Head ===
        return detect.forward([fpn_p3, pan_p4, pan_p5]);
    }

    /// <summary>
    /// Forward pass for training that returns raw predictions needed for loss.
    /// </summary>
    public (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes) ForwardTrain(Tensor x)
    {
        // === Backbone ===
        var p1 = backbone0.forward(x);
        var p2 = backbone1.forward(p1);
        p2 = backbone2.forward(p2);
        var p3 = backbone3.forward(p2);
        p3 = backbone4.forward(p3);
        var p4 = backbone5.forward(p3);
        p4 = backbone6.forward(p4);
        var p5 = backbone7.forward(p4);
        p5 = backbone8.forward(p5);
        p5 = backbone9.forward(p5);

        // === Neck ===
        var up1 = neck_up1.forward(p5);
        var cat1 = torch.cat([up1, p4], dim: 1);
        var fpn_p4 = neck_c2f1.forward(cat1);

        var up2 = neck_up2.forward(fpn_p4);
        var cat2 = torch.cat([up2, p3], dim: 1);
        var fpn_p3 = neck_c2f2.forward(cat2);

        var down1 = neck_down1.forward(fpn_p3);
        var cat3 = torch.cat([down1, fpn_p4], dim: 1);
        var pan_p4 = neck_c2f3.forward(cat3);

        var down2 = neck_down2.forward(pan_p4);
        var cat4 = torch.cat([down2, p5], dim: 1);
        var pan_p5 = neck_c2f4.forward(cat4);

        return detect.ForwardTrain([fpn_p3, pan_p4, pan_p5]);
    }
}
