using System.Globalization;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Models;

public sealed class EffectCatalogTests
{
    [Fact]
    public void Configs_RoundTripThroughInvariantText()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A comma-decimal culture must not change what is written to settings.json.
            CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
            var pitch = EffectCatalog.PitchConfig(-3.5f);
            var reverb = EffectCatalog.ReverbConfig(0.25f, 0.4f);

            pitch.Params[EffectCatalog.PitchSemitones].Should().Be("-3.5");
            pitch.GetFloat(EffectCatalog.PitchSemitones, 0f).Should().Be(-3.5f);
            reverb.GetFloat(EffectCatalog.ReverbRoom, 0f).Should().Be(0.25f);
            reverb.GetFloat(EffectCatalog.ReverbWet, 0f).Should().Be(0.4f);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("")]
    public void GetFloat_MalformedValue_FallsBack(string text)
    {
        var effect = new EffectConfig(EffectCatalog.Pitch, new Dictionary<string, string> { [EffectCatalog.PitchSemitones] = text });

        effect.GetFloat(EffectCatalog.PitchSemitones, 1.5f).Should().Be(1.5f);
    }

    [Fact]
    public void Find_ReturnsFirstMatchOrNull()
    {
        IReadOnlyList<EffectConfig> effects = [EffectCatalog.ReverbConfig(0.5f, 0.5f)];

        effects.Find(EffectCatalog.Reverb).Should().NotBeNull();
        effects.Find(EffectCatalog.Pitch).Should().BeNull();
    }

    [Fact]
    public void FeatureFlags_AllDefaultToOff()
    {
        var flags = new FeatureFlags();

        new[] { flags.Ducking, flags.SoundEffects, flags.VoiceChanger, flags.Playlist, flags.WebRemote }.Should().OnlyContain(on => !on);
    }
}
