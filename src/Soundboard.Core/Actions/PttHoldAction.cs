using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>The push-to-talk key: voice reaches the game only while it is held (PROJECT_PROMPT.md §5.2).</summary>
public sealed class PttHoldAction(IAudioEngine engine) : IAction
{
    public string Id => ActionIds.PttHold;

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        engine.SetPushToTalk(context.Phase == TriggerPhase.Down);
    }
}
