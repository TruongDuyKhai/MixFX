using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Soundboard.Audio.Conversion;

/// <summary>
/// Hands float samples to WASAPI as bytes. Replaces NAudio's SampleToWaveProvider, which allocates
/// a wrapper object on every callback.
/// </summary>
internal sealed class FloatToWaveProvider : IWaveProvider
{
    private const int BytesPerSample = sizeof(float);

    private readonly ISampleProvider _source;
    private readonly float[] _scratch;

    /// <param name="deviceFormat">
    /// The device's own 32-bit float mix format, reported as-is so WASAPI accepts it without inserting
    /// a converter. Must match the source's rate and channel count.
    /// </param>
    public FloatToWaveProvider(ISampleProvider source, WaveFormat deviceFormat)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(deviceFormat);
        if (source.WaveFormat.SampleRate != deviceFormat.SampleRate
            || source.WaveFormat.Channels != deviceFormat.Channels
            || deviceFormat.BitsPerSample != BytesPerSample * 8)
        {
            throw new ArgumentException("Device format must be 32-bit float with the source's rate and channels.", nameof(deviceFormat));
        }

        _source = source;
        _scratch = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels];
        WaveFormat = deviceFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(byte[] buffer, int offset, int count)
    {
        var samplesWanted = count / BytesPerSample;
        var output = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, samplesWanted * BytesPerSample));
        var done = 0;
        while (done < samplesWanted)
        {
            var chunk = Math.Min(samplesWanted - done, _scratch.Length);
            var read = _source.Read(_scratch, 0, chunk);
            _scratch.AsSpan(0, read).CopyTo(output[done..]);
            done += read;
            if (read < chunk)
            {
                break;
            }
        }

        return done * BytesPerSample;
    }
}
