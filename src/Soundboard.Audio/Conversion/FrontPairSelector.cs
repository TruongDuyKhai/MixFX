using NAudio.Wave;

namespace Soundboard.Audio.Conversion;

/// <summary>Keeps the front left/right pair of a multichannel file; surround sound effects are rare and the rest would be unheard anyway.</summary>
internal sealed class FrontPairSelector : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sourceChannels;
    private readonly float[] _scratch;

    public FrontPairSelector(ISampleProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.WaveFormat.Channels <= 2)
        {
            throw new ArgumentException("Source must have more than two channels.", nameof(source));
        }

        _source = source;
        _sourceChannels = source.WaveFormat.Channels;
        _scratch = new float[source.WaveFormat.SampleRate * _sourceChannels];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var framesWanted = count / 2;
        var done = 0;
        while (done < framesWanted)
        {
            var chunk = Math.Min(framesWanted - done, _scratch.Length / _sourceChannels);
            var read = _source.Read(_scratch, 0, chunk * _sourceChannels) / _sourceChannels;
            for (var i = 0; i < read; i++)
            {
                buffer[offset + ((done + i) * 2)] = _scratch[i * _sourceChannels];
                buffer[offset + ((done + i) * 2) + 1] = _scratch[(i * _sourceChannels) + 1];
            }

            done += read;
            if (read < chunk)
            {
                break;
            }
        }

        return done * 2;
    }
}
