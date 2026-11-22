namespace Soundboard.Core.Models;

/// <summary>
/// Switches for features that are not stable yet (P6+). Every flag defaults to off
/// (PROJECT_PROMPT.md §7.2.7): a missing value in settings.json means "off".
/// </summary>
public sealed record FeatureFlags
{
    /// <summary>Lowers sounds sent to the game while you talk, so teammates hear your voice over them.</summary>
    public bool Ducking { get; init; }

    /// <summary>Per-sound pitch and reverb (the effect chain in <see cref="EditParams.Effects"/>).</summary>
    public bool SoundEffects { get; init; }

    /// <summary>Pitch-shifts your real voice before it reaches the game.</summary>
    public bool VoiceChanger { get; init; }

    /// <summary>Plays the sounds in <see cref="AppSettings.Playlist"/> one after another.</summary>
    public bool Playlist { get; init; }

    /// <summary>Local HTTP API and web page to trigger actions (Stream Deck, scripts, a browser on this PC).</summary>
    public bool WebRemote { get; init; }

    /// <summary>Adds sounds from a YouTube link: the app fetches the audio with yt-dlp, which it downloads itself.</summary>
    public bool YouTubeImport { get; init; }
}
