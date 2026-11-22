namespace Soundboard.Core.Models;

/// <summary>How a sound reacts to its key being pressed and released.</summary>
public enum TriggerMode
{
    /// <summary>Key down starts the sound; key up is ignored.</summary>
    Press,

    /// <summary>Key down starts the sound, the next key down stops it.</summary>
    Toggle,

    /// <summary>Plays only while the key is held.</summary>
    Hold,

    /// <summary>Key down always restarts the sound from the beginning.</summary>
    Restart,
}
