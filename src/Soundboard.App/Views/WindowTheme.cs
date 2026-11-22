using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Soundboard.App.Views;

/// <summary>Gives the native title bar the dark look of the rest of the window.</summary>
internal static class WindowTheme
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1+ / Windows 11).
    private const int UseImmersiveDarkMode = 20;

    public static void UseDarkTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.SourceInitialized += (_, _) =>
        {
            var enabled = 1;
            // Purely cosmetic: on older Windows the call fails and the default title bar stays.
            _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
