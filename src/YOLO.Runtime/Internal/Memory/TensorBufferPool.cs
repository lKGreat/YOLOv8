using System.Buffers;

namespace YOLO.Runtime.Internal.Memory;

/// <summary>
/// Pool for reusing float[] tensor buffers, avoiding GC pressure on the hot path.
/// Wraps <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
internal static class TensorBufferPool
{
    private static readonly ArrayPool<float> Pool = ArrayPool<float>.Shared;

    /// <summary>
    /// Rent a buffer of at least <paramref name="minimumLength"/> floats.
    /// The returned <see cref="BufferHandle"/> must be disposed to return the buffer.
    /// </summary>
    public static BufferHandle Rent(int minimumLength)
    {
        var array = Pool.Rent(minimumLength);
        return new BufferHandle(array, minimumLength, Pool);
    }

    /// <summary>
    /// Rent a buffer and clear it to zeros.
    /// </summary>
    public static BufferHandle RentCleared(int minimumLength)
    {
        var array = Pool.Rent(minimumLength);
        System.Array.Clear(array, 0, minimumLength);
        return new BufferHandle(array, minimumLength, Pool);
    }
}
