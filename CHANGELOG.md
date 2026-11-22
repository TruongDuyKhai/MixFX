# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), version theo [SemVer](https://semver.org/lang/vi/).

## [Unreleased]

### Added
- **P4 — Import + Editor.**
  - Import bằng nút "+ Thêm sound" hoặc kéo thả file vào lưới, nhận wav / mp3 / ogg / flac / aac / m4a / wma. File được copy vào `library\{guid}.{ext}`; file gốc chỉ được mở để đọc và không bao giờ bị ghi.
  - Mỗi file được giải mã thử khi import: file không đọc được thì bỏ bản sao và báo lỗi. Import xong tự mở Editor.
  - **Waveform:** đỉnh tính theo bucket 10 ms, cache ở `library\{guid}.peaks`. File cache hỏng hoặc lạ thì tự tính lại.
  - **Editor:**
    - Kéo 2 marker vàng để cắt, có ô nhập số ms. Hiện độ dài sau cắt và cho biết sound sẽ được nạp vào RAM hay đọc dần từ đĩa.
    - Fade in/out (vẽ trên waveform), gain −24…+12 dB, nút Normalize (đỉnh đoạn đã cắt về −1 dBFS), âm lượng, tên, màu.
    - "Nghe thử" chỉ phát qua Output B, không bao giờ ra game.
    - "Export WAV…" xuất file 48 kHz 16-bit stereo đã áp dụng chỉnh sửa, cắt ngọn (clip) chứ không để giá trị bị tràn.
  - Mọi chỉnh sửa chỉ là `EditParams`, áp dụng lúc phát; file trong thư viện không đổi.
- Thêm project test `Soundboard.App.Tests` cho ViewModel.

### Fixed
- Thoát app ném `ObjectDisposedException` vì `MainViewModel` bị Dispose hai lần (một lần ở `OnExit`, một lần do DI). Lỗi này làm việc giải phóng engine và hook bị dừng giữa chừng. Giờ `Dispose` gọi nhiều lần vẫn an toàn và chỉ DI giải phóng; có test tái hiện.
- Mở app lần hai không đưa được cửa sổ đang chạy lên trước khi app khác giữ foreground. Giờ tiến trình mở sau nhường quyền foreground bằng `AllowSetForegroundWindow`.

### Added (P3)
- **P3 — Giao diện chính.** Dùng theme Fluent Dark của WPF, thanh tiêu đề cũng tối.
  - **Thanh trên:** chọn micro, Output A, Output B. Mỗi thiết bị có chấm trạng thái (đang chạy / chưa có / lỗi / mất kết nối). Có đèn MIC sáng khi giọng đang được truyền, cùng nút "Phím tắt chung" và "Hướng dẫn".
  - **Lưới ô sound:** tên, màu, phím tắt, thanh tiến trình khi phát. Bấm ô sẽ đi qua `IActionDispatcher` với `TriggerMode` của sound. Chuột phải có "Đổi phím" / "Nhân bản" / "Xóa"; xóa chỉ xóa bản sao trong thư viện.
  - **Thanh dưới:** slider "Sound → đồng đội", "Sound → tai nghe", "Giọng của bạn", các tùy chọn mic luôn mở, sound mới dừng sound cũ, khởi động cùng Windows, và nút "Dừng tất cả".
  - **Cửa sổ gán phím:** nhấn để ghi nhận, có cảnh báo phím game và phím trùng, chọn kiểu kích hoạt, `PassThrough` mặc định bật.
  - **Cửa sổ "Phím tắt chung":** PTT, dừng tất cả, bật/tắt mic, tăng/giảm âm lượng.
  - **Hướng dẫn lần đầu:** kiểm tra VB-Cable, mở trang tải chính thức, hướng dẫn chọn "CABLE Output" và Open Mic trong Valorant. Có banner cảnh báo khi thiếu VB-Cable.
  - Thu nhỏ xuống khay hệ thống; menu khay có Mở / Dừng tất cả / Thoát. Khởi động cùng Windows ghi vào HKCU Run và mở với `--minimized`.
  - Chỉ chạy một phiên bản: mở lần hai thì cửa sổ đang chạy được đưa lên trước.
- Thư viện: nhân bản và xóa bản sao sound. Không bao giờ ghi đè file có sẵn, không bao giờ đụng file ngoài thư viện.
- Icon ứng dụng (`Assets/app.ico`).

### Added (P2)
- **P2 — Input.** Phím tắt toàn cục bằng `WH_KEYBOARD_LL` + `WH_MOUSE_LL`, không dùng `RegisterHotKey`.
  - Hook chạy trên luồng riêng có độ ưu tiên cao. Callback chỉ tra bảng binding rồi đẩy sự kiện vào `Channel`; action chạy ở luồng khác.
  - Hỗ trợ phím đơn, tổ hợp Ctrl/Alt/Shift/Win, và Mouse 4/5/giữa. Bỏ qua auto-repeat. Nhả phím vẫn được tính kể cả khi đã thả modifier trước.
  - Tự phục hồi khi một lần nhả phím bị mất (Win+L, UAC). Nhờ vậy không bị "kẹt" modifier.
  - `PassThrough`: khi bằng `false`, cả lúc nhấn, lặp và nhả đều bị ẩn khỏi game.
  - Chế độ "nhấn để ghi nhận" phím (Esc để hủy), trong lúc đó binding không chạy.
  - Hook chuột chỉ được cài khi thật sự cần.
- Các action `PlaySound` (4 chế độ `Press` / `Toggle` / `Hold` / `Restart`), `StopAll`, `ToggleMic`, `PttHold`, `VolumeUp`, `VolumeDown`.
- `BindingRules` cảnh báo phím game phổ biến và binding trùng. `TriggerFormatter` hiển thị tên phím như "Ctrl+Shift+Numpad 1" hay "Mouse 4".
- `ISettingsProvider`: thay đổi settings là nguyên tử và tự lưu sau 500 ms. `SettingsSync` đẩy thay đổi tới engine và hook.
- Test: 18 test cho bộ so khớp binding, 41 test cho action/quy tắc, và 2 test explicit với hook thật.

### Added (P1)
- **P1 — Audio.** `AudioEngine` (NAudio, WASAPI shared mode) theo sơ đồ mục 2:
  - Mic → `PttGate` (fade 10 ms, chế độ `AlwaysOn`) → trộn với sound → `PeakLimiter` (trần −1 dBFS) → Output A. Sound → Output B, không có giọng.
  - Định dạng nội bộ 48 kHz float stereo. Tự đổi tần số mẫu và số kênh theo mix format của từng thiết bị. Mic mono được nhân đôi thành stereo.
  - Sound ≤ 15 s sau khi cắt được nạp sẵn vào RAM. Sound dài được đọc trước từ đĩa ở luồng engine, audio callback không làm I/O.
  - Trim, fade in/out, gain, volume được áp dụng lúc phát. Dừng sound có fade 5 ms để không bị tiếng "bụp". Hỗ trợ chế độ `Overlap` / `Exclusive`.
  - Mất thiết bị thì phát `DeviceLost`, không crash, và tự mở lại khi thiết bị quay về. Output A khi chưa cấu hình chỉ tự chọn "CABLE Input", không bao giờ chọn loa.
- Core: `IAudioEngine`, `ISoundLibrary`, event `SoundStarted` / `SoundStopped` / `SoundFailed` / `DeviceLost`, cùng các model thiết bị.
- Storage: `SoundLibrary`, chặn tên file thư viện chứa đường dẫn (path traversal).
- Test: 59 unit test cho Audio, gồm test chứng minh audio callback không cấp phát bộ nhớ, và 2 test explicit chạy trên thiết bị thật (loopback).

### Added (P0)
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
