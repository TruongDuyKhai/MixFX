using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Storage;

/// <summary>Library copies live flat in <see cref="StoragePaths.LibraryDirectory"/> as <c>{guid}.{ext}</c>.</summary>
public sealed class SoundLibrary : ISoundLibrary
{
    private readonly StoragePaths _paths;

    public SoundLibrary(StoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public string GetFilePath(SoundItem sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        return ResolveLibraryFile(sound.LibraryFile);
    }

    /// <summary>
    /// Accepts only a bare file name. settings.json can be hand-edited, and a value such as
    /// "..\..\Documents\x.wav" must never make the app read (or later delete) a file outside its library.
    /// </summary>
    internal string ResolveLibraryFile(string libraryFile)
    {
        if (string.IsNullOrWhiteSpace(libraryFile)
            || libraryFile != Path.GetFileName(libraryFile)
            || libraryFile is "." or "..")
        {
            throw new ArgumentException($"'{libraryFile}' is not a library file name.", nameof(libraryFile));
        }

        return Path.Combine(_paths.LibraryDirectory, libraryFile);
    }
}
