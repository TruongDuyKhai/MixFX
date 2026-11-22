namespace Soundboard.Audio.Tests;

public sealed class PttGateTests
{
    private const int FadeFrames = 480; // 10 ms at 48 kHz

    [Fact]
    public void NotPressed_OutputsSilenceButStillDrainsTheMic()
    {
        var mic = new CountingSource(0.5f);
        var gate = new PttGate { Source = mic };

        var output = Signals.ReadAll(gate, 2_000);

        output.Should().OnlyContain(s => s == 0f);
        mic.SamplesRead.Should().Be(2_000);
        gate.IsTransmitting.Should().BeFalse();
    }

    [Fact]
    public void Pressed_FadesInOverTenMillisecondsThenPassesVoice()
    {
        var gate = new PttGate { Source = new ConstantSampleProvider(0.5f), PushToTalkPressed = true };

        var output = Signals.ReadAll(gate, (FadeFrames + 100) * 2);

        output[0].Should().BeLessThan(0.01f);
        output[((FadeFrames / 2) - 1) * 2].Should().BeApproximately(0.25f, 0.001f, "halfway through the fade");
        output[(FadeFrames - 1) * 2].Should().Be(0.5f, "fully open after 10 ms");
        output[(FadeFrames + 50) * 2].Should().Be(0.5f);
        gate.IsTransmitting.Should().BeTrue();
    }

    [Fact]
    public void Released_FadesOutOverTenMillisecondsThenSilence()
    {
        var gate = new PttGate { Source = new ConstantSampleProvider(0.5f), PushToTalkPressed = true };
        Signals.ReadAll(gate, FadeFrames * 4);

        gate.PushToTalkPressed = false;
        var output = Signals.ReadAll(gate, (FadeFrames + 100) * 2);

        output[0].Should().BeGreaterThan(0.49f);
        output[((FadeFrames / 2) - 1) * 2].Should().BeApproximately(0.25f, 0.001f, "halfway through the fade");
        output[(FadeFrames - 1) * 2].Should().Be(0f, "fully closed after 10 ms");
        output[(FadeFrames + 50) * 2].Should().Be(0f);
        gate.IsTransmitting.Should().BeFalse();
    }

    [Fact]
    public void FadeIsMonotonic_NoClickFromAJump()
    {
        var gate = new PttGate { Source = new ConstantSampleProvider(1f), PushToTalkPressed = true };

        var output = Signals.ReadAll(gate, FadeFrames * 2);

        for (var i = 2; i < output.Length; i += 2)
        {
            (output[i] - output[i - 2]).Should().BeInRange(0f, 1f / FadeFrames + 1e-6f);
        }
    }

    [Fact]
    public void AlwaysOn_PassesVoiceWithoutPtt()
    {
        var gate = new PttGate { Source = new ConstantSampleProvider(0.25f), AlwaysOn = true };

        var output = Signals.ReadAll(gate, FadeFrames * 4);

        output[^1].Should().Be(0.25f);
    }

    [Fact]
    public void NoMic_OutputsSilenceEvenWhenPressed()
    {
        var gate = new PttGate { PushToTalkPressed = true };

        Signals.ReadAll(gate, 1_000).Should().OnlyContain(s => s == 0f);
        gate.IsTransmitting.Should().BeFalse();
    }

    private sealed class CountingSource(float value) : NAudio.Wave.ISampleProvider
    {
        public int SamplesRead { get; private set; }

        public NAudio.Wave.WaveFormat WaveFormat => EngineFormat.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            buffer.AsSpan(offset, count).Fill(value);
            SamplesRead += count;
            return count;
        }
    }
}
