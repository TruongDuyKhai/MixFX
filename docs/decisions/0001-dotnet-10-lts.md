# ADR 0001 — Chuyển từ .NET 8 sang .NET 10 LTS

- **Ngày:** 2026-11-22
- **Trạng thái:** Chấp nhận

## Bối cảnh

Mục 3 của `PROJECT_PROMPT.md` chốt C# 12 / .NET 8 (LTS). .NET 8 đã hết hỗ trợ ngày 10/11/2026, tức là trước khi project có dòng code đầu tiên. Bắt đầu một project mới trên runtime không còn bản vá bảo mật là không hợp lý, nhất là với app chạy nền cùng game và có hook bàn phím toàn hệ thống.

## Quyết định

- Dùng **.NET 10 (LTS, hỗ trợ đến 11/2028)** và **C# 14**.
- SDK ghim trong `global.json` (`10.0.404`, `rollForward: latestFeature`).
- TFM:
  - `net10.0` cho `Soundboard.Core`, `Soundboard.Storage` và test của chúng. Không dùng `-windows` để trình biên dịch chặn luôn việc vô tình dùng API chỉ có trên Windows trong hai project này (giữ đúng mục 4.1).
  - `net10.0-windows` cho `Soundboard.Audio`, `Soundboard.Input`, `Soundboard.App`.
- Solution vẫn dùng định dạng `.sln` (SDK 10 mặc định tạo `.slnx`) để khớp với cây thư mục ở mục 4.

## Hệ quả

- Có bản vá bảo mật đến 11/2028. Phải nâng lên LTS kế tiếp trước mốc đó (ghi ADR mới khi làm).
- NAudio, CommunityToolkit.Mvvm, Serilog đều chạy được trên .NET 10.
- Máy build cần .NET SDK 10. Bản publish là self-contained nên máy người dùng không cần cài .NET.
