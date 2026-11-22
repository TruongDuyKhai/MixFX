using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>The live settings of this session, shared by the UI and the actions.</summary>
public interface ISettingsProvider
{
    /// <summary>Immutable snapshot; safe to read from any thread.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// Raised after every <see cref="Update"/>, on the thread that made the change.
    /// UI subscribers must marshal to the dispatcher.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>Atomically replaces the settings with <paramref name="change"/>(current) and schedules a save.</summary>
    void Update(Func<AppSettings, AppSettings> change);
}
