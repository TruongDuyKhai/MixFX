using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.App.YouTube;
using Soundboard.Core.Downloads;
using Soundboard.Storage;

namespace Soundboard.App.Tests.YouTube;

public sealed class DownloadToolsTests : IDisposable
{
    private readonly TempFolder _dir = new();
    private readonly FakeGitHub _github = new();
    private readonly StepTime _time = new(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero));
    private readonly StoragePaths _paths;
    private readonly DownloadTools _tools;

    public DownloadToolsTests()
    {
        _paths = new StoragePaths(_dir.Path);
        _tools = new DownloadTools(_paths, new HttpClient(_github), _time, NullLogger<DownloadTools>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _tools.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void Fresh_IsNotInstalled()
    {
        _tools.GetStatus().Should().Be(new DownloadToolsStatus(false, false, null));
    }

    [Fact]
    public async Task Install_VerifiesAndInstallsBothTools()
    {
        await _tools.InstallOrUpdateAsync(null, Ct);

        _tools.GetStatus().Should().Be(new DownloadToolsStatus(true, false, FakeGitHub.YtDlpTag));
        File.ReadAllText(_tools.YtDlpPath).Should().Be("yt-dlp " + FakeGitHub.YtDlpTag);
        File.ReadAllText(_tools.DenoPath).Should().Be("deno " + FakeGitHub.DenoTag);
        Directory.EnumerateFiles(_paths.ToolsDirectory, "*.download").Should().BeEmpty("partial files never stay behind");
    }

    [Fact]
    public async Task Install_ReportsProgress()
    {
        var reports = new List<DownloadProgress>();

        await _tools.InstallOrUpdateAsync(new InlineProgress(reports.Add), Ct);

        reports.Should().Contain(r => r.Fraction == 1.0 && r.Message.Contains("yt-dlp", StringComparison.Ordinal));
        reports.Should().Contain(r => r.Fraction == 1.0 && r.Message.Contains("Deno", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChecksumMismatch_InstallsNothing()
    {
        _github.Bytes($"https://github.com/yt-dlp/yt-dlp/releases/download/{FakeGitHub.YtDlpTag}/yt-dlp.exe", Encoding.ASCII.GetBytes("tampered"));

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        (await act.Should().ThrowAsync<AudioDownloadException>()).Which.Message.Should().Contain("SHA-256");
        File.Exists(_tools.YtDlpPath).Should().BeFalse();
        _tools.GetStatus().IsInstalled.Should().BeFalse();
        Directory.EnumerateFiles(_paths.ToolsDirectory, "*.download").Should().BeEmpty();
    }

    [Fact]
    public async Task ChecksumMismatchOnUpdate_KeepsTheWorkingVersion()
    {
        await _tools.InstallOrUpdateAsync(null, Ct);
        _github.ServeRelease("2026.12.01", Encoding.ASCII.GetBytes("new yt-dlp"), FakeGitHub.DenoTag, FakeGitHub.DenoZip("deno " + FakeGitHub.DenoTag));
        _github.Bytes("https://github.com/yt-dlp/yt-dlp/releases/download/2026.12.01/yt-dlp.exe", Encoding.ASCII.GetBytes("tampered"));

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        await act.Should().ThrowAsync<AudioDownloadException>();
        File.ReadAllText(_tools.YtDlpPath).Should().Be("yt-dlp " + FakeGitHub.YtDlpTag);
        _tools.GetStatus().Should().Be(new DownloadToolsStatus(true, false, FakeGitHub.YtDlpTag));
    }

    [Fact]
    public async Task ChecksumNotPublished_InstallsNothing()
    {
        _github.Text($"https://github.com/yt-dlp/yt-dlp/releases/download/{FakeGitHub.YtDlpTag}/SHA2-256SUMS", "nothing useful");

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        await act.Should().ThrowAsync<AudioDownloadException>();
        File.Exists(_tools.YtDlpPath).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCheck_IsDueAfterADay_AndSkipsDownloadsWhenUpToDate()
    {
        await _tools.InstallOrUpdateAsync(null, Ct);
        _time.Advance(DownloadTools.UpdateCheckInterval);
        _tools.GetStatus().IsUpdateDue.Should().BeTrue();
        _github.Requests.Clear();

        await _tools.InstallOrUpdateAsync(null, Ct);

        _github.Requests.Should().OnlyContain(r => r.StartsWith("HEAD ", StringComparison.Ordinal), "nothing changed, so nothing is downloaded");
        _tools.GetStatus().IsUpdateDue.Should().BeFalse();
    }

    [Fact]
    public async Task NewRelease_ReplacesTheInstalledTool()
    {
        await _tools.InstallOrUpdateAsync(null, Ct);
        _github.ServeRelease("2026.12.01", Encoding.ASCII.GetBytes("new yt-dlp"), FakeGitHub.DenoTag, FakeGitHub.DenoZip("deno " + FakeGitHub.DenoTag));

        await _tools.InstallOrUpdateAsync(null, Ct);

        File.ReadAllText(_tools.YtDlpPath).Should().Be("new yt-dlp");
        _tools.GetStatus().Version.Should().Be("2026.12.01");
    }

    [Fact]
    public async Task DenoZip_OnlyTheTopLevelExeIsExtracted()
    {
        var zip = FakeGitHub.DenoZip("real deno", ("../evil.exe", "x"), ("sub/deno.exe", "nested"), ("README.md", "readme"));
        _github.ServeRelease(FakeGitHub.YtDlpTag, Encoding.ASCII.GetBytes("yt-dlp"), FakeGitHub.DenoTag, zip);

        await _tools.InstallOrUpdateAsync(null, Ct);

        File.ReadAllText(_tools.DenoPath).Should().Be("real deno");
        Directory.EnumerateFiles(_dir.Path, "*", SearchOption.AllDirectories).Select(Path.GetFileName)
            .Should().NotContain(["evil.exe", "README.md"]);
        File.Exists(Path.Combine(_dir.Path, "evil.exe")).Should().BeFalse();
    }

    [Fact]
    public async Task ToolFilesWithoutManifest_AreNotTrusted()
    {
        Directory.CreateDirectory(_paths.ToolsDirectory);
        File.WriteAllText(_tools.YtDlpPath, "dropped here by something else");
        File.WriteAllText(_tools.DenoPath, "dropped here by something else");

        _tools.GetStatus().IsInstalled.Should().BeFalse();

        await _tools.InstallOrUpdateAsync(null, Ct);
        File.ReadAllText(_tools.YtDlpPath).Should().Be("yt-dlp " + FakeGitHub.YtDlpTag);
    }

    [Fact]
    public async Task CorruptManifest_MeansReinstall()
    {
        await _tools.InstallOrUpdateAsync(null, Ct);
        File.WriteAllText(Path.Combine(_paths.ToolsDirectory, "tools.json"), "{ not json");

        _tools.GetStatus().IsInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task UnexpectedRedirect_IsRefused()
    {
        _github.Route("https://github.com/yt-dlp/yt-dlp/releases/latest", () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Head, "https://evil.example/yt-dlp/yt-dlp/releases/tag/2026.10.26"),
        });

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        await act.Should().ThrowAsync<AudioDownloadException>();
        _github.Requests.Should().NotContain(r => r.StartsWith("GET ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("..%2F..%2Fsomething")]
    [InlineData("v1.0%20beta")]
    public async Task OddTag_IsRefused(string tag)
    {
        _github.Latest("yt-dlp/yt-dlp", tag);

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        await act.Should().ThrowAsync<AudioDownloadException>();
        _github.Requests.Should().NotContain(r => r.StartsWith("GET ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NetworkFailure_BecomesAUserMessage()
    {
        _github.Route("https://github.com/yt-dlp/yt-dlp/releases/latest", () => throw new HttpRequestException("offline"));

        var act = () => _tools.InstallOrUpdateAsync(null, Ct);

        (await act.Should().ThrowAsync<AudioDownloadException>()).WithInnerException<HttpRequestException>();
    }

    [Fact]
    public async Task UserCancel_StaysACancel()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => _tools.InstallOrUpdateAsync(null, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StepTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class InlineProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
