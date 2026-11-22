using System.Collections.Frozen;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>Routes a dispatch to the <see cref="IAction"/> with the matching id.</summary>
public sealed class ActionDispatcher : IActionDispatcher
{
    // Frozen: built once at startup, then read concurrently from the input and UI threads without locks.
    private readonly FrozenDictionary<string, IAction> _actions;

    public ActionDispatcher(IEnumerable<IAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Ordinal: ids are persisted identifiers, so "playSound" must not quietly match "PlaySound".
        var byId = new Dictionary<string, IAction>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.Id))
            {
                throw new ArgumentException(
                    $"Action {action.GetType().FullName} has an empty id.", nameof(actions));
            }

            // Fail at startup rather than letting one action shadow another at runtime.
            if (!byId.TryAdd(action.Id, action))
            {
                throw new ArgumentException(
                    $"Actions {byId[action.Id].GetType().FullName} and {action.GetType().FullName} share the id '{action.Id}'.",
                    nameof(actions));
            }
        }

        _actions = byId.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase)
    {
        ArgumentNullException.ThrowIfNull(actionId);

        if (!_actions.TryGetValue(actionId, out var action))
        {
            return DispatchResult.UnknownAction;
        }

        action.Execute(new ActionContext { TargetId = targetId, Phase = phase });
        return DispatchResult.Executed;
    }
}
