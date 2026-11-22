namespace Soundboard.Storage.Tests;

/// <summary>A fresh folder per test so tests never touch the real %AppData%\Soundboard.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = Directory.CreateTempSubdirectory("soundboard-tests-").FullName;
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
