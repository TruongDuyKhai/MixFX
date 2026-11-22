using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Soundboard.App.Services;
using Soundboard.Core.Downloads;
using Soundboard.Storage;

namespace Soundboard.App.YouTube;

/// <summary>
/// Installs and updates yt-dlp and Deno (the JavaScript runtime yt-dlp needs for YouTube) in
/// <see cref="StoragePaths.ToolsDirectory"/>, from the projects' official GitHub releases (ADR 0005).
/// Each file is hashed while it downloads and compared with the SHA-256 published in the same release;
/// only a matching file replaces the installed one, and only then is it recorded in <see cref="ToolsManifest"/>.
/// Not thread-safe: <see cref="YtDlpDownloader"/> runs one operation at a time.
/// </summary>
public sealed partial class DownloadTools : IDisposable
{
    /// <summary>YouTube changes often break old yt-dlp versions, so look for a new release at least this often.</summary>
    public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(1);

    private const string GitHub = "https://github.com/";
    private const string ManifestFileName = "tools.json";
    private const string PartialSuffix = ".download";
    private const string CacheFolderName = "cache";
    private const string DenoCacheFolderName = "deno-cache";
    private const int CopyBufferBytes = 81_920;
    private const long MaxChecksumFileBytes = 1024 * 1024;

    // Bounds a stalled connection; the largest file (Deno, ~45 MB) needs a fraction of this on a slow line.
    private static readonly TimeSpan ToolDownloadTimeout = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Tool YtDlp = new(
        DisplayName: "yt-dlp",
        Repository: "yt-dlp/yt-dlp",
        Asset: "yt-dlp.exe",
        ChecksumAsset: "SHA2-256SUMS",
        ZipEntry: null,
        InstalledFileName: "yt-dlp.exe",
        MaxBytes: 200L * 1024 * 1024);

    private static readonly Tool Deno = new(
        DisplayName: "Deno",
        Repository: "denoland/deno",
        Asset: "deno-x86_64-pc-windows-msvc.zip",
        ChecksumAsset: "deno-x86_64-pc-windows-msvc.zip.sha256sum",
        ZipEntry: "deno.exe",
        InstalledFileName: "deno.exe",
        MaxBytes: 300L * 1024 * 1024);

    private readonly StoragePaths _paths;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly ILogger<DownloadTools> _logger;

    /// <param name="http">Owned and disposed by this instance. Tests pass one with a fake handler.</param>
    public DownloadTools(StoragePaths paths, HttpClient http, TimeProvider time, ILogger<DownloadTools> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _http = http;
        _time = time;
        _logger = logger;
    }

    public string YtDlpPath => Path.Combine(_paths.ToolsDirectory, YtDlp.InstalledFileName);

    public string DenoPath => Path.Combine(_paths.ToolsDirectory, Deno.InstalledFileName);

    /// <summary>yt-dlp's cache (player scripts), kept inside the data folder instead of the user profile.</summary>
    public string CacheDirectory => Path.Combine(_paths.ToolsDirectory, CacheFolderName);

    /// <summary>Deno's cache (<c>DENO_DIR</c>), kept inside the data folder.</summary>
    public string DenoCacheDirectory => Path.Combine(_paths.ToolsDirectory, DenoCacheFolderName);

    private string ManifestPath => Path.Combine(_paths.ToolsDirectory, ManifestFileName);

    /// <summary>HttpClient for GitHub. The User-Agent names only the app: GitHub rejects requests without one.</summary>
    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Soundboard", AppInfo.Version));
        return http;
    }

    public DownloadToolsStatus GetStatus()
    {
        var manifest = ReadManifest();
        var installed = manifest.YtDlpVersion is not null
            && manifest.DenoVersion is not null
            && File.Exists(YtDlpPath)
            && File.Exists(DenoPath);
        var updateDue = installed && _time.GetUtcNow() - manifest.LastCheckedUtc >= UpdateCheckInterval;
        return new DownloadToolsStatus(installed, updateDue, installed ? manifest.YtDlpVersion : null);
    }

    /// <summary>Installs whichever tool is missing or older than its latest release, then records the check.</summary>
    public async Task InstallOrUpdateAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_paths.ToolsDirectory);
            var manifest = ReadManifest();

            progress?.Report(new DownloadProgress("Đang kiểm tra phiên bản mới của yt-dlp…", null));
            var ytDlpTag = await LatestTagAsync(YtDlp, cancellationToken).ConfigureAwait(false);
            if (manifest.YtDlpVersion != ytDlpTag || !File.Exists(YtDlpPath))
            {
                await InstallAsync(YtDlp, ytDlpTag, progress, cancellationToken).ConfigureAwait(false);
                manifest = manifest with { YtDlpVersion = ytDlpTag };
                WriteManifest(manifest);
            }

            progress?.Report(new DownloadProgress("Đang kiểm tra phiên bản mới của Deno…", null));
            var denoTag = await LatestTagAsync(Deno, cancellationToken).ConfigureAwait(false);
            if (manifest.DenoVersion != denoTag || !File.Exists(DenoPath))
            {
                await InstallAsync(Deno, denoTag, progress, cancellationToken).ConfigureAwait(false);
                manifest = manifest with { DenoVersion = denoTag };
            }

            WriteManifest(manifest with { LastCheckedUtc = _time.GetUtcNow() });
            _logger.LogInformation("Download tools up to date: yt-dlp {YtDlp}, Deno {Deno}", ytDlpTag, denoTag);
        }
        catch (Exception ex) when (IsDownloadFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Could not install or update the download tools");
            throw new AudioDownloadException("Không tải được công cụ từ GitHub. Kiểm tra kết nối mạng rồi thử lại.", ex);
        }
    }

    private static bool IsDownloadFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException
        // HttpClient's own timeouts surface as TaskCanceledException; the user's cancel must stay a cancel.
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>Reads the tag GitHub redirects <c>/releases/latest</c> to; avoids the rate-limited REST API.</summary>
    private async Task<string> LatestTagAsync(Tool tool, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, $"{GitHub}{tool.Repository}/releases/latest");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri;
        var tagPrefix = $"/{tool.Repository}/releases/tag/";
        if (finalUri is null
            || !finalUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || !finalUri.AbsolutePath.StartsWith(tagPrefix, StringComparison.Ordinal))
        {
            throw new AudioDownloadException($"GitHub trả về địa chỉ lạ khi tìm bản mới của {tool.DisplayName}.");
        }

        var tag = Uri.UnescapeDataString(finalUri.AbsolutePath[tagPrefix.Length..]);
        return ReleaseTag().IsMatch(tag)
            ? tag
            : throw new AudioDownloadException($"GitHub trả về tên phiên bản lạ cho {tool.DisplayName}.");
    }

    private async Task InstallAsync(Tool tool, string tag, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ToolDownloadTimeout);
        var token = timeout.Token;

        var releaseUrl = $"{GitHub}{tool.Repository}/releases/download/{Uri.EscapeDataString(tag)}/";
        var checksums = await DownloadTextAsync(releaseUrl + tool.ChecksumAsset, token).ConfigureAwait(false);
        var expected = ChecksumFile.FindSha256(checksums, tool.Asset)
            ?? throw new AudioDownloadException($"Bản {tag} của {tool.DisplayName} không công bố mã SHA-256 cho {tool.Asset}.");

        var downloadPath = Path.Combine(_paths.ToolsDirectory, tool.Asset + PartialSuffix);
        try
        {
            var actual = await DownloadFileAsync(tool, releaseUrl + tool.Asset, downloadPath, tag, progress, token).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected)))
            {
                _logger.LogError("{Tool} {Tag}: SHA-256 {Actual} does not match the published {Expected}", tool.DisplayName, tag, actual, expected);
                throw new AudioDownloadException($"File {tool.DisplayName} tải về không khớp mã SHA-256 đã công bố nên đã bị bỏ. Hãy thử lại sau.");
            }

            var installedPath = Path.Combine(_paths.ToolsDirectory, tool.InstalledFileName);
            if (tool.ZipEntry is null)
            {
                File.Move(downloadPath, installedPath, overwrite: true);
            }
            else
            {
                ExtractEntry(downloadPath, tool.ZipEntry, installedPath);
            }

            _logger.LogInformation("Installed {Tool} {Tag} (SHA-256 {Hash})", tool.DisplayName, tag, actual);
        }
        finally
        {
            File.Delete(downloadPath);
        }
    }

    /// <summary>Copies one top-level entry out of the archive; every other entry, and any path inside it, is ignored.</summary>
    private static void ExtractEntry(string zipPath, string entryName, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e => e.FullName == entryName)
            ?? throw new InvalidDataException($"'{entryName}' is missing from {Path.GetFileName(zipPath)}.");

        var extractedPath = destinationPath + PartialSuffix;
        try
        {
            entry.ExtractToFile(extractedPath, overwrite: true);
            File.Move(extractedPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(extractedPath);
        }
    }

    private async Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxChecksumFileBytes)
        {
            throw new InvalidDataException($"{url} is larger than a checksum file can be.");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return text.Length <= MaxChecksumFileBytes ? text : throw new InvalidDataException($"{url} is larger than a checksum file can be.");
    }

    /// <summary>Streams the file to disk, hashing it on the way. Returns the lowercase hex SHA-256.</summary>
    private async Task<string> DownloadFileAsync(
        Tool tool, string url, string destinationPath, string tag, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        if (total > tool.MaxBytes)
        {
            throw new InvalidDataException($"{tool.Asset} is {total} bytes, more than the {tool.MaxBytes} allowed.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferBytes];
        long received = 0;
        var lastPercent = -1;
        var label = $"Đang tải {tool.DisplayName} {tag}";

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferBytes, useAsync: true))
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                received += read;
                if (received > tool.MaxBytes)
                {
                    throw new InvalidDataException($"{tool.Asset} is larger than the {tool.MaxBytes} bytes allowed.");
                }

                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

                var percent = total > 0 ? (int)(received * 100 / total.Value) : -1;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress?.Report(new DownloadProgress(
                        percent >= 0 ? $"{label}… {percent}%" : $"{label}… {received / (1024 * 1024)} MB",
                        percent >= 0 ? percent / 100.0 : null));
                }
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private ToolsManifest ReadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return ToolsManifest.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<ToolsManifest>(File.ReadAllText(ManifestPath), ManifestJson) ?? ToolsManifest.Empty;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Unknown state: treat the tools as not installed so they are downloaded and verified again.
            _logger.LogWarning(ex, "Unreadable {Manifest}; the tools will be reinstalled", ManifestPath);
            return ToolsManifest.Empty;
        }
    }

    private void WriteManifest(ToolsManifest manifest)
    {
        var temp = ManifestPath + PartialSuffix;
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, ManifestJson));
        File.Move(temp, ManifestPath, overwrite: true);
    }

    public void Dispose() => _http.Dispose();

    // Release tags of both projects: "2026.10.26", "v2.9.8". Anything else is refused before it reaches a URL.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex ReleaseTag();

    private sealed record Tool(
        string DisplayName,
        string Repository,
        string Asset,
        string ChecksumAsset,
        string? ZipEntry,
        string InstalledFileName,
        long MaxBytes);
}
