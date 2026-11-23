using System.Windows;
using Soundboard.App.Services;
using Soundboard.Core.Models;

namespace Soundboard.App.Views;

/// <summary>
/// "Bạn muốn làm gì?" when the main window's X is pressed. Only collects the answer; remembering and acting
/// on it is <see cref="ViewModels.MainViewModel.ResolveCloseRequest"/>'s job, so that logic stays testable.
/// </summary>
public partial class CloseChoiceWindow : Window
{
    public CloseChoiceWindow()
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
    }

    /// <summary>The answer, or null when the user pressed "Hủy", Esc or the title bar's X.</summary>
    public CloseChoice? Choice { get; private set; }

    private void OnMinimizeToTray(object sender, RoutedEventArgs e) => Answer(CloseAction.MinimizeToTray);

    private void OnExit(object sender, RoutedEventArgs e) => Answer(CloseAction.Exit);

    private void Answer(CloseAction action)
    {
        Choice = new CloseChoice(action, RememberBox.IsChecked == true);
        DialogResult = true;
    }
}
