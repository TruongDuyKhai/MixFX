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

    private static SoundItem Sound(string libraryFile) =>
        new(Guid.NewGuid(), "Test", libraryFile, "#FFFFFF", 1f, PlaybackKind.Preloaded, EditParams.None);
}
