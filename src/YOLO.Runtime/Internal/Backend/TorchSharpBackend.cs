using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Core.Utils;
using static TorchSharp.torch;

namespace YOLO.Runtime.Internal.Backend;

/// <summary>
/// TorchSharp inference backend for .pt model files.
/// Uses YOLO.Core model definitions and WeightLoader.
/// </summary>
internal sealed class TorchSharpBackend : IInferenceBackend
{
    private readonly YOLOModel _model;
    private readonly Device _device;
    private bool _disposed;

    public string ModelPath { get; }
    public string BackendName => "TorchSharp";

    public TorchSharpBackend(string modelPath, YoloOptions options)
    {
        ModelPath = modelPath;

        // Determine device
        _device = options.Device switch
        {
            DeviceType.Gpu => torch.cuda.is_available() ? torch.CUDA : torch.CPU,
            DeviceType.Cpu => torch.CPU,
            DeviceType.Auto => torch.cuda.is_available() ? torch.CUDA : torch.CPU,
            _ => torch.CPU
        };

        // Determine model version and variant
        var version = options.ModelVersion ?? "v8";
        var variant = options.ModelVariant;
        var nc = options.NumClasses;

        // Ensure the model version is registered
        if (!ModelRegistry.IsRegistered(version))
        {
            // Try to load v8 by default
            ModelRegistry.EnsureLoaded(typeof(YOLO.Core.Models.YOLOv8Model));
        }

        // Create model and load weights
        _model = ModelRegistry.Create(version, nc, variant, _device);
        WeightLoader.SmartLoad(_model, modelPath);
        _model.eval();

        if (_device.type != TorchSharp.DeviceType.CPU)
        {
            _model.to(_device);
        }
    }

    public (float[] data, int[] shape) Run(ReadOnlySpan<float> input, int[] inputShape)
    {
        using var scope = torch.NewDisposeScope();
        using var noGrad = torch.no_grad();

        // Build input tensor: inputShape is [1, 3, H, W]
        var inputArray = input.ToArray();
        var longShape = Array.ConvertAll(inputShape, i => (long)i);
        var inputTensor = torch.tensor(inputArray, dtype: ScalarType.Float32)
            .reshape(longShape)
            .to(_device);

        // Run forward pass
        var (boxes, scores, _) = _model.forward(inputTensor);

        // Combine boxes and scores into a single output: (B, 4+nc, N)
        // boxes: (B, 4, N), scores: (B, nc, N)
        var combined = torch.cat([boxes, scores], dim: 1).cpu();

        var outputShape = combined.shape.Select(s => (int)s).ToArray();
        var outputData = combined.data<float>().ToArray();

        return (outputData, outputShape);
    }

    public Task<(float[] data, int[] shape)> RunAsync(float[] input, int[] inputShape, CancellationToken ct = default)
    {
        return Task.Run(() => Run(input.AsSpan(), inputShape), ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _model.Dispose();
    }
}
