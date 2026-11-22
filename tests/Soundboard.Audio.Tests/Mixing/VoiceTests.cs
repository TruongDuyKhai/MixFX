using Soundboard.Audio.Decoding;
using Soundboard.Audio.Mixing;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests.Mixing;

public sealed class VoiceTests
{
    [Fact]
    public void PlaysWholeSoundThenFinishes()
    {
        var voice = new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(1_000, 2, 0.5f)), Unity(1_000));
        var buffer = new float[1_500 * 2];

        var read = voice.Read(buffer);

        read.Should().Be(2_000);
        buffer.AsSpan(0, 2_000).ToArray().Should().OnlyContain(s => s == 0.5f);
        voice.IsFinished.Should().BeTrue();
        voice.Position.Should().Be(1_000);
    }

    [Fact]
    public void FadeIn_RampsFromZeroToFull()
    {
        var sound = Sound(fadeInMs: 10, fadeOutMs: 0);
        var voice = new CachedVoice(sound.Id, new CachedSound(Signals.Constant(4_800, 2, 1f)), EditEnvelope.For(sound, 4_800));
        var buffer = new float[4_800 * 2];

        voice.Read(buffer);

        buffer[0].Should().Be(0f);
        buffer[240 * 2].Should().BeApproximately(0.5f, 0.01f);
        buffer[480 * 2].Should().Be(1f);
    }

    [Fact]
    public void FadeOut_EndsAtZero()
    {
        var sound = Sound(fadeInMs: 0, fadeOutMs: 10);
        var voice = new CachedVoice(sound.Id, new CachedSound(Signals.Constant(4_800, 2, 1f)), EditEnvelope.For(sound, 4_800));
        var buffer = new float[4_800 * 2];

        voice.Read(buffer);

        buffer[(4_800 - 481) * 2].Should().Be(1f);
        buffer[(4_800 - 240) * 2].Should().BeApproximately(0.5f, 0.01f);
        buffer[^1].Should().BeLessThan(0.01f);
    }

    [Fact]
    public void GainAndVolume_Multiply()
    {
        var sound = Sound(fadeInMs: 0, fadeOutMs: 0, gainDb: -6.0206f, volume: 0.5f);
        var voice = new CachedVoice(sound.Id, new CachedSound(Signals.Constant(100, 2, 1f)), EditEnvelope.For(sound, 100));
        var buffer = new float[200];

        voice.Read(buffer);

        buffer[50].Should().BeApproximately(0.25f, 1e-4f);
    }

    [Fact]
    public void RequestStop_FadesOutOverFiveMillisecondsThenFinishes()
    {
        var voice = new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(48_000, 2, 1f)), Unity(48_000));
        voice.Read(new float[200]);

        voice.RequestStop();
        var buffer = new float[1_000 * 2];
        var read = voice.Read(buffer);

        read.Should().Be(240 * 2);
        buffer[0].Should().Be(1f);
        buffer[(240 - 1) * 2].Should().BeLessThan(0.01f);
        voice.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void FinishedVoice_ReadsNothing()
    {
        var voice = new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(10, 2, 1f)), Unity(10));
        voice.Read(new float[100]);

        voice.Read(new float[100]).Should().Be(0);
    }

    private static EditEnvelope Unity(long frames) => new(1f, 0, 0, frames);

    private static SoundItem Sound(int fadeInMs, int fadeOutMs, float gainDb = 0f, float volume = 1f) =>
        new(Guid.NewGuid(), "Test", "test.wav", "#FFFFFF", volume, PlaybackKind.Preloaded,
            new EditParams(0, null, fadeInMs, fadeOutMs, gainDb, []));
}
