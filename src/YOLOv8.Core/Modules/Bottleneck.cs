using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// Standard bottleneck block with two Conv blocks and optional residual connection.
/// Used inside C2f blocks.
/// </summary>
public class Bottleneck : Module<Tensor, Tensor>
{
    private readonly ConvBlock cv1;
    private readonly ConvBlock cv2;
    private readonly bool add;

    /// <summary>
    /// Creates a Bottleneck block.
    /// </summary>
    /// <param name="c1">Input channels</param>
    /// <param name="c2">Output channels</param>
    /// <param name="shortcut">Whether to use residual connection (requires c1==c2)</param>
    /// <param name="g">Groups for second conv</param>
    /// <param name="k1">Kernel size for first conv</param>
    /// <param name="k2">Kernel size for second conv</param>
    /// <param name="e">Expansion ratio for hidden channels</param>
    public Bottleneck(string name, long c1, long c2, bool shortcut = true,
        long g = 1, long k1 = 3, long k2 = 3, double e = 0.5)
        : base(name)
    {
        long hiddenChannels = (long)(c2 * e);
        cv1 = new ConvBlock($"{name}_cv1", c1, hiddenChannels, k: k1, s: 1);
        cv2 = new ConvBlock($"{name}_cv2", hiddenChannels, c2, k: k2, s: 1, g: g);
        add = shortcut && c1 == c2;

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        var y = cv1.forward(x);
        y = cv2.forward(y);
        if (add)
            y = y + x;
        return y;
    }
}
