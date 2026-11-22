using NAudio.Wave;
using Soundboard.Audio.Buffers;

namespace Soundboard.Audio.Capture;

/// <summary>
/// Output-side view of the mic ring buffer (mono, at the mic's own rate). The mic and the game
/// output run on different clocks, so this keeps a small cushion (target latency), trims back when
/// drift piles samples up, and re-buffers with silence after an underrun instead of stuttering.
/// </summary>
internal sealed class MicRingSampleProvider : ISampleProvider
{
    private readonly SpscRingBuffer _ring;
    private readonly int _targetSamples;
    private readonly int _maxSamples;

    // Audio thread only.
    private bool _buffering = true;

    public MicRingSampleProvider(SpscRingBuffer ring, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(ring);

        _ring = ring;
        _targetSamples = sampleRate * EngineFormat.MicTargetLatencyMs / 1000;
        _maxSamples = sampleRate * EngineFormat.MicMaxLatencyMs / 1000;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var output = buffer.AsSpan(offset, count);
        var available = _ring.Count;

        if (_buffering)
        {
            if (available < _targetSamples)
            {
                output.Clear();
                return count;
            }

            _buffering = false;
        }

        if (available > _maxSamples)
        {
            _ring.Skip(available - _targetSamples);
        }

        var read = _ring.Read(output);
        if (read < count)
        {
            output[read..].Clear();
            _buffering = true;
        }

        // Never report a short read: downstream resamplers would treat it as end of stream.
        return count;
    }
}
