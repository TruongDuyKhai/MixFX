using Soundboard.Core.Models;

namespace Soundboard.Core.Events;

/// <summary>Settings were replaced; both snapshots are given so listeners can react only to what changed.</summary>
public sealed class SettingsChangedEventArgs(AppSettings previous, AppSettings current) : EventArgs
{
    public AppSettings Previous { get; } = previous;

    public AppSettings Current { get; } = current;
}
