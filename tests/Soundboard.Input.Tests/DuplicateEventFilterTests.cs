using Soundboard.Input.Hooks;

namespace Soundboard.Input.Tests;

public sealed class DuplicateEventFilterTests
{
    private const int KeyDown = 0x0100;
    private const int KeyUp = 0x0101;

    [Fact]
    public void SameEventTwiceInARow_SecondIsDuplicate()
    {
        var filter = new DuplicateEventFilter();

        filter.IsDuplicate(KeyDown, 0x87, 1000).Should().BeFalse();
        filter.IsDuplicate(KeyDown, 0x87, 1000).Should().BeTrue();
    }

    [Fact]
    public void AutoRepeat_HasANewTimestampAndPasses()
    {
        var filter = new DuplicateEventFilter();

        filter.IsDuplicate(KeyDown, 0x87, 1000).Should().BeFalse();
        filter.IsDuplicate(KeyDown, 0x87, 1033).Should().BeFalse();
    }

    [Fact]
    public void DownAndUpInTheSameMillisecond_AreBothKept()
    {
        var filter = new DuplicateEventFilter();

        filter.IsDuplicate(KeyDown, 0x87, 1000).Should().BeFalse();
        filter.IsDuplicate(KeyUp, 0x87, 1000).Should().BeFalse();
    }

    [Fact]
    public void DifferentKeysInTheSameMillisecond_AreBothKept()
    {
        var filter = new DuplicateEventFilter();

        filter.IsDuplicate(KeyDown, 0x41, 1000).Should().BeFalse();
        filter.IsDuplicate(KeyDown, 0x42, 1000).Should().BeFalse();
    }
}
