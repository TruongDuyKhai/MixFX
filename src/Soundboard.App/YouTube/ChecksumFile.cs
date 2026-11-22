using System.Text.RegularExpressions;

namespace Soundboard.App.YouTube;

/// <summary>
/// Reads the SHA-256 of one file from a release's checksum file. Two formats are published:
/// <c>sha256sum</c> lines (<c>&lt;hex&gt;  name</c>, used by yt-dlp) and PowerShell <c>Get-FileHash</c>
/// output (<c>Hash : &lt;hex&gt;</c> / <c>Path : …\name</c>, used by Deno).
/// </summary>
public static partial class ChecksumFile
{
    /// <summary>The lowercase hex SHA-256 listed for <paramref name="fileName"/>, or null when it is not listed.</summary>
    public static string? FindSha256(string content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string? pendingHash = null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            var sum = SumLine().Match(line);
            if (sum.Success)
            {
                if (sum.Groups["name"].Value == fileName)
                {
                    return sum.Groups["hash"].Value.ToLowerInvariant();
                }

                continue;
            }

            var field = FieldLine().Match(line);
            if (!field.Success)
            {
                continue;
            }

            var value = field.Groups["value"].Value;
            if (field.Groups["key"].Value.Equals("Hash", StringComparison.OrdinalIgnoreCase))
            {
                pendingHash = Sha256Hex().IsMatch(value) ? value.ToLowerInvariant() : null;
            }
            else if (field.Groups["key"].Value.Equals("Path", StringComparison.OrdinalIgnoreCase)
                && pendingHash is not null
                && FileNameOf(value) == fileName)
            {
                return pendingHash;
            }
        }

        return null;
    }

    // The path was written on the publisher's machine, so take the last segment of either separator.
    private static string FileNameOf(string path) => path[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    [GeneratedRegex(@"^(?<hash>[0-9A-Fa-f]{64}) [ *](?<name>\S.*)$")]
    private static partial Regex SumLine();

    [GeneratedRegex(@"^(?<key>[A-Za-z]+)\s*:\s*(?<value>.+)$")]
    private static partial Regex FieldLine();

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")]
    private static partial Regex Sha256Hex();
}
