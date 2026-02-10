using TorchSharp;
using static TorchSharp.torch;
using YOLO.Core.Utils;

namespace YOLO.Training.Loss;

/// <summary>
/// Task-Aligned Assigner for anchor-free object detection.
/// Matches Python ultralytics TaskAlignedAssigner (ultralytics/utils/tal.py).
///
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
    private readonly double eps;

    public TaskAlignedAssigner(int topK = 10, double alpha = 0.5, double beta = 6.0, double eps = 1e-9)
    {
        this.topK = topK;
        this.alpha = alpha;
        this.beta = beta;
        this.eps = eps;
    }

    /// <summary>
    /// Perform label assignment (matches Python forward()).
    /// </summary>
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
            return (
                torch.zeros(batch, nAnchors, dtype: ScalarType.Int64, device: device),
                torch.zeros(batch, nAnchors, 4, dtype: dtype, device: device),
                torch.zeros(batch, nAnchors, nc, dtype: dtype, device: device),
                torch.zeros(batch, nAnchors, dtype: ScalarType.Bool, device: device),
                torch.zeros(batch, nAnchors, dtype: ScalarType.Int64, device: device)
            );
        }

        // === get_pos_mask ===
        var (maskPos, alignMetric, overlaps) = GetPosMask(
            predScores, predBboxes, anchorPoints, gtLabels, gtBboxes, maskGT,
            batch, nAnchors, maxGT, nc);

        // === select_highest_overlaps: resolve multi-GT conflicts ===
        var (targetGTIdx, fgMask, maskPosResolved) = SelectHighestOverlaps(maskPos, overlaps, maxGT);

        // === get_targets ===
        var gtLabelsLong = gtLabels.squeeze(-1).to(ScalarType.Int64); // (B, maxGT)
        var (targetLabels, targetBboxes, targetScoresOneHot) = GetTargets(
            gtLabelsLong, gtBboxes, targetGTIdx, fgMask, batch, nAnchors, maxGT, nc);

        // === Normalize alignment metric and apply to target scores ===
        alignMetric = alignMetric * maskPosResolved;
        var posAlignMetrics = alignMetric.amax(new long[] { -1 }, keepdim: true); // (B, maxGT, 1)
        var posOverlaps = (overlaps * maskPosResolved).amax(new long[] { -1 }, keepdim: true); // (B, maxGT, 1)
        var normAlignMetric = (alignMetric * posOverlaps / (posAlignMetrics + eps))
            .amax(new long[] { -2 })   // (B, N) - max across GTs
            .unsqueeze(-1);             // (B, N, 1)
        var targetScores = targetScoresOneHot.to(dtype) * normAlignMetric;

        return (targetLabels, targetBboxes, targetScores, fgMask.to(ScalarType.Bool), targetGTIdx);
    }

    /// <summary>
    /// Matches Python get_pos_mask: compute in-GT mask, alignment metric, overlaps, top-k mask.
    /// </summary>
    private (Tensor maskPos, Tensor alignMetric, Tensor overlaps) GetPosMask(
        Tensor predScores, Tensor predBboxes, Tensor anchorPoints,
        Tensor gtLabels, Tensor gtBboxes, Tensor maskGT,
        long batch, long nAnchors, long maxGT, int nc)
    {
        // Step 1: which anchors fall inside GT boxes
        var maskInGTs = SelectCandidatesInGTs(anchorPoints, gtBboxes); // (B, maxGT, N)

        // Step 2: compute alignment metric and IoU overlaps
        var (alignMetric, overlaps) = GetBoxMetrics(
            predScores, predBboxes, gtLabels, gtBboxes, maskInGTs * maskGT,
            batch, nAnchors, maxGT, nc);

        // Step 3: top-k selection with valid-GT masking
        // topk_mask matches Python: mask_gt.expand(-1, -1, self.topk).bool()
        var topkMask = maskGT.expand(-1, -1, topK).to(ScalarType.Bool); // (B, maxGT, topK)
        var maskTopK = SelectTopKCandidates(alignMetric, topkMask); // (B, maxGT, N)

        // Merge masks
        var maskPos = maskTopK * maskInGTs * maskGT; // (B, maxGT, N)

        return (maskPos, alignMetric, overlaps);
    }

    /// <summary>
    /// Compute alignment metric and IoU overlaps.
    /// Matches Python get_box_metrics.
    /// </summary>
    private (Tensor alignMetric, Tensor overlaps) GetBoxMetrics(
        Tensor predScores, Tensor predBboxes, Tensor gtLabels, Tensor gtBboxes,
        Tensor maskGTExpanded, long batch, long nAnchors, long maxGT, int nc)
    {
        var device = predScores.device;
        var dtype = predScores.dtype;
        var gtLabelsLong = gtLabels.squeeze(-1).to(ScalarType.Int64); // (B, maxGT)

        // Compute IoU between predictions and GTs: (B, maxGT, N)
        var overlaps = ComputeIoU(predBboxes, gtBboxes); // (B, maxGT, N)

        // Gather pred scores for GT classes: (B, maxGT, N)
        var bboxScores = GatherScoresForGTClasses(predScores, gtLabelsLong); // (B, maxGT, N)

        // Alignment metric = score^alpha * iou^beta
        var alignMetric = bboxScores.pow(alpha) * overlaps.pow(beta); // (B, maxGT, N)

        // Apply mask (only anchors inside valid GTs)
        alignMetric = alignMetric * maskGTExpanded;

        return (alignMetric, overlaps);
    }

    /// <summary>
    /// Resolve conflicts when one anchor is assigned to multiple GTs.
    /// Picks the GT with highest IoU for each conflicted anchor.
    /// Matches Python select_highest_overlaps.
    /// </summary>
    private (Tensor targetGTIdx, Tensor fgMask, Tensor maskPos) SelectHighestOverlaps(
        Tensor maskPos, Tensor overlaps, long maxGT)
    {
        // fg_mask = mask_pos.sum(-2) → sum over GT dim → (B, N)
        var fgMask = maskPos.sum(1); // sum over dim=1 (maxGT) → (B, N)

        if (fgMask.max().item<float>() > 1)
        {
            // Some anchors assigned to multiple GTs
            var maskMultiGTs = (fgMask.unsqueeze(1) > 1)
                .expand(-1, maxGT, -1); // (B, maxGT, N)

            // For each anchor, find which GT has highest IoU
            var maxOverlapsIdx = overlaps.argmax(1); // (B, N) - GT index with max IoU

            // Create one-hot: (B, maxGT, N)
            var isMaxOverlaps = torch.zeros_like(maskPos);
            var onesForScatter = torch.ones_like(maxOverlapsIdx.unsqueeze(1)).to(maskPos.dtype);
            isMaxOverlaps.scatter_(1, maxOverlapsIdx.unsqueeze(1), onesForScatter);

            // For multi-assigned anchors, keep only the highest-IoU GT
            maskPos = torch.where(maskMultiGTs, isMaxOverlaps, maskPos).to(ScalarType.Float32);
            fgMask = maskPos.sum(1); // recompute (B, N)
        }

        // For each anchor, which GT is it assigned to
        var targetGTIdx = maskPos.argmax(1); // argmax over dim=1 (maxGT) → (B, N)

        return (targetGTIdx, fgMask, maskPos);
    }

    /// <summary>
    /// Compute target labels, bboxes, and one-hot scores.
    /// Matches Python get_targets.
    /// </summary>
    private (Tensor targetLabels, Tensor targetBboxes, Tensor targetScores) GetTargets(
        Tensor gtLabelsLong, Tensor gtBboxes, Tensor targetGTIdx, Tensor fgMask,
        long batch, long nAnchors, long maxGT, int nc)
    {
        var device = gtLabelsLong.device;

        // Python: target_gt_idx = target_gt_idx + batch_ind * n_max_boxes
        var batchInd = torch.arange(batch, dtype: ScalarType.Int64, device: device)
            .unsqueeze(-1); // (B, 1)
        var flatIdx = targetGTIdx + batchInd * maxGT; // (B, N)

        // target_labels = gt_labels.flatten()[flat_idx]
        var targetLabels = gtLabelsLong.reshape(-1)[flatIdx]; // (B, N)

        // target_bboxes = gt_bboxes.view(-1, 4)[flat_idx]
        var targetBboxes = gtBboxes.view(-1, 4)[flatIdx]; // (B, N, 4)

        // Clamp labels (safety)
        targetLabels = targetLabels.clamp_min(0);

        // One-hot target scores: scatter 1 at the class index
        var targetScores = torch.zeros(batch, nAnchors, nc,
            dtype: ScalarType.Int64, device: device);
        var onesForOneHot = torch.ones_like(targetLabels.unsqueeze(-1)); // Int64 matching targetScores
        targetScores.scatter_(2, targetLabels.unsqueeze(-1), onesForOneHot);

        // Zero out background anchors
        var fgScoresMask = fgMask.unsqueeze(-1).expand(-1, -1, nc) > 0; // (B, N, nc)
        targetScores = torch.where(fgScoresMask, targetScores,
            torch.zeros_like(targetScores));

        return (targetLabels, targetBboxes, targetScores);
    }

    /// <summary>
    /// Check which anchor points fall inside GT bounding boxes.
    /// Uses epsilon threshold matching Python (eps=1e-9).
    /// </summary>
    private Tensor SelectCandidatesInGTs(Tensor anchorPoints, Tensor gtBboxes)
    {
        // anchorPoints: (N, 2), gtBboxes: (B, maxGT, 4) in xyxy
        var points = anchorPoints.unsqueeze(0).unsqueeze(1); // (1, 1, N, 2)
        var boxes = gtBboxes.unsqueeze(2); // (B, maxGT, 1, 4)

        var lt = points - boxes[.., .., .., ..2]; // (B, maxGT, N, 2)
        var rb = boxes[.., .., .., 2..] - points; // (B, maxGT, N, 2)

        var deltas = torch.cat([lt, rb], dim: -1); // (B, maxGT, N, 4)
        // Python uses > eps (1e-9) instead of > 0
        return deltas.amin(-1).gt(eps).to(ScalarType.Float32); // (B, maxGT, N)
    }

    /// <summary>
    /// Compute IoU between each prediction and each GT box.
    /// </summary>
    private Tensor ComputeIoU(Tensor predBboxes, Tensor gtBboxes)
    {
        // predBboxes: (B, N, 4), gtBboxes: (B, maxGT, 4) both in xyxy
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
    /// Select top-k candidates per GT with duplicate suppression.
    /// Matches Python select_topk_candidates exactly:
    ///   1. Get topk indices
    ///   2. Apply topk_mask (invalid GTs get indices zeroed)
    ///   3. Use scatter_add to count per-anchor assignments
    ///   4. Zero counts > 1 (duplicate suppression from masked-to-0 indices)
    /// </summary>
    private Tensor SelectTopKCandidates(Tensor alignMetric, Tensor? topkMask)
    {
        // alignMetric: (B, maxGT, N)
        int k = Math.Min(topK, (int)alignMetric.shape[2]);

        var (topkMetrics, topkIdxs) = alignMetric.topk(k, dim: -1); // each (B, maxGT, k)

        // If no external mask, create one: valid if any topk metric > eps
        if (topkMask is null)
        {
            topkMask = (topkMetrics.max(-1, keepdim: true).values > eps)
                .expand_as(topkIdxs); // (B, maxGT, k)
        }

        // Zero out indices for invalid GTs (they all point to anchor 0)
        topkIdxs = topkIdxs.masked_fill(~topkMask, 0);

        // Count how many times each anchor is selected (per GT)
        var countTensor = torch.zeros(alignMetric.shape,
            dtype: ScalarType.Int32, device: topkIdxs.device); // (B, maxGT, N)
        var ones = torch.ones(topkIdxs.shape[0], topkIdxs.shape[1], 1,
            dtype: ScalarType.Int32, device: topkIdxs.device); // (B, maxGT, 1)

        for (int ki = 0; ki < k; ki++)
        {
            var sliceIdx = topkIdxs.slice(-1, ki, ki + 1, 1).to(ScalarType.Int64); // (B, maxGT, 1)
            countTensor.scatter_add_(-1, sliceIdx, ones);
        }

        // Suppress duplicates: count > 1 means anchor 0 got hits from masked-out GTs
        countTensor = countTensor.masked_fill(countTensor > 1, 0);

        return countTensor.to(alignMetric.dtype); // (B, maxGT, N)
    }
}
