using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Soundboard.App.ViewModels;
using Soundboard.Core.Models;

namespace Soundboard.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // The tile whose mouse-down started a press, so its release is sent even if the pointer moved off it.
    private SoundTileViewModel? _pressedTile;

    private bool _closeWithoutAsking;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);
    }

    /// <summary>Brings the window back from the tray.</summary>
    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>
    /// The next close quits without the question: "Thoát" in the tray menu, or Windows signing out. A dialog
    /// there would hold up shutdown, and the tray menu already says what it does.
    /// </summary>
    public void AllowCloseWithoutAsking() => _closeWithoutAsking = true;

    // Minimising is left to Windows (taskbar), so the board is one click away during a game; the tray is
    // reached through the close button instead (§5.6).
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closeWithoutAsking)
        {
            return;
        }

        switch (_viewModel.ResolveCloseRequest())
        {
            case CloseAction.Exit:
                // ShutdownMode is OnMainWindowClose: letting this window close quits the app.
                break;
            case CloseAction.MinimizeToTray:
                e.Cancel = true;
                Hide();
                break;
            default:
                e.Cancel = true;
                break;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        // async void: WPF event handler. ImportAsync reports its own errors to the user.
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            await _viewModel.ImportAsync(files);
        }
    }

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SoundTileViewModel tile } element)
        {
            return;
        }

        _pressedTile = tile;
        element.CaptureMouse();
        _viewModel.PressTile(tile, down: true);
        e.Handled = true;
    }

    private void OnTileMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            // Releasing capture raises LostMouseCapture, which sends the key-up.
            element.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnTileLostCapture(object sender, MouseEventArgs e)
    {
        if (_pressedTile is { } tile)
        {
            _pressedTile = null;
            _viewModel.PressTile(tile, down: false);
        }
    }
}
