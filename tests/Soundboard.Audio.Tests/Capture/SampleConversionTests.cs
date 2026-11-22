using NAudio.Wave;
using Soundboard.Audio.Capture;

namespace Soundboard.Audio.Tests.Capture;

public sealed class SampleConversionTests
{
    [Fact]
    public void Float32Stereo_AveragesChannels()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        var bytes = ToBytes([0.2f, 0.4f, -1f, 1f]);
        var output = new float[2];

        var frames = SampleConversion.ToMono(bytes, format, SampleConversion.GetKind(format), output);

        frames.Should().Be(2);
        output[0].Should().BeApproximately(0.3f, 1e-6f);
        output[1].Should().Be(0f);
    }

    [Fact]
    public void Pcm16Mono_ScalesToUnitRange()
    {
        var format = new WaveFormat(44_100, 16, 1);
        var bytes = new byte[] { 0x00, 0x40, 0x00, 0x80 }; // 16384, -32768
        var output = new float[2];

        SampleConversion.ToMono(bytes, format, SampleConversion.GetKind(format), output);

        output[0].Should().BeApproximately(0.5f, 1e-6f);
        output[1].Should().Be(-1f);
    }

    [Fact]
    public void Pcm24_SignExtendsNegativeValues()
    {
        var format = new WaveFormat(48_000, 24, 1);
        var bytes = new byte[] { 0x00, 0x00, 0xC0 }; // -4194304 = -0.5
        var output = new float[1];

        SampleConversion.ToMono(bytes, format, SampleConversion.GetKind(format), output);

        output[0].Should().BeApproximately(-0.5f, 1e-6f);
    }

    [Fact]
    public void ExtensibleFloat_IsRecognised()
    {
        var format = new WaveFormatExtensible(48_000, 32, 2);

        SampleConversion.GetKind(format).Should().Be(CaptureSampleKind.Float32);
    }

    [Fact]
    public void UnsupportedFormat_Throws()
    {
        var act = () => SampleConversion.GetKind(new WaveFormat(48_000, 8, 1));

        act.Should().Throw<NotSupportedException>();
    }

    private static byte[] ToBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
