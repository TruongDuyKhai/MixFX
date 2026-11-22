# Checklist test thủ công

Definition of Done (PROJECT_PROMPT.md §8) yêu cầu test trên thiết bị thật mỗi khi sửa Audio/Input. Trước mỗi bản phát hành, chạy các mục dưới đây. Mỗi mục ghi rõ: **làm gì** → **kết quả đúng**.

Test tự động cho phần cứng (chạy riêng, cần thiết bị thật):

```powershell
dotnet test tests/Soundboard.Audio.Tests -- xUnit.Explicit=only   # mặc định dùng "CABLE Input"
dotnet test tests/Soundboard.Input.Tests -- xUnit.Explicit=only   # bắn phím F23/F24
```

## 0. Chuẩn bị
- [ ] Đã cài VB-Audio Virtual Cable và khởi động lại máy → Windows có thiết bị "CABLE Input" và "CABLE Output".
- [ ] Cài bằng `Soundboard-Setup-<version>.exe` (bản trên trang Releases) → app mở, màn hình "Hướng dẫn cài đặt" ghi "✓ Đã tìm thấy VB-Audio Virtual Cable".

## 1. Âm thanh (P1)
- [ ] Output A để "Tự động" → chấm trạng thái xanh, tooltip ghi "Đang chạy" trên CABLE Input.
- [ ] Valorant: Input Device = "CABLE Output", Open Mic. Vào phòng tập, mở "Mic Test" → giọng chỉ lên khi giữ phím PTT, đèn MIC trong Soundboard sáng đúng lúc đó.
- [ ] Bấm một ô sound khi **không** giữ PTT → đồng đội (hoặc Discord ở máy khác) nghe sound; không nghe giọng.
- [ ] Sound rất to (gain +12 dB) → đồng đội nghe không bị vỡ/chói (limiter −1 dBFS).
- [ ] Output B (tai nghe) nghe sound, **không** nghe lại giọng mình.
- [ ] Rút tai nghe khi đang phát → app không crash, thanh trạng thái báo mất thiết bị; cắm lại → tự chạy lại.
- [ ] Sound dài hơn 15 s phát liền mạch, không giật.

## 2. Phím tắt (P2)
- [ ] Gán Numpad 1 cho một sound, vào trận Valorant, bấm → sound phát; game vẫn nhận phím (PassThrough bật).
- [ ] Tổ hợp (Ctrl+Numpad 2) và Mouse 4/5 hoạt động trong trận.
- [ ] 4 kiểu kích hoạt: Nhấn (mỗi lần một bản), Bật/tắt, Giữ (nhả là tắt), Phát lại (về đầu).
- [ ] PTT bằng Mouse 5: giữ → đèn MIC sáng, nhả → tắt sau ~10 ms, không có tiếng "bụp".
- [ ] Chơi liên tục > 10 phút → phím tắt vẫn chạy (watchdog cài lại hook mỗi 10 s, không làm mất phím).
- [ ] Khóa máy (Win+L) rồi mở lại → phím tắt vẫn chạy, không có modifier bị "kẹt".

## 3. Giao diện (P3)
- [ ] Đổi micro / Output A / Output B trong danh sách → áp dụng ngay, giữ sau khi khởi động lại.
- [ ] Slider âm lượng thay đổi ngay khi kéo; phím VolumeUp/VolumeDown đổi slider theo.
- [ ] Thu nhỏ → app vào khay; nhấp đúp icon khay → hiện lại. Mở app lần hai → cửa sổ cũ hiện lên trước.
- [ ] Tích "Khởi động cùng Windows", đăng xuất/đăng nhập → app chạy ẩn dưới khay.

## 4. Thư viện và Editor (P4)
- [ ] Kéo thả 3 file (wav, mp3, ogg) vào lưới → thêm 3 ô, Editor tự mở. File gốc **không đổi** (ngày sửa đổi giữ nguyên).
- [ ] Kéo marker cắt, đặt fade, Normalize → "Nghe thử" chỉ phát ra tai nghe, đồng đội không nghe.
- [ ] Lưu → bấm ô phát đúng đoạn đã cắt.
- [ ] Export WAV → mở file bằng Media Player nghe giống trên soundboard.
- [ ] Nhân bản rồi xóa bản gốc → bản sao vẫn phát được.

## 5. Cài đặt / gỡ cài (P5)
- [ ] Máy sạch (chưa có .NET) → cài và chạy được.
- [ ] Gỡ cài, chọn "No" ở câu hỏi xóa dữ liệu → `%AppData%\Soundboard` còn nguyên; cài lại → sound và phím tắt vẫn còn.
- [ ] Gỡ cài, chọn "Yes" → `%AppData%\Soundboard` bị xóa.

## 6. Tính năng thử nghiệm (P6)
- [ ] Mở settings của bản cũ (schema v1) → app chạy bình thường, lần lưu đầu ghi v2, `settings.json.bak` là bản v1.
- [ ] Ducking: phát nhạc dài, giữ PTT → đồng đội nghe nhạc nhỏ đi rõ, nhả → nhạc to lại từ từ.
- [ ] Hiệu ứng: pitch +5, reverb 40% trong Editor → nghe thử, lưu, bấm ô → nghe đúng như lúc thử; tắt cờ → phát lại bình thường.
- [ ] Voice changer +5 cung → đồng đội nghe giọng cao hơn, độ trễ chấp nhận được.
- [ ] Playlist: thêm 2 sound, ▶ → phát lần lượt, tự sang bài kế; "Dừng tất cả" không làm bài kế tự phát.
- [ ] Web remote: mở link "Mở trang" → bấm nút phát được; sai token → 401; Stream Deck gọi `POST /api/trigger` được.
- [ ] Settings v2 (bản 1.1.0) → mở bản mới, lần lưu đầu ghi v3 có `"youTubeImport": false`, `.bak` là bản v2.
- [ ] Tải từ YouTube: bật cờ → có nút "Tải từ YouTube".
  - Lần đầu: app hiện hộp hỏi. Bấm "Tải công cụ" thì thấy tiến trình tải yt-dlp rồi Deno, và `%AppData%\Soundboard\tools` có `yt-dlp.exe`, `deno.exe`, `tools.json`.
  - Dán link một bài hát → thanh tiến trình chạy, cửa sổ tự đóng, sound mới mang tên video, Editor mở ra. Bấm ô sound → đồng đội nghe được.
  - Link video dài hơn 20 phút → báo "quá giới hạn 20 phút". Link playlist hoặc trang khác → báo link không hợp lệ.
  - Bấm Hủy hoặc đóng cửa sổ giữa chừng → không còn `yt-dlp.exe`/`deno.exe` trong Task Manager, thư mục `downloads` trống.

## Nhật ký đã test

| Ngày | Phiên bản | Máy | Đã kiểm | Chưa kiểm / ghi chú |
|---|---|---|---|---|
| 2026-11-23 | Tải từ YouTube (nhánh `feat/youtube-import`) | Máy dev | Test tự động gọi GitHub và YouTube thật: tải và kiểm SHA-256 yt-dlp 2026.10.26 cùng Deno v2.9.8, tải video 19 s ra m4a giải mã được, yt-dlp không cảnh báo về JavaScript runtime, hủy giữa chừng thì tắt hẳn tiến trình và xóa thư mục tạm | Chưa bấm thử cửa sổ trong app: bản 1.1.0 đang chạy dùng chung `%AppData%\Soundboard`, chạy bản dev sẽ chuyển settings của bạn sang v3 |
| 2026-11-22 | P6 (nhánh `feat/p6-extensions`) | Máy dev, không VB-Cable | Mục 6: migration v1→v2 trên dữ liệu thật (v1 giữ ở `.bak`), bật 5 cờ trong UI, playlist tự chuyển bài, Editor pitch/reverb lưu đúng, web remote: trang 200, trigger phát, sai token 401, Host lạ bị HTTP.sys chặn (400) | Ducking/voice changer chỉ kiểm bằng unit test (chưa nghe qua VB-Cable) |
| 2026-11-22 | 1.0.0-rc.1 | Máy dev, Windows 11, **không có VB-Cable** | Test hardware tự động: engine → Realtek Digital Output (loopback), mic qua PTT gate (đóng = 0 tuyệt đối), hook F23/F24 kể cả lúc cài lại hook. UI thật: Editor (kéo marker, Normalize, nghe thử), gán phím + cảnh báo, F9 phát sound, Dừng tất cả, stream sound 20 s. Cài/gỡ im lặng per-user: giữ dữ liệu, xóa khóa Run | Mục 0, 1 (qua VB-Cable/Valorant), 2 (trong trận), 5 (máy sạch) |
