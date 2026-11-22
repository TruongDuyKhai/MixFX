using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Models;

public sealed class EditLimitsTests
{
    [Theory]
    [InlineData(1.0f, -1f)]
    [InlineData(0.5f, 5.0206f)]
    [InlineData(0.891251f, 0f)]
    public void NormalizeGain_BringsPeakToMinusOneDbfs(float peak, float expectedDb)
    {
        EditLimits.NormalizeGainDb(peak).Should().BeApproximately(expectedDb, 0.001f);
    }

    [Fact]
    public void NormalizeGain_IsClampedToEditorRange()
    {
        EditLimits.NormalizeGainDb(0.0001f).Should().Be(EditLimits.MaxGainDb);
        EditLimits.NormalizeGainDb(100f).Should().Be(EditLimits.MinGainDb);
    }

    [Fact]
    public void NormalizeGain_SilenceIsLeftAlone()
    {
        EditLimits.NormalizeGainDb(0f).Should().Be(0f);
    }

    [Theory]
    [InlineData(1, PlaybackKind.Preloaded)]
    [InlineData(15, PlaybackKind.Preloaded)]
    [InlineData(16, PlaybackKind.Streamed)]
    public void KindFor_FifteenSecondsIsTheLimit(int seconds, PlaybackKind expected)
    {
        EditLimits.KindFor(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\music\horn.MP3", true)]
    [InlineData("a.wav", true)]
    [InlineData("a.ogg", true)]
    [InlineData("a.flac", true)]
    [InlineData("a.txt", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    public void SupportedFormats(string path, bool expected)
    {
        SupportedAudioFormats.IsSupported(path).Should().Be(expected);
    }
}
