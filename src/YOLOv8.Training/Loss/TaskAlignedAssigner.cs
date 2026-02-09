using TorchSharp;
using static TorchSharp.torch;
using YOLOv8.Core.Utils;

namespace YOLOv8.Training.Loss;

/// <summary>
/// Task-Aligned Assigner for anchor-free object detection.
/// Assigns ground truth boxes to anchor points based on alignment metric:
///   metric = pred_score^alpha * IoU^beta
///
/// Selects top-k candidates per GT, resolves conflicts by highest IoU.
/// </summary>
public class TaskAlignedAssigner
{
    private readonly int topK;
    private readonly double alpha;
    private readonly double beta;

    public TaskAlignedAssigner(int topK = 10, double alpha = 0.5, double beta = 6.0)
    {
        this.topK = topK;
        this.alpha = alpha;
        this.beta = beta;
    }

    /// <summary>
    /// Perform label assignment.
    /// </summary>
    /// <param name="predScores">Predicted class scores after sigmoid (B, N, nc)</param>
    /// <param name="predBboxes">Predicted bboxes in xyxy (B, N, 4)</param>
    /// <param name="anchorPoints">Anchor point centers (N, 2)</param>
    /// <param name="gtLabels">Ground truth class labels (B, maxGT, 1)</param>
    /// <param name="gtBboxes">Ground truth bboxes xyxy (B, maxGT, 4)</param>
    /// <param name="maskGT">Valid GT mask (B, maxGT, 1)</param>
    /// <returns>Tuple of (targetLabels, targetBboxes, targetScores, fgMask, targetGTIdx)</returns>
    public (Tensor targetLabels, Tensor targetBboxes, Tensor targetScores,
            Tensor fgMask, Tensor targetGTIdx)
        Assign(Tensor predScores, Tensor predBboxes, Tensor anchorPoints,
               Tensor gtLabels, Tensor gtBboxes, Tensor maskGT)
    {
        var device = predScores.device;
        var dtype = predScores.dtype;
        long batch = predScores.shape[0];
        long nAnchors = predScores.shape[1];
        int nc = (int)predScores.shape[2];
        long maxGT = gtBboxes.shape[1];

        if (maxGT == 0)
        {
            // No ground truth - everything is background
            return (
                torch.zeros(batch, nAnchors, dtype: ScalarType.Int64, device: device),
                torch.zeros(batch, nAnchors, 4, dtype: dtype, device: device),
                torch.zeros(batch, nAnchors, nc, dtype: dtype, device: device),
                torch.zeros(batch, nAnchors, dtype: ScalarType.Bool, device: device),
                torch.zeros(batch, nAnchors, dtype: ScalarType.Int64, device: device)
            );
        }

        // Step 1: Determine which anchors are inside GT boxes
        // anchorPoints: (N, 2), gtBboxes: (B, maxGT, 4)
        var maskInGTs = SelectCandidatesInGTs(anchorPoints, gtBboxes); // (B, maxGT, N)

        // Step 2: Compute alignment metric for all anchor-GT pairs
        // Get predicted scores for each GT's class
        var gtLabelsLong = gtLabels.squeeze(-1).to(ScalarType.Int64); // (B, maxGT)

        // Compute IoU between predictions and GTs: (B, maxGT, N)
        var overlaps = ComputeIoU(predBboxes, gtBboxes); // (B, maxGT, N)

        // Gather pred scores for GT classes: (B, maxGT, N)
        var bboxScores = GatherScoresForGTClasses(predScores, gtLabelsLong); // (B, maxGT, N)

        // Alignment metric = score^alpha * iou^beta
        var alignMetric = bboxScores.pow(alpha) * overlaps.pow(beta); // (B, maxGT, N)

        // Apply masks: only consider anchors inside GTs and valid GTs
        alignMetric = alignMetric * maskInGTs * maskGT; // (B, maxGT, N)

        // Step 3: Select top-k candidates per GT
        var maskTopK = SelectTopKCandidates(alignMetric, topK); // (B, maxGT, N)

        // Final positive mask
        var maskPos = maskTopK * maskInGTs * maskGT; // (B, maxGT, N)

        // Step 4: Resolve conflicts - if one anchor assigned to multiple GTs, pick highest IoU
        var fgMask = maskPos.sum(1).to(ScalarType.Bool); // (B, N)

        // For each anchor, find which GT has highest IoU (if assigned to multiple)
        var targetGTIdx = maskPos.to(ScalarType.Float32).argmax(1); // (B, N)

        // Gather target bboxes and labels
        var targetLabels = GatherFromDim1(gtLabelsLong, targetGTIdx); // (B, N)
        var targetBboxes2 = GatherBboxes(gtBboxes, targetGTIdx); // (B, N, 4)

        // Zero out background
        targetLabels = targetLabels * fgMask.to(ScalarType.Int64);

        // Compute target scores (soft labels based on normalized alignment metric)
        var targetScores = torch.zeros(batch, nAnchors, nc, dtype: dtype, device: device);

        // Normalize alignment metric: divide by max per GT
        var alignMax = alignMetric.amax(new long[] { -1 }, keepdim: true).clamp_min(1e-8); // (B, maxGT, 1)
        var normAlignMetric = (alignMetric / alignMax * maskPos).amax(1); // (B, N)

        // Scatter normalized metric to target class channels
        var fgIdx = fgMask.nonzero(); // (numFG, 2) - [batch_idx, anchor_idx]
        if (fgIdx.shape[0] > 0)
        {
            for (long i = 0; i < fgIdx.shape[0]; i++)
            {
                var bi = fgIdx[i, 0].item<long>();
                var ai = fgIdx[i, 1].item<long>();
                var cls = targetLabels[bi, ai].item<long>();
                var score = normAlignMetric[bi, ai];
                targetScores[bi, ai, cls] = score;
            }
        }

        return (targetLabels, targetBboxes2, targetScores, fgMask, targetGTIdx);
    }

    /// <summary>
    /// Check which anchor points fall inside GT bounding boxes.
    /// </summary>
    private Tensor SelectCandidatesInGTs(Tensor anchorPoints, Tensor gtBboxes)
    {
        // anchorPoints: (N, 2), gtBboxes: (B, maxGT, 4) in xyxy
        long nAnchors = anchorPoints.shape[0];
        long batch = gtBboxes.shape[0];
        long maxGT = gtBboxes.shape[1];

        var points = anchorPoints.unsqueeze(0).unsqueeze(1); // (1, 1, N, 2)
        var boxes = gtBboxes.unsqueeze(2); // (B, maxGT, 1, 4)

        var lt = points - boxes[.., .., .., ..2]; // (B, maxGT, N, 2)
        var rb = boxes[.., .., .., 2..] - points; // (B, maxGT, N, 2)

        var deltas = torch.cat([lt, rb], dim: -1); // (B, maxGT, N, 4)
        var inside = deltas.amin(-1) > 0; // (B, maxGT, N) - all 4 deltas > 0

        return inside.to(ScalarType.Float32);
    }

    /// <summary>
    /// Compute IoU between each prediction and each GT box.
    /// </summary>
    private Tensor ComputeIoU(Tensor predBboxes, Tensor gtBboxes)
    {
        // predBboxes: (B, N, 4), gtBboxes: (B, maxGT, 4) both in xyxy
        long batch = predBboxes.shape[0];
        long nAnchors = predBboxes.shape[1];
        long maxGT = gtBboxes.shape[1];

        var pred = predBboxes.unsqueeze(1); // (B, 1, N, 4)
        var gt = gtBboxes.unsqueeze(2);     // (B, maxGT, 1, 4)

        var inter_x1 = torch.max(pred[.., .., .., 0], gt[.., .., .., 0]);
        var inter_y1 = torch.max(pred[.., .., .., 1], gt[.., .., .., 1]);
        var inter_x2 = torch.min(pred[.., .., .., 2], gt[.., .., .., 2]);
        var inter_y2 = torch.min(pred[.., .., .., 3], gt[.., .., .., 3]);

        var inter = (inter_x2 - inter_x1).clamp_min(0) * (inter_y2 - inter_y1).clamp_min(0);

        var area1 = (pred[.., .., .., 2] - pred[.., .., .., 0]) *
                     (pred[.., .., .., 3] - pred[.., .., .., 1]);
        var area2 = (gt[.., .., .., 2] - gt[.., .., .., 0]) *
                     (gt[.., .., .., 3] - gt[.., .., .., 1]);

        return inter / (area1 + area2 - inter + 1e-7); // (B, maxGT, N)
    }

    /// <summary>
    /// Gather predicted scores for each GT's class.
    /// </summary>
    private Tensor GatherScoresForGTClasses(Tensor predScores, Tensor gtLabels)
    {
        // predScores: (B, N, nc), gtLabels: (B, maxGT) as class indices
        long batch = predScores.shape[0];
        long nAnchors = predScores.shape[1];
        long maxGT = gtLabels.shape[1];

        // Expand gt labels to index into pred scores
        var idx = gtLabels.unsqueeze(2).expand(batch, maxGT, nAnchors); // (B, maxGT, N)

        // Permute predScores to (B, nc, N) then gather along nc dim
        var scores = predScores.permute(0, 2, 1); // (B, nc, N)

        // For each GT, gather the score of its class across all anchors
        var result = torch.zeros(batch, maxGT, nAnchors,
            dtype: predScores.dtype, device: predScores.device);

        for (long b = 0; b < batch; b++)
        {
            for (long g = 0; g < maxGT; g++)
            {
                var clsIdx = gtLabels[b, g].item<long>();
                if (clsIdx >= 0 && clsIdx < predScores.shape[2])
                    result[b, g] = predScores[b, .., clsIdx]; // (N,)
            }
        }

        return result;
    }

    /// <summary>
    /// Select top-k candidates per GT based on alignment metric.
    /// </summary>
    private Tensor SelectTopKCandidates(Tensor alignMetric, int topK)
    {
        // alignMetric: (B, maxGT, N)
        var (topkValues, topkIndices) = alignMetric.topk(
            Math.Min(topK, (int)alignMetric.shape[2]), dim: -1);

        // Create mask of top-k positions
        var mask = torch.zeros_like(alignMetric);
        mask.scatter_(-1, topkIndices, 1.0);

        return mask;
    }

    /// <summary>
    /// Gather values from dim=1 using indices.
    /// </summary>
    private Tensor GatherFromDim1(Tensor src, Tensor idx)
    {
        // src: (B, maxGT), idx: (B, N)
        return src.gather(1, idx.clamp(0, src.shape[1] - 1));
    }

    /// <summary>
    /// Gather bboxes from dim=1 using indices.
    /// </summary>
    private Tensor GatherBboxes(Tensor src, Tensor idx)
    {
        // src: (B, maxGT, 4), idx: (B, N)
        var expanded = idx.unsqueeze(-1).expand(-1, -1, 4);
        return src.gather(1, expanded.clamp(0, src.shape[1] - 1));
    }
}
