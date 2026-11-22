using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Storage;

/// <summary>
/// Library copies live flat in <see cref="StoragePaths.LibraryDirectory"/> as <c>{guid}.{ext}</c>.
/// Every path goes through <see cref="ResolveLibraryFile"/>, so nothing outside the library can be
/// read, overwritten or deleted (PROJECT_PROMPT.md §5.3, §7.4.12).
/// </summary>
public sealed class SoundLibrary : ISoundLibrary
{
    private const int CopyBufferBytes = 81_920;

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

    public async Task<string> DuplicateAsync(string libraryFile, Guid newSoundId, CancellationToken cancellationToken = default)
    {
        var source = ResolveLibraryFile(libraryFile);
        var newName = LibraryFileName(newSoundId, Path.GetExtension(libraryFile));
        await CopyNewAsync(source, ResolveLibraryFile(newName), cancellationToken).ConfigureAwait(false);
        return newName;
    }

    public void Delete(string libraryFile)
    {
        var path = ResolveLibraryFile(libraryFile);
        File.Delete(path);
        File.Delete(Path.ChangeExtension(path, PeaksExtension));
    }

    public async Task<string> ImportAsync(string sourcePath, Guid soundId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!SupportedAudioFormats.IsSupported(sourcePath))
        {
            throw new NotSupportedException($"'{Path.GetFileName(sourcePath)}' không phải định dạng âm thanh được hỗ trợ.");
        }

        var name = LibraryFileName(soundId, Path.GetExtension(sourcePath));
        await CopyNewAsync(sourcePath, ResolveLibraryFile(name), cancellationToken).ConfigureAwait(false);
        return name;
    }

    public string GetPeaksPath(string libraryFile) => Path.ChangeExtension(ResolveLibraryFile(libraryFile), PeaksExtension);

    private const string PeaksExtension = ".peaks";

    internal static string LibraryFileName(Guid soundId, string extension) =>
        soundId.ToString("D") + extension.ToLowerInvariant();

    /// <summary>
    /// Accepts only a bare file name. settings.json can be hand-edited, and a value such as
    /// "..\..\Documents\x.wav" must never make the app read, overwrite or delete a file outside its library.
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

    /// <summary>Copies to a destination that must not exist yet (FileMode.CreateNew), so no existing file is ever overwritten.</summary>
    internal static async Task CopyNewAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferBytes, useAsync: true);
        await using (input.ConfigureAwait(false))
        {
            var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferBytes, useAsync: true);
            var completed = false;
            try
            {
                await using (output.ConfigureAwait(false))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }

                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    // Never leave a half-copied file in the library.
                    File.Delete(destination);
                }
            }
        }
    }
}
