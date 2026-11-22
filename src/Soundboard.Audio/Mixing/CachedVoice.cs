using Soundboard.Audio.Decoding;
using Soundboard.Core.Abstractions;

namespace Soundboard.Audio.Mixing;

/// <summary>Plays a sound already decoded into RAM: no I/O and no allocation on the audio thread.</summary>
internal sealed class CachedVoice(Guid soundId, CachedSound sound, EditEnvelope envelope, IEffect[]? effects = null)
    : Voice(soundId, sound.Frames, envelope, effects)
{
    // Audio thread only.
    private int _cursor;

    protected override int ReadFrames(Span<float> destination, out bool sourceEnded)
    {
        var samples = sound.Samples;
        var count = Math.Min(destination.Length, samples.Length - _cursor);
        samples.AsSpan(_cursor, count).CopyTo(destination);
        _cursor += count;
        sourceEnded = _cursor >= samples.Length;
        return count / EngineFormat.Channels;
    }
}
