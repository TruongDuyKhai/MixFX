using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.App.Services;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>First-run guide (§5.6): checks VB-Cable and explains the Valorant settings.</summary>
public sealed class SetupViewModel : ObservableObject
{
    /// <summary>Official VB-Audio page. The cable is never bundled: its licence does not allow redistribution (§10.2).</summary>
    public const string VbCableUrl = "https://vb-audio.com/Cable/";

    private readonly IAudioEngine _engine;
    private bool _isCableInstalled;

    public SetupViewModel(IAudioEngine engine, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dialogs);

        _engine = engine;
        CheckAgainCommand = new RelayCommand(Refresh);
        OpenDownloadCommand = new RelayCommand(() => dialogs.OpenUrl(VbCableUrl));
        Refresh();
    }

    public ICommand CheckAgainCommand { get; }

    public ICommand OpenDownloadCommand { get; }

    public bool IsCableInstalled
    {
        get => _isCableInstalled;
        private set
        {
            if (SetProperty(ref _isCableInstalled, value))
            {
                OnPropertyChanged(nameof(IsCableMissing));
                OnPropertyChanged(nameof(CableStatusText));
            }
        }
    }

    public bool IsCableMissing => !IsCableInstalled;

    public string CableStatusText => IsCableInstalled
        ? "✓ Đã tìm thấy VB-Audio Virtual Cable."
        : "✗ Chưa tìm thấy VB-Audio Virtual Cable.";

    private void Refresh() =>
        IsCableInstalled = _engine.GetDevices(AudioDeviceFlow.Render).Any(d => d.IsVirtualCable);
}
