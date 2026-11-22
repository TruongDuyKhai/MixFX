using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Soundboard.App.ViewModels;
using Soundboard.App.Views;
using Soundboard.Core.Abstractions;

namespace Soundboard.App.Services;

public sealed class DialogService(IServiceProvider services) : IDialogService
{
    private const string AppTitle = "Soundboard";

    private static Window? Owner => Application.Current.MainWindow is { IsVisible: true } main ? main : null;

    public bool Confirm(string message, string title) =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message) => Show(message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public void EditBinding(string actionId, Guid? targetId, string title, bool allowModeChoice)
    {
        var viewModel = new BindingEditorViewModel(
            services.GetRequiredService<IInputService>(),
            services.GetRequiredService<ISettingsProvider>(),
            actionId,
            targetId,
            title,
            allowModeChoice);
        new BindingEditorWindow(viewModel) { Owner = Owner }.ShowDialog();
    }

    public void ShowGlobalHotkeys() =>
        new GlobalHotkeysWindow(services.GetRequiredService<GlobalHotkeysViewModel>()) { Owner = Owner }.ShowDialog();

    public void ShowSetup() =>
        new SetupWindow(services.GetRequiredService<SetupViewModel>()) { Owner = Owner }.ShowDialog();

    public void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    private static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
}
