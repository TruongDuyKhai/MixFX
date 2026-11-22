using Soundboard.Core.Actions;
using Soundboard.Core.Models;
using Soundboard.Core.Playlist;
using Soundboard.Core.Tests.Fakes;

namespace Soundboard.Core.Tests.Playlist;

public sealed class PlaylistControllerTests
{
    private static readonly SoundItem A = Sound("A");
    private static readonly SoundItem B = Sound("B");
    private static readonly SoundItem C = Sound("C");

    private readonly FakeAudioEngine _engine = new();

    [Fact]
    public void Next_PlaysTracksInOrderAndLoops()
    {
        using var playlist = Create([A.Id, B.Id]);

        playlist.Next();
        _engine.RaiseStopped(A.Id, completed: true);
        _engine.RaiseStopped(B.Id, completed: true);

        _engine.Calls.Should().Equal("Play A", "Play B", "Play A");
        playlist.CurrentSoundId.Should().Be(A.Id);
    }

    [Fact]
    public void Next_WhilePlaying_StopsCurrentAndSkips()
    {
        using var playlist = Create([A.Id, B.Id]);

        playlist.Next();
        playlist.Next();
        _engine.RaiseStopped(A.Id, completed: false);

        _engine.Calls.Should().Equal("Play A", "Stop", "Play B");
        playlist.CurrentSoundId.Should().Be(B.Id);
    }

    [Fact]
    public void StoppedOrCutTrack_DoesNotAdvance()
    {
        using var playlist = Create([A.Id, B.Id]);
        playlist.Next();

        _engine.RaiseStopped(A.Id, completed: false);

        _engine.Calls.Should().Equal("Play A");
    }

    [Fact]
    public void Stop_StopsAndLaterCompletionsAreIgnored()
    {
        using var playlist = Create([A.Id, B.Id]);
        playlist.Next();

        playlist.Stop();
        _engine.RaiseStopped(A.Id, completed: true);

        _engine.Calls.Should().Equal("Play A", "Stop");
        playlist.CurrentSoundId.Should().BeNull();
    }

    [Fact]
    public void DeletedSounds_AreSkipped()
    {
        using var playlist = Create([Guid.NewGuid(), B.Id]);

        playlist.Next();

        _engine.Calls.Should().Equal("Play B");
    }

    [Fact]
    public void CompletionOfAnotherSound_IsIgnored()
    {
        using var playlist = Create([A.Id, B.Id]);
        playlist.Next();

        _engine.RaiseStopped(C.Id, completed: true);

        _engine.Calls.Should().Equal("Play A");
    }

    [Fact]
    public void FeatureOff_DoesNothing()
    {
        using var playlist = Create([A.Id], enabled: false);

        playlist.Next();

        _engine.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Actions_ReactOnKeyDownOnly()
    {
        using var playlist = Create([A.Id, B.Id]);
        var next = PlaylistAction.Next(playlist);
        var stop = PlaylistAction.Stop(playlist);

        next.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });
        next.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Up });
        stop.Execute(new ActionContext { TargetId = null, Phase = TriggerPhase.Down });

        next.Id.Should().Be(ActionIds.PlaylistNext);
        _engine.Calls.Should().Equal("Play A", "Stop");
    }

    private PlaylistController Create(IReadOnlyList<Guid> order, bool enabled = true) =>
        new(_engine, new FakeSettingsProvider(new AppSettings
        {
            Sounds = [A, B, C],
            Playlist = order,
            Features = new FeatureFlags { Playlist = enabled },
        }));

    private static SoundItem Sound(string name) =>
        new(Guid.NewGuid(), name, $"{name}.wav", "#FFFFFF", 1f, PlaybackKind.Preloaded, EditParams.None);
}
