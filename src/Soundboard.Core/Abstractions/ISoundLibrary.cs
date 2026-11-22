using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>
/// The app's private copies of imported sounds (<c>%AppData%\Soundboard\library</c>). The user's
/// original files are never touched (PROJECT_PROMPT.md §5.3, §7.4.12).
/// </summary>
public interface ISoundLibrary
{
    /// <summary>Absolute path of the library copy behind <paramref name="sound"/>.</summary>
    string GetFilePath(SoundItem sound);

    /// <summary>Copies a library file for a duplicated sound; returns the new library file name.</summary>
    Task<string> DuplicateAsync(string libraryFile, Guid newSoundId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a library copy (and anything cached for it). Never touches files outside the library.</summary>
    void Delete(string libraryFile);
}
