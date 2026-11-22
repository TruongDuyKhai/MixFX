using System.Globalization;
using System.Text;
using Soundboard.Core.Models;

namespace Soundboard.Core.Bindings;

/// <summary>Human-readable names such as "Ctrl+Shift+Numpad 1" or "Mouse 4".</summary>
public static class TriggerFormatter
{
    private static readonly Dictionary<int, string> KeyNames = new()
    {
        [VirtualKeys.Backspace] = "Backspace",
        [VirtualKeys.Tab] = "Tab",
        [VirtualKeys.Enter] = "Enter",
        [VirtualKeys.Pause] = "Pause",
        [VirtualKeys.CapsLock] = "Caps Lock",
        [VirtualKeys.Escape] = "Esc",
        [VirtualKeys.Space] = "Space",
        [VirtualKeys.PageUp] = "Page Up",
        [VirtualKeys.PageDown] = "Page Down",
        [VirtualKeys.End] = "End",
        [VirtualKeys.Home] = "Home",
        [VirtualKeys.Left] = "←",
        [VirtualKeys.Up] = "↑",
        [VirtualKeys.Right] = "→",
        [VirtualKeys.Down] = "↓",
        [VirtualKeys.PrintScreen] = "Print Screen",
        [VirtualKeys.Insert] = "Insert",
        [VirtualKeys.Delete] = "Delete",
        [VirtualKeys.NumpadMultiply] = "Numpad *",
        [VirtualKeys.NumpadAdd] = "Numpad +",
        [VirtualKeys.NumpadSubtract] = "Numpad -",
        [VirtualKeys.NumpadDecimal] = "Numpad .",
        [VirtualKeys.NumpadDivide] = "Numpad /",
        [VirtualKeys.NumLock] = "Num Lock",
        [VirtualKeys.ScrollLock] = "Scroll Lock",
        [VirtualKeys.Shift] = "Shift",
        [VirtualKeys.LeftShift] = "Left Shift",
        [VirtualKeys.RightShift] = "Right Shift",
        [VirtualKeys.Control] = "Ctrl",
        [VirtualKeys.LeftControl] = "Left Ctrl",
        [VirtualKeys.RightControl] = "Right Ctrl",
        [VirtualKeys.Alt] = "Alt",
        [VirtualKeys.LeftAlt] = "Left Alt",
        [VirtualKeys.RightAlt] = "Right Alt",
        [VirtualKeys.LeftWin] = "Left Win",
        [VirtualKeys.RightWin] = "Right Win",
        [0xBA] = ";",
        [0xBB] = "=",
        [0xBC] = ",",
        [0xBD] = "-",
        [0xBE] = ".",
        [0xBF] = "/",
        [0xC0] = "`",
        [0xDB] = "[",
        [0xDC] = "\\",
        [0xDD] = "]",
        [0xDE] = "'",
    };

    private static readonly Dictionary<int, string> MouseNames = new()
    {
        [MouseButtonCodes.Left] = "Chuột trái",
        [MouseButtonCodes.Right] = "Chuột phải",
        [MouseButtonCodes.Middle] = "Chuột giữa",
        [MouseButtonCodes.X1] = "Mouse 4",
        [MouseButtonCodes.X2] = "Mouse 5",
    };

    public static string Format(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var text = new StringBuilder();
        AppendModifier(text, trigger.Modifiers, Modifiers.Ctrl, "Ctrl");
        AppendModifier(text, trigger.Modifiers, Modifiers.Alt, "Alt");
        AppendModifier(text, trigger.Modifiers, Modifiers.Shift, "Shift");
        AppendModifier(text, trigger.Modifiers, Modifiers.Win, "Win");
        text.Append(trigger.Device == InputDevice.Mouse ? MouseName(trigger.Code) : KeyName(trigger.Code));
        return text.ToString();
    }

    public static string KeyName(int virtualKey)
    {
        if (KeyNames.TryGetValue(virtualKey, out var name))
        {
            return name;
        }

        if (virtualKey is >= VirtualKeys.Digit0 and <= VirtualKeys.Digit9 or >= VirtualKeys.A and <= VirtualKeys.Z)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= VirtualKeys.Numpad0 and <= VirtualKeys.Numpad9)
        {
            return "Numpad " + (virtualKey - VirtualKeys.Numpad0).ToString(CultureInfo.InvariantCulture);
        }

        if (virtualKey is >= VirtualKeys.F1 and <= VirtualKeys.F24)
        {
            return "F" + (virtualKey - VirtualKeys.F1 + 1).ToString(CultureInfo.InvariantCulture);
        }

        return "Key 0x" + virtualKey.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static string MouseName(int code) =>
        MouseNames.TryGetValue(code, out var name) ? name : "Mouse " + code.ToString(CultureInfo.InvariantCulture);

    private static void AppendModifier(StringBuilder text, Modifiers held, Modifiers modifier, string name)
    {
        if (held.HasFlag(modifier))
        {
            text.Append(name).Append('+');
        }
    }
}
