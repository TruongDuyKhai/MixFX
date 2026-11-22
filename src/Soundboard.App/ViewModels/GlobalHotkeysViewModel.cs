using Soundboard.App.Services;
using Soundboard.Core.Abstractions;
using Soundboard.Core.Actions;
using Soundboard.Core.Bindings;

namespace Soundboard.App.ViewModels;

/// <summary>Keys for the actions that are not tied to one sound (§5.2 PTT key, §5.5 action list).</summary>
public sealed class GlobalHotkeysViewModel
{
    private readonly ISettingsProvider _settings;
    private readonly IDialogService _dialogs;

    public GlobalHotkeysViewModel(ISettingsProvider settings, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dialogs);

        _settings = settings;
        _dialogs = dialogs;
        Rows =
        [
            Row(ActionIds.PttHold, "Push-to-talk", "Giữ phím này thì đồng đội mới nghe giọng bạn. Sound luôn đi qua, không cần PTT."),
            Row(ActionIds.StopAll, "Dừng tất cả sound", "Tắt ngay mọi sound đang phát."),
            Row(ActionIds.ToggleMic, "Bật/tắt mic luôn mở", "Chuyển giữa chế độ PTT và mic luôn mở."),
            Row(ActionIds.VolumeUp, "Tăng âm lượng sound", "Tăng 10% âm lượng sound (cả game lẫn tai nghe)."),
            Row(ActionIds.VolumeDown, "Giảm âm lượng sound", "Giảm 10% âm lượng sound (cả game lẫn tai nghe)."),
        ];
        Refresh();
    }

    public IReadOnlyList<HotkeyRowViewModel> Rows { get; }

    private HotkeyRowViewModel Row(string actionId, string title, string description) =>
        new(actionId, title, description, Assign, Clear);

    private void Assign(HotkeyRowViewModel row)
    {
        _dialogs.EditBinding(row.ActionId, null, row.Title, allowModeChoice: false);
        Refresh();
    }

    private void Clear(HotkeyRowViewModel row)
    {
        _settings.Update(s => s with { Bindings = [.. s.Bindings.Where(b => !(b.ActionId == row.ActionId && b.TargetId is null))] });
        Refresh();
    }

    private void Refresh()
    {
        var bindings = _settings.Current.Bindings;
        foreach (var row in Rows)
        {
            var binding = bindings.FirstOrDefault(b => b.ActionId == row.ActionId && b.TargetId is null);
            row.KeyText = binding is null ? "Chưa gán" : TriggerFormatter.Format(binding.Trigger);
        }
    }
}
