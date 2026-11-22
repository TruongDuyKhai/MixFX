using System.Windows;
using Soundboard.App.ViewModels;

namespace Soundboard.App.Views;

public partial class BindingEditorWindow : Window
{
    public BindingEditorWindow(BindingEditorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        WindowTheme.UseDarkTitleBar(this);

        viewModel.CloseRequested += (_, saved) =>
        {
            DialogResult = saved;
        };

        // Start listening right away: the dialog exists to capture one key.
        Loaded += (_, _) => viewModel.CaptureCommand.Execute(null);
        Closed += (_, _) => viewModel.StopCapture();
    }
}
