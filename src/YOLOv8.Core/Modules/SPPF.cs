using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// Spatial Pyramid Pooling - Fast (SPPF).
/// Applies 3 sequential MaxPool2d operations and concatenates all results
/// to capture multi-scale spatial information efficiently.
///
/// Structure:
///   Input(c1) -> Conv1x1(c1, c1//2) -> [x, MaxPool(x), MaxPool^2(x), MaxPool^3(x)]
///   -> Concat(4 branches) -> Conv1x1(c1*2, c2)
/// </summary>
public class SPPF : Module<Tensor, Tensor>
{
    private readonly ConvBlock cv1;
    private readonly ConvBlock cv2;
    private readonly MaxPool2d pool;

    /// <summary>
    /// Creates an SPPF block.
    /// </summary>
    /// <param name="c1">Input channels</param>
    /// <param name="c2">Output channels</param>
    /// <param name="k">MaxPool kernel size</param>
    public SPPF(string name, long c1, long c2, long k = 5)
        : base(name)
    {
        long hidden = c1 / 2;
        cv1 = new ConvBlock($"{name}_cv1", c1, hidden, k: 1, s: 1);
        cv2 = new ConvBlock($"{name}_cv2", hidden * 4, c2, k: 1, s: 1);
        pool = MaxPool2d(k, stride: 1, padding: k / 2);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        x = cv1.forward(x);
        var y1 = pool.forward(x);
        var y2 = pool.forward(y1);
        var y3 = pool.forward(y2);
        var catted = torch.cat([x, y1, y2, y3], dim: 1);
        return cv2.forward(catted);
    }
}
