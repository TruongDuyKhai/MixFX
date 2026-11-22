namespace Soundboard.Core.Bindings;

/// <summary>
/// Windows virtual-key codes the app reasons about. Plain ints so Core stays free of Win32 types;
/// values are fixed by Windows and stored in users' bindings.
/// </summary>
public static class VirtualKeys
{
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Alt = 0x12;
    public const int Pause = 0x13;
    public const int CapsLock = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int PrintScreen = 0x2C;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int Digit0 = 0x30;
    public const int Digit9 = 0x39;
    public const int A = 0x41;
    public const int Z = 0x5A;
    public const int LeftWin = 0x5B;
    public const int RightWin = 0x5C;
    public const int Numpad0 = 0x60;
    public const int Numpad9 = 0x69;
    public const int NumpadMultiply = 0x6A;
    public const int NumpadAdd = 0x6B;
    public const int NumpadSubtract = 0x6D;
    public const int NumpadDecimal = 0x6E;
    public const int NumpadDivide = 0x6F;
    public const int F1 = 0x70;
    public const int F24 = 0x87;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftAlt = 0xA4;
    public const int RightAlt = 0xA5;

    /// <summary>Which modifier a key is, or None for ordinary keys.</summary>
    public static Models.Modifiers ModifierOf(int virtualKey) => virtualKey switch
    {
        Control or LeftControl or RightControl => Models.Modifiers.Ctrl,
        Alt or LeftAlt or RightAlt => Models.Modifiers.Alt,
        Shift or LeftShift or RightShift => Models.Modifiers.Shift,
        LeftWin or RightWin => Models.Modifiers.Win,
        _ => Models.Modifiers.None,
    };
}
