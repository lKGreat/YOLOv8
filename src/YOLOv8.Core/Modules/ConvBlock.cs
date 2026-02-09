using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// Standard Conv + BatchNorm + SiLU block used throughout YOLOv8.
/// Equivalent to ultralytics Conv module.
/// </summary>
public class ConvBlock : Module<Tensor, Tensor>
{
    private readonly Conv2d conv;
    private readonly BatchNorm2d bn;
    private readonly Module<Tensor, Tensor>? act;

    /// <summary>
    /// Computes auto-padding for "same" spatial output when stride=1.
    /// </summary>
    public static long AutoPad(long kernelSize, long dilation = 1)
    {
        return (kernelSize - 1) / 2 * dilation;
    }

    /// <summary>
    /// Creates a Conv + BN + SiLU block.
    /// </summary>
    /// <param name="c1">Input channels</param>
    /// <param name="c2">Output channels</param>
    /// <param name="k">Kernel size</param>
    /// <param name="s">Stride</param>
    /// <param name="p">Padding (-1 for auto)</param>
    /// <param name="g">Groups</param>
    /// <param name="d">Dilation</param>
    /// <param name="useAct">Whether to use activation (true=SiLU, false=Identity)</param>
    public ConvBlock(string name, long c1, long c2, long k = 1, long s = 1,
        long? p = null, long g = 1, long d = 1, bool useAct = true)
        : base(name)
    {
        long padding = p ?? AutoPad(k, d);

        conv = Conv2d(c1, c2, k, stride: s, padding: padding,
            groups: g, dilation: d, bias: false);
        bn = BatchNorm2d(c2, eps: 1e-3, momentum: 0.03);
        act = useAct ? SiLU(inplace: true) : null;

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        var y = conv.forward(x);
        y = bn.forward(y);
        if (act is not null)
            y = act.forward(y);
        return y;
    }
}
