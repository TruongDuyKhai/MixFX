using Soundboard.App.YouTube;

namespace Soundboard.App.Tests.YouTube;

public sealed class ChecksumFileTests
{
    private const string Hash = "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d";

    // Same shape as yt-dlp's SHA2-256SUMS.
    private const string SumFile = $"""
        e51e69e89489f199530f13d32a4342da3ddb2cf34842f364603a066db05afbf3  yt-dlp
        {Hash}  yt-dlp.exe
        0066dbac3c124028e2dcae2a197fccee33cd91f456313d24f7af89fd54590e98  yt-dlp_arm64.exe

        """;

    // Same shape as Deno's *.sha256sum: PowerShell Get-FileHash | Format-List, with CRLF.
    private const string PowerShellFile =
        "\r\nAlgorithm : SHA256\r\nHash      : CBA84B26E0DF5AE7F74FC66B86A95B866872A86292EBD84FA1E959927EDD93BF\r\n"
        + "Path      : C:\\a\\deno\\deno\\target\\release\\deno-x86_64-pc-windows-msvc.zip\r\n\r\n";

    [Fact]
    public void Sha256SumFormat_FindsTheExactFileName()
    {
        ChecksumFile.FindSha256(SumFile, "yt-dlp.exe").Should().Be(Hash);
        ChecksumFile.FindSha256(SumFile, "yt-dlp").Should().StartWith("e51e69");
    }

    [Fact]
    public void Sha256SumFormat_BinaryMarker_IsAccepted()
    {
        ChecksumFile.FindSha256($"{Hash.ToUpperInvariant()} *yt-dlp.exe", "yt-dlp.exe").Should().Be(Hash);
    }

    [Fact]
    public void PowerShellFormat_FindsTheHashOfThatPath()
    {
        ChecksumFile.FindSha256(PowerShellFile, "deno-x86_64-pc-windows-msvc.zip")
            .Should().Be("cba84b26e0df5ae7f74fc66b86a95b866872a86292ebd84fa1e959927edd93bf");
    }

    [Theory]
    [InlineData("yt-dlp.ex")]
    [InlineData("YT-DLP.EXE")]
    [InlineData("deno.exe")]
    public void UnlistedFile_ReturnsNull(string fileName)
    {
        ChecksumFile.FindSha256(SumFile, fileName).Should().BeNull();
    }

    [Fact]
    public void PowerShellFormat_OtherPath_ReturnsNull()
    {
        ChecksumFile.FindSha256(PowerShellFile, "deno-aarch64-pc-windows-msvc.zip").Should().BeNull();
    }

    [Theory]
    [InlineData("not a checksum file")]
    [InlineData("<html><body>Not Found</body></html>")]
    [InlineData("Hash : 1234\r\nPath : C:\\x\\deno-x86_64-pc-windows-msvc.zip")]
    public void Garbage_ReturnsNull(string content)
    {
        ChecksumFile.FindSha256(content, "deno-x86_64-pc-windows-msvc.zip").Should().BeNull();
    }
}
