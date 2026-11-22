using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Core.Playlist;

/// <summary>
/// Plays <see cref="AppSettings.Playlist"/> track after track, looping, while
/// <see cref="FeatureFlags.Playlist"/> is on. It advances only when a track plays to its end
/// (<see cref="SoundStoppedEventArgs.Completed"/>), so "Stop all" or stopping the track never
/// makes the next one start by surprise.
/// </summary>
public sealed class PlaylistController : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly ISettingsProvider _settings;
    private readonly Lock _lock = new();

    private Guid? _current;
    private int _index = -1;

    public PlaylistController(IAudioEngine engine, ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settings);

        _engine = engine;
        _settings = settings;
        _engine.SoundStopped += OnSoundStopped;
    }

    /// <summary>Raised (on any thread) when the current track changes or the playlist stops.</summary>
    public event EventHandler? CurrentChanged;

    /// <summary>The track currently playing from the playlist, or null when it is stopped.</summary>
    public Guid? CurrentSoundId
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Starts the playlist, or skips to the next track. Does nothing while the feature is off or the list is empty.</summary>
    public void Next() => Advance(stopPrevious: true);

    private void Advance(bool stopPrevious)
    {
        var settings = _settings.Current;
        if (!settings.Features.Playlist)
        {
            return;
        }

        // Ids of deleted sounds stay in the list until the user edits it; they are simply skipped.
        var tracks = settings.Playlist
            .Select(id => settings.Sounds.FirstOrDefault(s => s.Id == id))
            .OfType<SoundItem>()
            .ToList();

        Guid? previous;
        SoundItem next;
        lock (_lock)
        {
            previous = _current;
            if (tracks.Count == 0)
            {
                _current = null;
                _index = -1;
                next = null!;
            }
            else
            {
                _index = (_index + 1) % tracks.Count;
                next = tracks[_index];
                _current = next.Id;
            }
        }

        // Only a skip stops the old track (reported as not completed, so it does not advance twice).
        // After a natural end there is nothing left to stop, and stopping by id could cut a copy of
        // the same sound the user started by hand.
        if (stopPrevious && previous is { } stopping)
        {
            _engine.Stop(stopping);
        }

        if (tracks.Count > 0)
        {
            _engine.Play(next);
        }

        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        Guid? current;
        lock (_lock)
        {
            current = _current;
            _current = null;
            _index = -1;
        }

        if (current is { } id)
        {
            _engine.Stop(id);
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _engine.SoundStopped -= OnSoundStopped;

    private void OnSoundStopped(object? sender, SoundStoppedEventArgs e)
    {
        if (!e.Completed)
        {
            return;
        }

        lock (_lock)
        {
            if (_current != e.SoundId)
            {
                return;
            }
        }

        Advance(stopPrevious: false);
    }
}
