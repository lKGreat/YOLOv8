using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Core.Modules;

/// <summary>
/// YOLOv8 mask prototype generation module.
/// Matches Python ultralytics Proto class exactly:
///   cv1 = Conv(c1, c_, k=3)
///   upsample = ConvTranspose2d(c_, c_, 2, 2, 0, bias=True)
///   cv2 = Conv(c_, c_, k=3)
///   cv3 = Conv(c_, c2)
/// Forward: cv3(cv2(upsample(cv1(x))))
/// </summary>
public class Proto : Module<Tensor, Tensor>
{
    private readonly ConvBlock cv1;
    private readonly ConvTranspose2d upsample;
    private readonly ConvBlock cv2;
    private readonly ConvBlock cv3;

    public Proto(string name, long c1, long c_ = 256, long c2 = 32)
        : base(name)
    {
        cv1 = new ConvBlock("cv1", c1, c_, k: 3);
        upsample = ConvTranspose2d(c_, c_, 2, stride: 2, padding: 0, bias: true);
        cv2 = new ConvBlock("cv2", c_, c_, k: 3);
        cv3 = new ConvBlock("cv3", c_, c2);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        return cv3.forward(cv2.forward(upsample.forward(cv1.forward(x))));
    }
}
