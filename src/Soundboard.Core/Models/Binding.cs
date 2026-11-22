namespace Soundboard.Core.Models;

/// <summary>
/// Maps a <see cref="Trigger"/> to an action. New bindings get <see cref="PassThrough"/> = <c>true</c>
/// so the game still receives the key (PROJECT_PROMPT.md §5.5).
/// </summary>
public sealed record Binding(
    Guid Id,
    Trigger Trigger,
    string ActionId,
    Guid? TargetId,
    TriggerMode Mode,
    bool PassThrough);
