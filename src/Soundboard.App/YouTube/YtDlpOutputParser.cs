using System.Globalization;

namespace Soundboard.App.YouTube;

/// <summary>
/// Reads the lines yt-dlp prints for <see cref="YtDlpArguments"/>. Only the app's own prefixed lines and
/// yt-dlp's "ERROR:" lines are interpreted; yt-dlp's other output is free text that changes between versions.
/// </summary>
public static class YtDlpOutputParser
{
    private const string ErrorPrefix = "ERROR: ";
    private const string NotAvailable = "NA";

    /// <summary>Parses "SBINFO &lt;duration&gt; &lt;is_live&gt; &lt;title&gt;". The title is last because it may contain spaces.</summary>
    public static bool TryParseInfo(string line, out YtDlpVideoInfo? info)
    {
        info = null;
        if (!TryStrip(line, YtDlpArguments.InfoPrefix, out var rest))
        {
            return false;
        }

        var parts = rest.Split(' ', 3);
        if (parts.Length < 2)
        {
            return false;
        }

        TimeSpan? duration = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
        var isLive = parts[1].Equals("True", StringComparison.OrdinalIgnoreCase);
        var title = parts.Length == 3 ? parts[2].Trim() : string.Empty;
        info = new YtDlpVideoInfo(duration, isLive, title == NotAvailable ? string.Empty : title);
        return true;
    }

    /// <summary>
    /// Parses "SBPROGRESS &lt;downloaded&gt; &lt;total&gt;". <paramref name="fraction"/> is null when the size is unknown.
    /// </summary>
    public static bool TryParseProgress(string line, out double? fraction)
    {
        fraction = null;
        if (!TryStrip(line, YtDlpArguments.ProgressPrefix, out var rest))
        {
            return false;
        }

        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var done)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var total)
            && total > 0)
        {
            fraction = Math.Clamp(done / total, 0, 1);
        }

        return true;
    }

    /// <summary>Parses "SBFILE &lt;path&gt;", printed once the file is in its final place.</summary>
    public static bool TryParseFile(string line, out string? path)
    {
        path = TryStrip(line, YtDlpArguments.FilePrefix, out var rest) && rest.Trim().Length > 0 ? rest.Trim() : null;
        return path is not null;
    }

    /// <summary>Parses yt-dlp's "ERROR: …" lines.</summary>
    public static bool TryParseError(string line, out string? message)
    {
        message = TryStrip(line, ErrorPrefix, out var rest) && rest.Trim().Length > 0 ? rest.Trim() : null;
        return message is not null;
    }

    private static bool TryStrip(string line, string prefix, out string rest)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
            rest = line[prefix.Length..];
            return true;
        }

        rest = string.Empty;
        return false;
    }
}
