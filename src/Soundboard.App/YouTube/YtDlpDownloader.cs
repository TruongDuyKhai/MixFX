using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Downloads;
using Soundboard.Core.Models;
using Soundboard.Storage;

namespace Soundboard.App.YouTube;

/// <summary>
/// <see cref="IAudioDownloader"/> backed by yt-dlp (ADR 0005). Each download runs in its own folder under
/// <see cref="StoragePaths.DownloadsDirectory"/>; the folder is removed on failure, on cancel, and by
/// <see cref="Delete"/> once the file has been copied into the library.
/// </summary>
public sealed class YtDlpDownloader : IAudioDownloader, IDisposable
{
    private readonly DownloadTools _tools;
    private readonly StoragePaths _paths;
    private readonly ILogger<YtDlpDownloader> _logger;

    // One operation at a time: an update must never replace yt-dlp.exe while it runs.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public YtDlpDownloader(DownloadTools tools, StoragePaths paths, ILogger<YtDlpDownloader> logger)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        _tools = tools;
        _paths = paths;
        _logger = logger;
    }

    public async Task<DownloadToolsStatus> GetToolsStatusAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _tools.GetStatus();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task InstallToolsAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _tools.InstallOrUpdateAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DownloadedAudio> DownloadAudioAsync(Uri url, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!YouTubeUrl.TryParse(url.OriginalString, out var videoUrl))
        {
            throw new AudioDownloadException("Link này không phải một video YouTube.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tools.GetStatus().IsInstalled)
            {
                throw new AudioDownloadException("Chưa có công cụ tải. Hãy bấm \"Tải công cụ\" trước.");
            }

            RemoveLeftoverDownloads();
            var jobDirectory = Path.Combine(_paths.DownloadsDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(jobDirectory);
            try
            {
                return await RunAsync(videoUrl!, jobDirectory, progress, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Partial files, a rejected video or a cancel: nothing in this folder is useful any more.
                TryDeleteDirectory(jobDirectory);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Delete(DownloadedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var jobDirectory = Path.GetDirectoryName(Path.GetFullPath(audio.FilePath));
        var downloads = Path.GetFullPath(_paths.DownloadsDirectory);
        if (jobDirectory is null
            || !string.Equals(Path.GetDirectoryName(jobDirectory), downloads, StringComparison.OrdinalIgnoreCase))
        {
            // Only folders this class created are ever deleted, whatever path a caller passes.
            _logger.LogWarning("Refusing to delete {Path}: not a download folder", audio.FilePath);
            return;
        }

        TryDeleteDirectory(jobDirectory);
    }

    public void Dispose() => _gate.Dispose();

    private async Task<DownloadedAudio> RunAsync(Uri url, string jobDirectory, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_tools.YtDlpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = jobDirectory,
        };
        foreach (var argument in YtDlpArguments.Build(url, jobDirectory, _tools.CacheDirectory, _tools.DenoPath))
        {
            start.ArgumentList.Add(argument);
        }

        // Titles are often not ASCII; without these Python falls back to the console code page.
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["PYTHONUTF8"] = "1";

        // Keep Deno's cache inside the data folder and stop it from checking for its own updates or prompting.
        start.Environment["DENO_DIR"] = _tools.DenoCacheDirectory;
        start.Environment["DENO_NO_UPDATE_CHECK"] = "1";
        start.Environment["DENO_NO_PROMPT"] = "1";

        progress?.Report(new DownloadProgress("Đang lấy thông tin video…", null));
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogError(ex, "Could not start {YtDlp}", _tools.YtDlpPath);
            throw new AudioDownloadException("Không chạy được yt-dlp. Hãy tắt tính năng rồi bật lại để tải lại công cụ.", ex);
        }

        process.StandardInput.Close();

        YtDlpVideoInfo? info = null;
        string? file = null;
        string? error = null;

        async Task ReadOutputAsync()
        {
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                if (YtDlpOutputParser.TryParseProgress(line, out var fraction))
                {
                    var percent = fraction is { } f ? ((int)(f * 100)).ToString(CultureInfo.InvariantCulture) + "%" : string.Empty;
                    progress?.Report(new DownloadProgress($"Đang tải âm thanh… {percent}".TrimEnd(), fraction));
                }
                else if (YtDlpOutputParser.TryParseInfo(line, out var parsed))
                {
                    info = parsed;
                    _logger.LogInformation("yt-dlp: {Url} is \"{Title}\", {Duration}", url, parsed!.Title, parsed.Duration);
                }
                else if (YtDlpOutputParser.TryParseFile(line, out var path))
                {
                    file = path;
                }
                else if (line.Length > 0)
                {
                    _logger.LogInformation("yt-dlp: {Line}", line);
                }
            }
        }

        async Task ReadErrorsAsync()
        {
            while (await process.StandardError.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                if (YtDlpOutputParser.TryParseError(line, out var message))
                {
                    error = message;
                }

                if (line.Length > 0)
                {
                    _logger.LogWarning("yt-dlp: {Line}", line);
                }
            }
        }

        var reading = Task.WhenAll(ReadOutputAsync(), ReadErrorsAsync());
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Deno runs as a child of yt-dlp, so the whole tree has to go.
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException ex)
            {
                // It exited on its own between the cancel and the kill: nothing is left to stop.
                _logger.LogDebug(ex, "yt-dlp had already exited when the download was cancelled");
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await reading.ConfigureAwait(false);
            _logger.LogInformation("Download of {Url} cancelled", url);
            throw;
        }

        await reading.ConfigureAwait(false);
        _logger.LogInformation("yt-dlp exited with code {ExitCode}", process.ExitCode);
        return Result(info, file, error, process.ExitCode, jobDirectory);
    }

    private static DownloadedAudio Result(YtDlpVideoInfo? info, string? file, string? error, int exitCode, string jobDirectory)
    {
        if (info is { IsLive: true })
        {
            throw new AudioDownloadException("Đây là livestream đang phát nên không tải được.");
        }

        if (info?.Duration is { } duration && duration > DownloadLimits.MaxDuration)
        {
            throw new AudioDownloadException(
                $"Video dài {FormatDuration(duration)}, quá giới hạn {DownloadLimits.MaxDuration.TotalMinutes:0} phút.");
        }

        if (exitCode != 0 || file is null)
        {
            throw new AudioDownloadException(error is null
                ? "yt-dlp không tải được video này."
                : $"yt-dlp không tải được video này: {error}");
        }

        var fullPath = Path.GetFullPath(file);
        if (!string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(jobDirectory), StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath)
            || !SupportedAudioFormats.IsSupported(fullPath))
        {
            throw new AudioDownloadException("Không tìm thấy file âm thanh yt-dlp vừa tải.");
        }

        var title = info?.Title is { } reported && !string.IsNullOrWhiteSpace(reported)
            ? reported
            : Path.GetFileNameWithoutExtension(fullPath);
        return new DownloadedAudio(fullPath, title);
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.ToString(duration >= TimeSpan.FromHours(1) ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);

    /// <summary>Folders left by a crash or a kill. Safe: the gate guarantees no download is running.</summary>
    private void RemoveLeftoverDownloads()
    {
        if (!Directory.Exists(_paths.DownloadsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(_paths.DownloadsDirectory))
        {
            TryDeleteDirectory(directory);
        }
    }

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover only costs disk space and is retried before the next download.
            _logger.LogWarning(ex, "Could not delete download folder {Directory}", directory);
        }
    }
}
