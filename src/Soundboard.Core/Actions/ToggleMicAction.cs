using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>Switches between push-to-talk and always-on voice; persisted like any other setting.</summary>
public sealed class ToggleMicAction(ISettingsProvider settings) : IAction
{
    public string Id => ActionIds.ToggleMic;

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Phase != TriggerPhase.Down)
        {
            return;
        }

        settings.Update(s => s with
        {
            Audio = s.Audio with
            {
                MicMode = s.Audio.MicMode == MicMode.AlwaysOn ? MicMode.PushToTalk : MicMode.AlwaysOn,
            },
        });
    }
}
