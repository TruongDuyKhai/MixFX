using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.Services;
using Soundboard.App.ViewModels;
using Soundboard.Core.Downloads;

namespace Soundboard.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Regression: App.OnExit disposed the view model and then the DI container disposed it again;
        // the second call threw and aborted host disposal, so the engine and hooks were not released cleanly.
        var viewModel = Create();
        viewModel.Start();

        viewModel.Dispose();
        var second = viewModel.Dispose;

        second.Should().NotThrow();
    }

    [Fact]
    public async Task ImportDownloaded_UnreadableFile_ReportsItAndStillDeletesTheDownload()
    {
        var downloader = new FakeAudioDownloader();
        var dialogs = new RecordingDialogs();
        var viewModel = Create(downloader, dialogs);
        var audio = new DownloadedAudio(@"C:\downloads\job\jNQXAC9IVRw.m4a", "Me at the zoo");

        await viewModel.ImportDownloadedAsync(audio);

        downloader.Deleted.Should().ContainSingle().Which.Should().Be(audio);
        dialogs.Errors.Should().ContainSingle().Which.Should().Contain("Me at the zoo");
    }

    private static MainViewModel Create(FakeAudioDownloader? downloader = null, IDialogService? dialogs = null)
    {
        var engine = new StubEngine();
        var settings = new StubSettings();
        return new MainViewModel(
            engine,
            settings,
            new StubDispatcher(),
            new StubLibrary(),
            new StubWaveforms(),
            downloader ?? new FakeAudioDownloader(),
            dialogs ?? new StubDialogs(),
            new StubStartup(),
            new Soundboard.Core.Playlist.PlaylistController(engine, settings),
            NullLogger<MainViewModel>.Instance);
    }
}
