using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using Soundboard.Audio.Capture;
using Soundboard.Audio.Decoding;
using Soundboard.Audio.Devices;
using Soundboard.Audio.Dsp;
using Soundboard.Audio.Mixing;
using Soundboard.Audio.Output;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Events;
using Soundboard.Core.Models;

namespace Soundboard.Audio;

/// <summary>
/// <code>
/// Mic ─► PttGate ─┐
///                 ├─► OutputMix ─► PeakLimiter ─► Output A (VB-Cable → game)
/// Sounds ─► Mixer ┘
/// Sounds ─► Mixer ─► OutputMix ─► Output B (headphones, no voice)
/// </code>
/// Each output has its own mixer and its own voices, so the two devices never share a clock.
/// Device and file work runs on one MTA "engine thread"; public members only queue requests.
/// </summary>
public sealed class AudioEngine : IAudioEngine
{
    private const int WorkerIntervalMs = 10;
    private static readonly TimeSpan WorkerJoinTimeout = TimeSpan.FromSeconds(3);
    private static readonly long PreloadMaxFrames = EngineFormat.MsToFrames((long)EngineFormat.PreloadMaxDuration.TotalMilliseconds);

    // Slack for decoders whose reported duration is slightly short (VBR mp3) when no trim end is set.
    private static readonly long UntrimmedEndSlackFrames = EngineFormat.SampleRate;

    private readonly ISoundLibrary _library;
    private readonly ILogger<AudioEngine> _logger;
    private readonly ConcurrentQueue<Action> _requests = new();
    private readonly AutoResetEvent _wake = new(initialState: false);
    private readonly Thread _worker;
    private readonly SoundCache _cache = new();

    private readonly Mixer _gameSounds = new();
    private readonly Mixer _monitorSounds = new();
    private readonly PttGate _gate = new();
    private readonly OutputMix _gameMix;
    private readonly OutputMix _monitorMix;
    private readonly PeakLimiter _gameLimited;

    private readonly Lock _stateLock = new();
    private readonly List<Playback> _playbacks = [];
    private readonly Dictionary<Guid, int> _playingCounts = [];
    private readonly AudioEndpointStatus[] _status = new AudioEndpointStatus[Enum.GetValues<AudioEndpointRole>().Length];

    private int _playbackMode;
    private int _refreshQueued;
    private int _disposed;

    // Engine thread only.
    private MMDeviceEnumerator? _enumerator;
    private DeviceNotificationClient? _notifications;
    private MicInput? _mic;
    private OutputDevice? _gameOutput;
    private OutputDevice? _monitorOutput;
    private AudioSettings? _deviceSettings;

    public AudioEngine(ISoundLibrary library, ILogger<AudioEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(logger);

        _library = library;
        _logger = logger;
        _gameMix = new OutputMix(_gameSounds, _gate);
        _gameLimited = new PeakLimiter(_gameMix, EngineFormat.LimiterCeilingDb, EngineFormat.LimiterReleaseMs);
        _monitorMix = new OutputMix(_monitorSounds, voice: null);

        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Soundboard audio engine" };

        // WASAPI and Media Foundation objects are created and used here; MTA avoids cross-apartment marshalling.
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    public event EventHandler<SoundStartedEventArgs>? SoundStarted;

    public event EventHandler<SoundStoppedEventArgs>? SoundStopped;

    public event EventHandler<SoundFailedEventArgs>? SoundFailed;

    public event EventHandler<DeviceLostEventArgs>? DeviceLost;

    public event EventHandler? StatusChanged;

    public bool IsVoiceTransmitting => _gate.IsTransmitting;

    public void ApplySettings(AudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _gameMix.SoundVolume = settings.VolumeToGame;
        _gameMix.VoiceVolume = settings.MicVolume;
        _monitorMix.SoundVolume = settings.VolumeToMonitor;
        _gate.AlwaysOn = settings.MicMode == MicMode.AlwaysOn;
        Volatile.Write(ref _playbackMode, (int)settings.PlaybackMode);

        Post(() => ApplyDevices(settings));
    }

    public IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return DeviceCatalog.List(enumerator, flow);
    }

    public AudioEndpointStatus GetStatus(AudioEndpointRole role)
    {
        lock (_stateLock)
        {
            return _status[(int)role];
        }
    }

    public void Play(SoundItem sound)
    {
        ArgumentNullException.ThrowIfNull(sound);

        // Counted now, not when the engine thread gets to it, so Toggle sees the sound as playing immediately.
        lock (_stateLock)
        {
            _playingCounts[sound.Id] = _playingCounts.GetValueOrDefault(sound.Id) + 1;
        }

        Post(() => StartPlayback(sound));
    }

    public void Stop(Guid soundId) => Post(() => StopPlaybacks(p => p.SoundId == soundId));

    public void StopAll() => Post(() => StopPlaybacks(_ => true));

    public bool IsPlaying(Guid soundId)
    {
        lock (_stateLock)
        {
            return _playingCounts.GetValueOrDefault(soundId) > 0;
        }
    }

    public IReadOnlyList<PlaybackProgress> GetActivePlaybacks()
    {
        lock (_stateLock)
        {
            return _playbacks.Select(p => p.Progress).ToArray();
        }
    }

    public void SetPushToTalk(bool pressed) => _gate.PushToTalkPressed = pressed;

    public Task PreloadAsync(IReadOnlyList<SoundItem> sounds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sounds);

        // Thread pool threads are MTA, which Media Foundation needs.
        return Task.Run(() => Preload(sounds, cancellationToken), cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _wake.Set();
        if (!_worker.Join(WorkerJoinTimeout))
        {
            _logger.LogWarning("Audio engine thread did not stop within {Timeout}", WorkerJoinTimeout);
        }

        _wake.Dispose();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private void Post(Action request)
    {
        if (IsDisposed)
        {
            return;
        }

        _requests.Enqueue(request);
        _wake.Set();
    }

    private void WorkerLoop()
    {
        InitializeDeviceNotifications();

        while (!IsDisposed)
        {
            _wake.WaitOne(WorkerIntervalMs);

            while (_requests.TryDequeue(out var request))
            {
                try
                {
                    request();
                }
                catch (Exception ex)
                {
                    // One bad request (or event subscriber) must not kill the engine thread and silence everything.
                    _logger.LogError(ex, "Audio engine request failed");
                }
            }

            PumpStreams();
            CollectFinishedPlaybacks();
        }

        Teardown();
    }

    private void InitializeDeviceNotifications()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _notifications = new DeviceNotificationClient(QueueDeviceRefresh);
            _enumerator.RegisterEndpointNotificationCallback(_notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio device enumeration is unavailable; the engine will stay silent");
        }
    }

    private void Teardown()
    {
        if (_enumerator is not null && _notifications is not null)
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notifications);
        }

        StopMic();
        CloseOutput(AudioEndpointRole.GameOutput);
        CloseOutput(AudioEndpointRole.MonitorOutput);

        lock (_stateLock)
        {
            foreach (var playback in _playbacks)
            {
                playback.Dispose();
            }

            _playbacks.Clear();
            _playingCounts.Clear();
        }

        _enumerator?.Dispose();
    }

    // ── Devices ─────────────────────────────────────────────────────────────

    private void ApplyDevices(AudioSettings settings)
    {
        var previous = _deviceSettings;
        _deviceSettings = settings;

        if (previous is null || previous.MicDeviceId != settings.MicDeviceId)
        {
            RestartMic();
        }

        if (previous is null || previous.GameOutputDeviceId != settings.GameOutputDeviceId)
        {
            RestartOutput(AudioEndpointRole.GameOutput);
        }

        if (previous is null || previous.MonitorOutputDeviceId != settings.MonitorOutputDeviceId)
        {
            RestartOutput(AudioEndpointRole.MonitorOutput);
        }
    }

    private MMDevice? Resolve(AudioEndpointRole role)
    {
        if (_enumerator is null || _deviceSettings is null)
        {
            return null;
        }

        var configuredId = role switch
        {
            AudioEndpointRole.Microphone => _deviceSettings.MicDeviceId,
            AudioEndpointRole.GameOutput => _deviceSettings.GameOutputDeviceId,
            _ => _deviceSettings.MonitorOutputDeviceId,
        };
        return DeviceCatalog.Resolve(_enumerator, role, configuredId);
    }

    private void RestartMic()
    {
        StopMic();

        var device = Resolve(AudioEndpointRole.Microphone);
        if (device is null)
        {
            SetStatus(AudioEndpointRole.Microphone, AudioEndpointStatus.NotConfigured);
            return;
        }

        var name = device.FriendlyName;
        try
        {
            var mic = new MicInput(device, OnMicLost);
            mic.Start();
            _mic = mic;
            _gate.Source = mic.Output;
            SetStatus(AudioEndpointRole.Microphone, AudioEndpointStatus.Running);
            _logger.LogInformation("Microphone started: {Device}", name);
        }
        catch (Exception ex)
        {
            device.Dispose();
            _logger.LogError(ex, "Could not open microphone {Device}", name);
            SetStatus(AudioEndpointRole.Microphone, AudioEndpointStatus.Failed);
        }
    }

    private void StopMic()
    {
        _gate.Source = null;
        _mic?.Dispose();
        _mic = null;
    }

    private void RestartOutput(AudioEndpointRole role)
    {
        CloseOutput(role);

        var device = Resolve(role);
        if (device is null)
        {
            SetStatus(role, AudioEndpointStatus.NotConfigured);
            return;
        }

        var name = device.FriendlyName;
        try
        {
            var source = role == AudioEndpointRole.GameOutput ? (NAudio.Wave.ISampleProvider)_gameLimited : _monitorMix;
            var output = new OutputDevice(device, source, OnOutputLost);
            output.Start();
            if (role == AudioEndpointRole.GameOutput)
            {
                _gameOutput = output;
            }
            else
            {
                _monitorOutput = output;
            }

            SetStatus(role, AudioEndpointStatus.Running);
            _logger.LogInformation("{Role} started: {Device}", role, name);
        }
        catch (Exception ex)
        {
            device.Dispose();
            _logger.LogError(ex, "Could not open {Role} device {Device}", role, name);
            SetStatus(role, AudioEndpointStatus.Failed);
        }
    }

    private void CloseOutput(AudioEndpointRole role)
    {
        var (output, mixer) = role == AudioEndpointRole.GameOutput ? (_gameOutput, _gameSounds) : (_monitorOutput, _monitorSounds);
        if (output is null)
        {
            return;
        }

        output.Dispose();
        if (role == AudioEndpointRole.GameOutput)
        {
            _gameOutput = null;
        }
        else
        {
            _monitorOutput = null;
        }

        // Safe now: the device thread that read this mixer has stopped.
        mixer.Reset();
    }

    private void OnOutputLost(OutputDevice output, Exception error) => Post(() =>
    {
        var role = ReferenceEquals(output, _gameOutput) ? AudioEndpointRole.GameOutput
            : ReferenceEquals(output, _monitorOutput) ? AudioEndpointRole.MonitorOutput
            : (AudioEndpointRole?)null;
        if (role is null)
        {
            return; // Already replaced.
        }

        CloseOutput(role.Value);
        ReportLost(role.Value, output.DeviceName, error);
    });

    private void OnMicLost(MicInput mic, Exception error) => Post(() =>
    {
        if (!ReferenceEquals(mic, _mic))
        {
            return;
        }

        StopMic();
        ReportLost(AudioEndpointRole.Microphone, mic.DeviceName, error);
    });

    private void ReportLost(AudioEndpointRole role, string deviceName, Exception error)
    {
        _logger.LogWarning(error, "{Role} device lost: {Device}", role, deviceName);
        SetStatus(role, AudioEndpointStatus.Lost);
        DeviceLost?.Invoke(this, new DeviceLostEventArgs(role, deviceName, error.Message));
    }

    private void QueueDeviceRefresh()
    {
        // Windows sends bursts of notifications; one refresh per burst is enough.
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
        {
            Post(RefreshDevices);
        }
    }

    private void RefreshDevices()
    {
        Volatile.Write(ref _refreshQueued, 0);
        if (_deviceSettings is null)
        {
            return;
        }

        // Restart roles that are down (a device came back) or that follow a Windows default that moved.
        if (NeedsRestart(AudioEndpointRole.Microphone, _mic?.DeviceId, _deviceSettings.MicDeviceId))
        {
            RestartMic();
        }

        if (NeedsRestart(AudioEndpointRole.GameOutput, _gameOutput?.DeviceId, _deviceSettings.GameOutputDeviceId))
        {
            RestartOutput(AudioEndpointRole.GameOutput);
        }

        if (NeedsRestart(AudioEndpointRole.MonitorOutput, _monitorOutput?.DeviceId, _deviceSettings.MonitorOutputDeviceId))
        {
            RestartOutput(AudioEndpointRole.MonitorOutput);
        }
    }

    private bool NeedsRestart(AudioEndpointRole role, string? runningId, string? configuredId)
    {
        if (runningId is null)
        {
            return true;
        }

        if (configuredId is not null)
        {
            return false;
        }

        using var wanted = Resolve(role);
        return wanted is not null && wanted.ID != runningId;
    }

    private void SetStatus(AudioEndpointRole role, AudioEndpointStatus status)
    {
        lock (_stateLock)
        {
            if (_status[(int)role] == status)
            {
                return;
            }

            _status[(int)role] = status;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Playback ────────────────────────────────────────────────────────────

    private void StartPlayback(SoundItem sound)
    {
        if ((PlaybackMode)Volatile.Read(ref _playbackMode) == PlaybackMode.Exclusive)
        {
            StopPlaybacks(_ => true);
        }

        var toGame = _gameOutput is not null;
        var toMonitor = _monitorOutput is not null;
        if (!toGame && !toMonitor)
        {
            FailPlayback(sound, "Chưa có thiết bị phát nào đang chạy.");
            return;
        }

        Playback playback;
        try
        {
            playback = CreatePlayback(sound, toGame, toMonitor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not play sound {SoundId} ({Name})", sound.Id, sound.Name);
            FailPlayback(sound, $"Không phát được \"{sound.Name}\": {ex.Message}");
            return;
        }

        if ((playback.GameVoice ?? playback.MonitorVoice)!.TotalFrames == 0)
        {
            playback.Dispose();
            FailPlayback(sound, $"\"{sound.Name}\" không còn âm thanh nào sau khi cắt.");
            return;
        }

        lock (_stateLock)
        {
            _playbacks.Add(playback);
        }

        if (playback.GameVoice is not null)
        {
            _gameSounds.Add(playback.GameVoice);
        }

        if (playback.MonitorVoice is not null)
        {
            _monitorSounds.Add(playback.MonitorVoice);
        }

        SoundStarted?.Invoke(this, new SoundStartedEventArgs(sound.Id));
    }

    private Playback CreatePlayback(SoundItem sound, bool toGame, bool toMonitor)
    {
        var path = _library.GetFilePath(sound);
        var key = SoundCacheKey.For(path, sound.Edit);
        var start = TimeSpan.FromMilliseconds(Math.Max(0, sound.Edit.TrimStartMs));

        if (!_cache.TryGet(key, out var cached))
        {
            var decoded = AudioDecoder.Open(path, start);
            var frames = AudioDecoder.TrimmedFrames(decoded.SourceDuration, sound.Edit);
            if (frames <= PreloadMaxFrames)
            {
                using (decoded)
                {
                    cached = DecodeTrimmed(decoded, frames, sound.Edit);
                }

                _cache.Set(key, cached);
            }
            else
            {
                return CreateStreamedPlayback(sound, path, start, decoded, frames, toGame, toMonitor);
            }
        }

        var envelope = EditEnvelope.For(sound, cached.Frames);
        return new Playback(
            sound.Id,
            toGame ? new CachedVoice(sound.Id, cached, envelope) : null,
            toMonitor ? new CachedVoice(sound.Id, cached, envelope) : null);
    }

    private static Playback CreateStreamedPlayback(
        SoundItem sound, string path, TimeSpan start, DecodedAudio decoded, long frames, bool toGame, bool toMonitor)
    {
        var envelope = EditEnvelope.For(sound, frames);
        StreamedVoice? game = null;
        StreamedVoice? monitor = null;
        try
        {
            // Each output gets its own decoder so a stalled device cannot hold the other one back.
            game = toGame ? new StreamedVoice(sound.Id, decoded, frames, envelope) : null;
            if (toMonitor)
            {
                monitor = new StreamedVoice(sound.Id, game is null ? decoded : AudioDecoder.Open(path, start), frames, envelope);
            }
        }
        catch
        {
            game?.Dispose();
            if (game is null)
            {
                decoded.Dispose();
            }

            throw;
        }

        var playback = new Playback(sound.Id, game, monitor);

        // Fill the read-ahead before the voices go live so the first callbacks never underrun.
        playback.Pump();
        return playback;
    }

    private static CachedSound DecodeTrimmed(DecodedAudio decoded, long frames, EditParams edit) =>
        AudioDecoder.DecodeAll(decoded, edit.TrimEndMs is null ? frames + UntrimmedEndSlackFrames : frames);

    private void FailPlayback(SoundItem sound, string message)
    {
        DecrementPlaying(sound.Id);
        SoundFailed?.Invoke(this, new SoundFailedEventArgs(sound.Id, message));
    }

    private void StopPlaybacks(Func<Playback, bool> predicate)
    {
        lock (_stateLock)
        {
            foreach (var playback in _playbacks)
            {
                if (predicate(playback))
                {
                    playback.RequestStop();
                }
            }
        }
    }

    private void PumpStreams()
    {
        // Only this thread mutates _playbacks, so it may iterate without the lock.
        foreach (var playback in _playbacks)
        {
            playback.Pump();
        }
    }

    private void CollectFinishedPlaybacks()
    {
        List<Playback>? finished = null;
        lock (_stateLock)
        {
            for (var i = _playbacks.Count - 1; i >= 0; i--)
            {
                if (_playbacks[i].IsFinished)
                {
                    (finished ??= []).Add(_playbacks[i]);
                    _playbacks.RemoveAt(i);
                }
            }
        }

        if (finished is null)
        {
            return;
        }

        foreach (var playback in finished)
        {
            // Disposal closes files, so it happens here and never on an audio thread.
            playback.Dispose();
            DecrementPlaying(playback.SoundId);
            SoundStopped?.Invoke(this, new SoundStoppedEventArgs(playback.SoundId));
        }
    }

    private void DecrementPlaying(Guid soundId)
    {
        lock (_stateLock)
        {
            var count = _playingCounts.GetValueOrDefault(soundId) - 1;
            if (count > 0)
            {
                _playingCounts[soundId] = count;
            }
            else
            {
                _playingCounts.Remove(soundId);
            }
        }
    }

    // ── Preloading ──────────────────────────────────────────────────────────

    private void Preload(IReadOnlyList<SoundItem> sounds, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<SoundCacheKey>();
        foreach (var sound in sounds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string path;
            try
            {
                path = _library.GetFilePath(sound);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Sound {SoundId} has an invalid library file name", sound.Id);
                continue;
            }

            var key = SoundCacheKey.For(path, sound.Edit);
            wanted.Add(key);
            if (_cache.TryGet(key, out _))
            {
                continue;
            }

            try
            {
                using var decoded = AudioDecoder.Open(path, TimeSpan.FromMilliseconds(Math.Max(0, sound.Edit.TrimStartMs)));
                var frames = AudioDecoder.TrimmedFrames(decoded.SourceDuration, sound.Edit);
                if (frames <= PreloadMaxFrames)
                {
                    _cache.Set(key, DecodeTrimmed(decoded, frames, sound.Edit));
                }
            }
            catch (Exception ex)
            {
                // A broken file must not stop the others from loading; playing it will report the error.
                _logger.LogWarning(ex, "Could not preload sound {SoundId} ({Name})", sound.Id, sound.Name);
            }
        }

        _cache.RetainOnly(wanted);
        _logger.LogInformation("Preloaded {Count} sounds into memory", _cache.Count);
    }
}
