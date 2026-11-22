namespace Soundboard.Core.Actions;

/// <summary>
/// One thing the app can do. Every trigger source (hotkey, UI click, future remotes) reaches an
/// action only through <see cref="Abstractions.IActionDispatcher"/> (PROJECT_PROMPT.md §4.2).
/// </summary>
public interface IAction
{
    /// <summary>
    /// Stable identifier stored in users' bindings in settings.json. Renaming it silently breaks
    /// those bindings, so treat it as permanent.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// May be called from any thread (input thread, UI thread, future remote sources), so
    /// implementations must be thread-safe. Keep it short: further triggers wait behind it.
    /// </summary>
    void Execute(ActionContext context);
}
