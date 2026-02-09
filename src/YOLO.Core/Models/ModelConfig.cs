namespace YOLO.Core.Models;

/// <summary>
/// YOLOv model scaling configuration.
/// Defines depth, width, and max channel multipliers for each variant.
/// </summary>
public record ModelScale(double Depth, double Width, long MaxChannels);

/// <summary>
/// YOLOv model configuration with variant scaling.
/// </summary>
public static class ModelConfig
{
    /// <summary>
    /// Pre-defined model scales matching ultralytics YOLOv.yaml.
    /// </summary>
    public static readonly Dictionary<string, ModelScale> Scales = new()
    {
        ["n"] = new ModelScale(0.33, 0.25, 1024),
        ["s"] = new ModelScale(0.33, 0.50, 1024),
        ["m"] = new ModelScale(0.67, 0.75, 768),
        ["l"] = new ModelScale(1.00, 1.00, 512),
        ["x"] = new ModelScale(1.00, 1.25, 512),
    };

    /// <summary>
    /// Base channel configuration from YOLOv.yaml (before scaling).
    /// </summary>
    public static readonly long[] BackboneChannels = [64, 128, 256, 512, 1024];

    /// <summary>
    /// Base C2f repeat counts from YOLOv.yaml (before depth scaling).
    /// Index: [P2, P3, P4, P5] (backbone) and [N1, N2, N3, N4] (neck)
    /// </summary>
    public static readonly int[] BackboneDepths = [3, 6, 6, 3];
    public static readonly int[] NeckDepths = [3, 3, 3];

    /// <summary>
    /// Detection strides for P3, P4, P5.
    /// </summary>
    public static readonly long[] DetectionStrides = [8, 16, 32];

    /// <summary>
    /// Apply depth scaling: round(n * depth_mult), minimum 1.
    /// </summary>
    public static int ScaleDepth(int n, double depthMult)
    {
        return Math.Max((int)Math.Round(n * depthMult), 1);
    }

    /// <summary>
    /// Apply width scaling: make_divisible(min(c, max_channels) * width_mult, 8).
    /// </summary>
    public static long ScaleWidth(long c, double widthMult, long maxChannels)
    {
        var scaled = Math.Min(c, maxChannels) * widthMult;
        return MakeDivisible(scaled, 8);
    }

    private static long MakeDivisible(double value, long divisor)
    {
        var rounded = (long)Math.Ceiling(value / divisor) * divisor;
        return rounded;
    }
}
