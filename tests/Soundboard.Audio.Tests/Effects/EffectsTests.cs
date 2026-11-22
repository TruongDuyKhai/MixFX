using Soundboard.Audio.Decoding;
using Soundboard.Audio.Effects;
using Soundboard.Audio.Mixing;
using Soundboard.Core.Models;

namespace Soundboard.Audio.Tests.Effects;

public sealed class EffectsTests
{
    private const int Rate = 48_000;

    [Theory]
    [InlineData(12f, 880.0)]
    [InlineData(-12f, 220.0)]
    [InlineData(7f, 659.3)]
    public void PitchShifter_MovesTheFrequency(float semitones, double expectedHz)
    {
        var signal = Signals.Sine(Rate * 2, 2, 0.5f, frequency: 440);
        var shifter = new PitchShifter(semitones);

        shifter.Process(signal);

        // Skip the first window while the delay line fills.
        DominantFrequency(signal.AsSpan(Rate / 2 * 2)).Should().BeApproximately(expectedHz, expectedHz * 0.06);
    }

    [Fact]
    public void PitchShifter_NeverBoostsAndLosesAtMostAFewDecibels()
    {
        var signal = Signals.Sine(Rate, 2, 0.5f, frequency: 440);
        new PitchShifter(5f).Process(signal);

        var inputRms = 0.5f / MathF.Sqrt(2);
        Signals.Peak(signal).Should().BeLessThanOrEqualTo(0.5f + 1e-4f, "cross-fade weights sum to 1");
        Rms(signal.AsSpan(Rate / 4 * 2)).Should().BeInRange(inputRms * 0.6f, inputRms, "a pure tone partly cancels at cross-fades (~-2.5 dB)");
    }

    [Fact]
    public void Reverb_LeavesATailAfterTheSoundStops()
    {
        var signal = new float[Rate * 2];
        Signals.Sine(Rate / 10, 2, 0.8f).CopyTo(signal, 0); // 100 ms burst, then silence
        new Reverb(room: 0.8f, wet: 0.5f).Process(signal);

        Rms(signal.AsSpan((Rate / 2) * 2, (Rate / 10) * 2)).Should().BeGreaterThan(0.001f, "the room still rings 400 ms later");
        signal.Should().OnlyContain(s => float.IsFinite(s));
    }

    [Fact]
    public void Reverb_SilenceStaysSilent_AndDryWhenWetIsZero()
    {
        var silence = new float[9_600];
        new Reverb(0.5f, 0.5f).Process(silence);
        silence.Should().OnlyContain(s => s == 0f);

        var signal = Signals.Sine(4_800, 2, 0.5f);
        var original = (float[])signal.Clone();
        new Reverb(0.5f, 0f).Process(signal);
        signal.Should().Equal(original);
    }

    [Fact]
    public void Effects_SteadyState_AllocateNothing()
    {
        var buffer = Signals.Sine(1_440, 2, 0.5f);
        var pitch = new PitchShifter(3f);
        var reverb = new Reverb(0.5f, 0.3f);

        var allocated = Allocations.InSteadyState(() =>
        {
            pitch.Process(buffer);
            reverb.Process(buffer);
        });

        allocated.Should().Be(0);
    }

    [Fact]
    public void ChainFactory_SkipsNoOpAndUnknownEffects()
    {
        EffectChainFactory.Create([EffectCatalog.PitchConfig(0f), EffectCatalog.ReverbConfig(0.5f, 0f), new EffectConfig("future-effect", new Dictionary<string, string>())])
            .Should().BeEmpty();

        EffectChainFactory.Create([EffectCatalog.PitchConfig(-3f), EffectCatalog.ReverbConfig(0.5f, 0.2f)])
            .Select(e => e.Id).Should().Equal(EffectCatalog.Pitch, EffectCatalog.Reverb);
    }

    [Fact]
    public void Voice_AppliesItsEffectChain()
    {
        var sound = new CachedSound(Signals.Sine(4_800, 2, 0.5f));
        var dry = new float[9_600];
        var wet = new float[9_600];

        new CachedVoice(Guid.NewGuid(), sound, new EditEnvelope(1f, 0, 0, 4_800)).Read(dry);
        new CachedVoice(Guid.NewGuid(), sound, new EditEnvelope(1f, 0, 0, 4_800), [new PitchShifter(12f)]).Read(wet);

        wet.Should().NotEqual(dry);
    }

    [Fact]
    public void Voice_ReportsWhetherItEndedNaturally()
    {
        var sound = new CachedSound(Signals.Constant(1_000, 2, 0.5f));
        var played = new CachedVoice(Guid.NewGuid(), sound, new EditEnvelope(1f, 0, 0, 1_000));
        var stopped = new CachedVoice(Guid.NewGuid(), sound, new EditEnvelope(1f, 0, 0, 1_000));

        played.Read(new float[4_000]);
        stopped.RequestStop();
        stopped.Read(new float[4_000]);

        played.EndedNaturally.Should().BeTrue();
        stopped.IsFinished.Should().BeTrue();
        stopped.EndedNaturally.Should().BeFalse();
    }

    [Fact]
    public void VoiceChanger_IsAnExactPassThroughWhenOffOrAtZero()
    {
        var changer = new VoiceChanger { Source = new ConstantSampleProvider(0.3f), Semitones = 5f };
        Signals.ReadAll(changer, 2_000).Should().OnlyContain(s => s == 0.3f, "disabled");

        changer.Enabled = true;
        changer.Semitones = 0f;
        Signals.ReadAll(changer, 2_000).Should().OnlyContain(s => s == 0.3f, "enabled at 0 semitones");
    }

    [Fact]
    public void VoiceChanger_ShiftsTheVoiceWhenEnabled()
    {
        var changer = new VoiceChanger { Source = new ArraySampleProvider(Signals.Sine(Rate, 2, 0.5f, frequency: 300)), Enabled = true, Semitones = 12f };

        var output = Signals.ReadAll(changer, Rate * 2);

        DominantFrequency(output.AsSpan(Rate / 2)).Should().BeApproximately(600, 36);
    }

    [Fact]
    public void Ducking_LowersSoundsWhileTalkingAndRestoresAfter()
    {
        var mixer = new Mixer();
        mixer.Add(new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(Rate * 4, 2, 0.8f)), new EditEnvelope(1f, 0, 0, Rate * 4)));
        var gate = new PttGate { Source = new ConstantSampleProvider(0f) };
        var mix = new OutputMix(mixer, gate, gate) { DuckingEnabled = true, DuckingGain = 0.25f };
        var buffer = new float[Rate / 2 * 2];

        mix.Read(buffer, 0, buffer.Length);
        buffer[^1].Should().BeApproximately(0.8f, 1e-4f, "not talking: no ducking");

        gate.PushToTalkPressed = true;
        mix.Read(buffer, 0, buffer.Length);
        buffer[^1].Should().BeApproximately(0.2f, 1e-3f, "talking: sounds at -12 dB");

        // 250 ms release time constant: after 2 s the level is within 0.1% of full.
        gate.PushToTalkPressed = false;
        for (var i = 0; i < 4; i++)
        {
            mix.Read(buffer, 0, buffer.Length);
        }

        buffer[^1].Should().BeApproximately(0.8f, 0.002f, "released: sounds back to full level");
    }

    [Fact]
    public void Ducking_DisabledFlag_LeavesSoundsAlone()
    {
        var mixer = new Mixer();
        mixer.Add(new CachedVoice(Guid.NewGuid(), new CachedSound(Signals.Constant(Rate, 2, 0.8f)), new EditEnvelope(1f, 0, 0, Rate)));
        var gate = new PttGate { Source = new ConstantSampleProvider(0f), AlwaysOn = true };
        var mix = new OutputMix(mixer, gate, gate) { DuckingEnabled = false, DuckingGain = 0.25f };
        var buffer = new float[9_600];

        mix.Read(buffer, 0, buffer.Length);

        buffer[^1].Should().BeApproximately(0.8f, 1e-4f);
    }

    private static double DominantFrequency(ReadOnlySpan<float> interleavedStereo)
    {
        var crossings = 0;
        for (var i = 2; i < interleavedStereo.Length; i += 2)
        {
            if (interleavedStereo[i - 2] < 0f && interleavedStereo[i] >= 0f)
            {
                crossings++;
            }
        }

        return crossings / ((double)interleavedStereo.Length / 2 / Rate);
    }

    private static float Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
