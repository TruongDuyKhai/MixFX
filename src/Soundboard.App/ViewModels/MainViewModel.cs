using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Soundboard.App.Services;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Bindings;
using Soundboard.Core.Downloads;
using Soundboard.Core.Events;
using Soundboard.Core.Models;
using Soundboard.Core.Playlist;

namespace Soundboard.App.ViewModels;

/// <summary>
/// The main window (§5.6). Talks only to Core interfaces and App services, never to NAudio or Win32 (§4.1).
/// Engine and settings events can arrive on any thread; everything that touches bound state is posted
/// to the UI context captured at construction (§7.3.10).
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan FileReleaseDelay = TimeSpan.FromMilliseconds(200);

    private const string WindowsDefaultName = "Mặc định của Windows";
    private const string AutoCableName = "Tự động: CABLE Input (VB-Cable)";
    private const string MissingDeviceName = "Thiết bị đã chọn (hiện không thấy)";
    private const string DefaultSuffix = " (mặc định)";
    private const string CopySuffix = " (bản sao)";

    private readonly IAudioEngine _engine;
    private readonly ISettingsProvider _settings;
    private readonly IActionDispatcher _dispatcher;
    private readonly ISoundLibrary _library;
    private readonly IWaveformService _waveforms;
    private readonly IAudioDownloader _downloader;
    private readonly IDialogService _dialogs;
    private readonly IStartupRegistration _startup;
    private readonly PlaylistController _playlist;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SynchronizationContext? _ui;
    private readonly CancellationTokenSource _polling = new();

    // True while state is being copied *from* settings, so property setters do not write it back.
    private bool _syncing;
    private bool _disposed;

    private string _settingsStatus = "Đang tải cài đặt…";
    private string? _settingsWarning;
    private string _statusMessage = string.Empty;
    private string _nowPlaying = string.Empty;
    private string _pttHotkeyText = string.Empty;
    private DeviceOption? _selectedMicrophone;
    private DeviceOption? _selectedGameOutput;
    private DeviceOption? _selectedMonitorOutput;
    private AudioEndpointStatus _micStatus;
    private AudioEndpointStatus _gameStatus;
    private AudioEndpointStatus _monitorStatus;
    private double _volumeToGame = AudioSettings.DefaultVolume;
    private double _volumeToMonitor = AudioSettings.DefaultVolume;
    private double _micVolume = AudioSettings.DefaultVolume;
    private bool _micAlwaysOn;
    private bool _exclusivePlayback;
    private bool _startWithWindows;
    private bool _isVoiceTransmitting;
    private bool _showCableBanner;
    private bool _hasNoSounds = true;
    private bool _playlistEnabled;
    private bool _youTubeImportEnabled;
    private string _playlistNowText = "Playlist đang dừng";

    public MainViewModel(
        IAudioEngine engine,
        ISettingsProvider settings,
        IActionDispatcher dispatcher,
        ISoundLibrary library,
        IWaveformService waveforms,
        IAudioDownloader downloader,
        IDialogService dialogs,
        IStartupRegistration startup,
        PlaylistController playlist,
        ILogger<MainViewModel> logger)
    {
        _playlist = playlist;
        _engine = engine;
        _settings = settings;
        _dispatcher = dispatcher;
        _library = library;
        _waveforms = waveforms;
        _downloader = downloader;
        _dialogs = dialogs;
        _startup = startup;
        _logger = logger;
        _ui = SynchronizationContext.Current;

        StopAllCommand = new RelayCommand(_engine.StopAll);
        ImportCommand = new AsyncRelayCommand(() => ImportAsync(_dialogs.PickAudioFiles()));
        DownloadFromYouTubeCommand = new AsyncRelayCommand(DownloadFromYouTubeAsync);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        OpenHotkeysCommand = new RelayCommand(_dialogs.ShowGlobalHotkeys);
        OpenExperimentalCommand = new RelayCommand(_dialogs.ShowExperimental);

        // Buttons are trigger sources too: they go through the dispatcher like hotkeys (§4.2).
        PlaylistNextCommand = new RelayCommand(() => PressAction(ActionIds.PlaylistNext));
        PlaylistStopCommand = new RelayCommand(() => PressAction(ActionIds.PlaylistStop));
        OpenSetupCommand = new RelayCommand(() =>
        {
            _dialogs.ShowSetup();
            RefreshDevices();
        });
    }

    public string Title { get; } = $"Soundboard {AppInfo.Version}";

    public ObservableCollection<SoundTileViewModel> Tiles { get; } = [];

    public ObservableCollection<DeviceOption> Microphones { get; } = [];

    public ObservableCollection<DeviceOption> GameOutputs { get; } = [];

    public ObservableCollection<DeviceOption> MonitorOutputs { get; } = [];

    public ICommand StopAllCommand { get; }

    public ICommand ImportCommand { get; }

    public ICommand DownloadFromYouTubeCommand { get; }

    public ICommand RefreshDevicesCommand { get; }

    public ICommand OpenHotkeysCommand { get; }

    public ICommand OpenSetupCommand { get; }

    public ICommand OpenExperimentalCommand { get; }

    public ICommand PlaylistNextCommand { get; }

    public ICommand PlaylistStopCommand { get; }

    public bool PlaylistEnabled
    {
        get => _playlistEnabled;
        private set => SetProperty(ref _playlistEnabled, value);
    }

    /// <summary>Shows "Tải từ YouTube" (<see cref="FeatureFlags.YouTubeImport"/>).</summary>
    public bool YouTubeImportEnabled
    {
        get => _youTubeImportEnabled;
        private set => SetProperty(ref _youTubeImportEnabled, value);
    }

    public string PlaylistNowText
    {
        get => _playlistNowText;
        private set => SetProperty(ref _playlistNowText, value);
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        private set => SetProperty(ref _settingsStatus, value);
    }

    /// <summary>Message the user must acknowledge after loading, or null when loading was routine (§5.7: "báo người dùng").</summary>
    public string? SettingsWarning
    {
        get => _settingsWarning;
        private set => SetProperty(ref _settingsWarning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string NowPlaying
    {
        get => _nowPlaying;
        private set => SetProperty(ref _nowPlaying, value);
    }

    public string PttHotkeyText
    {
        get => _pttHotkeyText;
        private set => SetProperty(ref _pttHotkeyText, value);
    }

    public bool IsVoiceTransmitting
    {
        get => _isVoiceTransmitting;
        private set => SetProperty(ref _isVoiceTransmitting, value);
    }

    public bool ShowCableBanner
    {
        get => _showCableBanner;
        private set => SetProperty(ref _showCableBanner, value);
    }

    public bool HasNoSounds
    {
        get => _hasNoSounds;
        private set => SetProperty(ref _hasNoSounds, value);
    }

    public AudioEndpointStatus MicStatus
    {
        get => _micStatus;
        private set => SetProperty(ref _micStatus, value);
    }

    public AudioEndpointStatus GameStatus
    {
        get => _gameStatus;
        private set => SetProperty(ref _gameStatus, value);
    }

    public AudioEndpointStatus MonitorStatus
    {
        get => _monitorStatus;
        private set => SetProperty(ref _monitorStatus, value);
    }

    public string MicStatusText => StatusText(MicStatus);

    public string GameStatusText => StatusText(GameStatus);

    public string MonitorStatusText => StatusText(MonitorStatus);

    public DeviceOption? SelectedMicrophone
    {
        get => _selectedMicrophone;
        set
        {
            // null arrives when the list is rebuilt; it is not a user choice.
            if (SetProperty(ref _selectedMicrophone, value) && !_syncing && value is not null)
            {
                UpdateAudio(a => a with { MicDeviceId = value.Id });
            }
        }
    }

    public DeviceOption? SelectedGameOutput
    {
        get => _selectedGameOutput;
        set
        {
            if (SetProperty(ref _selectedGameOutput, value) && !_syncing && value is not null)
            {
                UpdateAudio(a => a with { GameOutputDeviceId = value.Id });
            }
        }
    }

    public DeviceOption? SelectedMonitorOutput
    {
        get => _selectedMonitorOutput;
        set
        {
            if (SetProperty(ref _selectedMonitorOutput, value) && !_syncing && value is not null)
            {
                UpdateAudio(a => a with { MonitorOutputDeviceId = value.Id });
            }
        }
    }

    public double MaxSoundVolume => AudioSettings.MaxSoundVolume;

    public double MaxMicVolume => AudioSettings.MaxMicVolume;

    public double VolumeToGame
    {
        get => _volumeToGame;
        set
        {
            if (SetProperty(ref _volumeToGame, value))
            {
                OnPropertyChanged(nameof(VolumeToGameText));
                if (!_syncing)
                {
                    UpdateAudio(a => a with { VolumeToGame = (float)value });
                }
            }
        }
    }

    public double VolumeToMonitor
    {
        get => _volumeToMonitor;
        set
        {
            if (SetProperty(ref _volumeToMonitor, value))
            {
                OnPropertyChanged(nameof(VolumeToMonitorText));
                if (!_syncing)
                {
                    UpdateAudio(a => a with { VolumeToMonitor = (float)value });
                }
            }
        }
    }

    public double MicVolume
    {
        get => _micVolume;
        set
        {
            if (SetProperty(ref _micVolume, value))
            {
                OnPropertyChanged(nameof(MicVolumeText));
                if (!_syncing)
                {
                    UpdateAudio(a => a with { MicVolume = (float)value });
                }
            }
        }
    }

    public string VolumeToGameText => Percent(VolumeToGame);

    public string VolumeToMonitorText => Percent(VolumeToMonitor);

    public string MicVolumeText => Percent(MicVolume);

    public bool MicAlwaysOn
    {
        get => _micAlwaysOn;
        set
        {
            if (SetProperty(ref _micAlwaysOn, value) && !_syncing)
            {
                UpdateAudio(a => a with { MicMode = value ? MicMode.AlwaysOn : MicMode.PushToTalk });
            }
        }
    }

    public bool ExclusivePlayback
    {
        get => _exclusivePlayback;
        set
        {
            if (SetProperty(ref _exclusivePlayback, value) && !_syncing)
            {
                UpdateAudio(a => a with { PlaybackMode = value ? PlaybackMode.Exclusive : PlaybackMode.Overlap });
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetProperty(ref _startWithWindows, value) || _syncing)
            {
                return;
            }

            try
            {
                _startup.SetEnabled(value);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                _logger.LogError(ex, "Could not change start-with-Windows");
                _dialogs.ShowError($"Không đổi được tùy chọn khởi động cùng Windows: {ex.Message}");
                _startWithWindows = _startup.IsEnabled;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Hooks up engine/settings events, fills the view and starts the progress/LED poll.</summary>
    public void Start()
    {
        _engine.StatusChanged += OnEngineStatusChanged;
        _engine.DeviceLost += OnDeviceLost;
        _engine.SoundFailed += OnSoundFailed;
        _settings.Changed += OnSettingsChanged;
        _playlist.CurrentChanged += OnPlaylistChanged;

        _syncing = true;
        _startWithWindows = _startup.IsEnabled;
        _syncing = false;
        OnPropertyChanged(nameof(StartWithWindows));

        RefreshDevices();
        SyncFromSettings(_settings.Current);
        RefreshStatuses();
        _ = PollAsync(_polling.Token);
    }

    public void ApplySettingsLoadResult(SettingsLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var preserved = result.PreservedFiles.Count == 0
            ? string.Empty
            : "\n\nFile không đọc được đã được giữ nguyên tại:\n" + string.Join('\n', result.PreservedFiles);

        (SettingsStatus, SettingsWarning) = result.Status switch
        {
            SettingsLoadStatus.Loaded => (
                $"Đã tải cài đặt: {result.Settings.Sounds.Count} sound, {result.Settings.Bindings.Count} phím tắt.",
                null),
            SettingsLoadStatus.FirstRun => (
                "Lần chạy đầu tiên: đang dùng cài đặt mặc định.",
                null),
            SettingsLoadStatus.RecoveredFromBackup => (
                "Đã khôi phục cài đặt từ bản sao lưu.",
                "File cài đặt bị hỏng hoặc bị mất, nên Soundboard đã khôi phục từ bản sao lưu gần nhất. "
                + "Thay đổi sau lần lưu đó có thể không còn." + preserved),
            SettingsLoadStatus.ResetToDefaults => (
                "Không đọc được cài đặt: đang dùng cài đặt mặc định.",
                "Không đọc được file cài đặt lẫn bản sao lưu, nên Soundboard bắt đầu với cài đặt mặc định." + preserved),
            SettingsLoadStatus.NewerSchemaReadOnly => (
                "Cài đặt thuộc phiên bản mới hơn: chế độ chỉ đọc.",
                "File cài đặt được tạo bởi phiên bản Soundboard mới hơn. Để không làm mất dữ liệu, "
                + "phiên bản này sẽ không ghi đè lên nó, nên thay đổi trong phiên này sẽ không được lưu."),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown settings load status."),
        };
    }

    /// <summary>Mouse down/up on a tile: goes through the dispatcher like any hotkey (§4.2), with the sound's binding mode.</summary>
    public void PressTile(SoundTileViewModel tile, bool down)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var mode = SoundBinding(_settings.Current, tile.Id)?.Mode ?? TriggerMode.Press;
        try
        {
            _dispatcher.Dispatch(ActionIds.PlaySound, tile.Id, down ? TriggerPhase.Down : TriggerPhase.Up, mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playing sound {SoundId} from the grid failed", tile.Id);
            StatusMessage = $"Không phát được: {ex.Message}";
        }
    }

    public void StopAll() => _engine.StopAll();

    /// <summary>
    /// Copies the files into the library (originals are only read, §5.3), adds them to the board and
    /// opens the editor on the first one ("Import xong thì tự mở Editor").
    /// </summary>
    public async Task ImportAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return;
        }

        var problems = paths.Where(p => !SupportedAudioFormats.IsSupported(p))
            .Select(p => $"{Path.GetFileName(p)}: định dạng không được hỗ trợ")
            .ToList();
        var imported = new List<SoundItem>();

        foreach (var path in paths.Where(SupportedAudioFormats.IsSupported))
        {
            StatusMessage = $"Đang thêm {Path.GetFileName(path)}…";
            if (await ImportOneAsync(path, _settings.Current.Sounds.Count + imported.Count) is { } sound)
            {
                imported.Add(sound);
            }
            else
            {
                problems.Add($"{Path.GetFileName(path)}: không đọc được file âm thanh");
            }
        }

        if (imported.Count > 0)
        {
            _settings.Update(s => s with { Sounds = [.. s.Sounds, .. imported] });
        }

        StatusMessage = $"Đã thêm {imported.Count} sound.";
        if (problems.Count > 0)
        {
            _dialogs.ShowError("Một số file không thêm được:\n" + string.Join('\n', problems));
        }

        if (imported.Count > 0)
        {
            _dialogs.EditSound(imported[0].Id);
        }
    }

    /// <summary>
    /// Adds a file fetched by "Tải từ YouTube", named after the video, then opens the editor (§5.3). The download
    /// is deleted afterwards either way: the library keeps its own copy.
    /// </summary>
    public async Task ImportDownloadedAsync(DownloadedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);

        StatusMessage = $"Đang thêm \"{audio.Title}\"…";
        SoundItem? sound;
        try
        {
            sound = await ImportOneAsync(audio.FilePath, _settings.Current.Sounds.Count, audio.Title.Trim());
        }
        finally
        {
            _downloader.Delete(audio);
        }

        if (sound is null)
        {
            StatusMessage = string.Empty;
            _dialogs.ShowError($"Không thêm được \"{audio.Title}\": file tải về không đọc được.");
            return;
        }

        _settings.Update(s => s with { Sounds = [.. s.Sounds, sound] });
        StatusMessage = $"Đã thêm \"{sound.Name}\".";
        _dialogs.EditSound(sound.Id);
    }

    public void Dispose()
    {
        // Idempotent: a throwing second call would abort the DI container's disposal of the engine and hooks.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _polling.Cancel();
        _polling.Dispose();
        _engine.StatusChanged -= OnEngineStatusChanged;
        _engine.DeviceLost -= OnDeviceLost;
        _engine.SoundFailed -= OnSoundFailed;
        _settings.Changed -= OnSettingsChanged;
        _playlist.CurrentChanged -= OnPlaylistChanged;
    }

    private void PressAction(string actionId)
    {
        try
        {
            _dispatcher.Dispatch(actionId, null, TriggerPhase.Down, TriggerMode.Press);
            _dispatcher.Dispatch(actionId, null, TriggerPhase.Up, TriggerMode.Press);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action {ActionId} from a button failed", actionId);
            StatusMessage = $"Không thực hiện được: {ex.Message}";
        }
    }

    private void OnPlaylistChanged(object? sender, EventArgs e) => OnUi(UpdatePlaylistNow);

    private void UpdatePlaylistNow()
    {
        var current = _playlist.CurrentSoundId;
        var name = current is null ? null : _settings.Current.Sounds.FirstOrDefault(s => s.Id == current)?.Name;
        PlaylistNowText = name is null ? "Playlist đang dừng" : $"Playlist: {name}";
    }

    private void TogglePlaylist(SoundTileViewModel tile) => _settings.Update(s => s with
    {
        Playlist = s.Playlist.Contains(tile.Id) ? [.. s.Playlist.Where(id => id != tile.Id)] : [.. s.Playlist, tile.Id],
    });

    private static Binding? SoundBinding(AppSettings settings, Guid soundId) =>
        settings.Bindings.FirstOrDefault(b => b.ActionId == ActionIds.PlaySound && b.TargetId == soundId);

    private static string Percent(double volume) =>
        Math.Round(volume * 100).ToString(CultureInfo.InvariantCulture) + "%";

    private static string StatusText(AudioEndpointStatus status) => status switch
    {
        AudioEndpointStatus.Running => "Đang chạy",
        AudioEndpointStatus.Failed => "Không mở được thiết bị",
        AudioEndpointStatus.Lost => "Mất kết nối, đang chờ thiết bị quay lại",
        _ => "Chưa có thiết bị",
    };

    private static string RoleName(AudioEndpointRole role) => role switch
    {
        AudioEndpointRole.Microphone => "micro",
        AudioEndpointRole.GameOutput => "Output A (game)",
        _ => "Output B (tai nghe)",
    };

    private void UpdateAudio(Func<AudioSettings, AudioSettings> change) =>
        _settings.Update(s => s with { Audio = change(s.Audio) });

    private void OnUi(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => OnUi(() => SyncFromSettings(_settings.Current));

    private void OnEngineStatusChanged(object? sender, EventArgs e) => OnUi(RefreshStatuses);

    private void OnSoundFailed(object? sender, SoundFailedEventArgs e) => OnUi(() => StatusMessage = e.Message);

    private void OnDeviceLost(object? sender, DeviceLostEventArgs e) => OnUi(() =>
    {
        StatusMessage = $"Mất {RoleName(e.Role)}: {e.DeviceName}. Sẽ tự kết nối lại khi thiết bị quay lại.";
        RefreshStatuses();
        RefreshDevices();
    });

    private void RefreshStatuses()
    {
        MicStatus = _engine.GetStatus(AudioEndpointRole.Microphone);
        GameStatus = _engine.GetStatus(AudioEndpointRole.GameOutput);
        MonitorStatus = _engine.GetStatus(AudioEndpointRole.MonitorOutput);
        OnPropertyChanged(nameof(MicStatusText));
        OnPropertyChanged(nameof(GameStatusText));
        OnPropertyChanged(nameof(MonitorStatusText));
    }

    private void RefreshDevices()
    {
        var audio = _settings.Current.Audio;
        var inputs = _engine.GetDevices(AudioDeviceFlow.Capture);
        var outputs = _engine.GetDevices(AudioDeviceFlow.Render);

        _syncing = true;
        try
        {
            Fill(Microphones, inputs, WindowsDefaultName, audio.MicDeviceId);
            Fill(GameOutputs, outputs, AutoCableName, audio.GameOutputDeviceId);
            Fill(MonitorOutputs, outputs, WindowsDefaultName, audio.MonitorOutputDeviceId);
            SelectDevices(audio);
        }
        finally
        {
            _syncing = false;
        }

        ShowCableBanner = !outputs.Any(d => d.IsVirtualCable);
    }

    private static void Fill(ObservableCollection<DeviceOption> options, IReadOnlyList<AudioDeviceInfo> devices, string defaultName, string? configuredId)
    {
        options.Clear();
        options.Add(new DeviceOption(null, defaultName));
        foreach (var device in devices)
        {
            options.Add(new DeviceOption(device.Id, device.IsDefault ? device.Name + DefaultSuffix : device.Name));
        }

        // Keep an unplugged choice visible instead of silently switching the user to another device.
        if (configuredId is not null && devices.All(d => d.Id != configuredId))
        {
            options.Add(new DeviceOption(configuredId, MissingDeviceName));
        }
    }

    private void SelectDevices(AudioSettings audio)
    {
        SelectedMicrophone = Microphones.FirstOrDefault(o => o.Id == audio.MicDeviceId);
        SelectedGameOutput = GameOutputs.FirstOrDefault(o => o.Id == audio.GameOutputDeviceId);
        SelectedMonitorOutput = MonitorOutputs.FirstOrDefault(o => o.Id == audio.MonitorOutputDeviceId);
    }

    private void SyncFromSettings(AppSettings settings)
    {
        _syncing = true;
        try
        {
            var audio = settings.Audio;
            VolumeToGame = audio.VolumeToGame;
            VolumeToMonitor = audio.VolumeToMonitor;
            MicVolume = audio.MicVolume;
            MicAlwaysOn = audio.MicMode == MicMode.AlwaysOn;
            ExclusivePlayback = audio.PlaybackMode == PlaybackMode.Exclusive;
            SelectDevices(audio);

            var ptt = settings.Bindings.FirstOrDefault(b => b.ActionId == ActionIds.PttHold);
            PttHotkeyText = ptt is null ? "chưa gán" : TriggerFormatter.Format(ptt.Trigger);

            PlaylistEnabled = settings.Features.Playlist;
            YouTubeImportEnabled = settings.Features.YouTubeImport;
            SyncTiles(settings);
            HasNoSounds = settings.Sounds.Count == 0;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncTiles(AppSettings settings)
    {
        var existing = Tiles.ToDictionary(t => t.Id);
        for (var i = 0; i < settings.Sounds.Count; i++)
        {
            var sound = settings.Sounds[i];
            if (!existing.TryGetValue(sound.Id, out var tile))
            {
                tile = new SoundTileViewModel(sound.Id, EditSound, ChangeKey, DeleteAsync, DuplicateAsync, TogglePlaylist);
                Tiles.Insert(i, tile);
            }
            else if (Tiles.IndexOf(tile) is var index && index != i)
            {
                Tiles.Move(index, i);
            }

            var binding = SoundBinding(settings, sound.Id);
            tile.Update(sound, binding is null ? null : TriggerFormatter.Format(binding.Trigger));
            tile.UpdatePlaylist(settings.Features.Playlist, settings.Playlist.Contains(sound.Id));
        }

        while (Tiles.Count > settings.Sounds.Count)
        {
            Tiles.RemoveAt(Tiles.Count - 1);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                UpdatePlaybackState();
            }
        }
        catch (OperationCanceledException)
        {
            // Window closing.
        }
    }

    private void UpdatePlaybackState()
    {
        IsVoiceTransmitting = _engine.IsVoiceTransmitting;

        var progress = new Dictionary<Guid, double>();
        foreach (var playback in _engine.GetActivePlaybacks())
        {
            var ratio = playback.Duration > TimeSpan.Zero ? playback.Position / playback.Duration : 0;
            progress[playback.SoundId] = Math.Max(progress.GetValueOrDefault(playback.SoundId), ratio);
        }

        foreach (var tile in Tiles)
        {
            var playing = progress.TryGetValue(tile.Id, out var ratio);
            tile.IsPlaying = playing;
            tile.Progress = playing ? ratio : 0;
        }

        NowPlaying = progress.Count == 0
            ? string.Empty
            : "Đang phát: " + string.Join(", ", Tiles.Where(t => t.IsPlaying).Select(t => t.Name));
    }

    private async Task DownloadFromYouTubeAsync()
    {
        if (_dialogs.DownloadFromYouTube() is { } audio)
        {
            await ImportDownloadedAsync(audio);
        }
    }

    /// <param name="name">Sound name; the file name when null.</param>
    private async Task<SoundItem?> ImportOneAsync(string path, int position, string? name = null)
    {
        var id = Guid.NewGuid();
        string libraryFile;
        try
        {
            libraryFile = await _library.ImportAsync(path, id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Could not import {Path}", path);
            return null;
        }

        var sound = new SoundItem(id, name ?? Path.GetFileNameWithoutExtension(path), libraryFile, SoundPalette.ForIndex(position),
            AudioSettings.DefaultVolume, PlaybackKind.Preloaded, EditParams.None);
        try
        {
            // Decoding once now proves the file is playable, sets Kind and warms the waveform cache for the editor.
            var waveform = await _waveforms.LoadAsync(sound);
            return sound with { Kind = EditLimits.KindFor(waveform.Duration) };
        }
        catch (Exception ex)
        {
            // Not a decodable audio file despite its extension: drop the library copy again.
            _logger.LogWarning(ex, "Imported file {Path} could not be decoded", path);
            TryDeleteLibraryFile(libraryFile);
            return null;
        }
    }

    private void TryDeleteLibraryFile(string libraryFile)
    {
        try
        {
            _library.Delete(libraryFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An orphaned library copy only costs disk space.
            _logger.LogWarning(ex, "Could not delete library file {File}", libraryFile);
        }
    }

    private void EditSound(SoundTileViewModel tile) => _dialogs.EditSound(tile.Id);

    private void ChangeKey(SoundTileViewModel tile) =>
        _dialogs.EditBinding(ActionIds.PlaySound, tile.Id, $"Phím tắt cho \"{tile.Name}\"", allowModeChoice: true);

    private async Task DeleteAsync(SoundTileViewModel tile)
    {
        var sound = _settings.Current.Sounds.FirstOrDefault(s => s.Id == tile.Id);
        if (sound is null || !_dialogs.Confirm($"Xóa \"{sound.Name}\" khỏi soundboard?\nFile gốc của bạn không bị ảnh hưởng.", "Xóa sound"))
        {
            return;
        }

        _engine.Stop(sound.Id);
        _settings.Update(s => s with
        {
            Sounds = [.. s.Sounds.Where(x => x.Id != sound.Id)],
            Bindings = [.. s.Bindings.Where(b => b.TargetId != sound.Id)],
            Playlist = [.. s.Playlist.Where(id => id != sound.Id)],
        });

        // Give a streamed playback time to close its file before deleting the library copy.
        await Task.Delay(FileReleaseDelay);
        TryDeleteLibraryFile(sound.LibraryFile);
    }

    private async Task DuplicateAsync(SoundTileViewModel tile)
    {
        var sound = _settings.Current.Sounds.FirstOrDefault(s => s.Id == tile.Id);
        if (sound is null)
        {
            return;
        }

        var copyId = Guid.NewGuid();
        string libraryFile;
        try
        {
            libraryFile = await _library.DuplicateAsync(sound.LibraryFile, copyId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogError(ex, "Duplicating sound {SoundId} failed", sound.Id);
            _dialogs.ShowError($"Không nhân bản được \"{sound.Name}\": {ex.Message}");
            return;
        }

        var copy = sound with { Id = copyId, Name = sound.Name + CopySuffix, LibraryFile = libraryFile };
        _settings.Update(s =>
        {
            var sounds = s.Sounds.ToList();
            var index = sounds.FindIndex(x => x.Id == sound.Id);
            sounds.Insert(index < 0 ? sounds.Count : index + 1, copy);
            return s with { Sounds = sounds };
        });
    }
}
