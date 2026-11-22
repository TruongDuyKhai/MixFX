using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class ExperimentalWindow : Window
{
    public ExperimentalWindow(ExperimentalViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);
    }
}
