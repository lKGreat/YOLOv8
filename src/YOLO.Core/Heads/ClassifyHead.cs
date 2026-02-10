using TorchSharp;
using TorchSharp.Modules;
using YOLO.Core.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Heads;

/// <summary>
/// YOLOv8 classification head.
/// Matches Python ultralytics Classify class exactly:
///   conv = Conv(c1, 1280, k, s, p, g)
///   pool = AdaptiveAvgPool2d(1)
///   drop = Dropout(p=0.0, inplace=True)
///   linear = Linear(1280, c2)
///
/// Forward:
///   Training: linear(drop(pool(conv(x)).flatten(1)))
///   Inference: softmax of the above
/// </summary>
public class ClassifyHead : Module<Tensor, Tensor>
{
    private const long HiddenDim = 1280; // efficientnet_b0 size, matching Python

    public int NumClasses { get; }

    private readonly ConvBlock conv;
    private readonly AdaptiveAvgPool2d pool;
    private readonly Dropout drop;
    private readonly Linear linear;

    public ClassifyHead(string name, long c1, int c2, long k = 1, long s = 1, long? p = null, long g = 1)
        : base(name)
    {
        NumClasses = c2;

        conv = new ConvBlock("conv", c1, HiddenDim, k: k, s: s, p: p, g: g);
        pool = AdaptiveAvgPool2d(1);
        drop = Dropout(0.0);
        linear = Linear(HiddenDim, c2);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        // If input is a list (from multi-level features), concat along channel dim
        // In C#, caller should pre-concat if needed

        var y = conv.forward(x);
        y = pool.forward(y);
        y = y.flatten(1);
        y = drop.forward(y);
        y = linear.forward(y);

        if (!training)
            y = y.softmax(1);

        return y;
    }
}
