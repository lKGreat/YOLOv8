using TorchSharp;
using static TorchSharp.torch;

namespace YOLO.Core.Abstractions;

/// <summary>
/// Abstract base class for all YOLO detection models (v8, v9, v10, ...).
/// 
/// Since TorchSharp requires Module&lt;TInput, TOutput&gt; inheritance for the forward pass,
/// we use an abstract class rather than a pure interface.
/// 
/// All YOLO model versions must:
///   1. Implement the inference forward pass (boxes + scores)
///   2. Implement the training forward pass (raw predictions for loss)
///   3. Implement the training forward with features (for knowledge distillation)
///   4. Expose metadata (version, variant, num classes, feature channels, strides)
/// </summary>
public abstract class YOLOModel : nn.Module<Tensor, (Tensor boxes, Tensor scores, Tensor[] rawFeats)>
{
    /// <summary>
    /// Model version identifier, e.g. "v8", "v9", "v10".
    /// </summary>
    public abstract string Version { get; }

    /// <summary>
    /// Model variant (size), e.g. "n", "s", "m", "l", "x".
    /// </summary>
    public abstract string Variant { get; }

    /// <summary>
    /// Number of detection classes.
    /// </summary>
    public abstract int NumClasses { get; }

    /// <summary>
    /// Feature channel sizes for multi-scale outputs (e.g. [ch_p3, ch_p4, ch_p5]).
    /// Used for knowledge distillation compatibility across model versions.
    /// </summary>
    public abstract long[] FeatureChannels { get; }

    /// <summary>
    /// Detection strides for each feature level (e.g. [8, 16, 32]).
    /// </summary>
    public abstract long[] Strides { get; }

    protected YOLOModel(string name) : base(name) { }

    /// <summary>
    /// Forward pass for training that returns raw predictions needed for loss computation.
    /// </summary>
    /// <param name="x">Input image tensor (B, 3, H, W)</param>
    /// <returns>
    /// rawBox: (B, 4*reg_max, N) raw box distributions
    /// rawCls: (B, nc, N) raw classification logits
    /// featureSizes: per-level (h, w) array
    /// </returns>
    public abstract (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes)
        ForwardTrain(Tensor x);

    /// <summary>
    /// Forward pass for training that returns raw predictions AND neck feature maps.
    /// Used for knowledge distillation (feature-level matching).
    /// </summary>
    /// <returns>
    /// rawBox: (B, 4*reg_max, N) raw box distributions
    /// rawCls: (B, nc, N) raw classification logits
    /// featureSizes: per-level (h, w) array
    /// neckFeatures: neck output tensors for each feature level
    /// </returns>
    public abstract (Tensor rawBox, Tensor rawCls, (long h, long w)[] featureSizes, Tensor[] neckFeatures)
        ForwardTrainWithFeatures(Tensor x);
}
