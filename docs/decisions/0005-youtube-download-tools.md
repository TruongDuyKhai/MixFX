# ADR 0005 — Tải nhạc từ YouTube: app tự tải yt-dlp và Deno lúc chạy

- **Ngày:** 2026-11-23
- **Trạng thái:** Chấp nhận (tính năng thử nghiệm, sau `FeatureFlags.YouTubeImport`)

## Bối cảnh

Người dùng muốn thêm sound bằng cách dán link YouTube. Công cụ phổ biến và được bảo trì cho việc này là **yt-dlp** (Unlicense). Từ bản 2025.11.12, yt-dlp cần thêm một **JavaScript runtime** để giải các thử thách của YouTube; dự án khuyên dùng **Deno** (MIT).

YouTube thay đổi thường xuyên và hay làm các bản yt-dlp cũ hỏng, nên yt-dlp phải được cập nhật liên tục.

Có ba cách đưa công cụ lên máy người dùng:

1. **Đóng gói kèm installer:** bản cố định sẽ nhanh hỏng. Installer nặng thêm khoảng 70 MB. Phải kèm giấy phép của Python và các thư viện mà yt-dlp.exe nhúng bên trong.
2. **Người dùng tự cài** (`winget`): an toàn và ít code nhất, nhưng người dùng phải tự làm thêm bước, và app phụ thuộc vào thứ có sẵn trong PATH.
3. **App tự tải lúc chạy:** tiện cho người dùng, nhưng app sẽ tải và chạy file exe lấy từ Internet. Đây là phương án **người dùng đã chọn**.

## Quyết định

- Chỉ tải công cụ khi người dùng **đã bật cờ** và **bấm "Tải công cụ"** trong cửa sổ tải. Cửa sổ ghi rõ nguồn tải, dung lượng và nơi lưu.
- **Nguồn tải** là bản phát hành chính thức trên GitHub:
  - `yt-dlp/yt-dlp`: file `yt-dlp.exe`.
  - `denoland/deno`: file `deno-x86_64-pc-windows-msvc.zip`; chỉ giải nén entry `deno.exe` ở gốc, mọi entry khác bị bỏ qua.
  - Tag mới nhất được đọc từ chỗ `/releases/latest` chuyển hướng tới, nên không dùng REST API vốn có giới hạn số lần gọi. App chỉ chấp nhận khi đích chuyển hướng là `github.com/<repo>/releases/tag/<tag>`, và tag chỉ được chứa `[A-Za-z0-9._-]`.
- **Kiểm SHA-256:**
  - Mỗi file được băm trong lúc tải, rồi so với mã SHA-256 công bố trong cùng bản phát hành (`SHA2-256SUMS` của yt-dlp, `*.sha256sum` của Deno).
  - Không khớp thì file bị xóa và không bao giờ được chạy. Bản đang dùng (nếu có) được giữ nguyên.
  - Kích thước file có giới hạn trên.
- **Ghi nhận bản đã kiểm:**
  - `tools\tools.json` ghi các bản đã cài và đã kiểm. File công cụ không có trong manifest thì không được chạy, và sẽ được tải lại.
  - Mỗi ngày, lần đầu mở cửa sổ tải, app kiểm tra bản mới. Nếu lần kiểm tra này lỗi, app vẫn dùng bản cũ.
- **Nơi lưu:** công cụ, cache của yt-dlp (`--cache-dir`) và cache của Deno (`DENO_DIR`) đều nằm trong `%AppData%\Soundboard\tools`. Gỡ cài kèm "Xóa cả dữ liệu" sẽ xóa luôn.
- **Cách chạy yt-dlp:**
  - Danh sách tham số cố định, truyền từng phần riêng (không qua shell). URL nằm sau `--`.
  - Dùng `--ignore-config` và `--no-plugin-dirs`, nên file cấu hình hay plugin bên ngoài không đổi được thứ sẽ chạy.
  - `--no-js-runtimes --js-runtimes deno:<đường dẫn Deno của app>`: chỉ dùng Deno của app.
  - Chỉ tải `bestaudio[ext=m4a]`: AAC giải mã được bằng Media Foundation như mọi file `.m4a` khác, nên **không cần ffmpeg** (LGPL/GPL, ngoài danh sách license của mục 3).
  - Chỉ nhận link một video YouTube (`YouTubeUrl`), không tải playlist, video tối đa 20 phút, không tải livestream.
- **Sau khi tải:**
  - File nằm trong một thư mục riêng dưới `%AppData%\Soundboard\downloads`. Từ đó nó được **import như mọi file khác**: copy vào `library`, rồi mở Editor (mục 5.3).
  - Sau khi import, thư mục tải bị xóa. Khi hủy hoặc lỗi, cả cây tiến trình (yt-dlp và Deno) bị tắt rồi thư mục cũng bị xóa.
  - `Delete` chỉ xóa thư mục con trực tiếp của `downloads`.
- Không thêm package NuGet nào: HTTP, SHA-256 và zip đều có sẵn trong BCL.

## Hệ quả

- Lần đầu dùng phải tải khoảng 70 MB và có mạng. Sau đó mỗi ngày app thêm 2 request `HEAD` tới GitHub khi mở cửa sổ tải.
- **Mức tin cậy:** checksum lấy từ cùng bản phát hành nên chống được file tải hỏng hoặc bị cắt, và HTTPS chống bị chặn giữa đường. Nhưng nếu chính bản phát hành trên GitHub bị kẻ xấu thay, app **không** phát hiện được. Muốn mạnh hơn thì phải kiểm chữ ký PGP `SHA2-256SUMS.sig` của yt-dlp, cần thêm thư viện, nên để cho ADR sau.
- yt-dlp và Deno **không** được phân phối kèm Soundboard, nên `THIRD-PARTY-NOTICES.md` chỉ ghi chú nguồn và license của chúng.
- **Pháp lý:** điều khoản của YouTube không cho phép tải nội dung, trừ khi YouTube tự cung cấp nút tải. Tính năng mặc định tắt, chỉ dành cho dùng cá nhân, và cửa sổ tải nhắc người dùng chỉ tải nội dung mình có quyền dùng.
- Đổi nguồn tải, bỏ kiểm checksum, hoặc mở rộng ra ngoài YouTube đều cần ADR mới.
