using YOLOv8.Data.Utils;

namespace YOLOv8.Data.Augmentation;

/// <summary>
/// Random affine/perspective augmentation matching Python ultralytics random_perspective().
/// Applies the full affine transformation: M = T @ S @ R @ P @ C
/// where:
///   C = Center translation (move image center to origin)
///   P = Perspective transform
///   R = Rotation + shear
///   S = Scale
///   T = Translation (move back + random offset)
/// </summary>
public class RandomPerspective
{
    private readonly float degrees;
    private readonly float translate;
    private readonly float scale;
    private readonly float shear;
    private readonly float perspective;
    private readonly Random rng;

    public RandomPerspective(float degrees = 0f, float translate = 0.1f,
        float scale = 0.5f, float shear = 0f, float perspective = 0f, int? seed = null)
    {
        this.degrees = degrees;
        this.translate = translate;
        this.scale = scale;
        this.shear = shear;
        this.perspective = perspective;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply full affine/perspective transformation to image and labels.
    /// Implements M = T @ S @ R @ P @ C matching Python ultralytics.
    /// </summary>
    public (byte[] data, int w, int h, List<BboxInstance> labels) Apply(
        byte[] imgData, int w, int h, List<BboxInstance> labels, int targetSize)
    {
        int newW = targetSize;
        int newH = targetSize;

        // Build the combined transformation matrix M = T @ S @ R @ P @ C
        // All matrices are 3x3 for homogeneous coordinates

        // C: Center matrix - translate image center to origin
        double[,] C = {
            { 1, 0, -w / 2.0 },
            { 0, 1, -h / 2.0 },
            { 0, 0, 1 }
        };

        // P: Perspective matrix
        double[,] P = {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { (rng.NextDouble() * 2 - 1) * perspective,
              (rng.NextDouble() * 2 - 1) * perspective,
              1 }
        };

        // R: Rotation + Shear matrix
        double angle = (rng.NextDouble() * 2 - 1) * degrees;
        double s = rng.NextDouble() * (2 * scale) + (1 - scale); // uniform in [1-scale, 1+scale]
        double shearX = (rng.NextDouble() * 2 - 1) * shear;
        double shearY = (rng.NextDouble() * 2 - 1) * shear;

        double cosA = Math.Cos(angle * Math.PI / 180.0);
        double sinA = Math.Sin(angle * Math.PI / 180.0);

        double[,] R = {
            { cosA, sinA, 0 },
            { -sinA, cosA, 0 },
            { 0, 0, 1 }
        };

        // Apply shear to R
        double[,] Shear = {
            { 1, Math.Tan(shearX * Math.PI / 180.0), 0 },
            { Math.Tan(shearY * Math.PI / 180.0), 1, 0 },
            { 0, 0, 1 }
        };

        // S: Scale matrix
        double[,] S = {
            { s, 0, 0 },
            { 0, s, 0 },
            { 0, 0, 1 }
        };

        // T: Translation matrix
        double tx = (rng.NextDouble() * 2 - 1) * translate * newW;
        double ty = (rng.NextDouble() * 2 - 1) * translate * newH;

        double[,] T = {
            { 1, 0, newW / 2.0 + tx },
            { 0, 1, newH / 2.0 + ty },
            { 0, 0, 1 }
        };

        // Combine: M = T @ S @ Shear @ R @ P @ C
        var M = MatMul3x3(T, MatMul3x3(S, MatMul3x3(Shear, MatMul3x3(R, MatMul3x3(P, C)))));

        // Compute inverse for backward mapping (dst -> src)
        var Minv = Invert3x3(M);

        // Apply transform via backward mapping
        var result = new byte[newW * newH * 3];
        // Fill with gray (114)
        for (int i = 0; i < result.Length; i++)
            result[i] = 114;

        for (int dy = 0; dy < newH; dy++)
        {
            for (int dx = 0; dx < newW; dx++)
            {
                // Map destination to source using inverse transform
                double denom = Minv[2, 0] * dx + Minv[2, 1] * dy + Minv[2, 2];
                double sx = (Minv[0, 0] * dx + Minv[0, 1] * dy + Minv[0, 2]) / denom;
                double sy = (Minv[1, 0] * dx + Minv[1, 1] * dy + Minv[1, 2]) / denom;

                int ix = (int)Math.Round(sx);
                int iy = (int)Math.Round(sy);

                if (ix >= 0 && ix < w && iy >= 0 && iy < h)
                {
                    int srcIdx = (iy * w + ix) * 3;
                    int dstIdx = (dy * newW + dx) * 3;
                    result[dstIdx] = imgData[srcIdx];
                    result[dstIdx + 1] = imgData[srcIdx + 1];
                    result[dstIdx + 2] = imgData[srcIdx + 2];
                }
            }
        }

        // Transform labels using forward mapping M
        var newLabels = new List<BboxInstance>();
        foreach (var inst in labels)
        {
            var ni = inst.Clone();
            // Denormalize from source image
            ni.Denormalize(w, h);

            // Transform bounding box corners through M
            // Get corner points of the bbox in xywh format
            float cx = ni.Bbox[0], cy = ni.Bbox[1], bw = ni.Bbox[2], bh = ni.Bbox[3];
            float x1 = cx - bw / 2, y1 = cy - bh / 2;
            float x2 = cx + bw / 2, y2 = cy + bh / 2;

            // Transform 4 corners through M
            var corners = new (double x, double y)[]
            {
                TransformPoint(M, x1, y1),
                TransformPoint(M, x2, y1),
                TransformPoint(M, x2, y2),
                TransformPoint(M, x1, y2)
            };

            // Get axis-aligned bounding box of transformed corners
            double minX = corners.Min(c => c.x);
            double minY = corners.Min(c => c.y);
            double maxX = corners.Max(c => c.x);
            double maxY = corners.Max(c => c.y);

            // Convert back to xywh
            float newCx = (float)(minX + maxX) / 2;
            float newCy = (float)(minY + maxY) / 2;
            float newBw = (float)(maxX - minX);
            float newBh = (float)(maxY - minY);

            ni.Bbox = [newCx, newCy, newBw, newBh];

            // Clip and normalize to new dimensions
            ni.Clip(newW, newH);
            ni.Normalize(newW, newH);

            // Filter out tiny/invalid boxes
            if (ni.IsValid(minArea: 100f / (newW * newH), maxAspectRatio: 20f))
                newLabels.Add(ni);
        }

        return (result, newW, newH, newLabels);
    }

    /// <summary>
    /// Transform a point through a 3x3 homogeneous matrix.
    /// </summary>
    private static (double x, double y) TransformPoint(double[,] M, double x, double y)
    {
        double w = M[2, 0] * x + M[2, 1] * y + M[2, 2];
        double nx = (M[0, 0] * x + M[0, 1] * y + M[0, 2]) / w;
        double ny = (M[1, 0] * x + M[1, 1] * y + M[1, 2]) / w;
        return (nx, ny);
    }

    /// <summary>
    /// Multiply two 3x3 matrices.
    /// </summary>
    private static double[,] MatMul3x3(double[,] A, double[,] B)
    {
        var result = new double[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 3; k++)
                    result[i, j] += A[i, k] * B[k, j];
        return result;
    }

    /// <summary>
    /// Invert a 3x3 matrix.
    /// </summary>
    private static double[,] Invert3x3(double[,] m)
    {
        double det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                   - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                   + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

        if (Math.Abs(det) < 1e-10)
        {
            // Return identity if singular
            return new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        }

        double invDet = 1.0 / det;
        var inv = new double[3, 3];

        inv[0, 0] = (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) * invDet;
        inv[0, 1] = (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) * invDet;
        inv[0, 2] = (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) * invDet;
        inv[1, 0] = (m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2]) * invDet;
        inv[1, 1] = (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) * invDet;
        inv[1, 2] = (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) * invDet;
        inv[2, 0] = (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]) * invDet;
        inv[2, 1] = (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) * invDet;
        inv[2, 2] = (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) * invDet;

        return inv;
    }
}
