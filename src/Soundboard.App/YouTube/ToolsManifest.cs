namespace Soundboard.App.YouTube;

/// <summary>
/// <c>tools\tools.json</c>: which releases were installed after their checksum was verified, and when updates
/// were last looked for. A tool file without an entry here is never run: it may be a half-finished or foreign file.
/// </summary>
/// <param name="YtDlpVersion">Release tag of the installed yt-dlp, or null.</param>
/// <param name="DenoVersion">Release tag of the installed Deno, or null.</param>
/// <param name="LastCheckedUtc">When both tools were last compared with their latest release.</param>
public sealed record ToolsManifest(string? YtDlpVersion, string? DenoVersion, DateTimeOffset LastCheckedUtc)
{
    public static ToolsManifest Empty { get; } = new(null, null, DateTimeOffset.MinValue);
}
