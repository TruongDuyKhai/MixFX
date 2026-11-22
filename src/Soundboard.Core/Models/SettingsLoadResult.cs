namespace Soundboard.Core.Models;

/// <summary>Settings plus what happened while loading them, so the UI can tell the user.</summary>
/// <param name="Settings">Settings to use for this session.</param>
/// <param name="Status">How they were obtained.</param>
/// <param name="PreservedFiles">Unreadable files that were renamed aside instead of being overwritten.</param>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    IReadOnlyList<string> PreservedFiles);
