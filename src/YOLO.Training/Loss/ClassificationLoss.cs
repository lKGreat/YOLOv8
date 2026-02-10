using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace YOLO.Training.Loss;

/// <summary>
/// YOLOv8 Classification Loss.
/// Matches Python v8ClassificationLoss exactly:
///   loss = cross_entropy(preds, batch['cls'], reduction='sum') / 64
///
/// The divisor of 64 is hardcoded in ultralytics to normalize per-image loss
/// (assuming ~64 images nominal, but it's a fixed constant).
/// </summary>
public class ClassificationLoss
{
    public string[] LossNames => ["cls"];

    /// <summary>
    /// Compute classification loss.
    /// </summary>
    /// <param name="predictions">Model output logits (B, nc) — raw logits, NOT softmax'd</param>
    /// <param name="targets">Ground truth class indices (B,) as Int64</param>
    /// <returns>Tuple of (totalLoss scalar, lossItems tensor [cls])</returns>
    public (Tensor totalLoss, Tensor lossItems) Compute(Tensor predictions, Tensor targets)
    {
        var loss = functional.cross_entropy(predictions, targets, reduction: Reduction.Sum) / 64.0;
        var lossItems = loss.detach().unsqueeze(0);
        return (loss, lossItems);
    }
}
