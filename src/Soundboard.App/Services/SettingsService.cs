using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.App.Services;

/// <summary>
/// This session's settings: loads them once, hands out immutable snapshots, applies changes
/// atomically and saves shortly after each change, so a crash loses at most the last half second.
/// </summary>
public sealed class SettingsService : ISettingsProvider, IDisposable
{
    // Slider drags produce dozens of changes per second; one write after they settle is enough.
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ISettingsStore _store;
    private readonly ILogger<SettingsService> _logger;
    private readonly Lock _updateLock = new();
    private readonly Timer _saveTimer;

    private AppSettings _current = new();
    private SettingsLoadResult? _lastLoad;

    public SettingsService(ISettingsStore store, ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _logger = logger;
        _saveTimer = new Timer(_ => _ = SaveInBackgroundAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Current => Volatile.Read(ref _current);

    /// <summary>
    /// False until a load succeeded. Saving before that would write defaults over a file we never
    /// managed to read (e.g. it was locked), which is exactly the data loss §5.7 forbids.
    /// </summary>
    public bool IsLoaded => Volatile.Read(ref _lastLoad) is not null;

    /// <summary>False when the file belongs to a newer app version; see <see cref="SettingsLoadStatus.NewerSchemaReadOnly"/>.</summary>
    public bool CanSave => Volatile.Read(ref _lastLoad) is { Status: not SettingsLoadStatus.NewerSchemaReadOnly };

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _current, result.Settings);
        Volatile.Write(ref _lastLoad, result);

        _logger.LogInformation(
            "Settings loaded with status {Status}: {SoundCount} sounds, {BindingCount} bindings",
            result.Status, result.Settings.Sounds.Count, result.Settings.Bindings.Count);
        return result;
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        AppSettings previous;
        AppSettings next;
        lock (_updateLock)
        {
            previous = _current;
            next = change(previous);
            if (next == previous)
            {
                return;
            }

            Volatile.Write(ref _current, next);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
        if (CanSave)
        {
            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(IsLoaded
                ? "Settings belong to a newer app version and must not be overwritten."
                : "Settings were never loaded; saving now could overwrite the user's file with defaults.");
        }

        _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        await _store.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Settings saved");
    }

    public void Dispose() => _saveTimer.Dispose();

    private async Task SaveInBackgroundAsync()
    {
        try
        {
            await SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The in-memory settings stay correct; the next change or exit retries the save.
            _logger.LogError(ex, "Saving settings failed");
        }
    }
}
