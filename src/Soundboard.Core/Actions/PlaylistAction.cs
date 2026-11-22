using Soundboard.Core.Models;
using Soundboard.Core.Playlist;

namespace Soundboard.Core.Actions;

/// <summary>"PlaylistNext" / "PlaylistStop" hotkeys and buttons (<see cref="FeatureFlags.Playlist"/>).</summary>
public sealed class PlaylistAction : IAction
{
    private readonly PlaylistController _playlist;
    private readonly bool _next;

    private PlaylistAction(PlaylistController playlist, string id, bool next)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        _playlist = playlist;
        Id = id;
        _next = next;
    }

    public string Id { get; }

    public static PlaylistAction Next(PlaylistController playlist) => new(playlist, ActionIds.PlaylistNext, next: true);

    public static PlaylistAction Stop(PlaylistController playlist) => new(playlist, ActionIds.PlaylistStop, next: false);

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Phase != TriggerPhase.Down)
        {
            return;
        }

        if (_next)
        {
            _playlist.Next();
        }
        else
        {
            _playlist.Stop();
        }
    }
}
