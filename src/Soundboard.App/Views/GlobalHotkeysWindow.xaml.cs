using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class GlobalHotkeysWindow : Window
{
    public GlobalHotkeysWindow(GlobalHotkeysViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);
    }
}
