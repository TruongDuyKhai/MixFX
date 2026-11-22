namespace Soundboard.App.Services;

/// <summary>"Start with Windows" (§5.6). Kept out of settings.json: Windows' own Run key is the source of truth, shared with the installer.</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
