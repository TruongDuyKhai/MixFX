using System.Windows;
using Forms = System.Windows.Forms;

namespace Soundboard.App.Services;

/// <summary>System tray icon (§5.6): double-click to open, menu to stop all sounds or quit.</summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/app.ico");

    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _image;

    public TrayIcon(Action show, Action stopAll, Action exit)
    {
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(stopAll);
        ArgumentNullException.ThrowIfNull(exit);

        using (var stream = Application.GetResourceStream(IconUri)!.Stream)
        {
            _image = new System.Drawing.Icon(stream);
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mở Soundboard", null, (_, _) => show());
        menu.Items.Add("Dừng tất cả sound", null, (_, _) => stopAll());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Thoát", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = "Soundboard",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => show();
    }

    public void Dispose()
    {
        // Hide first, otherwise a ghost icon stays in the tray until the mouse passes over it.
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
