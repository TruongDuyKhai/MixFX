using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

/// <summary>A <see cref="TriggerMode"/> with its label for the mode picker.</summary>
public sealed record TriggerModeOption(TriggerMode Mode, string Label)
{
    public static IReadOnlyList<TriggerModeOption> All { get; } =
    [
        new(TriggerMode.Press, "Nhấn: mỗi lần nhấn phát một lần"),
        new(TriggerMode.Toggle, "Bật/tắt: nhấn lần nữa để dừng"),
        new(TriggerMode.Hold, "Giữ: chỉ phát khi đang giữ phím"),
        new(TriggerMode.Restart, "Phát lại: nhấn là phát lại từ đầu"),
    ];
}
