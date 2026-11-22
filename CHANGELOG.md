# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), version theo [SemVer](https://semver.org/lang/vi/).

## [Unreleased] — 1.0.0-rc.1

### Added
- **P5 — Đóng gói.**
  - `build-installer.ps1`: đọc version từ `Directory.Build.props`, chạy `dotnet publish` (self-contained, single-file, win-x64), rồi gọi Inno Setup 6. Kết quả là `artifacts/Soundboard-Setup-<version>.exe`.
  - `installer/Soundboard.iss`:
    - Cài vào `{autopf}\Soundboard`. Mặc định cài cho riêng user (không cần quyền Admin) nhưng vẫn chọn được "all users".
    - Có shortcut Start Menu, tùy chọn shortcut Desktop, tùy chọn khởi động cùng Windows.
    - Kiểm tra VB-Cable và mời mở trang tải chính thức; không đóng gói VB-Cable.
    - Phát hiện app đang chạy qua `AppMutex`.
    - Gỡ cài giữ `%AppData%\Soundboard`; chỉ xóa khi người dùng chọn Yes, và không bao giờ xóa khi gỡ im lặng.
  - `THIRD-PARTY-NOTICES.md` và `licenses/Apache-2.0.txt` được cài kèm app; thêm `README.md`.
- Giấy phép MIT cho project (`LICENSE`, cũng được cài kèm app dưới tên `LICENSE.txt`).
- **P4 — Import + Editor.**
  - Import bằng nút hoặc kéo thả: copy vào `library\{guid}.{ext}`, file gốc chỉ được đọc. Mỗi file được giải mã thử khi import, và Editor tự mở sau đó.
  - Waveform (đỉnh theo bucket 10 ms) cache ở `library\{guid}.peaks`.
  - Editor:
    - Marker cắt kéo được, fade in/out, gain −24…+12 dB, Normalize về −1 dBFS, âm lượng, tên, màu.
    - Nghe thử chỉ qua Output B.
    - Export WAV 48 kHz 16-bit.
  - Mọi chỉnh sửa chỉ là `EditParams` và được áp dụng lúc phát.
- **P3 — Giao diện chính.** Theme Fluent Dark, thanh tiêu đề tối.
  - Chọn mic / Output A / Output B, có trạng thái từng thiết bị và đèn MIC.
  - Lưới ô sound có tiến trình phát, menu chuột phải.
  - Slider âm lượng; tùy chọn mic luôn mở, phát độc quyền, khởi động cùng Windows; nút "Dừng tất cả".
  - Cửa sổ gán phím: nhấn để ghi nhận, cảnh báo phím game / phím trùng.
  - Cửa sổ "Phím tắt chung" (PTT, dừng tất cả, bật/tắt mic, tăng/giảm âm lượng).
  - Hướng dẫn lần đầu kèm kiểm tra VB-Cable.
  - Khay hệ thống, chỉ chạy một phiên bản.
- **P2 — Input.** Hook `WH_KEYBOARD_LL` / `WH_MOUSE_LL` trên luồng riêng; callback chỉ so khớp rồi đẩy vào `Channel`.
  - Phím đơn, tổ hợp, Mouse 4/5. Bỏ qua auto-repeat, tự phục hồi khi mất sự kiện nhả phím.
  - `PassThrough`; hook chuột chỉ được cài khi cần.
  - Action `PlaySound` (Press / Toggle / Hold / Restart), `StopAll`, `ToggleMic`, `PttHold`, `VolumeUp`, `VolumeDown`.
- **P1 — Audio.** `AudioEngine` dùng NAudio WASAPI.
  - Mic → `PttGate` (fade 10 ms) → trộn → `PeakLimiter` (−1 dBFS) → Output A. Sound → Output B.
  - Định dạng nội bộ 48 kHz float stereo, tự đổi theo từng thiết bị.
  - Sound ≤ 15 s được nạp sẵn vào RAM; sound dài hơn được đọc trước từ đĩa ở luồng riêng.
  - `DeviceLost` cùng cơ chế tự kết nối lại. Audio callback không cấp phát bộ nhớ (có test).
- **P0 — Khung.** Solution .NET 10 (5 project, 5 project test), `Directory.Build.props` / `Directory.Packages.props` / `global.json`.
  - Model mục 6, `ActionDispatcher`.
  - `JsonSettingsStore` ghi atomic, có `.bak`, khôi phục mà không làm mất dữ liệu, kèm khung migration.
  - Serilog ghi log xoay vòng.
- ADR 0001 (.NET 10 LTS), ADR 0002 (ghim FluentAssertions 7.x).

### Changed
- Stack chuyển từ .NET 8 sang **.NET 10 LTS**, C# 14, vì .NET 8 đã hết hỗ trợ ngày 10/11/2026.

### Fixed
- Thoát app ném `ObjectDisposedException` vì `MainViewModel` bị Dispose hai lần, làm việc giải phóng engine và hook bị dừng giữa chừng. Đã sửa và có test tái hiện.
- Mở app lần hai không đưa được cửa sổ đang chạy lên trước khi app khác đang giữ foreground.
