using Soundboard.Core.Abstractions;

namespace Soundboard.Audio.Mixing;

/// <summary>
/// One playing instance of a sound on one output. <see cref="Read"/> runs on that output's audio
/// thread; everything else may be called from any thread.
/// </summary>
internal abstract class Voice : IDisposable
{
    private const int Channels = EngineFormat.Channels;

    private readonly EditEnvelope _envelope;
    private readonly IEffect[] _effects;
    private readonly long _stopFadeFrames = EngineFormat.MsToFrames(EngineFormat.StopFadeMs);

    // Audio thread only.
    private long _position;
    private long _stopFramesLeft = -1;

    // Cross-thread.
    private long _publishedPosition;
    private int _stopRequested;
    private int _finished;
    private int _endedNaturally;

    /// <param name="effects">This voice's own effect chain (effects keep state, so voices never share one).</param>
    protected Voice(Guid soundId, long totalFrames, EditEnvelope envelope, IEffect[]? effects)
    {
        SoundId = soundId;
        TotalFrames = totalFrames;
        _envelope = envelope;
        _effects = effects ?? [];
    }

    /// <summary>True when the sound played to its end, as opposed to being stopped or cut by a lost device.</summary>
    public bool EndedNaturally => Volatile.Read(ref _endedNaturally) != 0;

    public Guid SoundId { get; }

    /// <summary>Length of the trimmed sound in engine frames.</summary>
    public long TotalFrames { get; }

    public long Position => Volatile.Read(ref _publishedPosition);

    public bool IsFinished => Volatile.Read(ref _finished) != 0;

    public void RequestStop() => Volatile.Write(ref _stopRequested, 1);

    /// <summary>Ends the voice immediately, e.g. when its output device went away and nothing reads it anymore.</summary>
    public void MarkFinished() => Volatile.Write(ref _finished, 1);

    /// <summary>
    /// Audio thread: writes interleaved stereo into <paramref name="destination"/> and returns the
    /// number of samples that carry sound. The voice is done once <see cref="IsFinished"/> is true.
    /// </summary>
    public int Read(Span<float> destination)
    {
        if (IsFinished)
        {
            return 0;
        }

        if (_stopFramesLeft < 0 && Volatile.Read(ref _stopRequested) != 0)
        {
            _stopFramesLeft = _stopFadeFrames;
        }

        var framesWanted = destination.Length / Channels;
        var framesLeft = TotalFrames - _position;
        if (_stopFramesLeft >= 0)
        {
            framesLeft = Math.Min(framesLeft, _stopFramesLeft);
        }

        var frames = (int)Math.Clamp(framesLeft, 0, framesWanted);
        var read = ReadFrames(destination[..(frames * Channels)], out var sourceEnded);
        var audible = destination[..(read * Channels)];
        foreach (var effect in _effects)
        {
            effect.Process(audible);
        }

        ApplyEnvelope(audible);

        _position += read;
        if (_stopFramesLeft >= 0)
        {
            _stopFramesLeft -= read;
        }

        Volatile.Write(ref _publishedPosition, _position);

        if (sourceEnded || _position >= TotalFrames || _stopFramesLeft == 0)
        {
            if (_stopFramesLeft < 0)
            {
                Volatile.Write(ref _endedNaturally, 1);
            }

            MarkFinished();
            return read * Channels;
        }

        if (read < framesWanted)
        {
            // Streaming underrun: keep the voice alive and play silence until the disk catches up.
            destination[(read * Channels)..].Clear();
            return destination.Length;
        }

        return read * Channels;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Copies up to <paramref name="destination"/>.Length / 2 frames of raw sound.</summary>
    /// <param name="sourceEnded">True once no more data will ever come.</param>
    protected abstract int ReadFrames(Span<float> destination, out bool sourceEnded);

    protected virtual void Dispose(bool disposing)
    {
    }

    private void ApplyEnvelope(Span<float> samples)
    {
        var stopping = _stopFramesLeft >= 0;
        for (var i = 0; i < samples.Length / Channels; i++)
        {
            var gain = _envelope.GainAt(_position + i);
            if (stopping)
            {
                gain *= (float)(_stopFramesLeft - i) / _stopFadeFrames;
            }

            samples[i * Channels] *= gain;
            samples[(i * Channels) + 1] *= gain;
        }
    }
}
