namespace Soundboard.Core.Downloads;

/// <summary>An audio file fetched by <see cref="Abstractions.IAudioDownloader"/>, still in the downloader's own folder.</summary>
/// <param name="FilePath">Full path of the downloaded file.</param>
/// <param name="Title">The video's title, used as the sound's name.</param>
public sealed record DownloadedAudio(string FilePath, string Title);
