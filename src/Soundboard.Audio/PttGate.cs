using NAudio.Wave;

namespace Soundboard.Audio;

/// <summary>
/// Lets the voice through only while push-to-talk is held (or always, in AlwaysOn mode), with a
/// short linear fade so opening and closing never "pops" (PROJECT_PROMPT.md §5.2). Sounds do not
/// pass through here, so they reach the game regardless of PTT.
/// </summary>
internal sealed class PttGate : ISampleProvider
{
    private readonly float _step;

    private ISampleProvider? _source;
    private int _alwaysOn;
    private int _pressed;

    // Written by the audio thread, read by the UI LED.
    private float _gain;

    public PttGate(int fadeMs = EngineFormat.PttFadeMs)
    {
        _step = 1f / Math.Max(1, EngineFormat.MsToFrames(fadeMs));
    }

    public WaveFormat WaveFormat => EngineFormat.WaveFormat;

    /// <summary>Mic chain in the engine format. null (no mic running) outputs silence.</summary>
    public ISampleProvider? Source
    {
        get => Volatile.Read(ref _source);
        set => Volatile.Write(ref _source, value);
    }

    public bool AlwaysOn
    {
        get => Volatile.Read(ref _alwaysOn) != 0;
        set => Volatile.Write(ref _alwaysOn, value ? 1 : 0);
    }

    public bool PushToTalkPressed
    {
        get => Volatile.Read(ref _pressed) != 0;
        set => Volatile.Write(ref _pressed, value ? 1 : 0);
    }

    public bool IsOpen => AlwaysOn || PushToTalkPressed;

    /// <summary>A mic is running and the gate is open: the user is talking to the game (drives ducking).</summary>
    public bool IsVoiceOpen => Source is not null && IsOpen;

    /// <summary>True while any voice reaches the output (including the fade tail).</summary>
    public bool IsTransmitting => Source is not null && Volatile.Read(ref _gain) > 0f;

    public int Read(float[] buffer, int offset, int count)
    {
        var samples = buffer.AsSpan(offset, count);
        var source = Source;
        if (source is null)
        {
            samples.Clear();
            Volatile.Write(ref _gain, 0f);
            return count;
        }

        // Always pull from the mic even when closed, otherwise its buffer would back up and add delay.
        var read = source.Read(buffer, offset, count);
        samples[read..].Clear();

        var target = IsOpen ? 1f : 0f;
        var gain = _gain;
        if (gain == 0f && target == 0f)
        {
            samples.Clear();
            return count;
        }

        for (var frame = 0; frame + EngineFormat.Channels <= count; frame += EngineFormat.Channels)
        {
            if (gain < target)
            {
                gain = MathF.Min(target, gain + _step);
            }
            else if (gain > target)
            {
                gain = MathF.Max(target, gain - _step);
            }

            samples[frame] *= gain;
            samples[frame + 1] *= gain;
        }

        Volatile.Write(ref _gain, gain);
        return count;
    }
}
