using TorchSharp;
using YOLO.Core.Heads;
using YOLO.Core.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Models;

/// <summary>
/// YOLOv8 classification model.
/// Uses the same backbone as detection but replaces neck+head with a ClassifyHead.
///
/// Matches Python ClassificationModel(BaseModel) structure:
///   - Backbone: same CSPDarknet
///   - Head: Classify(c1=ch4, c2=nc) — Conv(c1,1280) + AvgPool + Dropout + Linear(1280,nc)
///   - Stride is always [1] (no spatial detection)
///
/// Unlike Python which builds from YAML, we hardcode the architecture like YOLOv8Model.
/// </summary>
public class YOLOv8ClsModel : Module<Tensor, Tensor>
{
    public string Version => "v8";
    public string Variant { get; }
    public int NumClasses { get; }

    // Backbone
    private readonly ConvBlock b0, b1, b3, b5, b7;
    private readonly C2f b2, b4, b6, b8;
    private readonly SPPF b9;

    // Classification Head
    private readonly ClassifyHead classifyHead;

    public ClassifyHead Head => classifyHead;

    public YOLOv8ClsModel(string name, int nc = 1000, string variant = "n", Device? device = null)
        : base(name)
    {
        NumClasses = nc;
        Variant = variant;

        if (!ModelConfig.Scales.TryGetValue(variant, out var scale))
            throw new ArgumentException($"Unknown variant '{variant}'. Use n/s/m/l/x.");

        var (depth, width, maxCh) = (scale.Depth, scale.Width, scale.MaxChannels);

        long ch0 = ModelConfig.ScaleWidth(64, width, maxCh);
        long ch1 = ModelConfig.ScaleWidth(128, width, maxCh);
        long ch2 = ModelConfig.ScaleWidth(256, width, maxCh);
        long ch3 = ModelConfig.ScaleWidth(512, width, maxCh);
        long ch4 = ModelConfig.ScaleWidth(1024, width, maxCh);

        int d0 = ModelConfig.ScaleDepth(3, depth);
        int d1 = ModelConfig.ScaleDepth(6, depth);
        int d2 = ModelConfig.ScaleDepth(6, depth);
        int d3 = ModelConfig.ScaleDepth(3, depth);

        // Backbone (no neck for classification)
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

        // Classify head takes the last backbone output channel
        classifyHead = new ClassifyHead("classify", ch4, nc);

        RegisterComponents();

        if (device is not null && device.type != DeviceType.CPU)
            this.to(device);
    }

    public override Tensor forward(Tensor x)
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

        return classifyHead.forward(p5);
    }
}
