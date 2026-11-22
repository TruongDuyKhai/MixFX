using Soundboard.App.Services;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.App.Tests;

internal sealed class StubEngine : IAudioEngine
{
#pragma warning disable CS0067 // Events are part of the interface; these tests never raise them.
    public event EventHandler<SoundStartedEventArgs>? SoundStarted;

    public event EventHandler<SoundStoppedEventArgs>? SoundStopped;

    public event EventHandler<SoundFailedEventArgs>? SoundFailed;

    public event EventHandler<DeviceLostEventArgs>? DeviceLost;

    public event EventHandler? StatusChanged;
#pragma warning restore CS0067

    public bool IsVoiceTransmitting => false;

    public void ApplySettings(AudioSettings settings)
    {
    }

    public void ApplyFeatures(FeatureFlags features)
    {
    }

    public IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceFlow flow) => [];

    public AudioEndpointStatus GetStatus(AudioEndpointRole role) => AudioEndpointStatus.NotConfigured;

    public void Play(SoundItem sound)
    {
    }

    public void Stop(Guid soundId)
    {
    }

    public void StopAll()
    {
    }

    public bool IsPlaying(Guid soundId) => false;

    public IReadOnlyList<PlaybackProgress> GetActivePlaybacks() => [];

    public void Preview(SoundItem sound)
    {
    }

    public void StopPreview()
    {
    }

    public void SetPushToTalk(bool pressed)
    {
    }

    public Task PreloadAsync(IReadOnlyList<SoundItem> sounds, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class StubSettings : ISettingsProvider
{
#pragma warning disable CS0067 // Event is part of the interface; these tests never raise it.
    public event EventHandler<SettingsChangedEventArgs>? Changed;
#pragma warning restore CS0067

    public AppSettings Current { get; } = new();

    public void Update(Func<AppSettings, AppSettings> change)
    {
    }
}

internal sealed class StubDispatcher : IActionDispatcher
{
    public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase) => DispatchResult.Executed;
}

internal sealed class RecordingDispatcher(params string[] knownActions) : IActionDispatcher
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Action, Guid? Target, TriggerPhase Phase, TriggerMode Mode)> _calls = new();

    public IReadOnlyList<(string Action, Guid? Target, TriggerPhase Phase, TriggerMode Mode)> Calls => [.. _calls];

    public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase) => Dispatch(actionId, targetId, phase, TriggerMode.Press);

    public DispatchResult Dispatch(string actionId, Guid? targetId, TriggerPhase phase, TriggerMode mode)
    {
        if (!knownActions.Contains(actionId))
        {
            return DispatchResult.UnknownAction;
        }

        _calls.Enqueue((actionId, targetId, phase, mode));
        return DispatchResult.Executed;
    }
}

internal sealed class MutableSettings(AppSettings initial) : ISettingsProvider
{
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Current { get; private set; } = initial;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var previous = Current;
        Current = change(previous);
        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, Current));
    }
}

internal sealed class RecordingDialogs : IDialogService
{
    public List<string> Errors { get; } = [];

    public bool Confirm(string message, string title) => false;

    public void ShowError(string message) => Errors.Add(message);

    public void EditBinding(string actionId, Guid? targetId, string title, bool allowModeChoice)
    {
    }

    public void ShowGlobalHotkeys()
    {
    }

    public void ShowSetup()
    {
    }

    public void ShowExperimental()
    {
    }

    public void OpenUrl(string url)
    {
    }

    public void EditSound(Guid soundId)
    {
    }

    public IReadOnlyList<string> PickAudioFiles() => [];

    public string? PickExportPath(string suggestedFileName) => null;
}

internal sealed class FixedSettings(AppSettings settings) : ISettingsProvider
{
#pragma warning disable CS0067 // Event is part of the interface; these tests never raise it.
    public event EventHandler<SettingsChangedEventArgs>? Changed;
#pragma warning restore CS0067

    public AppSettings Current { get; } = settings;

    public void Update(Func<AppSettings, AppSettings> change)
    {
    }
}

internal sealed class StubLibrary : ISoundLibrary
{
    public string GetFilePath(SoundItem sound) => throw new NotSupportedException();

    public Task<string> DuplicateAsync(string libraryFile, Guid newSoundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public void Delete(string libraryFile) => throw new NotSupportedException();

    public Task<string> ImportAsync(string sourcePath, Guid soundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public string GetPeaksPath(string libraryFile) => throw new NotSupportedException();
}

internal sealed class StubWaveforms : IWaveformService
{
    public Task<WaveformData> LoadAsync(SoundItem sound, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class StubDialogs : IDialogService
{
    public bool Confirm(string message, string title) => false;

    public void ShowError(string message)
    {
    }

    public void EditBinding(string actionId, Guid? targetId, string title, bool allowModeChoice)
    {
    }

    public void ShowGlobalHotkeys()
    {
    }

    public void ShowSetup()
    {
    }

    public void ShowExperimental()
    {
    }

    public void OpenUrl(string url)
    {
    }

    public void EditSound(Guid soundId)
    {
    }

    public IReadOnlyList<string> PickAudioFiles() => [];

    public string? PickExportPath(string suggestedFileName) => null;
}

internal sealed class StubStartup : IStartupRegistration
{
    public bool IsEnabled => false;

    public void SetEnabled(bool enabled)
    {
    }
}
