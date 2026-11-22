using Soundboard.App.ViewModels;
using Soundboard.Core.Models;

namespace Soundboard.App.Tests;

public sealed class ExperimentalViewModelTests
{
    private readonly MutableSettings _settings = new(new AppSettings());
    private readonly RecordingDialogs _dialogs = new();

    [Fact]
    public void AllFeatures_StartOff()
    {
        var vm = new ExperimentalViewModel(_settings, _dialogs);

        new[] { vm.Ducking, vm.SoundEffects, vm.VoiceChanger, vm.Playlist, vm.WebRemote, vm.YouTubeImport }.Should().OnlyContain(on => !on);
    }

    [Fact]
    public void YouTubeImport_TogglesItsFlagOnly()
    {
        var vm = new ExperimentalViewModel(_settings, _dialogs);

        vm.YouTubeImport = true;

        _settings.Current.Features.Should().Be(new FeatureFlags { YouTubeImport = true });
    }

    [Fact]
    public void EnablingWebRemote_CreatesATokenOnceAndKeepsIt()
    {
        var vm = new ExperimentalViewModel(_settings, _dialogs);

        vm.WebRemote = true;
        var token = _settings.Current.WebRemote.Token;
        vm.WebRemote = false;
        vm.WebRemote = true;

        token.Should().NotBeNullOrEmpty();
        _settings.Current.WebRemote.Token.Should().Be(token);
        _settings.Current.Features.WebRemote.Should().BeTrue();
        vm.RemoteUrl.Should().Contain($"token={token}");
    }

    [Theory]
    [InlineData("80")]
    [InlineData("70000")]
    [InlineData("abc")]
    public void InvalidPort_IsRejected(string port)
    {
        var vm = new ExperimentalViewModel(_settings, _dialogs);

        vm.Port = port;

        _settings.Current.WebRemote.Port.Should().Be(WebRemoteSettings.DefaultPort);
        _dialogs.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Sliders_WriteRoundedValues()
    {
        var vm = new ExperimentalViewModel(_settings, _dialogs);

        vm.DuckingDb = -9.6;
        vm.VoicePitch = 3.4;

        _settings.Current.Audio.DuckingDb.Should().Be(-10f);
        _settings.Current.Audio.VoicePitchSemitones.Should().Be(3f);
    }
}
