using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class EditorWindow : Window
{
    public EditorWindow(EditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);

        viewModel.CloseRequested += (_, saved) => DialogResult = saved;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.OnClosed();
    }
}
