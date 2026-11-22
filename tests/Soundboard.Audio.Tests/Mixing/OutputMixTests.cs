using Soundboard.Audio.Decoding;
using Soundboard.Audio.Mixing;

namespace Soundboard.Audio.Tests.Mixing;

public sealed class OutputMixTests
{
    [Fact]
    public void SumsSoundsAndVoiceWithTheirOwnVolumes()
    {
        var mixer = new Mixer();
        mixer.Add(new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(1_000, 2, 0.5f)), new EditEnvelope(1f, 0, 0, 1_000)));
        var mix = new OutputMix(mixer, new ConstantSampleProvider(0.2f)) { SoundVolume = 0.5f, VoiceVolume = 2f };
        var buffer = new float[100];

        mix.Read(buffer, 0, buffer.Length);

        buffer.Should().OnlyContain(s => MathF.Abs(s - 0.65f) < 1e-6f);
    }

    [Fact]
    public void WithoutVoice_OnlySounds()
    {
        var mixer = new Mixer();
        mixer.Add(new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(1_000, 2, 0.5f)), new EditEnvelope(1f, 0, 0, 1_000)));
        var mix = new OutputMix(mixer, voice: null) { SoundVolume = 0.5f };
        var buffer = new float[100];

        mix.Read(buffer, 0, buffer.Length);

        buffer.Should().OnlyContain(s => s == 0.25f);
    }
}
