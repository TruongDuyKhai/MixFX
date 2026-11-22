namespace Soundboard.Core.Models;

/// <summary>Direction of an audio endpoint.</summary>
public enum AudioDeviceFlow
{
    /// <summary>Microphones and other inputs.</summary>
    Capture,

    /// <summary>Speakers, headphones and virtual cables.</summary>
    Render,
}
