using TorchSharp;
using static TorchSharp.torch;

namespace YOLOv8.Core.Utils;

/// <summary>
/// Bounding box utility functions: anchor generation, coordinate conversion,
/// distance-to-bbox and bbox-to-distance transformations.
/// </summary>
public static class BboxUtils
{
    /// <summary>
    /// Generate anchor points and stride tensors for all feature map levels.
    /// Each anchor is placed at the center of a grid cell (offset 0.5).
    /// </summary>
    /// <param name="featureSizes">List of (H, W) for each feature level</param>
    /// <param name="strides">Stride for each feature level (e.g. 8, 16, 32)</param>
    /// <param name="gridCellOffset">Offset within grid cell (default 0.5 = center)</param>
    /// <param name="device">Target device</param>
    /// <returns>Tuple of (anchor_points [N,2], stride_tensor [N,1])</returns>
    public static (Tensor anchorPoints, Tensor strideTensor) MakeAnchors(
        (long h, long w)[] featureSizes, long[] strides,
        double gridCellOffset = 0.5, Device? device = null)
    {
        device ??= CPU;
        var anchorPointsList = new List<Tensor>();
        var strideTensorList = new List<Tensor>();

        for (int i = 0; i < featureSizes.Length; i++)
        {
            var (h, w) = featureSizes[i];
            var stride = strides[i];

            var sx = torch.arange(w, dtype: ScalarType.Float32, device: device) + gridCellOffset;
            var sy = torch.arange(h, dtype: ScalarType.Float32, device: device) + gridCellOffset;

            // meshgrid: sy rows, sx cols
            var grids = torch.meshgrid([sy, sx], indexing: "ij");
            var gridY = grids[0];
            var gridX = grids[1];

            // Stack as (x, y) pairs and flatten
            var points = torch.stack([gridX, gridY], dim: -1).view(-1, 2);
            anchorPointsList.Add(points);

            var strideT = torch.full(h * w, 1, stride, dtype: ScalarType.Float32, device: device);
            strideTensorList.Add(strideT);
        }

        var anchorPoints = torch.cat(anchorPointsList.ToArray(), dim: 0);
        var strideTensor = torch.cat(strideTensorList.ToArray(), dim: 0);
        return (anchorPoints, strideTensor);
    }

    /// <summary>
    /// Convert distance predictions (LTRB) to bounding boxes using anchor points.
    /// </summary>
    /// <param name="distance">Distance tensor (B, 4, N) or (N, 4) in LTRB format</param>
    /// <param name="anchorPoints">Anchor points (N, 2) in (x, y)</param>
    /// <param name="xywh">If true, return (cx, cy, w, h); otherwise (x1, y1, x2, y2)</param>
    /// <returns>Bounding boxes</returns>
    public static Tensor Dist2Bbox(Tensor distance, Tensor anchorPoints, bool xywh = true)
    {
        // distance: (B, 4, N), anchorPoints: (N, 2)
        // Split into left-top and right-bottom distances
        var chunks = distance.chunk(2, dim: -2); // each: (B, 2, N)
        var lt = chunks[0]; // left, top
        var rb = chunks[1]; // right, bottom

        // anchor_points needs to be (1, 2, N) for broadcasting
        var anchor = anchorPoints.T.unsqueeze(0); // (1, 2, N)

        var x1y1 = anchor - lt;
        var x2y2 = anchor + rb;

        if (xywh)
        {
            var cxy = (x1y1 + x2y2) / 2.0;
            var wh = x2y2 - x1y1;
            return torch.cat([cxy, wh], dim: -2);
        }

        return torch.cat([x1y1, x2y2], dim: -2);
    }

    /// <summary>
    /// Convert bounding boxes to distance (LTRB) from anchor points.
    /// </summary>
    /// <param name="anchorPoints">Anchor points (N, 2)</param>
    /// <param name="bbox">Bounding boxes (B, N, 4) in xyxy format</param>
    /// <param name="regMax">Maximum distance value (clamp upper bound)</param>
    /// <returns>Distance tensor (B, N, 4) in LTRB format</returns>
    public static Tensor Bbox2Dist(Tensor anchorPoints, Tensor bbox, double regMax)
    {
        // anchorPoints: (N, 2), bbox: (B, N, 4) as x1y1x2y2
        var anchor = anchorPoints.unsqueeze(0); // (1, N, 2)
        var x1y1 = bbox[.., ..2]; // (B, N, 2)
        var x2y2 = bbox[.., 2..]; // (B, N, 2)

        var lt = anchor - x1y1;
        var rb = x2y2 - anchor;
        var dist = torch.cat([lt, rb], dim: -1); // (B, N, 4)
        return dist.clamp_(0, regMax - 0.01);
    }

    /// <summary>
    /// Convert boxes from xywh (center) format to xyxy (corners) format.
    /// </summary>
    public static Tensor Xywh2Xyxy(Tensor x)
    {
        var y = x.clone();
        var dw = x[.., 2] / 2;
        var dh = x[.., 3] / 2;
        y[.., 0] = x[.., 0] - dw;
        y[.., 1] = x[.., 1] - dh;
        y[.., 2] = x[.., 0] + dw;
        y[.., 3] = x[.., 1] + dh;
        return y;
    }

    /// <summary>
    /// Convert boxes from xyxy (corners) format to xywh (center) format.
    /// </summary>
    public static Tensor Xyxy2Xywh(Tensor x)
    {
        var y = x.clone();
        y[.., 0] = (x[.., 0] + x[.., 2]) / 2;
        y[.., 1] = (x[.., 1] + x[.., 3]) / 2;
        y[.., 2] = x[.., 2] - x[.., 0];
        y[.., 3] = x[.., 3] - x[.., 1];
        return y;
    }
}
