using NAudio.Wave;

namespace Soundboard.Audio.Conversion;

/// <summary>
/// Maps the engine's stereo onto a device with another channel count: mono devices get the
/// average, surround devices get L/R on their front pair and silence elsewhere.
/// </summary>
internal sealed class ChannelAdapter : ISampleProvider
{
    private readonly ISampleProvider _stereo;
    private readonly int _outputChannels;
    private readonly float[] _scratch;

    public ChannelAdapter(ISampleProvider stereo, int outputChannels)
    {
        ArgumentNullException.ThrowIfNull(stereo);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputChannels, 1);
        if (stereo.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Source must be stereo.", nameof(stereo));
        }

        _stereo = stereo;
        _outputChannels = outputChannels;
        _scratch = new float[stereo.WaveFormat.SampleRate * 2];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(stereo.WaveFormat.SampleRate, outputChannels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var framesWanted = count / _outputChannels;
        var done = 0;
        while (done < framesWanted)
        {
            var chunk = Math.Min(framesWanted - done, _scratch.Length / 2);
            var read = _stereo.Read(_scratch, 0, chunk * 2) / 2;
            for (var i = 0; i < read; i++)
            {
                var left = _scratch[i * 2];
                var right = _scratch[(i * 2) + 1];
                var frame = buffer.AsSpan(offset + ((done + i) * _outputChannels), _outputChannels);
                if (_outputChannels == 1)
                {
                    frame[0] = (left + right) * 0.5f;
                }
                else
                {
                    frame[0] = left;
                    frame[1] = right;
                    frame[2..].Clear();
                }
            }

            done += read;
            if (read < chunk)
            {
                break;
            }
        }

        return done * _outputChannels;
    }
}
