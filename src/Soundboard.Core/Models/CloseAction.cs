namespace Soundboard.Core.Models;

/// <summary>What the main window's close button (X, Alt+F4) does.</summary>
public enum CloseAction
{
    /// <summary>Ask each time: send the window to the tray, or quit.</summary>
    Ask,

    /// <summary>Hide the window; the app keeps running in the tray with hotkeys working.</summary>
    MinimizeToTray,

    /// <summary>Quit the app.</summary>
    Exit,
}
