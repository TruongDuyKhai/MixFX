using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.Services;
using Soundboard.App.ViewModels;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Downloads;
using Soundboard.Core.Models;

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

    [Fact]
    public void Close_ByDefault_AsksEachTimeWithoutStoringTheAnswer()
    {
        var settings = new MutableSettings(new AppSettings());
        var dialogs = new RecordingDialogs { NextCloseChoice = new CloseChoice(CloseAction.MinimizeToTray, Remember: false) };
        using var viewModel = Create(dialogs: dialogs, settings: settings);

        viewModel.ResolveCloseRequest().Should().Be(CloseAction.MinimizeToTray);
        viewModel.ResolveCloseRequest().Should().Be(CloseAction.MinimizeToTray);

        dialogs.CloseQuestions.Should().Be(2);
        settings.Current.Window.CloseAction.Should().Be(CloseAction.Ask);
    }

    [Theory]
    [InlineData(CloseAction.MinimizeToTray)]
    [InlineData(CloseAction.Exit)]
    public void Close_DontAskAgain_StoresTheAnswerAndStopsAsking(CloseAction answer)
    {
        var settings = new MutableSettings(new AppSettings());
        var dialogs = new RecordingDialogs { NextCloseChoice = new CloseChoice(answer, Remember: true) };
        using var viewModel = Create(dialogs: dialogs, settings: settings);

        viewModel.ResolveCloseRequest().Should().Be(answer);
        viewModel.ResolveCloseRequest().Should().Be(answer);

        dialogs.CloseQuestions.Should().Be(1);
        settings.Current.Window.CloseAction.Should().Be(answer);
    }

    [Fact]
    public void Close_Cancelled_StaysOpenAndStoresNothing()
    {
        var settings = new MutableSettings(new AppSettings());
        var dialogs = new RecordingDialogs { NextCloseChoice = null };
        using var viewModel = Create(dialogs: dialogs, settings: settings);

        viewModel.ResolveCloseRequest().Should().BeNull();

        settings.Current.Window.CloseAction.Should().Be(CloseAction.Ask);
    }

    [Fact]
    public void Close_UnknownStoredValue_IsTreatedAsAsk()
    {
        // settings.json is hand-editable and enums also deserialize from numbers.
        var settings = new MutableSettings(new AppSettings { Window = new WindowSettings { CloseAction = (CloseAction)42 } });
        var dialogs = new RecordingDialogs { NextCloseChoice = new CloseChoice(CloseAction.Exit, Remember: false) };
        using var viewModel = Create(dialogs: dialogs, settings: settings);

        viewModel.ResolveCloseRequest().Should().Be(CloseAction.Exit);

        dialogs.CloseQuestions.Should().Be(1);
    }

    [Fact]
    public void ClosePicker_FollowsSettings_AndChangingItBringsTheQuestionBack()
    {
        var settings = new MutableSettings(new AppSettings());
        var dialogs = new RecordingDialogs { NextCloseChoice = new CloseChoice(CloseAction.MinimizeToTray, Remember: true) };
        using var viewModel = Create(dialogs: dialogs, settings: settings);
        viewModel.Start();
        viewModel.SelectedCloseAction!.Action.Should().Be(CloseAction.Ask);

        viewModel.ResolveCloseRequest();
        viewModel.SelectedCloseAction!.Action.Should().Be(CloseAction.MinimizeToTray, "the picker shows the remembered answer");

        viewModel.SelectedCloseAction = CloseActionOption.For(CloseAction.Ask);
        viewModel.ResolveCloseRequest();

        settings.Current.Window.CloseAction.Should().Be(CloseAction.MinimizeToTray);
        dialogs.CloseQuestions.Should().Be(2, "choosing \"Hỏi mỗi lần\" asks again");
    }

    private static MainViewModel Create(FakeAudioDownloader? downloader = null, IDialogService? dialogs = null, ISettingsProvider? settings = null)
    {
        var engine = new StubEngine();
        settings ??= new StubSettings();
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
