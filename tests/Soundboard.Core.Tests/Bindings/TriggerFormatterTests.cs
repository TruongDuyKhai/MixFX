using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Bindings;

public sealed class TriggerFormatterTests
{
    [Theory]
    [InlineData(VirtualKeys.Numpad0 + 1, Modifiers.None, "Numpad 1")]
    [InlineData(VirtualKeys.F1 + 12, Modifiers.None, "F13")]
    [InlineData(VirtualKeys.F1 + 23, Modifiers.None, "F24")]
    [InlineData('K', Modifiers.Ctrl | Modifiers.Shift, "Ctrl+Shift+K")]
    [InlineData('7', Modifiers.Alt | Modifiers.Win, "Alt+Win+7")]
    [InlineData(VirtualKeys.Space, Modifiers.None, "Space")]
    [InlineData(0xFF, Modifiers.None, "Key 0xFF")]
    public void Keyboard(int vk, Modifiers modifiers, string expected)
    {
        TriggerFormatter.Format(new Trigger(InputDevice.Keyboard, vk, modifiers)).Should().Be(expected);
    }

    [Theory]
    [InlineData(MouseButtonCodes.X1, "Mouse 4")]
    [InlineData(MouseButtonCodes.X2, "Mouse 5")]
    [InlineData(MouseButtonCodes.Middle, "Chuột giữa")]
    public void Mouse(int code, string expected)
    {
        TriggerFormatter.Format(new Trigger(InputDevice.Mouse, code, Modifiers.None)).Should().Be(expected);
    }
}
