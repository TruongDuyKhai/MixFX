using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Soundboard.Audio.Export;
using Soundboard.Audio.Waveform;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests.Editing;

public sealed class WaveformAndExportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("soundboard-edit-tests-").FullName;

    // Exports must go outside the folder holding the source (the exporter refuses its own library folder).
    private readonly string _exportDir = Directory.CreateTempSubdirectory("soundboard-export-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        Directory.Delete(_exportDir, recursive: true);
    }

    [Fact]
    public void Compute_ReportsPeakPerTenMillisecondBucket()
    {
        // 0.5 s silence, then 0.5 s of a 0.8-amplitude sine.
        var samples = new float[48_000 * 2];
        Signals.Sine(24_000, 2, 0.8f).CopyTo(samples, 24_000 * 2);
        var path = WriteWav("half.wav", samples);

        var waveform = WaveformService.Compute(path, Ct);

        waveform.Duration.Should().Be(TimeSpan.FromSeconds(1));
        waveform.BucketDuration.Should().Be(TimeSpan.FromMilliseconds(10));
        waveform.Peaks.Should().HaveCount(100);
        waveform.Peaks.Take(50).Should().OnlyContain(p => p == 0f);
        waveform.Peaks.Skip(51).Should().OnlyContain(p => p > 0.75f && p <= 0.8f + 1e-4f);
        waveform.PeakBetween(TimeSpan.Zero, TimeSpan.FromMilliseconds(400)).Should().Be(0f);
        waveform.PeakBetween(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(900)).Should().BeApproximately(0.8f, 0.01f);
    }

    [Fact]
    public async Task LoadAsync_WritesCacheAndReusesIt()
    {
        var path = WriteWav("cached.wav", Signals.Sine(4_800, 2, 0.5f));
        var service = new WaveformService(new FixedLibrary(path), NullLogger<WaveformService>.Instance);

        var first = await service.LoadAsync(Sound(), Ct);
        File.Delete(path); // only the cache is left
        var second = await service.LoadAsync(Sound(), Ct);

        File.Exists(path + ".peaks").Should().BeTrue();
        second.Peaks.Should().Equal(first.Peaks);
        second.Duration.Should().Be(first.Duration);
    }

    [Fact]
    public void ReadCache_TruncatedOrForeignFile_ReturnsNull()
    {
        var truncated = Path.Combine(_dir, "truncated.peaks");
        WaveformService.WriteCache(truncated, new WaveformData(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(10), new float[100]));
        var bytes = File.ReadAllBytes(truncated);
        File.WriteAllBytes(truncated, bytes[..^10]);
        var foreign = Path.Combine(_dir, "foreign.peaks");
        File.WriteAllText(foreign, "definitely not a peaks file");

        WaveformService.ReadCache(truncated).Should().BeNull();
        WaveformService.ReadCache(foreign).Should().BeNull();
    }

    [Fact]
    public async Task Export_BakesTrimFadeAndGainInto16BitWav()
    {
        var source = WriteWav("source.wav", Signals.Constant(48_000, 2, 0.25f));
        var exporter = new AudioExporter(new FixedLibrary(source));
        var sound = Sound() with { Edit = new EditParams(250, 750, 10, 10, 6.0206f, []) };
        var destination = Path.Combine(_exportDir, "out.wav");

        await exporter.ExportWavAsync(sound, destination, Ct);

        using var reader = new WaveFileReader(destination);
        reader.WaveFormat.SampleRate.Should().Be(48_000);
        reader.WaveFormat.BitsPerSample.Should().Be(16);
        reader.WaveFormat.Channels.Should().Be(2);
        var output = Signals.ReadAll(reader.ToSampleProvider(), 48_000 * 2);
        output.Length.Should().Be(24_000 * 2, "500 ms between the trim markers");
        output[0].Should().Be(0f, "fade-in starts from silence");
        output[12_000 * 2].Should().BeApproximately(0.5f, 0.001f, "+6 dB doubles 0.25");
        output[^1].Should().BeLessThan(0.01f, "fade-out ends at silence");
        File.Exists(source).Should().BeTrue();
        File.Exists(destination + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task Export_ClipsInsteadOfWrappingWhenGainOverdrives()
    {
        var source = WriteWav("loud.wav", Signals.Constant(4_800, 2, 0.9f));
        var exporter = new AudioExporter(new FixedLibrary(source));
        var destination = Path.Combine(_exportDir, "loud.wav");

        await exporter.ExportWavAsync(Sound() with { Edit = EditParams.None with { GainDb = 12f } }, destination, Ct);

        using var reader = new WaveFileReader(destination);
        Signals.ReadAll(reader.ToSampleProvider(), 9_600).Should().OnlyContain(s => s > 0.99f);
    }

    [Fact]
    public async Task Export_IntoTheLibraryFolder_IsRefused()
    {
        var source = WriteWav("library-copy.wav", Signals.Constant(480, 2, 0.5f));
        var exporter = new AudioExporter(new FixedLibrary(source));

        var act = () => exporter.ExportWavAsync(Sound(), Path.Combine(_dir, "other.wav"), Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(Path.Combine(_dir, "other.wav")).Should().BeFalse();
    }

    private static SoundItem Sound() =>
        new(Guid.NewGuid(), "Test", "test.wav", "#FFFFFF", 1f, PlaybackKind.Preloaded, EditParams.None);

    private string WriteWav(string name, float[] samples)
    {
        var path = Path.Combine(_dir, name);
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2));
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }
}
