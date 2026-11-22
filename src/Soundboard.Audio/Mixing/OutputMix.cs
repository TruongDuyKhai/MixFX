using NAudio.Wave;

namespace Soundboard.Audio.Mixing;

/// <summary>
/// Final sum for one output: sounds × sound volume, plus (Output A only) gated voice × mic volume.
/// Volumes are plain floats published with <see cref="Volatile"/> so the UI can move sliders
/// without locking the audio thread.
/// </summary>
internal sealed class OutputMix : ISampleProvider
{
    private readonly Mixer _sounds;
    private readonly ISampleProvider? _voice;
    private readonly float[] _scratch = new float[EngineFormat.ScratchSamples];

    private float _soundVolume = 1f;
    private float _voiceVolume = 1f;

    public OutputMix(Mixer sounds, ISampleProvider? voice)
    {
        ArgumentNullException.ThrowIfNull(sounds);

        _sounds = sounds;
        _voice = voice;
    }

    public WaveFormat WaveFormat => EngineFormat.WaveFormat;

    public float SoundVolume
    {
        get => Volatile.Read(ref _soundVolume);
        set => Volatile.Write(ref _soundVolume, value);
    }

    public float VoiceVolume
    {
        get => Volatile.Read(ref _voiceVolume);
        set => Volatile.Write(ref _voiceVolume, value);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        _sounds.Read(buffer, offset, count);
        var output = buffer.AsSpan(offset, count);

        var soundVolume = SoundVolume;
        if (soundVolume != 1f)
        {
            for (var i = 0; i < output.Length; i++)
            {
                output[i] *= soundVolume;
            }
        }

        if (_voice is null)
        {
            return count;
        }

        var voiceVolume = VoiceVolume;
        var done = 0;
        while (done < count)
        {
            var chunk = Math.Min(count - done, _scratch.Length);
            var read = _voice.Read(_scratch, 0, chunk);
            var target = output.Slice(done, read);
            for (var i = 0; i < read; i++)
            {
                target[i] += _scratch[i] * voiceVolume;
            }

            done += chunk;
        }

        return count;
    }
}
