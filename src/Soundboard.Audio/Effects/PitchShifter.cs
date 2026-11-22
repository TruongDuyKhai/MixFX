using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Effects;

/// <summary>
/// Real-time pitch shift without changing duration: two read taps sweep a short delay line at
/// <c>ratio</c> speed and are cross-faded with sin² windows. The weights sum to 1, so the output
/// never overshoots; but both taps carry the same signal, so a pure tone can partly cancel at a
/// cross-fade (on average ~2–3 dB quieter, with a slight flutter). About 20 ms of added latency, cheap
/// and allocation-free: good enough for voice changing and sound effects, not studio quality.
/// </summary>
internal sealed class PitchShifter : IEffect
{
    private const int Channels = EngineFormat.Channels;

    /// <summary>~43 ms window: long enough for low voices, short enough to keep latency small.</summary>
    private const int WindowFrames = 2048;

    private const int BufferFrames = WindowFrames * 2;

    private readonly float[] _left = new float[BufferFrames];
    private readonly float[] _right = new float[BufferFrames];

    private float _semitones;

    // Audio thread only.
    private int _write;
    private double _phase;

    public PitchShifter(float semitones = 0f)
    {
        Semitones = semitones;
    }

    public string Id => EffectCatalog.Pitch;

    /// <summary>Shift in semitones, clamped to ±12. May be changed from any thread while playing.</summary>
    public float Semitones
    {
        get => Volatile.Read(ref _semitones);
        set => Volatile.Write(ref _semitones, Math.Clamp(value, -EffectCatalog.MaxPitchSemitones, EffectCatalog.MaxPitchSemitones));
    }

    public void Process(Span<float> interleavedStereo)
    {
        var ratio = Math.Pow(2, Semitones / 12.0);
        var phaseStep = (1.0 - ratio) / WindowFrames;

        for (var i = 0; i + 1 < interleavedStereo.Length; i += Channels)
        {
            _left[_write] = interleavedStereo[i];
            _right[_write] = interleavedStereo[i + 1];

            _phase += phaseStep;
            _phase -= Math.Floor(_phase);

            var phaseB = _phase + 0.5;
            phaseB -= Math.Floor(phaseB);

            var gainA = Window(_phase);
            var gainB = 1.0 - gainA;

            // +1 so a tap never reads the sample being written in this very frame.
            var delayA = (_phase * WindowFrames) + 1;
            var delayB = (phaseB * WindowFrames) + 1;

            interleavedStereo[i] = (float)((Tap(_left, delayA) * gainA) + (Tap(_left, delayB) * gainB));
            interleavedStereo[i + 1] = (float)((Tap(_right, delayA) * gainA) + (Tap(_right, delayB) * gainB));

            _write = (_write + 1) % BufferFrames;
        }
    }

    public void Reset()
    {
        Array.Clear(_left);
        Array.Clear(_right);
        _phase = 0;
    }

    // sin²(πp): zero where a tap jumps back to the other end of the window, so the jump is inaudible.
    private static double Window(double phase)
    {
        var s = Math.Sin(Math.PI * phase);
        return s * s;
    }

    private double Tap(float[] buffer, double delay)
    {
        var position = _write - delay;
        if (position < 0)
        {
            position += BufferFrames;
        }

        var index = (int)position;
        var fraction = position - index;
        var next = (index + 1) % BufferFrames;
        return buffer[index] + ((buffer[next] - buffer[index]) * fraction);
    }
}
