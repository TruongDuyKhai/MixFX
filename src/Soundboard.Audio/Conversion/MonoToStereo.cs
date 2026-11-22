using NAudio.Wave;

namespace Soundboard.Audio.Conversion;

/// <summary>Duplicates a mono signal onto both channels (§5.1). Allocation-free, unlike NAudio's version on first use.</summary>
internal sealed class MonoToStereo : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float[] _scratch;

    public MonoToStereo(ISampleProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.WaveFormat.Channels != 1)
        {
            throw new ArgumentException("Source must be mono.", nameof(source));
        }

        _source = source;
        _scratch = new float[source.WaveFormat.SampleRate];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var framesWanted = count / 2;
        var done = 0;
        while (done < framesWanted)
        {
            var chunk = Math.Min(framesWanted - done, _scratch.Length);
            var read = _source.Read(_scratch, 0, chunk);
            for (var i = 0; i < read; i++)
            {
                var sample = _scratch[i];
                buffer[offset + ((done + i) * 2)] = sample;
                buffer[offset + ((done + i) * 2) + 1] = sample;
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
