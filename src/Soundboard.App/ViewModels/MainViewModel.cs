using CommunityToolkit.Mvvm.ComponentModel;
using Soundboard.App.Services;
using Soundboard.Core.Models;

namespace Soundboard.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private string _settingsStatus = "Đang tải cài đặt…";
    private string? _settingsWarning;

    public string Title { get; } = $"Soundboard {AppInfo.Version}";

    /// <summary>One-line state shown in the status bar.</summary>
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
}
