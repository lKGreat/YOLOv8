using TorchSharp;
using static TorchSharp.torch;

namespace YOLOv8.Core.Utils;

/// <summary>
/// Utility tensor operations used across the YOLOv8 codebase.
/// </summary>
public static class TensorOps
{
    /// <summary>
    /// Make a value divisible by the given divisor (round up).
    /// </summary>
    public static long MakeDivisible(long value, long divisor)
    {
        return ((value + divisor - 1) / divisor) * divisor;
    }

    /// <summary>
    /// Make a value divisible by the given divisor.
    /// </summary>
    public static long MakeDivisible(double value, long divisor)
    {
        return MakeDivisible((long)Math.Ceiling(value), divisor);
    }
}
