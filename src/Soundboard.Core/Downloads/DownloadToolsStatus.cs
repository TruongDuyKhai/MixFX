namespace Soundboard.Core.Downloads;

/// <summary>State of the external tools <see cref="Abstractions.IAudioDownloader"/> relies on.</summary>
/// <param name="IsInstalled">Every tool is present and was verified when it was installed.</param>
/// <param name="IsUpdateDue">Installed, but not checked for a newer release recently; YouTube changes often break old versions.</param>
/// <param name="Version">Installed downloader version for display, or null when not installed.</param>
public sealed record DownloadToolsStatus(bool IsInstalled, bool IsUpdateDue, string? Version);
