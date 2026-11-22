using Soundboard.Audio.Buffers;

namespace Soundboard.Audio.Tests.Buffers;

public sealed class SpscRingBufferTests
{
    [Fact]
    public void Capacity_IsRoundedUpToPowerOfTwo()
    {
        new SpscRingBuffer(1_000).Capacity.Should().Be(1_024);
    }

    [Fact]
    public void WriteThenRead_ReturnsSameSamplesAcrossTheWrap()
    {
        var ring = new SpscRingBuffer(8);
        ring.Write([1, 2, 3, 4, 5, 6]);
        ring.Read(new float[4]);

        ring.Write([7, 8, 9, 10, 11]).Should().Be(5);
        var output = new float[8];
        var read = ring.Read(output);

        read.Should().Be(7);
        output[..7].Should().Equal(5, 6, 7, 8, 9, 10, 11);
    }

    [Fact]
    public void Write_WhenFull_WritesOnlyWhatFits()
    {
        var ring = new SpscRingBuffer(4);

        ring.Write([1, 2, 3, 4, 5, 6]).Should().Be(4);
        ring.Count.Should().Be(4);
    }

    [Fact]
    public void Read_WhenEmpty_ReturnsZero()
    {
        new SpscRingBuffer(4).Read(new float[4]).Should().Be(0);
    }

    [Fact]
    public void Skip_DropsOldestSamples()
    {
        var ring = new SpscRingBuffer(8);
        ring.Write([1, 2, 3, 4, 5]);

        ring.Skip(3).Should().Be(3);
        var output = new float[2];
        ring.Read(output);

        output.Should().Equal(4, 5);
    }

    [Fact]
    public async Task ConcurrentProducerAndConsumer_PreserveOrder()
    {
        var ring = new SpscRingBuffer(256);
        const int Total = 200_000;
        var received = new List<float>(Total);

        var producer = Task.Run(() =>
        {
            var next = 0;
            var chunk = new float[37];
            while (next < Total)
            {
                var n = Math.Min(chunk.Length, Total - next);
                for (var i = 0; i < n; i++)
                {
                    chunk[i] = next + i;
                }

                var written = ring.Write(chunk.AsSpan(0, n));
                next += written;
            }
        }, TestContext.Current.CancellationToken);

        var buffer = new float[53];
        while (received.Count < Total)
        {
            var n = ring.Read(buffer);
            received.AddRange(buffer.AsSpan(0, n).ToArray());
        }

        await producer;
        received.Should().Equal(Enumerable.Range(0, Total).Select(i => (float)i));
    }
}
