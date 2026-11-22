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

    void OpenUrl(string url);
}
