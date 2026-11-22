using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Core.Abstractions;

/// <summary>
/// Mic + sounds → Output A (game) and sounds → Output B (monitor) (PROJECT_PROMPT.md §2, §5.1–5.2).
/// Every member is safe to call from any thread and returns quickly; device work happens on the
/// engine's own thread. Events are raised on engine threads, so UI subscribers must marshal.
/// </summary>
public interface IAudioEngine : IDisposable
{
    event EventHandler<SoundStartedEventArgs>? SoundStarted;

    event EventHandler<SoundStoppedEventArgs>? SoundStopped;

    event EventHandler<SoundFailedEventArgs>? SoundFailed;

    event EventHandler<DeviceLostEventArgs>? DeviceLost;

    /// <summary>Raised when any <see cref="GetStatus"/> value changes.</summary>
    event EventHandler? StatusChanged;

    /// <summary>True while the PTT gate lets voice through to the game.</summary>
    bool IsVoiceTransmitting { get; }

    /// <summary>Applies levels immediately and (re)opens only the devices whose choice changed.</summary>
    void ApplySettings(AudioSettings settings);

    /// <summary>
    /// Turns the experimental audio features on or off (ducking, per-sound effects, voice changer).
    /// Added in P6 without a default implementation: a silent no-op would make the flags lie.
    /// </summary>
    void ApplyFeatures(FeatureFlags features);

    IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceFlow flow);

    AudioEndpointStatus GetStatus(AudioEndpointRole role);

    /// <summary>Starts a new instance of the sound on every running output, honouring <see cref="PlaybackMode"/>.</summary>
    void Play(SoundItem sound);

    /// <summary>Stops every playing instance of the sound (with a short fade to avoid clicks).</summary>
    void Stop(Guid soundId);

    void StopAll();

    bool IsPlaying(Guid soundId);

    IReadOnlyList<PlaybackProgress> GetActivePlaybacks();

    /// <summary>
    /// Editor "listen": plays <paramref name="sound"/> (with its possibly unsaved edits) on the monitor
    /// output only, never to the game, replacing any previous preview.
    /// </summary>
    void Preview(SoundItem sound);

    void StopPreview();

    /// <summary>Held state of the push-to-talk key. Ignored for voice while <see cref="MicMode.AlwaysOn"/>.</summary>
    void SetPushToTalk(bool pressed);

    /// <summary>
    /// Decodes short sounds into memory ahead of time so the first trigger is instant, and frees
    /// cached audio for sounds no longer in <paramref name="sounds"/>.
    /// </summary>
    Task PreloadAsync(IReadOnlyList<SoundItem> sounds, CancellationToken cancellationToken = default);
}
