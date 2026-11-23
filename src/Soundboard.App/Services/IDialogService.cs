using Soundboard.Core.Downloads;

namespace Soundboard.App.Services;

/// <summary>Lets view models open windows and ask questions without referencing WPF types.</summary>
public interface IDialogService
{
    bool Confirm(string message, string title);

    void ShowError(string message);

    /// <summary>"Press a key" dialog for the binding of <paramref name="actionId"/> / <paramref name="targetId"/>.</summary>
    void EditBinding(string actionId, Guid? targetId, string title, bool allowModeChoice);

    void ShowGlobalHotkeys();

    void ShowSetup();

    /// <summary>"Tính năng thử nghiệm": the P6 feature flags.</summary>
    void ShowExperimental();

    void OpenUrl(string url);

    /// <summary>Opens the editor for a sound already on the board.</summary>
    void EditSound(Guid soundId);

    /// <summary>"Bạn muốn làm gì?" for the main window's X; null when the user cancelled.</summary>
    CloseChoice? AskCloseAction();

    /// <summary>"Tải từ YouTube" (§5.8). The downloaded file, or null when the user closed the window without one.</summary>
    DownloadedAudio? DownloadFromYouTube();

    /// <summary>Audio files chosen for import; empty when cancelled.</summary>
    IReadOnlyList<string> PickAudioFiles();

    /// <summary>Where to export a WAV, or null when cancelled.</summary>
    string? PickExportPath(string suggestedFileName);
}
