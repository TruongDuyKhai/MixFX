namespace Soundboard.Audio.Tests;

// The helper behind every "allocates nothing" test must still catch a callback that allocates.
public sealed class AllocationsTests
{
    private static object? _sink;

    [Fact]
    public void CallbackThatAllocatesOnEveryCall_IsCaught()
    {
        Allocations.InSteadyState(() => _sink = new byte[16]).Should().BeGreaterThanOrEqualTo(100 * 16);
    }

    [Fact]
    public void CallbackThatAllocatesEveryFewCalls_IsCaught()
    {
        var calls = 0;

        var allocated = Allocations.InSteadyState(() =>
        {
            if (++calls % 50 == 0)
            {
                _sink = new byte[16];
            }
        });

        allocated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ArithmeticOnly_IsZero()
    {
        var buffer = new float[256];

        Allocations.InSteadyState(() =>
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (buffer[i] * 0.5f) + 1f;
            }
        }).Should().Be(0);
    }
}
