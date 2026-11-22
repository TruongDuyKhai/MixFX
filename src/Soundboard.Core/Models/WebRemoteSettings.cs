namespace Soundboard.Core.Models;

/// <summary>Local web remote (behind <see cref="FeatureFlags.WebRemote"/>).</summary>
public sealed record WebRemoteSettings
{
    public const int DefaultPort = 8765;

    /// <summary>TCP port on localhost. The server never listens on other interfaces.</summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// Secret every request must carry. Generated when the remote is first enabled; null until then.
    /// Without it any web page open in a browser could trigger sounds through localhost.
    /// </summary>
    public string? Token { get; init; }
}
