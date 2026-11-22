using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.App.Services;

/// <summary>Holds this session's settings and decides whether they may be written back.</summary>
public sealed class SettingsService(ISettingsStore store, ILogger<SettingsService> logger)
{
    private SettingsLoadResult? _lastLoad;

    public AppSettings Current { get; private set; } = new();

    /// <summary>
    /// False until a load succeeded. Saving before that would write defaults over a file we never
    /// managed to read (e.g. it was locked), which is exactly the data loss §5.7 forbids.
    /// </summary>
    public bool IsLoaded => _lastLoad is not null;

    /// <summary>False when the file belongs to a newer app version; see <see cref="SettingsLoadStatus.NewerSchemaReadOnly"/>.</summary>
    public bool CanSave => IsLoaded && _lastLoad!.Status != SettingsLoadStatus.NewerSchemaReadOnly;

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Current = result.Settings;
        _lastLoad = result;

        logger.LogInformation(
            "Settings loaded with status {Status}: {SoundCount} sounds, {BindingCount} bindings",
            result.Status, result.Settings.Sounds.Count, result.Settings.Bindings.Count);
        return result;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException(IsLoaded
                ? "Settings belong to a newer app version and must not be overwritten."
                : "Settings were never loaded; saving now could overwrite the user's file with defaults.");
        }

        await store.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Settings saved");
    }
}
