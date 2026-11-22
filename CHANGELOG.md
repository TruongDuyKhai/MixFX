# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), version theo [SemVer](https://semver.org/lang/vi/).

## [Unreleased]

### Added
- **P0 — Khung.** Solution `Soundboard.sln` gồm 5 project (`Core`, `Audio`, `Input`, `Storage`, `App`) và 2 project test, đúng sơ đồ phụ thuộc ở mục 4.1. `Audio` và `Input` mới chỉ có `.csproj`.
- `Directory.Build.props` (version `0.1.0`, Nullable, TreatWarningsAsErrors), `Directory.Packages.props` (ghim version mọi package), `global.json` (ghim SDK 10.0.404).
- Core: các model ở mục 6; `AppSettings` (schema v1) với `AudioSettings` (thiết bị, 3 mức âm lượng, `MicMode`, `PlaybackMode`) và `FeatureFlags`; `IAction`, `IActionDispatcher`, `ActionDispatcher`; `ISettingsStore`.
- Storage: `JsonSettingsStore`
  - Ghi atomic (`.tmp` → `File.Replace`), giữ bản trước ở `settings.json.bak`.
  - `settings.json` hỏng hoặc mất thì khôi phục từ `.bak`. File không đọc được được đổi tên thành `*.corrupt-<thời điểm>`, không bao giờ bị xóa hay ghi đè.
  - File do bản app mới hơn tạo ra thì mở ở chế độ chỉ đọc và chặn lưu.
  - Chấp nhận file sửa tay (BOM, comment, dấu phẩy thừa).
  - Có khung migration (`SettingsMigrator`, `ISettingsMigration`) và fixture `settings.v1.json`.
- App: WPF + Generic Host + DI. Serilog ghi log xoay vòng theo ngày ở `%AppData%\Soundboard\logs` (giữ 14 file). Cửa sổ chính tối màu. Báo người dùng bằng hộp thoại khi cài đặt phải khôi phục. Lưu cài đặt khi thoát, nhưng chỉ khi đã đọc được cài đặt.
- ADR 0001 (.NET 10 LTS) và ADR 0002 (ghim FluentAssertions 7.x).

### Changed
- Stack chuyển từ .NET 8 sang **.NET 10 LTS**, C# 14 (ADR 0001), vì .NET 8 đã hết hỗ trợ ngày 10/11/2026.
