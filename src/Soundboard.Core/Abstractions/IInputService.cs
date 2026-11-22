using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>
/// Global hotkeys through low-level hooks (PROJECT_PROMPT.md §5.5). Matched triggers are sent to
/// <see cref="IActionDispatcher"/>; the service itself knows nothing about what actions do.
/// </summary>
public interface IInputService : IDisposable
{
    /// <summary>Installs the hooks. Calling it again has no effect.</summary>
    void Start();

    /// <summary>Replaces the active bindings; takes effect for the next key event.</summary>
    void SetBindings(IReadOnlyList<Binding> bindings);

    /// <summary>
    /// "Press a key to assign": completes with the next key or mouse button (with held modifiers),
    /// or null when the user presses Esc or <paramref name="cancellationToken"/> fires. Bindings do
    /// not fire while capturing, and the captured key is not passed on.
    /// </summary>
    Task<Trigger?> CaptureNextTriggerAsync(CancellationToken cancellationToken = default);
}
