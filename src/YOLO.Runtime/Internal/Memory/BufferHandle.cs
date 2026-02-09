using System.Buffers;

namespace YOLO.Runtime.Internal.Memory;

/// <summary>
/// A disposable handle to a rented float buffer.
/// Returning the buffer to the pool on <see cref="Dispose"/>.
/// </summary>
internal sealed class BufferHandle : IDisposable
{
    private readonly ArrayPool<float> _pool;
    private bool _disposed;

    /// <summary>The underlying rented array (may be larger than <see cref="Length"/>).</summary>
    public float[] Array { get; }

    /// <summary>The actual needed length (callers should only use [0..Length)).</summary>
    public int Length { get; }

    /// <summary>A span over the usable portion of the buffer.</summary>
    public Span<float> Span => Array.AsSpan(0, Length);

    /// <summary>A memory over the usable portion of the buffer.</summary>
    public Memory<float> Memory => Array.AsMemory(0, Length);

    internal BufferHandle(float[] array, int length, ArrayPool<float> pool)
    {
        Array = array;
        Length = length;
        _pool = pool;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.Return(Array);
    }
}
