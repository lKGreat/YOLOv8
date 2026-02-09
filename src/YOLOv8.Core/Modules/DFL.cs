using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// Distribution Focal Loss (DFL) integral module.
/// Converts distribution predictions over reg_max bins into scalar distance values.
///
/// Uses a fixed 1x1 convolution with weights [0, 1, 2, ..., reg_max-1]
/// to compute the expected value of the softmax distribution.
///
/// Input:  (B, 4 * reg_max, N) where N = total anchors
/// Output: (B, 4, N)
/// </summary>
public class DFL : Module<Tensor, Tensor>
{
    private readonly Conv2d conv;
    private readonly int regMax;

    public int RegMax => regMax;

    public DFL(string name, int regMax = 16) : base(name)
    {
        this.regMax = regMax;

        // 1x1 conv with fixed weights [0, 1, 2, ..., regMax-1], no gradient
        conv = Conv2d(regMax, 1, 1, bias: false);
        conv.weight!.requires_grad_(false);

        var weight = torch.arange(regMax, dtype: ScalarType.Float32)
            .view(1, regMax, 1, 1);
        conv.weight.copy_(weight);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        // x shape: (B, channels, anchors) where channels = 4 * reg_max
        var (b, c, a) = (x.shape[0], x.shape[1], x.shape[2]);

        // Reshape to (B, 4, reg_max, anchors) -> transpose to (B, reg_max, 4, anchors)
        // Apply softmax over reg_max dimension, then weighted sum via conv
        var reshaped = x.view(b, 4, regMax, a)
            .transpose(2, 1)     // (B, reg_max, 4, anchors)
            .softmax(1);         // softmax over reg_max dim

        var result = conv.forward(reshaped); // (B, 1, 4, anchors)
        return result.view(b, 4, a);         // (B, 4, anchors)
    }
}
