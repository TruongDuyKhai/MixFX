using System.Text.RegularExpressions;

namespace Soundboard.Core.Downloads;

/// <summary>
/// Accepts only links to a single YouTube video. yt-dlp understands thousands of sites and some URL forms
/// (local files, generic pages); restricting the input to known YouTube shapes keeps the feature to what
/// was decided in ADR 0005 and stops a pasted link from reaching anything else.
/// </summary>
public static partial class YouTubeUrl
{
    private const string ShortHost = "youtu.be";
    private const string WatchPath = "/watch";
    private const string VideoQueryKey = "v";

    private static readonly HashSet<string> FullHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
    };

    // Paths that carry the video id as their second segment, e.g. /shorts/<id>.
    private static readonly HashSet<string> IdPathPrefixes = new(StringComparer.OrdinalIgnoreCase) { "shorts", "live", "embed" };

    /// <summary>
    /// Parses user input such as "https://www.youtube.com/watch?v=…", "youtu.be/…" or a Shorts link.
    /// On success <paramref name="url"/> is an https URL without fragment, user info or custom port.
    /// </summary>
    public static bool TryParse(string? text, out Uri? url)
    {
        url = null;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            || parsed.UserInfo.Length > 0
            || !parsed.IsDefaultPort
            || VideoId(parsed) is null)
        {
            return false;
        }

        url = new UriBuilder(parsed) { Scheme = Uri.UriSchemeHttps, Port = -1, Fragment = string.Empty }.Uri;
        return true;
    }

    private static string? VideoId(Uri uri)
    {
        var host = uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string? id = null;
        if (host.Equals(ShortHost, StringComparison.OrdinalIgnoreCase))
        {
            id = segments.Length == 1 ? segments[0] : null;
        }
        else if (FullHosts.Contains(host))
        {
            if (uri.AbsolutePath.Equals(WatchPath, StringComparison.OrdinalIgnoreCase))
            {
                id = QueryValue(uri.Query, VideoQueryKey);
            }
            else if (segments.Length == 2 && IdPathPrefixes.Contains(segments[0]))
            {
                id = segments[1];
            }
        }

        return id is not null && VideoIdPattern().IsMatch(id) ? id : null;
    }

    private static string? QueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && pair[..separator] == key)
            {
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }

    // YouTube video ids are 11 characters of the URL-safe base64 alphabet.
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdPattern();
}
