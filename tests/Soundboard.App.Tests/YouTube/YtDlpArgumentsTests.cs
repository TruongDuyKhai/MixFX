using Soundboard.App.YouTube;

namespace Soundboard.App.Tests.YouTube;

public sealed class YtDlpArgumentsTests
{
    private static readonly Uri Video = new("https://www.youtube.com/watch?v=jNQXAC9IVRw");

    private static IReadOnlyList<string> Build() =>
        YtDlpArguments.Build(Video, @"C:\data\downloads\job", @"C:\data\tools\cache", @"C:\data\tools\deno.exe");

    [Fact]
    public void UrlComesLast_AfterDoubleDash_SoItIsNeverReadAsAnOption()
    {
        var args = Build();

        args[^2].Should().Be("--");
        args[^1].Should().Be(Video.AbsoluteUri);
    }

    [Fact]
    public void ConfigFilesAndPlugins_AreIgnored()
    {
        Build().Should().Contain(["--ignore-config", "--no-plugin-dirs", "--no-playlist"]);
    }

    [Fact]
    public void OnlyTheAppsDeno_IsUsedAsJavaScriptRuntime()
    {
        var args = Build().ToList();

        args.Should().Contain("--no-js-runtimes");
        args[args.IndexOf("--js-runtimes") + 1].Should().Be(@"deno:C:\data\tools\deno.exe");
        args.IndexOf("--no-js-runtimes").Should().BeLessThan(args.IndexOf("--js-runtimes"));
    }

    [Fact]
    public void DownloadsOnlyM4aAudio_WithinTheDurationLimit()
    {
        var args = Build().ToList();

        args[args.IndexOf("-f") + 1].Should().Be("bestaudio[ext=m4a]");
        args[args.IndexOf("--match-filters") + 1].Should().Be("duration<=1200 & !is_live");
    }

    [Fact]
    public void WritesOnlyIntoTheJobFolder()
    {
        var args = Build().ToList();

        args.Should().ContainInOrder("-P", @"home:C:\data\downloads\job");
        args.Should().ContainInOrder("-P", @"temp:C:\data\downloads\job");
        args[args.IndexOf("-o") + 1].Should().Be("%(id)s.%(ext)s");
        args[args.IndexOf("--cache-dir") + 1].Should().Be(@"C:\data\tools\cache");
    }

    [Fact]
    public void PrintedLines_MatchTheParserPrefixes()
    {
        var args = Build();

        args.Should().Contain(a => a.StartsWith("pre_process:" + YtDlpArguments.InfoPrefix, StringComparison.Ordinal));
        args.Should().Contain(a => a.StartsWith("after_move:" + YtDlpArguments.FilePrefix, StringComparison.Ordinal));
        args.Should().Contain(a => a.StartsWith("download:" + YtDlpArguments.ProgressPrefix, StringComparison.Ordinal));
    }
}
