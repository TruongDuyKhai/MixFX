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

    /// <summary>
    /// Same as the three-argument overload, carrying the binding's <see cref="TriggerMode"/> to the action.
    /// Added in P2; the default implementation keeps older implementations compiling (they treat every trigger as Press).
    /// </summary>
    DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase, TriggerMode mode) =>
        Dispatch(actionId, targetId, phase);
}
