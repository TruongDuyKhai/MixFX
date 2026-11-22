using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>One cell of the sound grid (§5.6): name, colour, hotkey and playback progress.</summary>
public sealed class SoundTileViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _color = string.Empty;
    private string _hotkeyText = string.Empty;
    private double _progress;
    private bool _isPlaying;
    private bool _isInPlaylist;
    private bool _playlistEnabled;

    public SoundTileViewModel(
        Guid id,
        Action<SoundTileViewModel> edit,
        Action<SoundTileViewModel> changeKey,
        Func<SoundTileViewModel, Task> delete,
        Func<SoundTileViewModel, Task> duplicate,
        Action<SoundTileViewModel> togglePlaylist)
    {
        Id = id;
        EditCommand = new RelayCommand(() => edit(this));
        ChangeKeyCommand = new RelayCommand(() => changeKey(this));
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
        DuplicateCommand = new AsyncRelayCommand(() => duplicate(this));
        TogglePlaylistCommand = new RelayCommand(() => togglePlaylist(this));
    }

    public ICommand TogglePlaylistCommand { get; }

    /// <summary>Shown as a ♫ badge and drives the context-menu text.</summary>
    public bool IsInPlaylist
    {
        get => _isInPlaylist;
        private set
        {
            if (SetProperty(ref _isInPlaylist, value))
            {
                OnPropertyChanged(nameof(PlaylistMenuText));
                OnPropertyChanged(nameof(ShowPlaylistBadge));
            }
        }
    }

    /// <summary>The playlist feature flag; the menu entry and badge exist only while it is on.</summary>
    public bool PlaylistEnabled
    {
        get => _playlistEnabled;
        private set
        {
            if (SetProperty(ref _playlistEnabled, value))
            {
                OnPropertyChanged(nameof(ShowPlaylistBadge));
            }
        }
    }

    public bool ShowPlaylistBadge => PlaylistEnabled && IsInPlaylist;

    public string PlaylistMenuText => IsInPlaylist ? "Bỏ khỏi playlist" : "Thêm vào playlist";

    public void UpdatePlaylist(bool enabled, bool inPlaylist)
    {
        PlaylistEnabled = enabled;
        IsInPlaylist = inPlaylist;
    }

    public Guid Id { get; }

    public ICommand EditCommand { get; }

    public ICommand ChangeKeyCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand DuplicateCommand { get; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>"#RRGGBB".</summary>
    public string Color
    {
        get => _color;
        private set => SetProperty(ref _color, value);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        private set => SetProperty(ref _hotkeyText, value);
    }

    /// <summary>0…1 through the trimmed sound.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    public void Update(SoundItem sound, string? hotkey)
    {
        ArgumentNullException.ThrowIfNull(sound);

        Name = sound.Name;
        Color = sound.Color;
        HotkeyText = hotkey ?? "Chưa gán phím";
    }
}
