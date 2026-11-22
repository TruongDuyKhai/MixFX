# ADR 0003 — Phát hành qua tag và GitHub Actions

- **Ngày:** 2026-11-22
- **Trạng thái:** Chấp nhận

## Bối cảnh

Mục 9–10 yêu cầu: version chỉ khai báo trong `Directory.Build.props`, mỗi bản phát hành có tag `vX.Y.Z`, installer được build bằng một lệnh. Repo là public, người dùng cần một chỗ cố định để tải bản cài (trang Releases), và bản cài phải khớp đúng mã nguồn đã gắn tag.

## Quyết định

- `release.ps1 <version> [-Push]` là cách **duy nhất** để tạo bản phát hành. Script làm các bước sau:
  - Kiểm SemVer, working tree sạch, đang ở `main` trùng `origin/main`, tag chưa tồn tại.
  - Ghi version vào `Directory.Build.props`.
  - Chuyển mục `## [Unreleased]` của CHANGELOG thành `## [X.Y.Z] - <ngày>`.
  - Build (warnings as errors) và test.
  - Commit `chore(release): vX.Y.Z` và tạo annotated tag.
- Push tag sẽ chạy workflow **Release** trên `windows-latest`. Workflow kiểm tag khớp `<Version>`, build, test, cài Inno Setup, chạy `build-installer.ps1`, rồi tạo GitHub Release kèm `Soundboard-Setup-X.Y.Z.exe`, file `.sha256` và release notes lấy từ CHANGELOG (`scripts/Get-ReleaseNotes.ps1`).
- Version có hậu tố (`-rc.1`, `-beta.2`) được đăng dạng **pre-release**.
- Workflow **CI** build và test mọi lần push lên `main` và mọi PR. Test cần phần cứng là `[Fact(Explicit = true)]` nên được bỏ qua trên CI.

## Hệ quả

- Bản cài luôn được build từ đúng commit đã tag, trên máy sạch của GitHub, kèm checksum.
- Release notes chỉ viết một chỗ (CHANGELOG).
- Push tag là thao tác công khai: chỉ làm khi người phát hành đồng ý.
- Chưa ký số file exe (mục 10.2, tùy chọn): SmartScreen có thể cảnh báo cho tới khi có chứng chỉ.
