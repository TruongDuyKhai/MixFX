namespace Soundboard.Core.Models;

/// <summary>When the real microphone is let through to the game. Sounds always pass regardless.</summary>
public enum MicMode
{
    /// <summary>Voice passes only while the PTT key is held.</summary>
    PushToTalk,

    /// <summary>Voice always passes.</summary>
    AlwaysOn,
}
