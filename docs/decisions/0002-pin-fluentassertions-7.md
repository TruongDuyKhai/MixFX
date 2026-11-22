# ADR 0002 — Ghim FluentAssertions 7.x

- **Ngày:** 2026-11-22
- **Trạng thái:** Chấp nhận

## Bối cảnh

Mục 3 chọn FluentAssertions cho test và chỉ chấp nhận package có license MIT/BSD/Apache/MS-PL. Từ bản 8.0, FluentAssertions chuyển sang license thương mại của Xceed (metadata NuGet không còn `licenseExpression`), nên bản 8+ vi phạm luật này. Dòng 7.x vẫn là Apache-2.0 (bản cuối: 7.2.2).

## Quyết định

- Ghim `FluentAssertions` ở **7.2.2** trong `Directory.Packages.props`.
- **Không** nâng lên 8.x. Công cụ cập nhật package (Dependabot, `dotnet outdated`…) phải bỏ qua package này.
- Nếu sau này cần tính năng mới hơn: cân nhắc `AwesomeAssertions` (fork Apache-2.0 của FluentAssertions) qua một ADR mới.

## Hệ quả

- 7.x không còn nhận tính năng mới, nhưng API hiện có đủ cho nhu cầu test của project.
