using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>Plays the target sound, interpreting Down/Up according to the binding's <see cref="TriggerMode"/>.</summary>
public sealed class PlaySoundAction(IAudioEngine engine, ISettingsProvider settings) : IAction
{
    public string Id => ActionIds.PlaySound;

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TargetId is not { } soundId)
        {
            return;
        }

        // Bindings may outlive their sound (deleted sound, hand-edited settings): nothing to play then.
        var sound = settings.Current.Sounds.FirstOrDefault(s => s.Id == soundId);
        if (sound is null)
        {
            return;
        }

        var down = context.Phase == TriggerPhase.Down;
        switch (context.Mode)
        {
            case TriggerMode.Press when down:
                engine.Play(sound);
                break;

            case TriggerMode.Restart when down:
                engine.Stop(soundId);
                engine.Play(sound);
                break;

            case TriggerMode.Toggle when down:
                if (engine.IsPlaying(soundId))
                {
                    engine.Stop(soundId);
                }
                else
                {
                    engine.Play(sound);
                }

                break;

            case TriggerMode.Hold:
                if (down)
                {
                    engine.Play(sound);
                }
                else
                {
                    engine.Stop(soundId);
                }

                break;
        }
    }
}
