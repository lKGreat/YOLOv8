using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLOv8.Core.Modules;

/// <summary>
/// Concatenation module along a given dimension.
/// </summary>
public class Concat : Module<Tensor[], Tensor>
{
    private readonly long dimension;

    public Concat(string name, long dimension = 1) : base(name)
    {
        this.dimension = dimension;
        RegisterComponents();
    }

    public override Tensor forward(Tensor[] inputs)
    {
        return torch.cat(inputs, dimension);
    }
}
