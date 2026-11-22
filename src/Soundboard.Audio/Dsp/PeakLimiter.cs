using NAudio.Wave;

namespace Soundboard.Audio.Dsp;

/// <summary>
/// Keeps every sample at or below the ceiling so a loud sound plus voice cannot blast teammates
/// (PROJECT_PROMPT.md §5.1). Instant attack guarantees the ceiling without look-ahead latency;
/// the release lets the gain recover smoothly afterwards.
/// </summary>
internal sealed class PeakLimiter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly float _ceiling;
    private readonly float _releaseCoefficient;

    // Audio thread only.
    private float _gain = 1f;

    public PeakLimiter(ISampleProvider source, float ceilingDb, float releaseMs)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _channels = source.WaveFormat.Channels;
        _ceiling = Decibels.ToGain(ceilingDb);
        var releaseFrames = releaseMs * source.WaveFormat.SampleRate / 1000f;
        _releaseCoefficient = 1f - MathF.Exp(-1f / releaseFrames);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Current gain reduction factor (1 = untouched), for tests and metering.</summary>
    public float CurrentGain => _gain;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        var samples = buffer.AsSpan(offset, read);
        var gain = _gain;

        for (var frame = 0; frame + _channels <= samples.Length; frame += _channels)
        {
            var peak = 0f;
            for (var c = 0; c < _channels; c++)
            {
                peak = MathF.Max(peak, MathF.Abs(samples[frame + c]));
            }

            gain += (1f - gain) * _releaseCoefficient;
            if (peak * gain > _ceiling)
            {
                gain = _ceiling / peak;
            }

            for (var c = 0; c < _channels; c++)
            {
                samples[frame + c] *= gain;
            }
        }

        _gain = gain;
        return read;
    }
}
