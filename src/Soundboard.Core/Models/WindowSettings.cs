namespace Soundboard.Core.Models;

/// <summary>How the main window behaves (§5.6).</summary>
public sealed record WindowSettings
{
    /// <summary>
    /// Asks by default: closing used to quit, minimising used to hide in the tray, and neither is a safe
    /// guess now that minimising goes to the taskbar. "Không hỏi lại" in the question stores the answer here.
    /// </summary>
    public CloseAction CloseAction { get; init; } = CloseAction.Ask;
}
