using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Core.Actions;

/// <summary>
/// VolumeUp / VolumeDown: moves the sound volume on both outputs by one step, so what teammates
/// hear and what the user hears stay in proportion. The mic level is left alone.
/// </summary>
public sealed class VolumeStepAction : IAction
{
    private readonly ISettingsProvider _settings;
    private readonly float _step;

    private VolumeStepAction(ISettingsProvider settings, string id, float step)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        Id = id;
        _step = step;
    }

    public string Id { get; }

    public static VolumeStepAction Up(ISettingsProvider settings) => new(settings, ActionIds.VolumeUp, AudioSettings.VolumeStep);

    public static VolumeStepAction Down(ISettingsProvider settings) => new(settings, ActionIds.VolumeDown, -AudioSettings.VolumeStep);

    public void Execute(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Phase != TriggerPhase.Down)
        {
            return;
        }

        _settings.Update(s => s with
        {
            Audio = s.Audio with
            {
                VolumeToGame = Step(s.Audio.VolumeToGame),
                VolumeToMonitor = Step(s.Audio.VolumeToMonitor),
            },
        });
    }

    // Rounded to the step grid so repeated presses land on clean values (0.7, not 0.70000005).
    private float Step(float volume) =>
        Math.Clamp(MathF.Round((volume + _step) / AudioSettings.VolumeStep) * AudioSettings.VolumeStep, AudioSettings.MinVolume, AudioSettings.MaxSoundVolume);
}
