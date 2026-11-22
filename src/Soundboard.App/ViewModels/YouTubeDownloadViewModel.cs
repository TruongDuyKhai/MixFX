using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Downloads;

namespace Soundboard.App.ViewModels;

/// <summary>
/// "Tải từ YouTube" (§5.8, ADR 0005): asks before the first tool download, updates the tools when due,
/// then fetches the audio of one video. The window closes with <see cref="Result"/> set on success;
/// the caller imports the file like any other.
/// </summary>
public sealed class YouTubeDownloadViewModel : ObservableObject
{
    private readonly IAudioDownloader _downloader;
    private readonly ILogger<YouTubeDownloadViewModel> _logger;
    private readonly SynchronizationContext? _ui;

    private CancellationTokenSource? _running;
    private string _url = string.Empty;
    private string _statusText = string.Empty;
    private string _toolsText = "Đang kiểm tra công cụ…";
    private bool _isError;
    private bool _isBusy;
    private bool _toolsReady;
    private bool _needsTools;
    private double _progress;
    private bool _isProgressKnown;

    public YouTubeDownloadViewModel(IAudioDownloader downloader, ILogger<YouTubeDownloadViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(logger);

        _downloader = downloader;
        _logger = logger;
        _ui = SynchronizationContext.Current;

        InstallToolsCommand = new AsyncRelayCommand(InstallToolsAsync, () => NeedsTools && !IsBusy);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => ToolsReady && !IsBusy && Url.Trim().Length > 0);
        CancelCommand = new RelayCommand(CancelOrClose);
    }

    /// <summary>Raised when the window should close: after a successful download, or on "Đóng".</summary>
    public event EventHandler? CloseRequested;

    public ICommand InstallToolsCommand { get; }

    public ICommand DownloadCommand { get; }

    public ICommand CancelCommand { get; }

    /// <summary>The downloaded file, set when the window closes after a successful download.</summary>
    public DownloadedAudio? Result { get; private set; }

    public string Notice { get; } = string.Format(
        CultureInfo.InvariantCulture,
        "Chỉ tải nội dung bạn có quyền sử dụng. App chỉ lấy phần âm thanh, video tối đa {0:0} phút.",
        DownloadLimits.MaxDuration.TotalMinutes);

    public string ConsentText { get; } =
        "Để tải từ YouTube, Soundboard cần yt-dlp (khoảng 25 MB) và Deno (khoảng 45 MB). "
        + "Hai công cụ được tải từ trang GitHub chính thức của chúng, kiểm tra mã SHA-256, rồi lưu trong "
        + "%AppData%\\Soundboard\\tools. Mỗi ngày bạn dùng tính năng này, app tự kiểm tra và cập nhật chúng.";

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value ?? string.Empty))
            {
                RefreshCommands();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public string ToolsText
    {
        get => _toolsText;
        private set => SetProperty(ref _toolsText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CancelText));
                OnPropertyChanged(nameof(IsProgressIndeterminate));
                RefreshCommands();
            }
        }
    }

    public string CancelText => IsBusy ? "Hủy" : "Đóng";

    public bool ToolsReady
    {
        get => _toolsReady;
        private set
        {
            if (SetProperty(ref _toolsReady, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool NeedsTools
    {
        get => _needsTools;
        private set
        {
            if (SetProperty(ref _needsTools, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>0…1 while <see cref="IsProgressKnown"/>; the bar is indeterminate otherwise.</summary>
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public bool IsProgressKnown
    {
        get => _isProgressKnown;
        private set
        {
            if (SetProperty(ref _isProgressKnown, value))
            {
                OnPropertyChanged(nameof(IsProgressIndeterminate));
            }
        }
    }

    public bool IsProgressIndeterminate => IsBusy && !IsProgressKnown;

    /// <summary>Checks the tools when the window opens and updates them when a check is due.</summary>
    public async Task InitializeAsync()
    {
        var status = await _downloader.GetToolsStatusAsync();
        ApplyStatus(status);
        if (status.IsUpdateDue)
        {
            // Old yt-dlp versions stop working when YouTube changes; a failed check still leaves a usable copy.
            await RunAsync(
                token => _downloader.InstallToolsAsync(CreateProgress(), token),
                "Không kiểm tra được bản cập nhật, vẫn dùng bản đang có.",
                warnOnly: true);
        }
    }

    /// <summary>Called when the window is closing: a running download is cancelled and cleaned up.</summary>
    public void CancelRunning() => _running?.Cancel();

    private async Task InstallToolsAsync() =>
        await RunAsync(token => _downloader.InstallToolsAsync(CreateProgress(), token), failureMessage: null, warnOnly: false);

    private async Task DownloadAsync()
    {
        if (!YouTubeUrl.TryParse(Url, out var url))
        {
            ShowError("Link không hợp lệ. Hãy dán link của một video YouTube, ví dụ https://www.youtube.com/watch?v=…");
            return;
        }

        DownloadedAudio? downloaded = null;
        await RunAsync(
            async token => downloaded = await _downloader.DownloadAudioAsync(url!, CreateProgress(), token),
            failureMessage: null,
            warnOnly: false);

        if (downloaded is not null)
        {
            Result = downloaded;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Runs one operation with busy state, cancel support and user-facing errors.</summary>
    private async Task RunAsync(Func<CancellationToken, Task> operation, string? failureMessage, bool warnOnly)
    {
        using var running = new CancellationTokenSource();
        _running = running;
        IsBusy = true;
        IsError = false;
        try
        {
            await operation(running.Token);
            StatusText = string.Empty;
        }
        catch (OperationCanceledException) when (running.IsCancellationRequested)
        {
            StatusText = "Đã hủy.";
        }
        catch (AudioDownloadException ex)
        {
            _logger.LogWarning(ex, "YouTube download step failed");
            if (warnOnly)
            {
                StatusText = failureMessage ?? ex.Message;
            }
            else
            {
                ShowError(failureMessage ?? ex.Message);
            }
        }
        finally
        {
            _running = null;
            IsBusy = false;
            IsProgressKnown = false;
            Progress = 0;
            ApplyStatus(await _downloader.GetToolsStatusAsync());
        }
    }

    private void ApplyStatus(DownloadToolsStatus status)
    {
        ToolsReady = status.IsInstalled;
        NeedsTools = !status.IsInstalled;
        ToolsText = status.IsInstalled ? $"yt-dlp {status.Version}" : "Chưa có công cụ tải";
    }

    private void ShowError(string message)
    {
        StatusText = message;
        IsError = true;
    }

    private void OnProgress(DownloadProgress progress)
    {
        StatusText = progress.Message;
        IsError = false;
        IsProgressKnown = progress.Fraction is not null;
        Progress = progress.Fraction ?? 0;
    }

    private IProgress<DownloadProgress> CreateProgress() => new UiProgress(_ui, OnProgress);

    private void CancelOrClose()
    {
        if (_running is { } running)
        {
            running.Cancel();
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshCommands()
    {
        ((AsyncRelayCommand)InstallToolsCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)DownloadCommand).NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Like <see cref="Progress{T}"/>, but runs the handler inline when there is no UI context (tests),
    /// instead of on the thread pool where updates could land after the operation has finished.
    /// </summary>
    private sealed class UiProgress(SynchronizationContext? context, Action<DownloadProgress> handler) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            if (context is null)
            {
                handler(value);
            }
            else
            {
                context.Post(_ => handler(value), null);
            }
        }
    }
}
