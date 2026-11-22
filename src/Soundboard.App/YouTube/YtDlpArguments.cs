using System.Globalization;
using Soundboard.Core.Downloads;

namespace Soundboard.App.YouTube;

/// <summary>
/// The yt-dlp command line. Every value is a separate argument (no shell, no quoting), the URL comes last
/// after <c>--</c> so it can never be read as an option, and user/system config files and plugins are ignored
/// so nothing outside the app (for example an <c>--exec</c> line in a config file) changes what runs.
/// </summary>
public static class YtDlpArguments
{
    /// <summary>Line prefixes printed through <c>--print</c> / <c>--progress-template</c>; parsed by <see cref="YtDlpOutputParser"/>.</summary>
    public const string InfoPrefix = "SBINFO ";

    public const string ProgressPrefix = "SBPROGRESS ";

    public const string FilePrefix = "SBFILE ";

    // AAC in an m4a container: decoded by Media Foundation like any imported .m4a, so no ffmpeg is needed.
    private const string AudioFormat = "bestaudio[ext=m4a]";
    private const int SocketTimeoutSeconds = 30;

    public static IReadOnlyList<string> Build(Uri url, string outputDirectory, string cacheDirectory, string denoPath)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(denoPath);

        var maxSeconds = ((int)DownloadLimits.MaxDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return
        [
            "--ignore-config",
            "--no-plugin-dirs",
            "--no-playlist",
            "--color", "no_color",
            "--encoding", "utf-8",
            "--socket-timeout", SocketTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            "--no-js-runtimes",
            "--js-runtimes", "deno:" + denoPath,
            "--cache-dir", cacheDirectory,
            "-f", AudioFormat,
            "--match-filters", $"duration<={maxSeconds} & !is_live",
            "-P", "home:" + outputDirectory,
            "-P", "temp:" + outputDirectory,
            "-o", "%(id)s.%(ext)s",

            // pre_process runs before the match filter, so a rejected video still reports why.
            "--print", $"pre_process:{InfoPrefix}%(duration)s %(is_live)s %(title)s",
            "--print", $"after_move:{FilePrefix}%(filepath)s",
            "--no-simulate",

            // --print implies --quiet, which would also hide progress.
            "--progress",
            "--newline",
            "--progress-template", $"download:{ProgressPrefix}%(progress.downloaded_bytes)s %(progress.total_bytes,progress.total_bytes_estimate)s",
            "--",
            url.AbsoluteUri,
        ];
    }
}
