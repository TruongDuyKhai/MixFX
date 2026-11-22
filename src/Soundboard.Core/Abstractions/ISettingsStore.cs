using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>Persists <see cref="AppSettings"/> without ever losing the user's data (PROJECT_PROMPT.md §5.7, §7.4).</summary>
public interface ISettingsStore
{
    /// <summary>
    /// Unreadable or corrupt content does not throw: the store recovers (backup, then defaults) and
    /// reports how in the result. I/O failures such as a locked file do throw, because guessing in
    /// that situation could overwrite good data on the next save.
    /// </summary>
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomic: after a crash either the old file or the new one is on disk, never a half-written mix.
    /// The previous version is kept as a backup.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The last load returned <see cref="SettingsLoadStatus.NewerSchemaReadOnly"/>.
    /// </exception>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
