# Soundboard

[![CI](https://github.com/TruongDuyKhai/MixFX/actions/workflows/ci.yml/badge.svg)](https://github.com/TruongDuyKhai/MixFX/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/TruongDuyKhai/MixFX?include_prereleases)](https://github.com/TruongDuyKhai/MixFX/releases)

Ứng dụng Windows phát sound effect và nhạc **qua micro** khi chơi game (chủ yếu Valorant): đồng đội nghe được cả giọng thật của bạn lẫn âm thanh bạn phát.

```
Mic thật ──► [Push-to-talk] ──┐
                              ├─► [Trộn 48 kHz] ─► [Limiter −1 dBFS] ─► CABLE Input (VB-Cable) ─► Valorant dùng "CABLE Output" làm mic
Sound / nhạc ─────────────────┘                                       └─► Tai nghe của bạn (chỉ có sound, không có giọng bạn)
```

App **không** can thiệp vào game: không inject, không đọc/ghi bộ nhớ game, không overlay, không giả lập phím gửi vào game. Micro ảo dùng [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) (cài riêng, miễn phí).

> Trạng thái: **1.0.0-rc.1**. Chưa được kiểm chứng trong trận Valorant thật với VB-Cable.

## Tính năng

- Lưới ô sound: bấm để phát, có thanh tiến trình. Chuột phải để sửa, đổi phím, nhân bản hoặc xóa.
- Phím tắt toàn cục, hoạt động cả khi đang ở trong game:
  - Phím đơn (Numpad, F13–F24…), tổ hợp Ctrl/Alt/Shift/Win, Mouse 4/5.
  - 4 kiểu kích hoạt: Nhấn / Bật-tắt / Giữ / Phát lại.
  - Tùy chọn cho game vẫn nhận phím.
- Push-to-talk riêng cho giọng; sound luôn được phát. Có đèn MIC.
- Editor không phá file gốc:
  - Waveform, kéo marker để cắt, fade in/out, gain, Normalize.
  - Nghe thử chỉ qua tai nghe.
  - Export WAV.
- Âm lượng riêng cho: sound → đồng đội, sound → tai nghe, giọng.
- Thu nhỏ xuống khay hệ thống, khởi động cùng Windows, tự kết nối lại khi rút/cắm thiết bị.
- **Thử nghiệm** (tắt sẵn, bật ở nút "Thử nghiệm"): ducking, pitch/reverb cho sound, voice changer, playlist, web remote, tải nhạc từ YouTube.

### Web remote (Stream Deck, script)

Bật "Web remote" trong cửa sổ "Thử nghiệm", rồi lấy link có token ở đó. API chỉ nghe trên `localhost`:

```text
GET  http://localhost:8765/?token=<token>                    trang nút bấm
GET  http://localhost:8765/api/sounds?token=<token>          danh sách sound (JSON)
POST http://localhost:8765/api/trigger?token=<token>&action=PlaySound&target=<id sound>
POST http://localhost:8765/api/trigger?token=<token>&action=StopAll
POST http://localhost:8765/api/trigger?token=<token>&action=PttHold&phase=down    (rồi phase=up)
```

Với Stream Deck, dùng một plugin gửi HTTP request (ví dụ "Web Requests" / "API Ninja") trỏ tới các URL trên. Token cũng có thể gửi qua header `X-Soundboard-Token`.

### Tải nhạc từ YouTube

Bật "Tải nhạc từ YouTube" trong cửa sổ "Thử nghiệm", bấm "Tải từ YouTube", rồi dán link một video (tối đa 20 phút). App lấy phần âm thanh, thêm vào soundboard và mở Editor để bạn cắt.

Lần đầu dùng, app cần [yt-dlp](https://github.com/yt-dlp/yt-dlp) và [Deno](https://github.com/denoland/deno) (khoảng 70 MB):

- App chỉ tải sau khi bạn bấm "Tải công cụ". Nguồn là trang phát hành chính thức trên GitHub; app kiểm mã SHA-256 rồi lưu trong `%AppData%\Soundboard\tools`.
- App tự kiểm tra bản mới mỗi ngày khi bạn mở cửa sổ tải. Không cần ffmpeg.

Chỉ tải nội dung bạn có quyền sử dụng: điều khoản của YouTube không cho phép tải video, trừ khi YouTube tự cung cấp nút tải.

## Cài đặt và dùng

1. Cài **VB-Audio Virtual Cable** từ trang chính thức, rồi khởi động lại máy.
2. Tải `Soundboard-Setup-<version>.exe` ở trang [Releases](https://github.com/TruongDuyKhai/MixFX/releases) rồi chạy. Mặc định cài cho riêng bạn, không cần quyền Administrator.
3. Trong Soundboard:
   - **Micro thật:** micro bạn dùng.
   - **Output A:** "Tự động" (CABLE Input).
   - **Output B:** tai nghe của bạn.
4. Trong Valorant (Settings → Audio → Voice Chat):
   - **Input Device:** "CABLE Output (VB-Audio Virtual Cable)".
   - **Chế độ voice:** Open Mic.

   Push-to-talk do Soundboard lo: vào **Phím tắt chung** → Push-to-talk.
5. Bấm **+ Thêm sound** hoặc kéo thả file (wav, mp3, ogg, flac, aac, m4a, wma). Chuột phải vào ô → **Đổi phím** để gán phím tắt.

Dữ liệu (thư viện sound, cài đặt, log) nằm ở `%AppData%\Soundboard`. Gỡ cài sẽ giữ lại thư mục này, trừ khi bạn chọn xóa.

## Build từ mã nguồn

Cần: Windows 10/11 x64, [.NET SDK 10](https://dotnet.microsoft.com/download), và [Inno Setup 6](https://jrsoftware.org/isinfo.php) nếu muốn đóng gói installer.

```powershell
dotnet build Soundboard.sln -warnaserror
dotnet test Soundboard.sln
./build-installer.ps1            # → artifacts/Soundboard-Setup-<version>.exe
```

Một số test cần phần cứng thật và chỉ chạy khi được gọi riêng:

```powershell
# Engine → thiết bị phát thật (mặc định "CABLE Input"; đặt SOUNDBOARD_TEST_RENDER_DEVICE để chọn thiết bị khác)
dotnet test tests/Soundboard.Audio.Tests -- xUnit.Explicit=only
# Hook bàn phím toàn cục thật (bắn phím F23/F24)
dotnet test tests/Soundboard.Input.Tests -- xUnit.Explicit=only
```

### Phát hành phiên bản mới

Ghi thay đổi vào mục `## [Unreleased]` của [CHANGELOG.md](CHANGELOG.md), rồi chạy:

```powershell
./release.ps1 1.1.0 -Push        # hoặc 1.2.0-rc.1 cho bản pre-release
```

Script đổi version trong `Directory.Build.props`, đánh ngày cho CHANGELOG, build + test, commit và gắn tag `v1.1.0`. GitHub Actions sau đó tự build installer và đăng lên trang [Releases](https://github.com/TruongDuyKhai/MixFX/releases) kèm checksum SHA-256.

Kiến trúc, quy tắc code và lộ trình nằm trong [PROJECT_PROMPT.md](PROJECT_PROMPT.md). Lịch sử thay đổi xem [CHANGELOG.md](CHANGELOG.md); thư viện bên thứ ba xem [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Giấy phép

[MIT](LICENSE). VB-Audio Virtual Cable không nằm trong repo hay trong bản cài, và có giấy phép riêng của VB-Audio.
