namespace Soundboard.Core.Models;

/// <summary>The three endpoints the engine drives (PROJECT_PROMPT.md §2).</summary>
public enum AudioEndpointRole
{
    /// <summary>The user's real microphone.</summary>
    Microphone,

    /// <summary>Output A: the virtual cable the game uses as its microphone.</summary>
    GameOutput,

    /// <summary>Output B: the user's headphones.</summary>
    MonitorOutput,
}
