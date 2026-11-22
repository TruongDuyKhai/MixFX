namespace Soundboard.Core.Events;

/// <summary>A sound could not be played (missing file, unsupported format, no running output).</summary>
public sealed class SoundFailedEventArgs(Guid soundId, string message) : EventArgs
{
    public Guid SoundId { get; } = soundId;

    /// <summary>Human-readable reason, already suitable for the UI.</summary>
    public string Message { get; } = message;
}
