using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Models;

// Pins the defaults users rely on (PROJECT_PROMPT.md §7.2.6): changing one must be a deliberate decision.
public sealed class AppSettingsTests
{
    [Fact]
    public void Default_IsCurrentSchemaAndEmptyBoard()
    {
        var settings = new AppSettings();

        settings.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        settings.Sounds.Should().BeEmpty();
        settings.Bindings.Should().BeEmpty();
        settings.Features.Should().NotBeNull();
    }

    [Fact]
    public void DefaultAudio_IsUnityGainPushToTalkOverlapAndNoDevicesChosen()
    {
        var audio = new AppSettings().Audio;

        audio.VolumeToGame.Should().Be(1f);
        audio.VolumeToMonitor.Should().Be(1f);
        audio.MicVolume.Should().Be(1f);
        audio.MicMode.Should().Be(MicMode.PushToTalk);
        audio.PlaybackMode.Should().Be(PlaybackMode.Overlap);
        audio.MicDeviceId.Should().BeNull();
        audio.GameOutputDeviceId.Should().BeNull();
        audio.MonitorOutputDeviceId.Should().BeNull();
    }

    [Fact]
    public void EditParamsNone_ChangesNothing()
    {
        var none = EditParams.None;

        none.TrimStartMs.Should().Be(0);
        none.TrimEndMs.Should().BeNull();
        none.FadeInMs.Should().Be(0);
        none.FadeOutMs.Should().Be(0);
        none.GainDb.Should().Be(0f);
        none.Effects.Should().BeEmpty();
    }
}
