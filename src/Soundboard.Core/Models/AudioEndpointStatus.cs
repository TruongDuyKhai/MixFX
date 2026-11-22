namespace Soundboard.Core.Models;

/// <summary>State of one engine endpoint, shown in the UI.</summary>
public enum AudioEndpointStatus
{
    /// <summary>No device chosen and no sensible default found (e.g. VB-Cable not installed).</summary>
    NotConfigured,

    Running,

    /// <summary>The device could not be opened.</summary>
    Failed,

    /// <summary>The device disappeared while running (unplugged, driver removed). Restarts automatically when it returns.</summary>
    Lost,
}
