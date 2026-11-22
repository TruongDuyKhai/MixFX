# ADR 0004 — Web remote: chỉ localhost, có token, kiểm Host

- **Ngày:** 2026-11-22
- **Trạng thái:** Chấp nhận (tính năng thử nghiệm, sau `FeatureFlags.WebRemote`)

## Bối cảnh

Mục 11 (P6) có "web remote / Stream Deck": điều khiển soundboard từ bên ngoài app. Một HTTP server trên máy người dùng là bề mặt tấn công mới:

- Bất kỳ trang web nào đang mở trong trình duyệt cũng gửi request tới `localhost` được (CSRF).
- Kỹ thuật DNS rebinding có thể biến tên miền của kẻ tấn công thành `127.0.0.1`.
- Nghe trên mạng LAN thì cần quyền Administrator (URL ACL của HTTP.sys) và để lộ soundboard cho cả mạng.

## Quyết định

- `HttpListener` chỉ nghe ở `http://localhost:<port>/` (mặc định 8765). Không nghe trên LAN.
- Mọi request phải có token: query `token=` hoặc header `X-Soundboard-Token`. Token gồm 192 bit ngẫu nhiên (`RandomNumberGenerator`), tự tạo khi bật lần đầu, lưu trong settings, so sánh bằng `CryptographicOperations.FixedTimeEquals`. Có nút "Tạo token mới".
- Header `Host` phải là `localhost`, `127.0.0.1` hoặc `[::1]`. HTTP.sys đã chặn Host lạ (trả 400), và handler kiểm thêm lần nữa (trả 403).
- Request chỉ được kích hoạt action qua `IActionDispatcher` (mục 4.2). Action không tồn tại trả 404; không có endpoint nào khác ngoài trang nút, `/api/sounds` và `/api/trigger`.
- Tên sound được HTML-encode, màu chỉ nhận dạng `#RRGGBB`, token được nhúng vào trang qua JSON-encode.
- Mặc định tắt. Logic định tuyến (`WebRemoteRequestHandler`) tách khỏi HTTP để test không cần mạng; vẫn có thêm một test HTTP thật.

## Hệ quả

- Stream Deck (qua plugin gửi HTTP request), script hoặc trình duyệt **trên cùng máy** dùng được.
- Điện thoại hoặc máy khác trong LAN **không** dùng được. Muốn hỗ trợ thì cần ADR mới (TLS, ghép đôi thiết bị, quyền URL ACL).
- Token nằm trong `settings.json` dạng plain text: ai đọc được thư mục `%AppData%` của user cũng đã có quyền như user đó, nên chấp nhận được.
