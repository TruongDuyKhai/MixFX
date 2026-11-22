using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Soundboard.App.ViewModels;
using Soundboard.App.Views;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Downloads;
using Soundboard.Core.Models;

namespace Soundboard.App.Services;

public sealed class DialogService(IServiceProvider services) : IDialogService
{
    private const string AppTitle = "Soundboard";
    private const string WavFilter = "WAV (*.wav)|*.wav";

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

    public void ShowExperimental() =>
        new ExperimentalWindow(new ExperimentalViewModel(services.GetRequiredService<ISettingsProvider>(), this)) { Owner = Owner }.ShowDialog();

    public void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    public void EditSound(Guid soundId)
    {
        var sound = services.GetRequiredService<ISettingsProvider>().Current.Sounds.FirstOrDefault(s => s.Id == soundId);
        if (sound is null)
        {
            return;
        }

        var viewModel = new EditorViewModel(
            sound,
            services.GetRequiredService<ISettingsProvider>(),
            services.GetRequiredService<IWaveformService>(),
            services.GetRequiredService<IAudioEngine>(),
            services.GetRequiredService<IAudioExporter>(),
            this,
            services.GetRequiredService<ILogger<EditorViewModel>>());
        new EditorWindow(viewModel) { Owner = Owner }.ShowDialog();
    }

    public DownloadedAudio? DownloadFromYouTube()
    {
        var viewModel = new YouTubeDownloadViewModel(
            services.GetRequiredService<IAudioDownloader>(),
            services.GetRequiredService<ILogger<YouTubeDownloadViewModel>>());
        new YouTubeDownloadWindow(viewModel) { Owner = Owner }.ShowDialog();
        return viewModel.Result;
    }

    public IReadOnlyList<string> PickAudioFiles()
    {
        var patterns = string.Join(';', SupportedAudioFormats.Extensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file âm thanh",
            Multiselect = true,
            Filter = $"Âm thanh ({patterns})|{patterns}",
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileNames : [];
    }

    public string? PickExportPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Xuất file WAV",
            Filter = WavFilter,
            FileName = string.Concat(suggestedFileName.Split(Path.GetInvalidFileNameChars())),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    private static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
}
