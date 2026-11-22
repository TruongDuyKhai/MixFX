using Soundboard.Core.Models;

namespace Soundboard.Input;

/// <summary>One key or mouse button transition as seen by the low-level hook.</summary>
internal readonly record struct RawInputEvent(InputDevice Device, int Code, bool IsDown);
