using Soundboard.Audio.Decoding;
using Soundboard.Audio.Mixing;

namespace Soundboard.Audio.Tests.Mixing;

public sealed class MixerTests
{
    [Fact]
    public void NoVoices_ReturnsFullBufferOfSilence()
    {
        var mixer = new Mixer();
        var buffer = Signals.Constant(512, 1, 9f);

        var read = mixer.Read(buffer, 0, buffer.Length);

        read.Should().Be(buffer.Length);
        buffer.Should().OnlyContain(s => s == 0f);
    }

    [Fact]
    public void TwoVoices_AreSummed()
    {
        var mixer = new Mixer();
        mixer.Add(Voice(1_000, 0.25f));
        mixer.Add(Voice(1_000, 0.5f));
        var buffer = new float[200];

        mixer.Read(buffer, 0, buffer.Length);

        buffer.Should().OnlyContain(s => s == 0.75f);
    }

    [Fact]
    public void FinishedVoice_IsRemovedAndRestIsSilence()
    {
        var mixer = new Mixer();
        var voice = Voice(50, 1f);
        mixer.Add(voice);
        var buffer = new float[200];

        mixer.Read(buffer, 0, buffer.Length);
        mixer.Read(buffer, 0, buffer.Length);

        voice.IsFinished.Should().BeTrue();
        buffer.Should().OnlyContain(s => s == 0f);
    }

    [Fact]
    public void Reset_FinishesQueuedAndPlayingVoices()
    {
        var mixer = new Mixer();
        var playing = Voice(10_000, 1f);
        var queued = Voice(10_000, 1f);
        mixer.Add(playing);
        mixer.Read(new float[100], 0, 100);
        mixer.Add(queued);

        mixer.Reset();

        playing.IsFinished.Should().BeTrue();
        queued.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void BeyondPolyphonyLimit_ExtraVoicesAreDropped()
    {
        var mixer = new Mixer();
        var voices = Enumerable.Range(0, EngineFormat.MaxVoicesPerOutput + 1).Select(_ => Voice(10_000, 0f)).ToList();
        voices.ForEach(mixer.Add);

        mixer.Read(new float[100], 0, 100);

        voices[^1].IsFinished.Should().BeTrue();
        voices[0].IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Read_SteadyState_AllocatesNothing()
    {
        // PROJECT_PROMPT.md §7.3.8: no allocation inside the audio callback.
        var mixer = new Mixer();
        for (var i = 0; i < 8; i++)
        {
            mixer.Add(Voice(480_000, 0.1f));
        }

        var buffer = new float[1_440 * 2];

        Allocations.InSteadyState(() => mixer.Read(buffer, 0, buffer.Length)).Should().Be(0);
    }

    [Fact]
    public void OutputMixWithGateAndLimiter_SteadyState_AllocatesNothing()
    {
        var mixer = new Mixer();
        mixer.Add(Voice(480_000, 0.9f));
        var gate = new PttGate { Source = new ConstantSampleProvider(0.9f), PushToTalkPressed = true };
        var mix = new OutputMix(mixer, gate) { SoundVolume = 0.8f, VoiceVolume = 1.2f };
        var limiter = new Soundboard.Audio.Dsp.PeakLimiter(mix, -1f, 100f);
        var buffer = new float[1_440 * 2];

        Allocations.InSteadyState(() => limiter.Read(buffer, 0, buffer.Length)).Should().Be(0);
    }

    private static CachedVoice Voice(int frames, float value) =>
        new(Guid.NewGuid(), new CachedSound(Signals.Constant(frames, 2, value)), new EditEnvelope(1f, 0, 0, frames));
}
