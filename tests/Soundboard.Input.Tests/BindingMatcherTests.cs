using Soundboard.Core.Actions;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.Input.Tests;

public sealed class BindingMatcherTests
{
    private const int Numpad1 = VirtualKeys.Numpad0 + 1;
    private const int F13 = VirtualKeys.F1 + 12;

    private readonly HashSet<int> _physicallyDown = [];
    private readonly BindingMatcher _matcher;
    private readonly List<MatchedTrigger> _fired = [];

    public BindingMatcherTests()
    {
        _matcher = new BindingMatcher(_physicallyDown.Contains);
    }

    [Fact]
    public void SingleKey_FiresDownThenUp()
    {
        var binding = Bind(Key(Numpad1));

        Press(Numpad1);
        Release(Numpad1);

        _fired.Should().Equal(new MatchedTrigger(binding, TriggerPhase.Down), new MatchedTrigger(binding, TriggerPhase.Up));
    }

    [Fact]
    public void UnboundKey_FiresNothingAndPassesThrough()
    {
        Bind(Key(Numpad1));

        Press(F13).Should().BeFalse();
        Release(F13).Should().BeFalse();

        _fired.Should().BeEmpty();
    }

    [Fact]
    public void Combo_FiresOnlyWithExactModifiers()
    {
        var binding = Bind(Key(Numpad1, Modifiers.Ctrl | Modifiers.Shift));

        Press(Numpad1);
        Release(Numpad1);
        _fired.Should().BeEmpty("no modifiers held");

        Press(VirtualKeys.LeftControl);
        Press(Numpad1);
        Release(Numpad1);
        _fired.Should().BeEmpty("only Ctrl held");

        Press(VirtualKeys.RightShift);
        Press(Numpad1);
        _fired.Should().ContainSingle().Which.Should().Be(new MatchedTrigger(binding, TriggerPhase.Down));
    }

    [Fact]
    public void PlainBinding_DoesNotFireWhileAModifierIsHeld()
    {
        Bind(Key(Numpad1));

        Press(VirtualKeys.LeftAlt);
        Press(Numpad1);

        _fired.Should().BeEmpty();
    }

    [Fact]
    public void KeyUp_FiresEvenIfModifierWasReleasedFirst()
    {
        var binding = Bind(Key(Numpad1, Modifiers.Ctrl), mode: TriggerMode.Hold);

        Press(VirtualKeys.LeftControl);
        Press(Numpad1);
        Release(VirtualKeys.LeftControl);
        Release(Numpad1);

        _fired.Should().Equal(new MatchedTrigger(binding, TriggerPhase.Down), new MatchedTrigger(binding, TriggerPhase.Up));
    }

    [Fact]
    public void AutoRepeat_FiresOnlyOnce()
    {
        Bind(Key(Numpad1));

        Press(Numpad1);
        Press(Numpad1);
        Press(Numpad1);

        _fired.Should().ContainSingle();
    }

    [Fact]
    public void MissedKeyUp_NextPressFiresAgain()
    {
        Bind(Key(Numpad1));
        Press(Numpad1);
        _physicallyDown.Remove(Numpad1); // the key-up never reached the hook (e.g. Win+L)

        _matcher.Process(Down(Numpad1), _fired);

        _fired.Should().HaveCount(2);
    }

    [Fact]
    public void StuckModifier_IsDroppedWhenWindowsSaysItIsUp()
    {
        Bind(Key(Numpad1));
        Press(VirtualKeys.LeftControl);
        _physicallyDown.Remove(VirtualKeys.LeftControl); // its key-up was lost

        Press(Numpad1);

        _fired.Should().ContainSingle("Ctrl is no longer considered held");
    }

    [Fact]
    public void PassThroughTrue_LetsTheGameSeeTheKey()
    {
        Bind(Key(Numpad1), passThrough: true);

        Press(Numpad1).Should().BeFalse();
        Release(Numpad1).Should().BeFalse();
    }

    [Fact]
    public void PassThroughFalse_HidesDownRepeatAndUp()
    {
        Bind(Key(Numpad1), passThrough: false);

        Press(Numpad1).Should().BeTrue();
        Press(Numpad1).Should().BeTrue("auto-repeat");
        Release(Numpad1).Should().BeTrue();
    }

    [Fact]
    public void TwoBindingsOnSameTrigger_BothFire()
    {
        Bind(Key(Numpad1));
        Bind(Key(Numpad1));

        Press(Numpad1);

        _fired.Should().HaveCount(2);
    }

    [Fact]
    public void MouseSideButton_Fires()
    {
        var binding = Bind(new Trigger(InputDevice.Mouse, MouseButtonCodes.X1, Modifiers.None));

        _matcher.Process(new RawInputEvent(InputDevice.Mouse, MouseButtonCodes.X1, true), _fired);
        _matcher.Process(new RawInputEvent(InputDevice.Mouse, MouseButtonCodes.X1, false), _fired);

        _fired.Should().Equal(new MatchedTrigger(binding, TriggerPhase.Down), new MatchedTrigger(binding, TriggerPhase.Up));
        _matcher.NeedsMouse.Should().BeTrue();
    }

    [Fact]
    public void KeyboardOnlyBindings_DoNotNeedTheMouseHook()
    {
        Bind(Key(Numpad1));

        _matcher.NeedsMouse.Should().BeFalse();
    }

    [Fact]
    public async Task Capture_ReturnsKeyWithModifiersAndSwallowsIt()
    {
        Bind(Key(Numpad1));
        var capture = new TaskCompletionSource<Trigger?>();
        _matcher.BeginCapture(capture);

        Press(VirtualKeys.LeftControl).Should().BeFalse();
        Press(Numpad1).Should().BeTrue();
        Press(Numpad1).Should().BeTrue("repeat of the captured key");
        Release(Numpad1).Should().BeTrue();

        (await capture.Task).Should().Be(Key(Numpad1, Modifiers.Ctrl));
        _fired.Should().BeEmpty("bindings do not fire while assigning a key");
        _matcher.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public async Task Capture_EscapeCancels()
    {
        var capture = new TaskCompletionSource<Trigger?>();
        _matcher.BeginCapture(capture);

        Press(VirtualKeys.Escape);

        (await capture.Task).Should().BeNull();
    }

    [Fact]
    public async Task Capture_IgnoresLeftClickButTakesSideButton()
    {
        var capture = new TaskCompletionSource<Trigger?>();
        _matcher.BeginCapture(capture);

        _matcher.Process(new RawInputEvent(InputDevice.Mouse, MouseButtonCodes.Left, true), _fired).Should().BeFalse();
        capture.Task.IsCompleted.Should().BeFalse();
        _matcher.Process(new RawInputEvent(InputDevice.Mouse, MouseButtonCodes.X2, true), _fired);

        (await capture.Task).Should().Be(new Trigger(InputDevice.Mouse, MouseButtonCodes.X2, Modifiers.None));
    }

    [Fact]
    public async Task CancelCapture_CompletesWithNull()
    {
        var capture = new TaskCompletionSource<Trigger?>();
        _matcher.BeginCapture(capture);

        _matcher.CancelCapture(capture);

        (await capture.Task).Should().BeNull();
        _matcher.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void SetBindings_ReplacesPreviousOnes()
    {
        Bind(Key(Numpad1));
        _matcher.SetBindings([]);

        Press(Numpad1);

        _fired.Should().BeEmpty();
    }

    private readonly List<Binding> _bindings = [];

    private Binding Bind(Trigger trigger, bool passThrough = true, TriggerMode mode = TriggerMode.Press)
    {
        var binding = new Binding(Guid.NewGuid(), trigger, ActionIds.PlaySound, Guid.NewGuid(), mode, passThrough);
        _bindings.Add(binding);
        _matcher.SetBindings(_bindings);
        return binding;
    }

    private static Trigger Key(int vk, Modifiers modifiers = Modifiers.None) => new(InputDevice.Keyboard, vk, modifiers);

    private static RawInputEvent Down(int vk) => new(InputDevice.Keyboard, vk, true);

    private bool Press(int vk)
    {
        var suppress = _matcher.Process(Down(vk), _fired);
        _physicallyDown.Add(vk); // Windows updates its key state after the hook runs
        return suppress;
    }

    private bool Release(int vk)
    {
        var suppress = _matcher.Process(new RawInputEvent(InputDevice.Keyboard, vk, false), _fired);
        _physicallyDown.Remove(vk);
        return suppress;
    }
}
