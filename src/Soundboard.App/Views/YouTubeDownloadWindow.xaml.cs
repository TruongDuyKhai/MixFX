using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class YouTubeDownloadWindow : Window
{
    public YouTubeDownloadWindow(YouTubeDownloadViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);

        viewModel.CloseRequested += (_, _) => Close();
        Loaded += async (_, _) => await viewModel.InitializeAsync();

        // Closing with the title bar's X mid-download must stop yt-dlp and remove its partial files.
        Closing += (_, _) => viewModel.CancelRunning();
    }
}
