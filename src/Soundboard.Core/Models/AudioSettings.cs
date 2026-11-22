namespace Soundboard.Core.Models;

/// <summary>Device choice and levels for the audio engine (PROJECT_PROMPT.md §5.1–5.2).</summary>
public sealed record AudioSettings
{
    /// <summary>Unity gain: the signal passes unchanged.</summary>
    public const float DefaultVolume = 1f;

    // Devices are stored by WASAPI endpoint id rather than display name because names change
    // with driver updates and Windows language. null means "not chosen yet".
    public string? MicDeviceId { get; init; }

    /// <summary>Output A: the virtual cable the game reads as its microphone.</summary>
    public string? GameOutputDeviceId { get; init; }

    /// <summary>Output B: the user's headphones.</summary>
    public string? MonitorOutputDeviceId { get; init; }

    public float VolumeToGame { get; init; } = DefaultVolume;

    public float VolumeToMonitor { get; init; } = DefaultVolume;

    public float MicVolume { get; init; } = DefaultVolume;

    // PTT by default: with the game on Open Mic, AlwaysOn would broadcast the user's room to
    // teammates the first time the app starts.
    public MicMode MicMode { get; init; } = MicMode.PushToTalk;

    public PlaybackMode PlaybackMode { get; init; } = PlaybackMode.Overlap;
}
