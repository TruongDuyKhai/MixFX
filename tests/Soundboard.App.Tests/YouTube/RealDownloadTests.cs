using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Soundboard.App.YouTube;
using Soundboard.Core.Downloads;
using Soundboard.Storage;

namespace Soundboard.App.Tests.YouTube;

/// <summary>
/// Hits the real GitHub and YouTube: downloads yt-dlp and Deno (~70 MB) into a temp folder, then the 19-second
/// "Me at the zoo" video. Explicit, so CI and normal runs never touch the network. Run it with
/// <c>Soundboard.App.Tests.exe -explicit only -class Soundboard.App.Tests.YouTube.RealDownloadTests</c>.
/// </summary>
public sealed class RealDownloadTests : IDisposable
{
    private const string ShortVideo = "https://www.youtube.com/watch?v=jNQXAC9IVRw";

    private readonly TempFolder _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact(Explicit = true)]
    public async Task InstallsTools_ThenDownloadsDecodableAudio()
    {
        var ct = TestContext.Current.CancellationToken;
        var paths = new StoragePaths(_dir.Path);
        using var tools = new DownloadTools(paths, DownloadTools.CreateHttpClient(), TimeProvider.System, NullLogger<DownloadTools>.Instance);
        var log = new ListLogger();
        using var downloader = new YtDlpDownloader(tools, paths, log);
        var reports = new List<DownloadProgress>();
        var progress = new Recorder(reports);

        await downloader.InstallToolsAsync(progress, ct);
        var status = await downloader.GetToolsStatusAsync(ct);
        YouTubeUrl.TryParse(ShortVideo, out var url).Should().BeTrue();
        var audio = await downloader.DownloadAudioAsync(url!, progress, ct);

        status.IsInstalled.Should().BeTrue();
        audio.Title.Should().Be("Me at the zoo");
        Path.GetExtension(audio.FilePath).Should().Be(".m4a");
        using (var reader = new MediaFoundationReader(audio.FilePath))
        {
            reader.TotalTime.Should().BeCloseTo(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(1));
        }

        reports.Should().Contain(r => r.Fraction > 0 && r.Message.StartsWith("Đang tải âm thanh", StringComparison.Ordinal));

        // A short old video can download without Deno; this proves the app's Deno really solved YouTube's challenges.
        log.Lines.Should().NotContain(l => l.Contains("challenge solving failed", StringComparison.OrdinalIgnoreCase)
            || l.Contains("JavaScript runtime", StringComparison.OrdinalIgnoreCase));

        downloader.Delete(audio);
        File.Exists(audio.FilePath).Should().BeFalse();

        // Cancel mid-run: yt-dlp and its Deno child must be gone and the job folder removed.
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cancel.CancelAfter(TimeSpan.FromSeconds(1.5));
        var cancelled = () => downloader.DownloadAudioAsync(url!, null, cancel.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();

        Directory.EnumerateFileSystemEntries(paths.DownloadsDirectory).Should().BeEmpty();
        RunningFrom(paths.ToolsDirectory).Should().BeEmpty();
    }

    private static List<string> RunningFrom(string directory)
    {
        var running = new List<string>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } file && file.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                    {
                        running.Add(file);
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // System and other users' processes cannot be inspected; they are not ours anyway.
                }
            }
        }

        return running;
    }

    private sealed class ListLogger : ILogger<YtDlpDownloader>
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var line = $"{logLevel}: {formatter(state, exception)}";
            Console.WriteLine(line);
            lock (_lines)
            {
                _lines.Add(line);
            }
        }
    }

    private sealed class Recorder(List<DownloadProgress> reports) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            lock (reports)
            {
                reports.Add(value);
            }
        }
    }
}
