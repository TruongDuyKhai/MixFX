using System.IO;

namespace Soundboard.App.Tests.YouTube;

/// <summary>A fresh folder per test so tests never touch the real %AppData%\Soundboard.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = Directory.CreateTempSubdirectory("soundboard-app-tests-").FullName;
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
