using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.YouTube;
using Soundboard.Core.Downloads;
using Soundboard.Storage;

namespace Soundboard.App.Tests.YouTube;

public sealed class YtDlpDownloaderTests : IDisposable
{
    private readonly TempFolder _dir = new();
    private readonly StoragePaths _paths;
    private readonly DownloadTools _tools;
    private readonly YtDlpDownloader _downloader;

    public YtDlpDownloaderTests()
    {
        _paths = new StoragePaths(_dir.Path);
        _tools = new DownloadTools(_paths, new HttpClient(new FakeGitHub()), TimeProvider.System, NullLogger<DownloadTools>.Instance);
        _downloader = new YtDlpDownloader(_tools, _paths, NullLogger<YtDlpDownloader>.Instance);
    }

    public void Dispose()
    {
        _downloader.Dispose();
        _tools.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public async Task Download_WithoutTools_AsksForThemFirst()
    {
        var act = () => _downloader.DownloadAudioAsync(new Uri("https://youtu.be/jNQXAC9IVRw"), null, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AudioDownloadException>()).Which.Message.Should().Contain("Tải công cụ");
    }

    [Fact]
    public async Task Download_NonYouTubeUrl_IsRefusedBeforeAnythingRuns()
    {
        var act = () => _downloader.DownloadAudioAsync(new Uri("https://evil.example/watch?v=jNQXAC9IVRw"), null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<AudioDownloadException>();
        Directory.Exists(_paths.DownloadsDirectory).Should().BeFalse();
    }

    [Fact]
    public void Delete_RemovesTheJobFolder()
    {
        var job = Path.Combine(_paths.DownloadsDirectory, "job1");
        Directory.CreateDirectory(job);
        var file = Path.Combine(job, "jNQXAC9IVRw.m4a");
        File.WriteAllText(file, "audio");

        _downloader.Delete(new DownloadedAudio(file, "Me at the zoo"));

        Directory.Exists(job).Should().BeFalse();
        Directory.Exists(_paths.DownloadsDirectory).Should().BeTrue("only the job folder goes");
    }

    [Fact]
    public void Delete_NeverTouchesFilesOutsideTheDownloadsFolder()
    {
        // e.g. a user's own music: the library import reads it, nothing may delete it (§7.4.12).
        var userFolder = Path.Combine(_dir.Path, "Music");
        Directory.CreateDirectory(userFolder);
        var userFile = Path.Combine(userFolder, "song.m4a");
        File.WriteAllText(userFile, "mine");
        var downloadsItself = Path.Combine(_paths.DownloadsDirectory, "song.m4a");
        Directory.CreateDirectory(_paths.DownloadsDirectory);
        File.WriteAllText(downloadsItself, "x");

        _downloader.Delete(new DownloadedAudio(userFile, "song"));
        _downloader.Delete(new DownloadedAudio(downloadsItself, "song"));
        _downloader.Delete(new DownloadedAudio(Path.Combine(_paths.DownloadsDirectory, "job", "..", "..", "Music", "song.m4a"), "song"));

        File.Exists(userFile).Should().BeTrue();
        File.Exists(downloadsItself).Should().BeTrue("the downloads root is not a job folder");
    }
}
