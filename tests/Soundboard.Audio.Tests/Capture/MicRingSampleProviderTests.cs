using Soundboard.Audio.Buffers;
using Soundboard.Audio.Capture;

namespace Soundboard.Audio.Tests.Capture;

public sealed class MicRingSampleProviderTests
{
    private const int Rate = 48_000;
    private const int Target = Rate * EngineFormat.MicTargetLatencyMs / 1000;
    private const int Max = Rate * EngineFormat.MicMaxLatencyMs / 1000;

    [Fact]
    public void BelowTargetLatency_OutputsSilenceAndKeepsBuffering()
    {
        var ring = new SpscRingBuffer(Rate);
        ring.Write(Signals.Constant(Target - 1, 1, 0.5f));
        var mic = new MicRingSampleProvider(ring, Rate);

        var output = Signals.ReadAll(mic, 100);

        output.Should().HaveCount(100).And.OnlyContain(s => s == 0f);
        ring.Count.Should().Be(Target - 1);
    }

    [Fact]
    public void AtTargetLatency_StartsPlaying()
    {
        var ring = new SpscRingBuffer(Rate);
        ring.Write(Signals.Constant(Target, 1, 0.5f));
        var mic = new MicRingSampleProvider(ring, Rate);

        Signals.ReadAll(mic, 100).Should().OnlyContain(s => s == 0.5f);
    }

    [Fact]
    public void AboveMaxLatency_TrimsBackToTarget()
    {
        var ring = new SpscRingBuffer(Rate);
        ring.Write(Signals.Constant(Max + 500, 1, 0.5f));
        var mic = new MicRingSampleProvider(ring, Rate);

        mic.Read(new float[100], 0, 100);

        ring.Count.Should().Be(Target - 100);
    }

    [Fact]
    public void Underrun_FillsSilenceNeverShortReadsAndRebuffers()
    {
        var ring = new SpscRingBuffer(Rate);
        ring.Write(Signals.Constant(Target, 1, 0.5f));
        var mic = new MicRingSampleProvider(ring, Rate);
        var buffer = new float[Target + 50];

        var read = mic.Read(buffer, 0, buffer.Length);

        read.Should().Be(buffer.Length);
        buffer[Target - 1].Should().Be(0.5f);
        buffer[Target].Should().Be(0f);

        ring.Write(Signals.Constant(10, 1, 0.5f));
        Signals.ReadAll(mic, 5).Should().OnlyContain(s => s == 0f, "it waits for the target cushion again");
    }
}
