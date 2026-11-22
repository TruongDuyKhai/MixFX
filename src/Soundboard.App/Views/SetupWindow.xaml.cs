using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class SetupWindow : Window
{
    public SetupWindow(SetupViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);
    }
}
