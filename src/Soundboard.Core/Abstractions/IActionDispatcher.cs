using Soundboard.Core.Actions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>Single entry point from every trigger source to the actions (PROJECT_PROMPT.md §4.2).</summary>
public interface IActionDispatcher
{
    /// <summary>
    /// Runs the action registered under <paramref name="actionId"/>. Exceptions thrown by the action
    /// propagate to the caller so they are never silently lost.
    /// </summary>
    DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase);
}
