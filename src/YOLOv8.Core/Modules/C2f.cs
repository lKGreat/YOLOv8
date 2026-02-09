using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// C2f: Faster implementation of CSP Bottleneck with 2 convolutions.
/// This is the core building block of YOLOv8, replacing C3 from YOLOv5.
///
/// Structure:
///   Input(c1) -> Conv1x1(c1, 2*c) -> chunk(2)
///     branch0(c) + branch1(c) -> n Bottleneck blocks (each outputs c)
///   Concat(n+2 branches) -> Conv1x1((n+2)*c, c2)
/// </summary>
public class C2f : Module<Tensor, Tensor>
{
    private readonly ConvBlock cv1;
    private readonly ConvBlock cv2;
    private readonly Bottleneck[] m;
    private readonly int c; // hidden channels

    /// <summary>
    /// Creates a C2f block.
    /// </summary>
    /// <param name="c1">Input channels</param>
    /// <param name="c2">Output channels</param>
    /// <param name="n">Number of Bottleneck blocks</param>
    /// <param name="shortcut">Whether bottlenecks use residual connections</param>
    /// <param name="g">Groups for bottleneck convolutions</param>
    /// <param name="e">Expansion ratio (hidden channels = c2 * e)</param>
    public C2f(string name, long c1, long c2, int n = 1, bool shortcut = false,
        long g = 1, double e = 0.5)
        : base(name)
    {
        c = (int)(c2 * e);
        cv1 = new ConvBlock($"{name}_cv1", c1, 2 * c, k: 1, s: 1);
        cv2 = new ConvBlock($"{name}_cv2", (2 + n) * c, c2, k: 1, s: 1);

        m = new Bottleneck[n];
        for (int i = 0; i < n; i++)
        {
            m[i] = new Bottleneck($"{name}_m{i}", c, c, shortcut, g, e: 1.0);
        }

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        // cv1 produces 2*c channels, split into two branches
        var y = cv1.forward(x);
        var chunks = y.chunk(2, dim: 1);

        // Collect all branches: chunk0, chunk1, and each bottleneck output
        var branches = new List<Tensor> { chunks[0], chunks[1] };

        var current = chunks[1];
        foreach (var bn in m)
        {
            current = bn.forward(current);
            branches.Add(current);
        }

        // Concatenate all branches and reduce with cv2
        var catted = torch.cat(branches.ToArray(), dim: 1);
        return cv2.forward(catted);
    }
}
