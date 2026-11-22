using Microsoft.Extensions.Logging;
using Soundboard.App.WebRemote;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.App.Services;

/// <summary>Pushes settings changes to the engine, the hotkey hooks and the web remote, touching only what changed.</summary>
public sealed class SettingsSync(
    ISettingsProvider settings,
    IAudioEngine engine,
    IInputService input,
    WebRemoteServer webRemote,
    ILogger<SettingsSync> logger) : IDisposable
{
    /// <summary>Applies the current settings once, then follows every change.</summary>
    public void Start()
    {
        var current = settings.Current;
        engine.ApplySettings(current.Audio);
        engine.ApplyFeatures(current.Features);
        input.SetBindings(current.Bindings);
        webRemote.Apply(current);
        Preload(current.Sounds);

        settings.Changed += OnChanged;
    }

    public void Dispose() => settings.Changed -= OnChanged;

    private void OnChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Current.Audio != e.Previous.Audio)
        {
            engine.ApplySettings(e.Current.Audio);
        }

        if (e.Current.Features != e.Previous.Features)
        {
            engine.ApplyFeatures(e.Current.Features);
        }

        if (!ReferenceEquals(e.Current.Bindings, e.Previous.Bindings))
        {
            input.SetBindings(e.Current.Bindings);
        }

        if (e.Current.Features.WebRemote != e.Previous.Features.WebRemote || e.Current.WebRemote != e.Previous.WebRemote)
        {
            webRemote.Apply(e.Current);
        }

        if (!ReferenceEquals(e.Current.Sounds, e.Previous.Sounds))
        {
            Preload(e.Current.Sounds);
        }
    }

    private void Preload(IReadOnlyList<SoundItem> sounds) => _ = PreloadAsync(sounds);

    private async Task PreloadAsync(IReadOnlyList<SoundItem> sounds)
    {
        try
        {
            await engine.PreloadAsync(sounds).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Preloading is an optimisation: sounds still decode on first play.
            logger.LogWarning(ex, "Preloading sounds failed");
        }
    }
}
