using NAudio.Wave;
using Soundboard.Audio.Effects;

namespace Soundboard.Audio;

/// <summary>
/// Sits between the microphone and the PTT gate and pitch-shifts the voice when enabled
/// (<c>FeatureFlags.VoiceChanger</c>). Disabled or at 0 semitones it is an exact pass-through.
/// </summary>
internal sealed class VoiceChanger : ISampleProvider
{
    private readonly PitchShifter _pitch = new();

    private ISampleProvider? _source;
    private int _enabled;

    public WaveFormat WaveFormat => EngineFormat.WaveFormat;

    /// <summary>Mic chain in the engine format; swapped by the engine thread when the mic changes.</summary>
    public ISampleProvider? Source
    {
        get => Volatile.Read(ref _source);
        set => Volatile.Write(ref _source, value);
    }

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public float Semitones
    {
        get => _pitch.Semitones;
        set => _pitch.Semitones = value;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var source = Source;
        if (source is null)
        {
            buffer.AsSpan(offset, count).Clear();
            return count;
        }

        var read = source.Read(buffer, offset, count);
        if (Enabled && Semitones != 0f)
        {
            _pitch.Process(buffer.AsSpan(offset, read));
        }

        return read;
    }
}
