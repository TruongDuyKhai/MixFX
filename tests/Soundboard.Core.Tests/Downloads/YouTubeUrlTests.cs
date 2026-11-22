using Soundboard.Core.Downloads;

namespace Soundboard.Core.Tests.Downloads;

public sealed class YouTubeUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw", "https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("  https://youtube.com/watch?v=jNQXAC9IVRw&t=5s  ", "https://youtube.com/watch?v=jNQXAC9IVRw&t=5s")]
    [InlineData("http://m.youtube.com/watch?v=jNQXAC9IVRw", "https://m.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://music.youtube.com/watch?v=jNQXAC9IVRw&list=RDAMVM", "https://music.youtube.com/watch?v=jNQXAC9IVRw&list=RDAMVM")]
    [InlineData("https://youtu.be/jNQXAC9IVRw?si=abc", "https://youtu.be/jNQXAC9IVRw?si=abc")]
    [InlineData("youtu.be/jNQXAC9IVRw", "https://youtu.be/jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/shorts/jNQXAC9IVRw", "https://www.youtube.com/shorts/jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw#comments", "https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    public void TryParse_SingleVideoLinks_AreAcceptedAsHttps(string input, string expected)
    {
        YouTubeUrl.TryParse(input, out var url).Should().BeTrue();

        url!.AbsoluteUri.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/playlist?list=PL1234567890")]
    [InlineData("https://www.youtube.com/@channel")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw!")]
    [InlineData("https://youtu.be/")]
    [InlineData("https://youtu.be/jNQXAC9IVRw/extra")]
    [InlineData("https://www.youtube.com.evil.example/watch?v=jNQXAC9IVRw")]
    [InlineData("https://evil.example/watch?v=jNQXAC9IVRw")]
    [InlineData("https://user@www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com:8443/watch?v=jNQXAC9IVRw")]
    [InlineData("ftp://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("file:///C:/Users/me/song.m4a")]
    [InlineData("javascript:alert(1)")]
    [InlineData("--exec calc https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    public void TryParse_AnythingElse_IsRejected(string? input)
    {
        YouTubeUrl.TryParse(input, out var url).Should().BeFalse();

        url.Should().BeNull();
    }
}
