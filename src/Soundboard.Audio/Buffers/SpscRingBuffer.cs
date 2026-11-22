namespace Soundboard.Audio.Buffers;

/// <summary>
/// Lock-free ring of samples for exactly one producer thread and one consumer thread. Used where an
/// audio callback must exchange data with another thread without locking or allocating (§7.3.8).
/// </summary>
internal sealed class SpscRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;

    // Monotonic positions; only the producer writes _writePosition, only the consumer writes _readPosition.
    private long _writePosition;
    private long _readPosition;

    /// <param name="minimumCapacity">Rounded up to a power of two so wrapping is a mask, not a division.</param>
    public SpscRingBuffer(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumCapacity, 1);

        var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)minimumCapacity);
        _buffer = new float[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _buffer.Length;

    /// <summary>Samples currently stored. Exact for the consumer; a lower bound for the producer.</summary>
    public int Count => (int)(Volatile.Read(ref _writePosition) - Volatile.Read(ref _readPosition));

    /// <summary>Producer: copies as much of <paramref name="source"/> as fits and returns how many samples were written.</summary>
    public int Write(ReadOnlySpan<float> source)
    {
        var write = _writePosition;
        var free = _buffer.Length - (int)(write - Volatile.Read(ref _readPosition));
        var count = Math.Min(free, source.Length);
        if (count <= 0)
        {
            return 0;
        }

        var start = (int)(write & _mask);
        var firstPart = Math.Min(count, _buffer.Length - start);
        source[..firstPart].CopyTo(_buffer.AsSpan(start));
        source[firstPart..count].CopyTo(_buffer);

        // Publish only after the data is in place, so the consumer never sees unwritten samples.
        Volatile.Write(ref _writePosition, write + count);
        return count;
    }

    /// <summary>Consumer: copies up to <paramref name="destination"/>.Length samples and returns how many were read.</summary>
    public int Read(Span<float> destination)
    {
        var read = _readPosition;
        var available = (int)(Volatile.Read(ref _writePosition) - read);
        var count = Math.Min(available, destination.Length);
        if (count <= 0)
        {
            return 0;
        }

        var start = (int)(read & _mask);
        var firstPart = Math.Min(count, _buffer.Length - start);
        _buffer.AsSpan(start, firstPart).CopyTo(destination);
        _buffer.AsSpan(0, count - firstPart).CopyTo(destination[firstPart..]);

        Volatile.Write(ref _readPosition, read + count);
        return count;
    }

    /// <summary>Consumer: discards up to <paramref name="count"/> of the oldest samples.</summary>
    public int Skip(int count)
    {
        var read = _readPosition;
        var available = (int)(Volatile.Read(ref _writePosition) - read);
        var skipped = Math.Clamp(count, 0, available);
        Volatile.Write(ref _readPosition, read + skipped);
        return skipped;
    }
}
