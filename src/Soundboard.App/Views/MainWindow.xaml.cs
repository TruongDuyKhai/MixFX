using System.Windows;
using System.Windows.Input;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // The tile whose mouse-down started a press, so its release is sent even if the pointer moved off it.
    private SoundTileViewModel? _pressedTile;

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

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // Minimising sends the app to the tray (§5.6); it keeps running and listening for hotkeys.
        if (WindowState == WindowState.Minimized)
        {
            Hide();
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
