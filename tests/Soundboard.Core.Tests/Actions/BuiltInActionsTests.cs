using Soundboard.Core.Actions;
using Soundboard.Core.Models;
using Soundboard.Core.Tests.Fakes;

namespace Soundboard.Core.Tests.Actions;

public sealed class BuiltInActionsTests
{
    private static readonly SoundItem Horn = new(Guid.NewGuid(), "Horn", "horn.wav", "#FF0000", 1f, PlaybackKind.Preloaded, EditParams.None);

    private readonly FakeAudioEngine _engine = new();
    private readonly FakeSettingsProvider _settings = new(new AppSettings { Sounds = [Horn] });

    [Fact]
    public void Press_PlaysOnDownIgnoresUp()
    {
        Run(TriggerMode.Press, TriggerPhase.Down);
        Run(TriggerMode.Press, TriggerPhase.Up);
        Run(TriggerMode.Press, TriggerPhase.Down);

        _engine.Calls.Should().Equal("Play Horn", "Play Horn");
    }

    [Fact]
    public void Restart_StopsThenPlays()
    {
        Run(TriggerMode.Restart, TriggerPhase.Down);
        Run(TriggerMode.Restart, TriggerPhase.Down);

        _engine.Calls.Should().Equal("Stop", "Play Horn", "Stop", "Play Horn");
    }

    [Fact]
    public void Toggle_AlternatesPlayAndStop()
    {
        Run(TriggerMode.Toggle, TriggerPhase.Down);
        Run(TriggerMode.Toggle, TriggerPhase.Up);
        Run(TriggerMode.Toggle, TriggerPhase.Down);
        Run(TriggerMode.Toggle, TriggerPhase.Down);

        _engine.Calls.Should().Equal("Play Horn", "Stop", "Play Horn");
    }

    [Fact]
    public void Hold_PlaysOnDownStopsOnUp()
    {
        Run(TriggerMode.Hold, TriggerPhase.Down);
        Run(TriggerMode.Hold, TriggerPhase.Up);

        _engine.Calls.Should().Equal("Play Horn", "Stop");
    }

    [Fact]
    public void PlaySound_UnknownOrMissingTarget_DoesNothing()
    {
        var action = new PlaySoundAction(_engine, _settings);

        action.Execute(new ActionContext { TargetId = Guid.NewGuid(), Phase = TriggerPhase.Down });
        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });

        _engine.Calls.Should().BeEmpty();
    }

    [Fact]
    public void StopAll_OnDownOnly()
    {
        var action = new StopAllAction(_engine);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Up });
        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });

        _engine.Calls.Should().Equal("StopAll");
    }

    [Fact]
    public void PttHold_FollowsKeyState()
    {
        var action = new PttHoldAction(_engine);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        _engine.PushToTalk.Should().BeTrue();

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Up });
        _engine.PushToTalk.Should().BeFalse();
    }

    [Fact]
    public void ToggleMic_SwitchesBetweenPushToTalkAndAlwaysOn()
    {
        var action = new ToggleMicAction(_settings);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        _settings.Current.Audio.MicMode.Should().Be(MicMode.AlwaysOn);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Up });
        _settings.Current.Audio.MicMode.Should().Be(MicMode.AlwaysOn, "key-up does nothing");

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        _settings.Current.Audio.MicMode.Should().Be(MicMode.PushToTalk);
    }

    [Fact]
    public void VolumeDown_StepsBothSoundVolumesAndLeavesMicAlone()
    {
        var action = VolumeStepAction.Down(_settings);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });

        _settings.Current.Audio.VolumeToGame.Should().BeApproximately(0.8f, 1e-6f);
        _settings.Current.Audio.VolumeToMonitor.Should().BeApproximately(0.8f, 1e-6f);
        _settings.Current.Audio.MicVolume.Should().Be(1f);
    }

    [Fact]
    public void VolumeUp_IsClampedAtMaximum()
    {
        var action = VolumeStepAction.Up(_settings);

        action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });

        _settings.Current.Audio.VolumeToGame.Should().Be(AudioSettings.MaxSoundVolume);
    }

    [Fact]
    public void VolumeDown_IsClampedAtZero()
    {
        var action = VolumeStepAction.Down(_settings);

        for (var i = 0; i < 20; i++)
        {
            action.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        }

        _settings.Current.Audio.VolumeToGame.Should().Be(0f);
    }

    [Fact]
    public void Dispatcher_PassesTriggerModeThrough()
    {
        var dispatcher = new ActionDispatcher([new PlaySoundAction(_engine, _settings)]);

        dispatcher.Dispatch(ActionIds.PlaySound, Horn.Id, TriggerPhase.Down, TriggerMode.Hold);
        dispatcher.Dispatch(ActionIds.PlaySound, Horn.Id, TriggerPhase.Up, TriggerMode.Hold);

        _engine.Calls.Should().Equal("Play Horn", "Stop");
    }

    private void Run(TriggerMode mode, TriggerPhase phase) =>
        new PlaySoundAction(_engine, _settings).Execute(new ActionContext { TargetId = Horn.Id, Phase = phase, Mode = mode });
}
