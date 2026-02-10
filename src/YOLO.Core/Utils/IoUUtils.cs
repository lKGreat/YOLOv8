using TorchSharp;
using static TorchSharp.torch;

namespace YOLO.Core.Utils;

/// <summary>
/// Intersection over Union (IoU) utilities.
/// Supports standard IoU, GIoU, DIoU, and CIoU calculations.
/// </summary>
public static class IoUUtils
{
    /// <summary>
    /// Compute IoU between two sets of bounding boxes.
    /// </summary>
    /// <param name="box1">Boxes set 1 (..., 4) in xyxy format</param>
    /// <param name="box2">Boxes set 2 (..., 4) in xyxy format</param>
    /// <param name="eps">Small value to avoid division by zero</param>
    /// <returns>IoU tensor</returns>
    public static Tensor BoxIoU(Tensor box1, Tensor box2, double eps = 1e-7)
    {
        // box1, box2: (..., 4) as x1,y1,x2,y2
        var b1_x1 = box1[.., 0];
        var b1_y1 = box1[.., 1];
        var b1_x2 = box1[.., 2];
        var b1_y2 = box1[.., 3];

        var b2_x1 = box2[.., 0];
        var b2_y1 = box2[.., 1];
        var b2_x2 = box2[.., 2];
        var b2_y2 = box2[.., 3];

        var inter_x1 = torch.max(b1_x1, b2_x1);
        var inter_y1 = torch.max(b1_y1, b2_y1);
        var inter_x2 = torch.min(b1_x2, b2_x2);
        var inter_y2 = torch.min(b1_y2, b2_y2);

        var inter = (inter_x2 - inter_x1).clamp_min(0) * (inter_y2 - inter_y1).clamp_min(0);

        var area1 = (b1_x2 - b1_x1) * (b1_y2 - b1_y1);
        var area2 = (b2_x2 - b2_x1) * (b2_y2 - b2_y1);
        var union = area1 + area2 - inter + eps;

        return inter / union;
    }

    /// <summary>
    /// Compute IoU matrix between two sets of boxes.
    /// </summary>
    /// <param name="box1">(N, 4) xyxy</param>
    /// <param name="box2">(M, 4) xyxy</param>
    /// <returns>(N, M) IoU matrix</returns>
    public static Tensor BatchBoxIoU(Tensor box1, Tensor box2, double eps = 1e-7)
    {
        // box1: (N, 4), box2: (M, 4)
        // Split into coordinate components first
        var b1 = box1.unsqueeze(1).chunk(4, dim: -1); // 4 x (N, 1, 1)
        var b2 = box2.unsqueeze(0).chunk(4, dim: -1); // 4 x (1, M, 1)

        var inter_x1 = torch.max(b1[0], b2[0]);
        var inter_y1 = torch.max(b1[1], b2[1]);
        var inter_x2 = torch.min(b1[2], b2[2]);
        var inter_y2 = torch.min(b1[3], b2[3]);

        var inter = (inter_x2 - inter_x1).clamp_min(0) * (inter_y2 - inter_y1).clamp_min(0);

        var area1 = (b1[2] - b1[0]) * (b1[3] - b1[1]);
        var area2 = (b2[2] - b2[0]) * (b2[3] - b2[1]);

        return (inter / (area1 + area2 - inter + eps)).squeeze(-1);
    }

    /// <summary>
    /// Compute Complete IoU (CIoU) between matched box pairs.
    /// CIoU = IoU - (rho^2 / c^2) - alpha*v
    /// where:
    ///   rho = center distance, c = diagonal of enclosing box,
    ///   v = aspect ratio consistency, alpha = v / (1 - IoU + v)
    /// </summary>
    /// <param name="box1">Predicted boxes (..., 4) in xyxy format</param>
    /// <param name="box2">Target boxes (..., 4) in xyxy format</param>
    /// <param name="eps">Small value to avoid division by zero</param>
    /// <returns>CIoU values</returns>
    public static Tensor CIoU(Tensor box1, Tensor box2, double eps = 1e-7)
    {
        var b1_x1 = box1[.., 0]; var b1_y1 = box1[.., 1];
        var b1_x2 = box1[.., 2]; var b1_y2 = box1[.., 3];
        var b2_x1 = box2[.., 0]; var b2_y1 = box2[.., 1];
        var b2_x2 = box2[.., 2]; var b2_y2 = box2[.., 3];

        // Intersection
        var inter_x1 = torch.max(b1_x1, b2_x1);
        var inter_y1 = torch.max(b1_y1, b2_y1);
        var inter_x2 = torch.min(b1_x2, b2_x2);
        var inter_y2 = torch.min(b1_y2, b2_y2);
        var inter = (inter_x2 - inter_x1).clamp_min(0) * (inter_y2 - inter_y1).clamp_min(0);

        // Union
        var w1 = b1_x2 - b1_x1; var h1 = b1_y2 - b1_y1;
        var w2 = b2_x2 - b2_x1; var h2 = b2_y2 - b2_y1;
        var area1 = w1 * h1;
        var area2 = w2 * h2;
        var union = area1 + area2 - inter + eps;
        var iou = inter / union;

        // Enclosing box
        var cw = torch.max(b1_x2, b2_x2) - torch.min(b1_x1, b2_x1);
        var ch = torch.max(b1_y2, b2_y2) - torch.min(b1_y1, b2_y1);

        // Diagonal squared of enclosing box
        var c2 = cw.pow(2) + ch.pow(2) + eps;

        // Center distance squared
        var cx1 = (b1_x1 + b1_x2) / 2; var cy1 = (b1_y1 + b1_y2) / 2;
        var cx2 = (b2_x1 + b2_x2) / 2; var cy2 = (b2_y1 + b2_y2) / 2;
        var rho2 = (cx1 - cx2).pow(2) + (cy1 - cy2).pow(2);

        // Aspect ratio term
        var v = (4.0 / (Math.PI * Math.PI)) *
            torch.pow(torch.atan(w2 / (h2 + eps)) - torch.atan(w1 / (h1 + eps)), 2);

        // alpha is treated as a constant (no gradient) matching Python ultralytics
        Tensor alpha_val;
        using (torch.no_grad())
        {
            alpha_val = v / (1.0 - iou + v + eps);
        }
        // Return OUTSIDE no_grad so gradients flow through iou, rho2/c2, and v
        return iou - rho2 / c2 - alpha_val * v;
    }
}
