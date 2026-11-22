using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Soundboard.App.ViewModels;

/// <summary>One global action (PTT, Stop all…) and its current key.</summary>
public sealed class HotkeyRowViewModel : ObservableObject
{
    private string _keyText = string.Empty;

    public HotkeyRowViewModel(string actionId, string title, string description, Action<HotkeyRowViewModel> assign, Action<HotkeyRowViewModel> clear)
    {
        ActionId = actionId;
        Title = title;
        Description = description;
        AssignCommand = new RelayCommand(() => assign(this));
        ClearCommand = new RelayCommand(() => clear(this));
    }

    public string ActionId { get; }

    public string Title { get; }

    public string Description { get; }

    public ICommand AssignCommand { get; }

    public ICommand ClearCommand { get; }

    public string KeyText
    {
        get => _keyText;
        set => SetProperty(ref _keyText, value);
    }
}
