using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>
/// Input to <see cref="IAction.Execute"/>. Init-only properties (not a positional record) so new
/// fields can be added later without breaking every existing action (PROJECT_PROMPT.md §7.2.5).
/// </summary>
public sealed record ActionContext
{
    /// <summary>What the action acts on, e.g. the sound id for PlaySound. null for global actions such as StopAll.</summary>
    public required Guid? TargetId { get; init; }

    public required TriggerPhase Phase { get; init; }

    /// <summary>How the binding that fired wants Down/Up interpreted. UI clicks without a binding use Press.</summary>
    public TriggerMode Mode { get; init; } = TriggerMode.Press;
}
