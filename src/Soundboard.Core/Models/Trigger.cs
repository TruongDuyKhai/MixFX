namespace Soundboard.Core.Models;

/// <summary>
/// A physical input. <see cref="Code"/> is a virtual-key code for the keyboard or a button id for the
/// mouse; it is a plain int so Core stays free of Win32 types.
/// </summary>
public sealed record Trigger(InputDevice Device, int Code, Modifiers Modifiers);
