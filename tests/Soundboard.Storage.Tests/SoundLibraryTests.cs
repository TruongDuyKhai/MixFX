using Soundboard.Core.Models;

namespace Soundboard.Storage.Tests;

public sealed class SoundLibraryTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly StoragePaths _paths;
    private readonly SoundLibrary _library;

    public SoundLibraryTests()
    {
        _paths = new StoragePaths(_dir.Path);
        _library = new SoundLibrary(_paths);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void GetFilePath_PlainFileName_ResolvesInsideLibrary()
    {
        var id = Guid.NewGuid();

        var path = _library.GetFilePath(Sound($"{id}.mp3"));

        path.Should().Be(Path.Combine(_paths.LibraryDirectory, $"{id}.mp3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..")]
    [InlineData(@"..\settings.json")]
    [InlineData(@"..\..\Documents\song.mp3")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("sub/file.wav")]
    public void GetFilePath_AnythingButAPlainFileName_Throws(string libraryFile)
    {
        var act = () => _library.GetFilePath(Sound(libraryFile));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Duplicate_CopiesUnderTheNewIdAndKeepsTheOriginal()
    {
        var original = WriteLibraryFile($"{Guid.NewGuid()}.wav", [1, 2, 3]);
        var newId = Guid.NewGuid();

        var copy = await _library.DuplicateAsync(original, newId, Ct);

        copy.Should().Be($"{newId}.wav");
        File.ReadAllBytes(Path.Combine(_paths.LibraryDirectory, copy)).Should().Equal(1, 2, 3);
        File.ReadAllBytes(Path.Combine(_paths.LibraryDirectory, original)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Duplicate_NeverOverwritesAnExistingFile()
    {
        var original = WriteLibraryFile($"{Guid.NewGuid()}.wav", [1]);
        var newId = Guid.NewGuid();
        WriteLibraryFile($"{newId}.wav", [9, 9]);

        var act = () => _library.DuplicateAsync(original, newId, Ct);

        await act.Should().ThrowAsync<IOException>();
        File.ReadAllBytes(Path.Combine(_paths.LibraryDirectory, $"{newId}.wav")).Should().Equal(9, 9);
    }

    [Fact]
    public void Delete_RemovesOnlyTheLibraryCopy()
    {
        var file = WriteLibraryFile($"{Guid.NewGuid()}.wav", [1]);
        var outside = Path.Combine(_dir.Path, "user-original.wav");
        File.WriteAllBytes(outside, [7]);

        _library.Delete(file);
        var escape = () => _library.Delete(@"..\user-original.wav");

        File.Exists(Path.Combine(_paths.LibraryDirectory, file)).Should().BeFalse();
        escape.Should().Throw<ArgumentException>();
        File.Exists(outside).Should().BeTrue();
    }

    [Fact]
    public async Task Import_CopiesIntoLibraryAndNeverTouchesTheOriginal()
    {
        var original = Path.Combine(_dir.Path, "My Horn.MP3");
        File.WriteAllBytes(original, [4, 5, 6]);
        var writtenAt = File.GetLastWriteTimeUtc(original);
        var id = Guid.NewGuid();

        var name = await _library.ImportAsync(original, id, Ct);

        name.Should().Be($"{id}.mp3");
        File.ReadAllBytes(Path.Combine(_paths.LibraryDirectory, name)).Should().Equal(4, 5, 6);
        File.ReadAllBytes(original).Should().Equal(4, 5, 6);
        File.GetLastWriteTimeUtc(original).Should().Be(writtenAt);
    }

    [Fact]
    public async Task Import_UnsupportedType_IsRejectedWithoutCopying()
    {
        var original = Path.Combine(_dir.Path, "notes.txt");
        File.WriteAllText(original, "hi");

        var act = () => _library.ImportAsync(original, Guid.NewGuid(), Ct);

        await act.Should().ThrowAsync<NotSupportedException>();
        Directory.Exists(_paths.LibraryDirectory).Should().BeFalse();
    }

    [Fact]
    public void Delete_AlsoRemovesTheWaveformCache()
    {
        var file = WriteLibraryFile($"{Guid.NewGuid()}.wav", [1]);
        var peaks = _library.GetPeaksPath(file);
        File.WriteAllBytes(peaks, [2]);

        _library.Delete(file);

        peaks.Should().EndWith(".peaks");
        File.Exists(peaks).Should().BeFalse();
    }

    private string WriteLibraryFile(string name, byte[] content)
    {
        Directory.CreateDirectory(_paths.LibraryDirectory);
        File.WriteAllBytes(Path.Combine(_paths.LibraryDirectory, name), content);
        return name;
    }

    private static SoundItem Sound(string libraryFile) =>
        new(Guid.NewGuid(), "Test", libraryFile, "#FFFFFF", 1f, PlaybackKind.Preloaded, EditParams.None);
}
