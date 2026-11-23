# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), version theo [SemVer](https://semver.org/lang/vi/).

## [Unreleased]

## [1.3.0] - 2026-11-23

### Added
- **Nút X hỏi bạn muốn làm gì:** "Thu nhỏ xuống khay" (app chạy ngầm, phím tắt vẫn dùng được), "Thoát" hoặc "Hủy".
  - Tick "Không hỏi lại" thì những lần sau làm luôn theo lựa chọn đó. Đổi lại được ở mục "Khi bấm X" trong cửa sổ chính.
  - "Thoát" trong menu của icon khay, hoặc Windows tắt máy / đăng xuất, thì thoát luôn, không hỏi.
- Schema settings **v4** (`MigrateV3ToV4`, test bằng fixture v3): thêm `window.closeAction` (mặc định `Ask`).

### Changed
- **Nút thu nhỏ giờ đưa cửa sổ xuống taskbar** như app bình thường, không ẩn vào khay nữa, để mở lại nhanh khi đang chơi game.

## [1.2.0] - 2026-11-23

### Added
- **Tải nhạc từ YouTube** (thử nghiệm, mặc định tắt, ADR 0005). Bật trong cửa sổ "Thử nghiệm" thì có thêm nút "Tải từ YouTube":
  - Dán link một video (tối đa 20 phút). App lấy phần âm thanh, thêm vào soundboard với tên video, rồi mở Editor.
  - Lần đầu dùng, app hỏi trước rồi mới tải yt-dlp và Deno (khoảng 70 MB) từ GitHub chính thức. Mỗi file được kiểm SHA-256; file không khớp bị bỏ và không bao giờ được chạy.
  - App tự kiểm tra bản mới mỗi ngày khi bạn mở cửa sổ tải.
  - Hủy giữa chừng thì tắt hẳn tiến trình tải và xóa file tạm.
- Schema settings **v3** (`MigrateV2ToV3`, test bằng fixture v2): thêm cờ `youTubeImport`.

### Changed
- Interface mới `IAudioDownloader`. `MainViewModel` nhận thêm tham số này, và `IDialogService` có thêm `DownloadFromYouTube`.

## [1.1.0] - 2026-11-22

### Added
- **P6 — Tính năng thử nghiệm**, mỗi cái sau một feature flag **mặc định tắt**, bật trong cửa sổ "Thử nghiệm":
  - **Ducking:** hạ sound gửi vào game khi bạn đang nói (mặc định −12 dB, chỉnh được).
  - **Hiệu ứng cho sound:** pitch (±12 cung) và reverb, chỉnh trong Editor; áp dụng lúc phát, khi nghe thử và khi export.
  - **Voice changer:** đổi cao độ giọng thật trước cổng PTT.
  - **Playlist:** "Thêm vào playlist" trong menu chuột phải, có nút ▶/■ và phím tắt chung; phát lần lượt, lặp lại, chỉ chuyển bài khi bài hiện tại phát hết.
  - **Web remote:** trang nút và API HTTP trên `localhost` có token, dùng được với Stream Deck, script hoặc trình duyệt trên cùng máy (ADR 0004).
- Schema settings **v2** với migration từ v1 (test bằng fixture v1); file v1 cũ được giữ làm `.bak` sau lần lưu đầu.

### Changed
- `SoundStoppedEventArgs` có thêm `Completed` (phát hết tự nhiên hay bị dừng). `IAudioEngine` có thêm `ApplyFeatures`.

## [1.0.0] - 2026-11-22

### Added
- **Cơ chế phát hành.**
  - `release.ps1 <version> [-Push]`: kiểm SemVer, repo sạch, đang ở `main` mới nhất; ghi version vào `Directory.Build.props`; chuyển mục Unreleased của CHANGELOG thành `[<version>] - <ngày>`; build, test, commit, rồi tạo tag `v<version>`.
  - Workflow **Release**: chạy khi push tag `v*`. Kiểm tag khớp version, build, test, đóng gói installer bằng Inno Setup, rồi tạo GitHub Release kèm `Soundboard-Setup-<version>.exe`, file `.sha256` và release notes lấy từ CHANGELOG. Bản `-rc` / `-beta` được đánh dấu pre-release.
  - Workflow **CI**: build (warnings as errors) và test cho mỗi lần push lên `main` hoặc mở PR.
- **Watchdog cho phím tắt.** Windows có thể tự gỡ low-level hook mà không báo khi callback bị chậm (máy quá tải). Hook giờ được cài lại mỗi 10 giây:
  - Hook mới được cài trước, các lời gọi hook còn chờ được xử lý hết, rồi mới gỡ hook cũ. Nhờ vậy không có khoảng trống và không mất phím nào, kể cả phím nhả của PTT.
  - Bộ lọc `DuplicateEventFilter` bỏ sự kiện đi qua cả hai hook trong lúc chồng nhau.
  - Có test hook thật cho phím đến đúng lúc đang cài lại.
- **P5 — Đóng gói.**
  - `build-installer.ps1`: đọc version từ `Directory.Build.props`, chạy `dotnet publish` (self-contained, single-file, win-x64), rồi gọi Inno Setup 6. Kết quả là `artifacts/Soundboard-Setup-<version>.exe`.
  - `installer/Soundboard.iss`:
    - Cài vào `{autopf}\Soundboard`. Mặc định cài cho riêng user (không cần quyền Admin) nhưng vẫn chọn được "all users".
    - Có shortcut Start Menu, tùy chọn shortcut Desktop, tùy chọn khởi động cùng Windows.
    - Kiểm tra VB-Cable và mời mở trang tải chính thức; không đóng gói VB-Cable.
    - Phát hiện app đang chạy qua `AppMutex`.
    - Gỡ cài giữ `%AppData%\Soundboard`; chỉ xóa khi người dùng tick ô "Xóa cả dữ liệu" (mặc định không tick), và không bao giờ xóa khi gỡ im lặng.
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
