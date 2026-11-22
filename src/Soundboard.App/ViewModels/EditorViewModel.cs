using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Soundboard.App.Services;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>
/// Non-destructive editor (§5.4): everything here only produces new <see cref="EditParams"/>; the
/// library file is never rewritten. Preview plays the unsaved edits on the monitor output only.
/// </summary>
public sealed class EditorViewModel : ObservableObject
{
    private readonly SoundItem _original;
    private readonly ISettingsProvider _settings;
    private readonly IWaveformService _waveformService;
    private readonly IAudioEngine _engine;
    private readonly IAudioExporter _exporter;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    private WaveformData? _waveform;
    private string _name;
    private string _color;
    private double _volume;
    private double _durationMs;
    private double _trimStartMs;
    private double _trimEndMs;
    private double _fadeInMs;
    private double _fadeOutMs;
    private double _gainDb;
    private bool _isLoading = true;
    private string _statusMessage = string.Empty;

    public EditorViewModel(
        SoundItem sound,
        ISettingsProvider settings,
        IWaveformService waveformService,
        IAudioEngine engine,
        IAudioExporter exporter,
        IDialogService dialogs,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(sound);

        _original = sound;
        _settings = settings;
        _waveformService = waveformService;
        _engine = engine;
        _exporter = exporter;
        _dialogs = dialogs;
        _logger = logger;

        _name = sound.Name;
        _color = sound.Color;
        _volume = sound.Volume;
        _trimStartMs = sound.Edit.TrimStartMs;
        _fadeInMs = sound.Edit.FadeInMs;
        _fadeOutMs = sound.Edit.FadeOutMs;
        _gainDb = sound.Edit.GainDb;

        PreviewCommand = new RelayCommand(() => _engine.Preview(BuildEdited()), () => !IsLoading);
        StopPreviewCommand = new RelayCommand(_engine.StopPreview);
        NormalizeCommand = new RelayCommand(Normalize, () => !IsLoading);
        ResetTrimCommand = new RelayCommand(ResetTrim, () => !IsLoading);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsLoading);
        SaveCommand = new RelayCommand(Save, () => !IsLoading);
        CancelCommand = new RelayCommand(() => Close(saved: false));
        SelectColorCommand = new RelayCommand<string>(c => Color = c ?? Color);
    }

    /// <summary>Raised with true after saving, false when cancelled.</summary>
    public event EventHandler<bool>? CloseRequested;

    public IReadOnlyList<string> Colors => SoundPalette.Colors;

    public double MinGainDb => EditLimits.MinGainDb;

    public double MaxGainDb => EditLimits.MaxGainDb;

    public double MinTrimmedMs => EditLimits.MinTrimmedMs;

    public RelayCommand PreviewCommand { get; }

    public ICommand StopPreviewCommand { get; }

    public RelayCommand NormalizeCommand { get; }

    public RelayCommand ResetTrimCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public RelayCommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand SelectColorCommand { get; }

    public IReadOnlyList<float>? Peaks => _waveform?.Peaks;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, Math.Clamp(value, AudioSettings.MinVolume, AudioSettings.MaxSoundVolume)))
            {
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    public string VolumeText => Math.Round(Volume * 100).ToString(CultureInfo.InvariantCulture) + "%";

    public double DurationMs
    {
        get => _durationMs;
        private set => SetProperty(ref _durationMs, value);
    }

    public double TrimStartMs
    {
        get => _trimStartMs;
        set
        {
            if (SetProperty(ref _trimStartMs, Math.Round(Math.Clamp(value, 0, Math.Max(0, TrimEndMs - EditLimits.MinTrimmedMs)))))
            {
                OnTrimChanged();
            }
        }
    }

    public double TrimEndMs
    {
        get => _trimEndMs;
        set
        {
            if (SetProperty(ref _trimEndMs, Math.Round(Math.Clamp(value, Math.Min(DurationMs, TrimStartMs + EditLimits.MinTrimmedMs), DurationMs))))
            {
                OnTrimChanged();
            }
        }
    }

    public double FadeInMs
    {
        get => _fadeInMs;
        set => SetProperty(ref _fadeInMs, Math.Round(Math.Clamp(value, 0, TrimmedMs)));
    }

    public double FadeOutMs
    {
        get => _fadeOutMs;
        set => SetProperty(ref _fadeOutMs, Math.Round(Math.Clamp(value, 0, TrimmedMs)));
    }

    public double GainDb
    {
        get => _gainDb;
        set
        {
            if (SetProperty(ref _gainDb, Math.Round(Math.Clamp(value, EditLimits.MinGainDb, EditLimits.MaxGainDb), 1)))
            {
                OnPropertyChanged(nameof(GainText));
            }
        }
    }

    public string GainText => GainDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB";

    public string TrimmedText => (TrimmedMs / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s"
        + (EditLimits.KindFor(TimeSpan.FromMilliseconds(TrimmedMs)) == PlaybackKind.Preloaded ? " · nạp sẵn vào RAM" : " · đọc dần từ ổ đĩa");

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                PreviewCommand.NotifyCanExecuteChanged();
                NormalizeCommand.NotifyCanExecuteChanged();
                ResetTrimCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private double TrimmedMs => Math.Max(0, TrimEndMs - TrimStartMs);

    public async Task LoadAsync()
    {
        try
        {
            _waveform = await _waveformService.LoadAsync(_original);
        }
        catch (Exception ex)
        {
            // Unreadable or missing library file: say so instead of showing an empty editor.
            _logger.LogError(ex, "Could not read waveform of {SoundId}", _original.Id);
            StatusMessage = $"Không đọc được file âm thanh: {ex.Message}";
            return;
        }

        // Fields, not setters: the clamps need the duration first, and the stored values are already valid.
        _durationMs = _waveform.Duration.TotalMilliseconds;
        _trimEndMs = Math.Min(_original.Edit.TrimEndMs ?? _durationMs, _durationMs);
        _trimStartMs = Math.Min(_trimStartMs, Math.Max(0, _trimEndMs - EditLimits.MinTrimmedMs));
        OnPropertyChanged(nameof(DurationMs));
        OnPropertyChanged(nameof(Peaks));
        OnPropertyChanged(nameof(TrimStartMs));
        OnPropertyChanged(nameof(TrimEndMs));
        OnPropertyChanged(nameof(TrimmedText));
        IsLoading = false;
    }

    /// <summary>Stops a running preview when the window closes, so it does not keep playing in the headphones.</summary>
    public void OnClosed() => _engine.StopPreview();

    private void OnTrimChanged()
    {
        OnPropertyChanged(nameof(TrimmedText));

        // Re-apply the fade clamps against the new length.
        FadeInMs = _fadeInMs;
        FadeOutMs = _fadeOutMs;
    }

    private SoundItem BuildEdited()
    {
        // A trim end at the very end is stored as "no trim", so the sound follows the file exactly.
        int? trimEnd = TrimEndMs >= DurationMs - 1 ? null : (int)TrimEndMs;
        var edit = new EditParams((int)TrimStartMs, trimEnd, (int)FadeInMs, (int)FadeOutMs, (float)GainDb, _original.Edit.Effects);
        return _original with
        {
            Name = string.IsNullOrWhiteSpace(Name) ? _original.Name : Name.Trim(),
            Color = Color,
            Volume = (float)Volume,
            Kind = EditLimits.KindFor(TimeSpan.FromMilliseconds(TrimmedMs)),
            Edit = edit,
        };
    }

    private void Normalize()
    {
        var peak = _waveform!.PeakBetween(TimeSpan.FromMilliseconds(TrimStartMs), TimeSpan.FromMilliseconds(TrimEndMs));
        GainDb = EditLimits.NormalizeGainDb(peak);
        StatusMessage = peak > 0
            ? $"Đã chỉnh gain để đỉnh đạt {EditLimits.NormalizeTargetDb:0} dBFS."
            : "Đoạn đã cắt hoàn toàn im lặng, không có gì để normalize.";
    }

    private void ResetTrim()
    {
        _trimStartMs = 0;
        _trimEndMs = DurationMs;
        OnPropertyChanged(nameof(TrimStartMs));
        OnPropertyChanged(nameof(TrimEndMs));
        OnTrimChanged();
    }

    private async Task ExportAsync()
    {
        var edited = BuildEdited();
        var path = _dialogs.PickExportPath(edited.Name + ".wav");
        if (path is null)
        {
            return;
        }

        StatusMessage = "Đang xuất…";
        try
        {
            await _exporter.ExportWavAsync(edited, path);
            StatusMessage = $"Đã xuất: {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export of {SoundId} failed", _original.Id);
            StatusMessage = string.Empty;
            _dialogs.ShowError($"Không xuất được file: {ex.Message}");
        }
    }

    private void Save()
    {
        var edited = BuildEdited();
        _settings.Update(s => s with { Sounds = [.. s.Sounds.Select(x => x.Id == edited.Id ? edited : x)] });
        Close(saved: true);
    }

    private void Close(bool saved)
    {
        _engine.StopPreview();
        CloseRequested?.Invoke(this, saved);
    }
}
