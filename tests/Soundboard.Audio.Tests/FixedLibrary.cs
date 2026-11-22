using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests;

/// <summary>Library stub that maps every sound to one existing audio file.</summary>
internal sealed class FixedLibrary(string path) : ISoundLibrary
{
    public string GetFilePath(SoundItem sound) => path;

    public string GetPeaksPath(string libraryFile) => path + ".peaks";

    public Task<string> DuplicateAsync(string libraryFile, Guid newSoundId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Delete(string libraryFile) => throw new NotSupportedException();

    public Task<string> ImportAsync(string sourcePath, Guid soundId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
