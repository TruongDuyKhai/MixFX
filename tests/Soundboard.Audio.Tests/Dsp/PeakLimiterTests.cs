using Soundboard.Audio.Dsp;

namespace Soundboard.Audio.Tests.Dsp;

public sealed class PeakLimiterTests
{
    private static readonly float Ceiling = Decibels.ToGain(-1f);

    [Fact]
    public void QuietSignal_PassesUnchanged()
    {
        var input = Signals.Sine(4_800, 2, amplitude: 0.5f);
        var limiter = new PeakLimiter(new ArraySampleProvider((float[])input.Clone()), -1f, 100f);

        var output = Signals.ReadAll(limiter, input.Length);

        output.Should().Equal(input, (a, b) => MathF.Abs(a - b) < 1e-6f);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    [InlineData(8.0f)]
    public void LoudSignal_NeverExceedsCeiling(float amplitude)
    {
        var limiter = new PeakLimiter(new ArraySampleProvider(Signals.Sine(48_000, 2, amplitude)), -1f, 100f);

        var output = Signals.ReadAll(limiter, 96_000);

        Signals.Peak(output).Should().BeLessThanOrEqualTo(Ceiling + 1e-6f);
    }

    [Fact]
    public void Impulse_IsClampedInstantlyWithoutLookahead()
    {
        var input = new float[200];
        input[100] = 4f;
        input[101] = -4f;
        var limiter = new PeakLimiter(new ArraySampleProvider(input), -1f, 100f);

        var output = Signals.ReadAll(limiter, input.Length);

        MathF.Abs(output[100]).Should().BeApproximately(Ceiling, 1e-6f);
        output.AsSpan(0, 100).ToArray().Should().OnlyContain(s => s == 0f);
    }

    [Fact]
    public void AfterLoudBurst_GainRecoversDuringRelease()
    {
        var loud = Signals.Constant(480, 2, 4f);
        var quiet = Signals.Constant(48_000, 2, 0.1f);
        var limiter = new PeakLimiter(new ArraySampleProvider([.. loud, .. quiet]), -1f, 100f);

        var output = Signals.ReadAll(limiter, loud.Length + quiet.Length);

        limiter.CurrentGain.Should().BeGreaterThan(0.99f);
        output[^1].Should().BeApproximately(0.1f, 0.002f);
        output[loud.Length].Should().BeLessThan(0.1f); // still recovering right after the burst
    }

    [Fact]
    public void Silence_StaysSilent()
    {
        var limiter = new PeakLimiter(new ArraySampleProvider(new float[9_600]), -1f, 100f);

        Signals.ReadAll(limiter, 9_600).Should().OnlyContain(s => s == 0f);
    }
}
