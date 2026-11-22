namespace Soundboard.App.YouTube;

/// <summary>What yt-dlp reported about a video before downloading it.</summary>
/// <param name="Duration">Length, or null when unknown (live streams).</param>
/// <param name="IsLive">Whether the video is a live stream right now.</param>
/// <param name="Title">The video's title.</param>
public sealed record YtDlpVideoInfo(TimeSpan? Duration, bool IsLive, string Title);
