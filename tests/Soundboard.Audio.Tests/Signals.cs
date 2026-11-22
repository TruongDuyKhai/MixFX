using NAudio.Wave;

namespace Soundboard.Audio.Tests;

/// <summary>Generated test signals (PROJECT_PROMPT.md §7.7.22: sine, silence, impulse).</summary>
internal static class Signals
{
    public static float[] Sine(int frames, int channels, float amplitude, double frequency = 440, int sampleRate = 48_000)
    {
        var samples = new float[frames * channels];
        for (var f = 0; f < frames; f++)
        {
            var value = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * f / sampleRate));
            for (var c = 0; c < channels; c++)
            {
                samples[(f * channels) + c] = value;
            }
        }

        return samples;
    }

    public static float[] Constant(int frames, int channels, float value)
    {
        var samples = new float[frames * channels];
        Array.Fill(samples, value);
        return samples;
    }

    public static float Peak(ReadOnlySpan<float> samples)
    {
        var peak = 0f;
        foreach (var s in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(s));
        }

        return peak;
    }

    public static float[] ReadAll(ISampleProvider provider, int samples)
    {
        var buffer = new float[samples];
        var read = provider.Read(buffer, 0, samples);
        return buffer[..read];
    }
}

/// <summary>Plays a fixed array once, then reports the end with short reads like a real file reader.</summary>
internal sealed class ArraySampleProvider(float[] samples, int sampleRate = 48_000, int channels = 2) : ISampleProvider
{
    private int _position;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

    public int Read(float[] buffer, int offset, int count)
    {
        var n = Math.Min(count, samples.Length - _position);
        Array.Copy(samples, _position, buffer, offset, n);
        _position += n;
        return n;
    }
}

/// <summary>Endless constant signal, like a live microphone.</summary>
internal sealed class ConstantSampleProvider(float value, int sampleRate = 48_000, int channels = 2) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

    public int Read(float[] buffer, int offset, int count)
    {
        buffer.AsSpan(offset, count).Fill(value);
        return count;
    }
}
