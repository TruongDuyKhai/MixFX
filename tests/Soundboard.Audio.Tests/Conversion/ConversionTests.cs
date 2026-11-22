using NAudio.Wave;
using Soundboard.Audio.Conversion;

namespace Soundboard.Audio.Tests.Conversion;

public sealed class ConversionTests
{
    [Fact]
    public void MonoToStereo_DuplicatesEachSample()
    {
        var stereo = new MonoToStereo(new ArraySampleProvider([0.1f, 0.2f, 0.3f], channels: 1));

        Signals.ReadAll(stereo, 6).Should().Equal(0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f);
        stereo.WaveFormat.Channels.Should().Be(2);
    }

    [Fact]
    public void ChannelAdapter_ToMono_Averages()
    {
        var adapter = new ChannelAdapter(new ArraySampleProvider([0.2f, 0.4f, 1f, -1f]), 1);

        Signals.ReadAll(adapter, 2).Should().Equal([0.3f, 0f], (a, b) => MathF.Abs(a - b) < 1e-6f);
    }

    [Fact]
    public void ChannelAdapter_ToSurround_PutsStereoOnFrontPairAndSilenceElsewhere()
    {
        var adapter = new ChannelAdapter(new ArraySampleProvider([0.2f, 0.4f]), 6);

        Signals.ReadAll(adapter, 6).Should().Equal(0.2f, 0.4f, 0f, 0f, 0f, 0f);
    }

    [Fact]
    public void FrontPairSelector_KeepsFirstTwoChannels()
    {
        var selector = new FrontPairSelector(new ArraySampleProvider([1f, 2f, 3f, 4f, 5f, 6f], channels: 3));

        Signals.ReadAll(selector, 4).Should().Equal(1f, 2f, 4f, 5f);
    }

    [Fact]
    public void FloatToWaveProvider_WritesFloatBytes()
    {
        var provider = new FloatToWaveProvider(new ArraySampleProvider([0.5f, -0.25f]), WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2));
        var bytes = new byte[8];

        provider.Read(bytes, 0, 8).Should().Be(8);

        BitConverter.ToSingle(bytes, 0).Should().Be(0.5f);
        BitConverter.ToSingle(bytes, 4).Should().Be(-0.25f);
    }

    [Fact]
    public void FloatToWaveProvider_RejectsNonFloatDeviceFormat()
    {
        var act = () => new FloatToWaveProvider(new ArraySampleProvider([]), new WaveFormat(48_000, 16, 2));

        act.Should().Throw<ArgumentException>();
    }
}
