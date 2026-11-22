using Soundboard.Core.Downloads;

namespace Soundboard.Core.Abstractions;

/// <summary>
/// Fetches the audio track of an online video (YouTube, PROJECT_PROMPT.md §5.8) with external tools that the
/// implementation downloads and keeps up to date itself. Operations run one at a time; a second call waits.
/// Expected failures (network, rejected video, bad checksum) are reported as <see cref="AudioDownloadException"/>
/// with a message for the user.
/// </summary>
public interface IAudioDownloader
{
    /// <summary>Whether the tools are installed and whether it is time to look for a newer release.</summary>
    Task<DownloadToolsStatus> GetToolsStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the tools from their official releases, or updates them when a newer release exists.
    /// A download whose checksum does not match is discarded and never run.
    /// </summary>
    Task InstallToolsAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the audio of <paramref name="url"/> (checked with <see cref="YouTubeUrl.TryParse"/>) into a
    /// folder the downloader owns. Videos longer than <see cref="DownloadLimits.MaxDuration"/> and live streams
    /// are rejected. Pass the result to <see cref="Delete"/> once it has been copied into the library.
    /// </summary>
    Task<DownloadedAudio> DownloadAudioAsync(Uri url, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file returned by <see cref="DownloadAudioAsync"/>. Paths outside the downloader's folder are left alone.</summary>
    void Delete(DownloadedAudio audio);
}
