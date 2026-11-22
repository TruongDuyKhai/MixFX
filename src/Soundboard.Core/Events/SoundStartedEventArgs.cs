namespace Soundboard.Core.Events;

/// <summary>A sound started playing on at least one output.</summary>
public sealed class SoundStartedEventArgs(Guid soundId) : EventArgs
{
    public Guid SoundId { get; } = soundId;
}
