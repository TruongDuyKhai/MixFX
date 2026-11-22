using NAudio.Wave;

namespace Soundboard.Audio.Mixing;

/// <summary>
/// Final sum for one output: sounds × sound volume (× ducking), plus (Output A only) gated voice ×
/// mic volume. Parameters are plain values published with <see cref="Volatile"/> so the UI can move
/// sliders without locking the audio thread.
/// </summary>
internal sealed class OutputMix : ISampleProvider
{
    private const float DuckAttackMs = 10f;
    private const float DuckReleaseMs = 250f;

    private readonly Mixer _sounds;
    private readonly ISampleProvider? _voice;
    private readonly PttGate? _duckingTrigger;
    private readonly float[] _scratch = new float[EngineFormat.ScratchSamples];
    private readonly float _duckAttack = Coefficient(DuckAttackMs);
    private readonly float _duckRelease = Coefficient(DuckReleaseMs);

    private float _soundVolume = 1f;
    private float _voiceVolume = 1f;
    private int _duckingEnabled;
    private float _duckingGain = 1f;

    // Audio thread only: the smoothed ducking gain currently applied.
    private float _duck = 1f;

    /// <param name="duckingTrigger">Gate whose open state lowers the sounds when ducking is enabled (Output A only).</param>
    public OutputMix(Mixer sounds, ISampleProvider? voice, PttGate? duckingTrigger = null)
    {
        ArgumentNullException.ThrowIfNull(sounds);

        _sounds = sounds;
        _voice = voice;
        _duckingTrigger = duckingTrigger;
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

    /// <summary>Ducking on/off (<c>FeatureFlags.Ducking</c>).</summary>
    public bool DuckingEnabled
    {
        get => Volatile.Read(ref _duckingEnabled) != 0;
        set => Volatile.Write(ref _duckingEnabled, value ? 1 : 0);
    }

    /// <summary>Linear gain applied to sounds while the voice is open (e.g. 0.25 for −12 dB).</summary>
    public float DuckingGain
    {
        get => Volatile.Read(ref _duckingGain);
        set => Volatile.Write(ref _duckingGain, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>Current smoothed ducking gain, for tests and metering.</summary>
    public float CurrentDuck => _duck;

    public int Read(float[] buffer, int offset, int count)
    {
        _sounds.Read(buffer, offset, count);
        var output = buffer.AsSpan(offset, count);
        ApplySoundGain(output);

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

    private static float Coefficient(float milliseconds) =>
        1f - MathF.Exp(-1f / (milliseconds * EngineFormat.SampleRate / 1000f));

    private void ApplySoundGain(Span<float> output)
    {
        var volume = SoundVolume;
        var duckTarget = DuckingEnabled && _duckingTrigger is { IsVoiceOpen: true } ? DuckingGain : 1f;
        if (volume == 1f && duckTarget == 1f && _duck == 1f)
        {
            return;
        }

        var duck = _duck;
        for (var i = 0; i + 1 < output.Length; i += EngineFormat.Channels)
        {
            // Fast attack so the first syllable is already clear, slow release so sounds swell back smoothly.
            duck += (duckTarget - duck) * (duckTarget < duck ? _duckAttack : _duckRelease);
            if (MathF.Abs(duck - duckTarget) < 1e-4f)
            {
                duck = duckTarget;
            }

            var gain = volume * duck;
            output[i] *= gain;
            output[i + 1] *= gain;
        }

        _duck = duck;
    }
}
