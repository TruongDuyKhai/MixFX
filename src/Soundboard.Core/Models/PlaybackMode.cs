namespace Soundboard.Core.Models;

/// <summary>Global rule for what happens when a sound starts while others are playing.</summary>
public enum PlaybackMode
{
    /// <summary>Sounds play on top of each other.</summary>
    Overlap,

    /// <summary>A new sound stops every sound that is already playing.</summary>
    Exclusive,
}
