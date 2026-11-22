namespace Soundboard.Core.Models;

/// <summary>How a sound is fed to the mixer. Computed from the trimmed length, not chosen by the user.</summary>
public enum PlaybackKind
{
    /// <summary>Short sound decoded once into RAM so triggering it never touches the disk.</summary>
    Preloaded,

    /// <summary>Long sound read from disk while playing, to keep memory bounded.</summary>
    Streamed,
}
