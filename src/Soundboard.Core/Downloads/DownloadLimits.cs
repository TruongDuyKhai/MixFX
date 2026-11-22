namespace Soundboard.Core.Downloads;

/// <summary>Limits for audio fetched from the internet.</summary>
public static class DownloadLimits
{
    /// <summary>
    /// Longest video accepted. A soundboard plays clips and songs; hour-long mixes would only fill the disk
    /// and the library, and the editor's trim markers become impractical.
    /// </summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(20);
}
