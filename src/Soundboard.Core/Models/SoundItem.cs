namespace Soundboard.Core.Models;

/// <summary>A sound on the board. <see cref="LibraryFile"/> is the app's own copy, never the user's original.</summary>
public sealed record SoundItem(
    Guid Id,
    string Name,
    string LibraryFile,
    string Color,
    float Volume,
    PlaybackKind Kind,
    EditParams Edit);
