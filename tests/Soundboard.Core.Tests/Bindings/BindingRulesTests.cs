using Soundboard.Core.Actions;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Bindings;

public sealed class BindingRulesTests
{
    [Theory]
    [InlineData('W')]
    [InlineData('A')]
    [InlineData('4')]
    [InlineData('B')]
    [InlineData(VirtualKeys.Space)]
    [InlineData(VirtualKeys.LeftShift)]
    [InlineData(VirtualKeys.RightControl)]
    public void CommonGameKeys_AreFlagged(int vk)
    {
        BindingRules.IsCommonGameKey(new Trigger(InputDevice.Keyboard, vk, Modifiers.None)).Should().BeTrue();
    }

    [Fact]
    public void CommonGameKey_IsFlaggedEvenWithModifiers()
    {
        BindingRules.IsCommonGameKey(new Trigger(InputDevice.Keyboard, 'W', Modifiers.Ctrl)).Should().BeTrue();
    }

    [Theory]
    [InlineData(VirtualKeys.Numpad0 + 1)]
    [InlineData(VirtualKeys.F1 + 12)]
    [InlineData('5')]
    [InlineData('P')]
    public void OtherKeys_AreNotFlagged(int vk)
    {
        BindingRules.IsCommonGameKey(new Trigger(InputDevice.Keyboard, vk, Modifiers.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(MouseButtonCodes.Left, true)]
    [InlineData(MouseButtonCodes.Right, true)]
    [InlineData(MouseButtonCodes.X1, false)]
    [InlineData(MouseButtonCodes.X2, false)]
    public void MouseButtons_LeftAndRightAreFlagged(int code, bool expected)
    {
        BindingRules.IsCommonGameKey(new Trigger(InputDevice.Mouse, code, Modifiers.None)).Should().Be(expected);
    }

    [Fact]
    public void FindConflicts_MatchesSameTriggerExceptTheOneBeingEdited()
    {
        var trigger = new Trigger(InputDevice.Keyboard, VirtualKeys.Numpad0, Modifiers.Ctrl);
        var editing = Binding(trigger);
        var other = Binding(trigger);
        var different = Binding(trigger with { Modifiers = Modifiers.None });

        BindingRules.FindConflicts(trigger, [editing, other, different], editing.Id).Should().Equal(other);
    }

    [Fact]
    public void Warnings_ListBothProblems()
    {
        var trigger = new Trigger(InputDevice.Keyboard, 'G', Modifiers.None);

        BindingRules.Warnings(trigger, [Binding(trigger)]).Should().HaveCount(2);
    }

    [Fact]
    public void Warnings_EmptyForSafeUniqueKey()
    {
        var trigger = new Trigger(InputDevice.Keyboard, VirtualKeys.F1 + 12, Modifiers.None);

        BindingRules.Warnings(trigger, []).Should().BeEmpty();
    }

    private static Binding Binding(Trigger trigger) =>
        new(Guid.NewGuid(), trigger, ActionIds.PlaySound, Guid.NewGuid(), TriggerMode.Press, PassThrough: true);
}
