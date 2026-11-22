using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.ViewModels;
using Soundboard.Core.Downloads;

namespace Soundboard.App.Tests.YouTube;

public sealed class YouTubeDownloadViewModelTests
{
    private readonly FakeAudioDownloader _downloader = new();

    private YouTubeDownloadViewModel Create() => new(_downloader, NullLogger<YouTubeDownloadViewModel>.Instance);

    [Fact]
    public async Task ToolsMissing_AsksBeforeDownloadingAnything()
    {
        _downloader.Status = new DownloadToolsStatus(false, false, null);
        var vm = Create();

        await vm.InitializeAsync();

        vm.NeedsTools.Should().BeTrue();
        vm.ToolsReady.Should().BeFalse();
        vm.DownloadCommand.CanExecute(null).Should().BeFalse();
        _downloader.InstallCalls.Should().Be(0, "tools are only fetched after the user agrees");
    }

    [Fact]
    public async Task InstallTools_AfterConsent_EnablesDownloading()
    {
        _downloader.Status = new DownloadToolsStatus(false, false, null);
        var vm = Create();
        await vm.InitializeAsync();
        vm.Url = "https://youtu.be/jNQXAC9IVRw";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.InstallToolsCommand).ExecuteAsync(null);

        _downloader.InstallCalls.Should().Be(1);
        vm.NeedsTools.Should().BeFalse();
        vm.DownloadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDue_UpdatesOnOpen()
    {
        _downloader.Status = new DownloadToolsStatus(true, true, "2026.10.26");
        var vm = Create();

        await vm.InitializeAsync();

        _downloader.InstallCalls.Should().Be(1);
        vm.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateFailure_IsOnlyAWarning_TheOldToolsStayUsable()
    {
        _downloader.Status = new DownloadToolsStatus(true, true, "2026.10.26");
        _downloader.OnInstall = (_, _) => throw new AudioDownloadException("offline");
        var vm = Create();
        vm.Url = "https://youtu.be/jNQXAC9IVRw";

        await vm.InitializeAsync();

        vm.IsError.Should().BeFalse();
        vm.StatusText.Should().Contain("vẫn dùng bản đang có");
        vm.DownloadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task InvalidLink_ShowsErrorAndDownloadsNothing()
    {
        var vm = Create();
        await vm.InitializeAsync();
        vm.Url = "https://example.com/video";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DownloadCommand).ExecuteAsync(null);

        vm.IsError.Should().BeTrue();
        _downloader.DownloadedUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessfulDownload_SetsResultAndClosesTheWindow()
    {
        var expected = new DownloadedAudio(@"C:\d\job\jNQXAC9IVRw.m4a", "Me at the zoo");
        _downloader.OnDownload = (_, progress, _) =>
        {
            progress?.Report(new DownloadProgress("Đang tải âm thanh… 50%", 0.5));
            return Task.FromResult(expected);
        };
        var vm = Create();
        await vm.InitializeAsync();
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        vm.Url = "  youtu.be/jNQXAC9IVRw  ";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DownloadCommand).ExecuteAsync(null);

        vm.Result.Should().Be(expected);
        closed.Should().BeTrue();
        _downloader.DownloadedUrls.Should().ContainSingle().Which.AbsoluteUri.Should().Be("https://youtu.be/jNQXAC9IVRw");
    }

    [Fact]
    public async Task FailedDownload_ShowsTheMessageAndKeepsTheWindowOpen()
    {
        _downloader.OnDownload = (_, _, _) => throw new AudioDownloadException("Video dài 45:00, quá giới hạn 20 phút.");
        var vm = Create();
        await vm.InitializeAsync();
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        vm.Url = "https://youtu.be/jNQXAC9IVRw";

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DownloadCommand).ExecuteAsync(null);

        vm.IsError.Should().BeTrue();
        vm.StatusText.Should().Contain("quá giới hạn");
        vm.Result.Should().BeNull();
        closed.Should().BeFalse();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_DuringDownload_CancelsInsteadOfClosing()
    {
        var started = new TaskCompletionSource();
        _downloader.OnDownload = async (_, _, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        };
        var vm = Create();
        await vm.InitializeAsync();
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        vm.Url = "https://youtu.be/jNQXAC9IVRw";

        var download = ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DownloadCommand).ExecuteAsync(null);
        await started.Task;
        vm.IsBusy.Should().BeTrue();
        vm.CancelText.Should().Be("Hủy");
        vm.CancelCommand.Execute(null);
        await download;

        closed.Should().BeFalse();
        vm.StatusText.Should().Be("Đã hủy.");
        vm.Result.Should().BeNull();
        vm.CancelText.Should().Be("Đóng");
    }
}
