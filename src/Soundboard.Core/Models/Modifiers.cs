namespace Soundboard.Core.Models;

/// <summary>Modifier keys that must be held for a <see cref="Trigger"/> to fire.</summary>
[Flags]
public enum Modifiers
{
    None = 0,
    Ctrl = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3,
}
