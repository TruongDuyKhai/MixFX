using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Core.Tests.Fakes;

/// <summary>Records calls; "playing" state follows Play/Stop so Toggle logic can be tested.</summary>
internal sealed class FakeAudioEngine : IAudioEngine
{
    private readonly Dictionary<Guid, int> _playing = [];

#pragma warning disable CS0067 // Events are part of the interface; these tests never raise them.
    public event EventHandler<SoundStartedEventArgs>? SoundStarted;

    public event EventHandler<SoundStoppedEventArgs>? SoundStopped;

    public event EventHandler<SoundFailedEventArgs>? SoundFailed;

    public event EventHandler<DeviceLostEventArgs>? DeviceLost;

    public event EventHandler? StatusChanged;
#pragma warning restore CS0067

    public List<string> Calls { get; } = [];

    public bool PushToTalk { get; private set; }

    public bool IsVoiceTransmitting => PushToTalk;

    public void ApplySettings(AudioSettings settings) => Calls.Add("Apply");

    public IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceFlow flow) => [];

    public AudioEndpointStatus GetStatus(AudioEndpointRole role) => AudioEndpointStatus.Running;

    public void Play(SoundItem sound)
    {
        Calls.Add($"Play {sound.Name}");
        _playing[sound.Id] = _playing.GetValueOrDefault(sound.Id) + 1;
    }

    public void Stop(Guid soundId)
    {
        Calls.Add("Stop");
        _playing.Remove(soundId);
    }

    public void StopAll()
    {
        Calls.Add("StopAll");
        _playing.Clear();
    }

    public bool IsPlaying(Guid soundId) => _playing.GetValueOrDefault(soundId) > 0;

    public IReadOnlyList<PlaybackProgress> GetActivePlaybacks() => [];

    public void Preview(SoundItem sound) => Calls.Add($"Preview {sound.Name}");

    public void StopPreview() => Calls.Add("StopPreview");

    public void SetPushToTalk(bool pressed) => PushToTalk = pressed;

    public Task PreloadAsync(IReadOnlyList<SoundItem> sounds, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }
}
