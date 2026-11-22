using Soundboard.Core.Models;

namespace Soundboard.Core.Events;

/// <summary>An endpoint stopped unexpectedly, e.g. headphones unplugged or VB-Cable removed (PROJECT_PROMPT.md §5.1).</summary>
public sealed class DeviceLostEventArgs(AudioEndpointRole role, string? deviceName, string message) : EventArgs
{
    public AudioEndpointRole Role { get; } = role;

    public string? DeviceName { get; } = deviceName;

    public string Message { get; } = message;
}
