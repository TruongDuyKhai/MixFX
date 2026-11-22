using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.App.Services;
using Soundboard.App.WebRemote;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>"Tính năng thử nghiệm": the P6 feature flags (all off by default, §7.2.7) and their settings.</summary>
public sealed class ExperimentalViewModel : ObservableObject
{
    private readonly ISettingsProvider _settings;
    private readonly IDialogService _dialogs;

    public ExperimentalViewModel(ISettingsProvider settings, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dialogs);

        _settings = settings;
        _dialogs = dialogs;
        OpenRemoteCommand = new RelayCommand(() => _dialogs.OpenUrl(RemoteUrl), () => WebRemote && HasToken);
        NewTokenCommand = new RelayCommand(() => UpdateRemote(r => r with { Token = WebRemoteToken.Generate() }));
    }

    public ICommand OpenRemoteCommand { get; }

    public ICommand NewTokenCommand { get; }

    private AppSettings Current => _settings.Current;

    public bool Ducking
    {
        get => Current.Features.Ducking;
        set => UpdateFeatures(f => f with { Ducking = value });
    }

    public double MinDuckingDb => AudioSettings.MinDuckingDb;

    public double DuckingDb
    {
        get => Current.Audio.DuckingDb;
        set
        {
            _settings.Update(s => s with { Audio = s.Audio with { DuckingDb = (float)Math.Round(value) } });
            OnPropertyChanged();
            OnPropertyChanged(nameof(DuckingText));
        }
    }

    public string DuckingText => DuckingDb.ToString("0", CultureInfo.InvariantCulture) + " dB";

    public bool SoundEffects
    {
        get => Current.Features.SoundEffects;
        set => UpdateFeatures(f => f with { SoundEffects = value });
    }

    public bool VoiceChanger
    {
        get => Current.Features.VoiceChanger;
        set => UpdateFeatures(f => f with { VoiceChanger = value });
    }

    public double MaxVoicePitch => AudioSettings.MaxVoicePitchSemitones;

    public double VoicePitch
    {
        get => Current.Audio.VoicePitchSemitones;
        set
        {
            _settings.Update(s => s with { Audio = s.Audio with { VoicePitchSemitones = (float)Math.Round(value) } });
            OnPropertyChanged();
            OnPropertyChanged(nameof(VoicePitchText));
        }
    }

    public string VoicePitchText => VoicePitch.ToString("+0;-0;0", CultureInfo.InvariantCulture) + " cung";

    public bool Playlist
    {
        get => Current.Features.Playlist;
        set => UpdateFeatures(f => f with { Playlist = value });
    }

    public bool WebRemote
    {
        get => Current.Features.WebRemote;
        set
        {
            // The remote is useless (and unsafe) without a token: create one the first time it is switched on.
            if (value && !HasToken)
            {
                UpdateRemote(r => r with { Token = WebRemoteToken.Generate() });
            }

            UpdateFeatures(f => f with { WebRemote = value });
        }
    }

    public string Port
    {
        get => Current.WebRemote.Port.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1024 and <= 65535)
            {
                UpdateRemote(r => r with { Port = port });
            }
            else
            {
                _dialogs.ShowError("Cổng phải là số từ 1024 đến 65535.");
                OnPropertyChanged();
            }
        }
    }

    public string RemoteUrl => $"http://localhost:{Current.WebRemote.Port}/?token={Current.WebRemote.Token}";

    private bool HasToken => !string.IsNullOrEmpty(Current.WebRemote.Token);

    private void UpdateFeatures(Func<FeatureFlags, FeatureFlags> change)
    {
        _settings.Update(s => s with { Features = change(s.Features) });
        RaiseAll();
    }

    private void UpdateRemote(Func<WebRemoteSettings, WebRemoteSettings> change)
    {
        _settings.Update(s => s with { WebRemote = change(s.WebRemote) });
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(string.Empty);
        ((RelayCommand)OpenRemoteCommand).NotifyCanExecuteChanged();
    }
}
