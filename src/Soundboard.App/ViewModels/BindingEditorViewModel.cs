using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Bindings;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>"Press a key to assign" (§5.5): captures a trigger, shows warnings, saves one binding.</summary>
public sealed class BindingEditorViewModel : ObservableObject
{
    private readonly IInputService _input;
    private readonly ISettingsProvider _settings;
    private readonly string _actionId;
    private readonly Guid? _targetId;
    private readonly Binding? _existing;

    private CancellationTokenSource? _capture;
    private Trigger? _trigger;
    private string _keyText;
    private bool _isCapturing;
    private IReadOnlyList<string> _warnings = [];
    private TriggerMode _mode;
    private bool _passThrough;

    public BindingEditorViewModel(
        IInputService input,
        ISettingsProvider settings,
        string actionId,
        Guid? targetId,
        string title,
        bool allowModeChoice)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        _input = input;
        _settings = settings;
        _actionId = actionId;
        _targetId = targetId;
        Title = title;
        AllowModeChoice = allowModeChoice;

        _existing = settings.Current.Bindings.FirstOrDefault(b => b.ActionId == actionId && b.TargetId == targetId);
        _trigger = _existing?.Trigger;
        _mode = _existing?.Mode ?? TriggerMode.Press;

        // Default true: the game keeps receiving the key unless the user deliberately opts out (§5.5).
        _passThrough = _existing?.PassThrough ?? true;
        _keyText = _trigger is null ? "Chưa gán phím" : TriggerFormatter.Format(_trigger);

        CaptureCommand = new AsyncRelayCommand(CaptureAsync);
        SaveCommand = new RelayCommand(Save, () => _trigger is not null && !IsCapturing);
        RemoveCommand = new RelayCommand(Remove, () => _existing is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Raised with true after saving or removing, false when cancelled.</summary>
    public event EventHandler<bool>? CloseRequested;

    public string Title { get; }

    public bool AllowModeChoice { get; }

    public IReadOnlyList<TriggerModeOption> Modes => TriggerModeOption.All;

    public AsyncRelayCommand CaptureCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string KeyText
    {
        get => _keyText;
        private set => SetProperty(ref _keyText, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (SetProperty(ref _isCapturing, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<string> Warnings
    {
        get => _warnings;
        private set => SetProperty(ref _warnings, value);
    }

    public TriggerMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public bool PassThrough
    {
        get => _passThrough;
        set => SetProperty(ref _passThrough, value);
    }

    /// <summary>Called when the window closes so the hook stops swallowing the next key.</summary>
    public void StopCapture() => _capture?.Cancel();

    private async Task CaptureAsync()
    {
        _capture?.Cancel();
        _capture?.Dispose();
        _capture = new CancellationTokenSource();

        IsCapturing = true;
        Warnings = [];
        try
        {
            var trigger = await _input.CaptureNextTriggerAsync(_capture.Token);
            if (trigger is not null)
            {
                _trigger = trigger;
                KeyText = TriggerFormatter.Format(trigger);
                Warnings = BindingRules.Warnings(trigger, _settings.Current.Bindings, _existing?.Id);
            }
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void Save()
    {
        var binding = new Binding(_existing?.Id ?? Guid.NewGuid(), _trigger!, _actionId, _targetId, Mode, PassThrough);
        _settings.Update(s => s with
        {
            Bindings = _existing is null
                ? [.. s.Bindings, binding]
                : [.. s.Bindings.Select(b => b.Id == binding.Id ? binding : b)],
        });
        CloseRequested?.Invoke(this, true);
    }

    private void Remove()
    {
        var id = _existing!.Id;
        _settings.Update(s => s with { Bindings = [.. s.Bindings.Where(b => b.Id != id)] });
        CloseRequested?.Invoke(this, true);
    }
}
