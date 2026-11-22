namespace Soundboard.Core.Events;

/// <summary>One playing instance of a sound ended, either naturally or because it was stopped.</summary>
public sealed class SoundStoppedEventArgs(Guid soundId, bool completed = false) : EventArgs
{
    public Guid SoundId { get; } = soundId;

    /// <summary>True when the sound played to its end; false when it was stopped or its device went away.</summary>
    public bool Completed { get; } = completed;
}
