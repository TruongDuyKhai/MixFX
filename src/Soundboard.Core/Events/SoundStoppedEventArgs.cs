namespace Soundboard.Core.Events;

/// <summary>One playing instance of a sound ended, either naturally or because it was stopped.</summary>
public sealed class SoundStoppedEventArgs(Guid soundId) : EventArgs
{
    public Guid SoundId { get; } = soundId;
}
