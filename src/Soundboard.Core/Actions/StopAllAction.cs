using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>Stops every playing sound.</summary>
public sealed class StopAllAction(IAudioEngine engine) : IAction
{
    public string Id => ActionIds.StopAll;

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Phase == TriggerPhase.Down)
        {
            engine.StopAll();
        }
    }
}
