namespace Soundboard.Core.Models;

/// <summary>File types the importer accepts (PROJECT_PROMPT.md §3: wav, mp3, ogg, flac; aac/m4a/wma via Media Foundation).</summary>
public static class SupportedAudioFormats
{
    public static IReadOnlyList<string> Extensions { get; } = [".wav", ".mp3", ".ogg", ".flac", ".aac", ".m4a", ".wma"];

    public static bool IsSupported(string path) =>
        !string.IsNullOrEmpty(path) && Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
