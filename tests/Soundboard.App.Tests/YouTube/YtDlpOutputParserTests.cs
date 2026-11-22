using Soundboard.App.YouTube;

namespace Soundboard.App.Tests.YouTube;

// Lines below were captured from a real yt-dlp run with YtDlpArguments.
public sealed class YtDlpOutputParserTests
{
    [Fact]
    public void Info_ParsesDurationLiveAndTitleWithSpaces()
    {
        YtDlpOutputParser.TryParseInfo("SBINFO 19 False Me at the zoo", out var info).Should().BeTrue();

        info.Should().Be(new YtDlpVideoInfo(TimeSpan.FromSeconds(19), false, "Me at the zoo"));
    }

    [Fact]
    public void Info_UnicodeTitle_IsKept()
    {
        YtDlpOutputParser.TryParseInfo("SBINFO 212.5 False Nơi này có anh | Official MV", out var info).Should().BeTrue();

        info!.Title.Should().Be("Nơi này có anh | Official MV");
        info.Duration.Should().Be(TimeSpan.FromSeconds(212.5));
    }

    [Fact]
    public void Info_LiveStream_HasNoDuration()
    {
        YtDlpOutputParser.TryParseInfo("SBINFO NA True Lofi radio", out var info).Should().BeTrue();

        info.Should().Be(new YtDlpVideoInfo(null, true, "Lofi radio"));
    }

    [Theory]
    [InlineData("SBPROGRESS 1024 309288", 1024.0 / 309288)]
    [InlineData("SBPROGRESS 309288 309288", 1.0)]
    [InlineData("SBPROGRESS 400 200", 1.0)]
    public void Progress_KnownTotal_GivesFraction(string line, double expected)
    {
        YtDlpOutputParser.TryParseProgress(line, out var fraction).Should().BeTrue();

        fraction.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("SBPROGRESS 1024 NA")]
    [InlineData("SBPROGRESS 1024 0")]
    [InlineData("SBPROGRESS NA NA")]
    public void Progress_UnknownTotal_IsProgressWithoutFraction(string line)
    {
        YtDlpOutputParser.TryParseProgress(line, out var fraction).Should().BeTrue();

        fraction.Should().BeNull();
    }

    [Fact]
    public void File_ParsesFullPathWithSpaces()
    {
        YtDlpOutputParser.TryParseFile(@"SBFILE C:\Users\A B\AppData\Roaming\Soundboard\downloads\x\jNQXAC9IVRw.m4a", out var path).Should().BeTrue();

        path.Should().Be(@"C:\Users\A B\AppData\Roaming\Soundboard\downloads\x\jNQXAC9IVRw.m4a");
    }

    [Fact]
    public void Error_ParsesYtDlpErrorLine()
    {
        YtDlpOutputParser.TryParseError("ERROR: [youtube] abc: Video unavailable", out var message).Should().BeTrue();

        message.Should().Be("[youtube] abc: Video unavailable");
    }

    [Theory]
    [InlineData("WARNING: [youtube] something")]
    [InlineData("[download] Destination: x.m4a")]
    [InlineData("")]
    [InlineData(" SBINFO 19 False indented")]
    public void OtherLines_AreNotInterpreted(string line)
    {
        YtDlpOutputParser.TryParseInfo(line, out _).Should().BeFalse();
        YtDlpOutputParser.TryParseProgress(line, out _).Should().BeFalse();
        YtDlpOutputParser.TryParseFile(line, out _).Should().BeFalse();
        YtDlpOutputParser.TryParseError(line, out _).Should().BeFalse();
    }
}
