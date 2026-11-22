using NAudio.Wave;
using Soundboard.Audio.Decoding;
using Soundboard.Audio.Mixing;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests.Decoding;

public sealed class AudioDecoderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("soundboard-audio-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Wav48kStereo_DecodesUnchanged()
    {
        var path = WriteWav("stereo.wav", 48_000, 2, Signals.Sine(4_800, 2, 0.5f));

        using var decoded = AudioDecoder.Open(path, TimeSpan.Zero);
        var sound = AudioDecoder.DecodeAll(decoded, 10_000);

        sound.Frames.Should().Be(4_800);
        sound.Samples[200].Should().BeApproximately(Signals.Sine(4_800, 2, 0.5f)[200], 1e-4f);
    }

    [Fact]
    public void Wav44kMono16Bit_IsResampledToEngineFormat()
    {
        var path = WriteWav("mono.wav", 44_100, 1, Signals.Sine(44_100, 1, 0.5f, sampleRate: 44_100), bits: 16);

        using var decoded = AudioDecoder.Open(path, TimeSpan.Zero);
        var sound = AudioDecoder.DecodeAll(decoded, 60_000);

        decoded.Samples.WaveFormat.SampleRate.Should().Be(48_000);
        decoded.Samples.WaveFormat.Channels.Should().Be(2);
        sound.Frames.Should().BeCloseTo(48_000, 200);
        Signals.Peak(sound.Samples.AsSpan(4_800, 4_800)).Should().BeApproximately(0.5f, 0.02f);
        sound.Samples[9_601].Should().Be(sound.Samples[9_600], "mono is duplicated on both channels");
    }

    [Fact]
    public void Open_WithStart_SeeksToTrimStart()
    {
        var samples = new float[48_000 * 2];
        samples.AsSpan(24_000 * 2).Fill(0.75f); // second half is non-silent
        var path = WriteWav("seek.wav", 48_000, 2, samples);

        using var decoded = AudioDecoder.Open(path, TimeSpan.FromMilliseconds(500));
        var sound = AudioDecoder.DecodeAll(decoded, 100);

        sound.Samples.Should().OnlyContain(s => s == 0.75f);
    }

    [Theory]
    [InlineData(0, null, 48_000)]
    [InlineData(250, null, 36_000)]
    [InlineData(250, 750, 24_000)]
    [InlineData(0, 5_000, 48_000)]
    [InlineData(800, 500, 0)]
    public void TrimmedFrames_ClampsToFile(int trimStartMs, int? trimEndMs, long expected)
    {
        var edit = EditParams.None with { TrimStartMs = trimStartMs, TrimEndMs = trimEndMs };

        AudioDecoder.TrimmedFrames(TimeSpan.FromSeconds(1), edit).Should().Be(expected);
    }

    [Fact]
    public void StreamedVoice_PumpsFromDiskAndPlaysToTheEnd()
    {
        var source = Signals.Sine(48_000 * 3, 2, 0.5f);
        var path = WriteWav("long.wav", 48_000, 2, source);
        var decoded = AudioDecoder.Open(path, TimeSpan.Zero);
        using var voice = new StreamedVoice(Guid.NewGuid(), decoded, 48_000 * 3, new EditEnvelope(1f, 0, 0, 48_000 * 3));

        var played = new List<float>();
        var buffer = new float[4_800];
        while (!voice.IsFinished)
        {
            voice.Pump();
            var n = voice.Read(buffer);
            played.AddRange(buffer.AsSpan(0, n).ToArray());
        }

        played.Should().HaveCount(source.Length);
        played[100_001].Should().BeApproximately(source[100_001], 1e-4f);
    }

    [Fact]
    public void StreamedVoice_WithoutPump_PlaysSilenceInsteadOfEnding()
    {
        var path = WriteWav("underrun.wav", 48_000, 2, Signals.Constant(48_000, 2, 0.5f));
        using var voice = new StreamedVoice(Guid.NewGuid(), AudioDecoder.Open(path, TimeSpan.Zero), 48_000, new EditEnvelope(1f, 0, 0, 48_000));
        var buffer = new float[960];

        voice.Read(buffer).Should().Be(960);

        buffer.Should().OnlyContain(s => s == 0f);
        voice.IsFinished.Should().BeFalse();
    }

    private string WriteWav(string name, int rate, int channels, float[] samples, int bits = 32)
    {
        var path = Path.Combine(_dir, name);
        var format = bits == 32 ? WaveFormat.CreateIeeeFloatWaveFormat(rate, channels) : new WaveFormat(rate, bits, channels);
        using var writer = new WaveFileWriter(path, format);
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }
}
