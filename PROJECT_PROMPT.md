# PROJECT PROMPT — Soundboard cho Valorant (Windows)

> File này là **nguồn sự thật duy nhất** của project. Mọi AI agent hay lập trình viên PHẢI đọc toàn bộ file này trước khi viết hoặc sửa code.
> Đặt file ở gốc repo. Có thể đổi tên thành `CLAUDE.md` / `AGENTS.md` / `.cursorrules` tùy công cụ AI đang dùng.
> Khi file này mâu thuẫn với code hiện có, **file này thắng**. Muốn đổi luật thì phải sửa file này trước, có ghi lý do ở mục 12.

---

## 0. Vai trò của bạn (dành cho AI agent)

Bạn là kỹ sư C#/.NET cấp cao, chuyên về audio thời gian thực trên Windows. Bạn làm việc **cẩn thận, phạm vi hẹp, có kiểm chứng**. Ưu tiên của bạn theo thứ tự:
1. Không làm hỏng tính năng đang chạy.
2. Tuân thủ kiến trúc và luật trong file này.
3. Hoàn thành yêu cầu.
4. Code gọn, dễ đọc.

Nếu yêu cầu của người dùng buộc bạn phải phá một luật, **dừng lại và hỏi**, không tự ý làm.

---

## 1. Mục tiêu sản phẩm

Ứng dụng desktop Windows, **dùng cá nhân**, cho phép phát sound effect và nhạc **qua micro** khi chơi game (chủ yếu Valorant). Đồng đội nghe được cả giọng thật lẫn âm thanh phát ra.

Không phải mục tiêu (hiện tại): đa nền tảng, đa người dùng, cloud sync, bán thương mại.

---

## 2. Nguyên lý kỹ thuật cốt lõi

```
Mic thật ──► [PttGate] ──┐
                         ├─► [Mixer 48kHz float stereo] ─► [Limiter] ─► Output A: "CABLE Input" (VB-Audio Virtual Cable) ─► Valorant đọc "CABLE Output" làm mic
Sound/Nhạc ─► [Effect chain] ┘                                        └─► Output B: Tai nghe (monitor, mặc định KHÔNG có giọng mình)
```

- **Micro ảo:** dùng **VB-Audio Virtual Cable** (cài riêng). **CẤM** tự viết driver âm thanh, vì Vanguard chặn driver chưa ký.
- **Push-to-Talk:** Valorant đặt ở chế độ **Open Mic**. App tự xử lý PTT cho giọng bằng `PttGate`. **Sound luôn đi qua**, kể cả khi không giữ PTT.
- **Không bao giờ** inject vào process game, đọc hay ghi bộ nhớ game, vẽ overlay trong game, hay giả lập phím gửi vào game.

---

## 3. Tech stack (cố định — đổi phải ghi ADR, xem mục 12)

| Hạng mục | Lựa chọn |
|---|---|
| Ngôn ngữ / runtime | C# 12, **.NET 8** (LTS), `win-x64` |
| UI | **WPF** + **MVVM** (`CommunityToolkit.Mvvm`) |
| DI / Hosting | `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` |
| Audio | **NAudio** (WASAPI capture/out, mixing). `NAudio.Vorbis` cho .ogg |
| Định dạng file | wav, mp3, ogg, flac (mp3/flac/aac qua `MediaFoundationReader`) |
| Lưu trữ | JSON (`System.Text.Json`) |
| Logging | `Microsoft.Extensions.Logging` + Serilog ghi file (rolling) |
| Test | xUnit + FluentAssertions |
| Đóng gói | `dotnet publish` self-contained + **Inno Setup 6** ra file `Setup.exe` |

**Chỉ được thêm package NuGet mới khi:** có lý do rõ ràng, đã xem license (chỉ chấp nhận MIT/BSD/Apache/MS-PL), và đã ghi vào mục 12.

---

## 4. Kiến trúc & cấu trúc thư mục

```
/
├─ PROJECT_PROMPT.md          ← file này
├─ CHANGELOG.md
├─ Directory.Build.props      ← version, nullable, warnings-as-errors dùng chung
├─ Soundboard.sln
├─ src/
│  ├─ Soundboard.Core/        ← Model, interface, Action. KHÔNG tham chiếu NAudio/WPF/Win32
│  │  ├─ Models/              (SoundItem, EditParams, Binding, Trigger, AppSettings)
│  │  ├─ Actions/             (IAction, PlaySoundAction, StopAllAction, ToggleMicAction…)
│  │  ├─ Abstractions/        (IAudioEngine, IInputService, ISoundLibrary, ISettingsStore, IEffect, IActionDispatcher)
│  │  └─ Events/              (event nội bộ: SoundStarted, SoundStopped, DeviceLost…)
│  ├─ Soundboard.Audio/       ← NAudio: AudioEngine, Mixer, PttGate, Effects/, Decoding/, Waveform/
│  ├─ Soundboard.Input/       ← Win32 hook bàn phím/chuột, BindingManager
│  ├─ Soundboard.Storage/     ← JsonSettingsStore, SoundLibrary, Migrations/
│  └─ Soundboard.App/         ← WPF: Views/, ViewModels/, Services/, App.xaml (Composition Root)
├─ tests/
│  ├─ Soundboard.Core.Tests/
│  ├─ Soundboard.Audio.Tests/
│  └─ Soundboard.Storage.Tests/
├─ installer/
│  └─ Soundboard.iss          ← script Inno Setup
└─ docs/
   └─ decisions/              ← ADR (Architecture Decision Record)
```

### 4.1 Luật phụ thuộc giữa các project (BẮT BUỘC)

```
App ──► Audio, Input, Storage, Core
Audio ──► Core
Input ──► Core
Storage ──► Core
Core ──► (không gì cả ngoài BCL)
```

- `Core` **không** được tham chiếu bất kỳ project nào khác hay NAudio/WPF/Win32.
- `Audio`, `Input`, `Storage` **không** tham chiếu lẫn nhau. Chúng giao tiếp qua interface trong `Core`.
- **Chỉ** `Soundboard.App` (Composition Root) mới được `new` các implementation và đăng ký DI.
- ViewModel **không** gọi NAudio hay Win32 trực tiếp. Chỉ dùng interface từ `Core`.

### 4.2 Luồng kích hoạt thống nhất

Mọi nguồn kích hoạt (hotkey, click UI, sau này là web remote / Stream Deck / Discord bot) **phải** đi qua:

```
Nguồn kích hoạt ─► IActionDispatcher.Dispatch(actionId, targetId, TriggerPhase) ─► IAction.Execute(...)
```

Thêm tính năng mới nghĩa là **thêm class `IAction` mới**, không sửa các nguồn kích hoạt.

---

## 5. Đặc tả tính năng

### 5.1 Audio engine
- Định dạng nội bộ: **48 kHz, float32, stereo**. Mọi nguồn khác thì resample (`WdlResamplingSampleProvider`). Mic mono thì nhân đôi thành stereo.
- Có 2 output độc lập: **Output A** (cho game, qua VB-Cable) và **Output B** (monitor ở tai nghe).
- Âm lượng riêng: `VolumeToGame`, `VolumeToMonitor`, `MicVolume`, và volume từng sound.
- **Limiter** ở cuối Output A (trần −1 dBFS) để không làm đồng đội ù tai.
- Sound ngắn (≤ 15 s sau khi cắt) được **nạp sẵn vào RAM**. Sound dài thì **stream từ đĩa**.
- Mất thiết bị (rút tai nghe, VB-Cable bị gỡ) thì phát event `DeviceLost`, báo trên UI, **không crash**.
- Chế độ phát chồng: `Overlap` (cho nhiều sound cùng lúc) hoặc `Exclusive` (sound mới dừng sound cũ). Đây là cài đặt toàn cục.

### 5.2 PTT cho mic
- Phím PTT riêng, cấu hình được. Giữ phím thì giọng đi qua, nhả phím thì giọng bị chặn (có fade 10 ms để tránh tiếng "bụp").
- Có chế độ `AlwaysOn` để mic luôn mở.
- Đèn trên UI báo trạng thái đang truyền giọng.

### 5.3 Thư viện & import
- Kéo thả hoặc nút Import. App **copy** file vào `%AppData%\Soundboard\library\{guid}.{ext}`.
- **CẤM** sửa, ghi đè hay xóa file gốc người dùng chọn.
- Import xong thì tự mở Editor.

### 5.4 Editor (chỉnh sửa không phá file)
- Hiển thị **waveform** (tính peak theo bucket, cache ra `library\{guid}.peaks`).
- Cắt bằng 2 marker `TrimStartMs` / `TrimEndMs`, có nút nghe thử đoạn đã cắt.
- `FadeInMs`, `FadeOutMs`, `GainDb` (−24 … +12), nút **Normalize** (tính gain để đỉnh đạt −1 dBFS).
- Mọi chỉnh sửa lưu vào `EditParams` trong config. Effect chain được áp dụng **lúc phát**.
- Nút **Export** xuất file wav mới đã áp dụng chỉnh sửa, ghi vào thư mục người dùng chọn.

### 5.5 Phím tắt
- Dùng **low-level hook** `WH_KEYBOARD_LL` + `WH_MOUSE_LL`. **Không** dùng `RegisterHotKey`, vì nó nuốt phím của game.
- Hỗ trợ: **phím đơn** (Numpad, F13–F24, Mouse4/5…), **tổ hợp** (Ctrl/Alt/Shift/Win + phím), và **click trong app**.
- Mỗi binding có `PassThrough` (mặc định `true`: game vẫn nhận phím).
- `TriggerMode` cho từng sound: `Press`, `Toggle`, `Hold`, `Restart`.
- UI gán phím dạng "nhấn để ghi nhận". **Cảnh báo** khi trùng binding hoặc khi gán phím game phổ biến (W, A, S, D, Space, Shift, Ctrl, chuột trái/phải, 1–4, E, Q, C, X, F, G, V, B).
- Các Action có sẵn: `PlaySound`, `StopAll`, `ToggleMic`, `PttHold`, `VolumeUp`, `VolumeDown`.

### 5.6 Giao diện
- Dark theme. Thanh trên: chọn mic / Output A / Output B, đèn mic. Giữa: **lưới ô sound** (tên, màu, hotkey, thanh tiến trình khi phát). Dưới: các slider âm lượng, nút Stop all, tên sound đang phát.
- Chuột phải vào ô để có menu Sửa / Đổi phím / Xóa / Nhân bản.
- Thu nhỏ xuống **system tray**. Có tùy chọn khởi động cùng Windows.
- Màn hình **Setup lần đầu**: kiểm tra VB-Cable. Nếu chưa có thì hướng dẫn tải và cài, rồi hướng dẫn chọn mic "CABLE Output" + Open Mic trong Valorant.

### 5.7 Lưu trữ
- `%AppData%\Soundboard\settings.json` chứa toàn bộ cấu hình, có `"schemaVersion": <int>`.
- Ghi file theo kiểu **atomic**: ghi ra `.tmp` rồi `File.Replace`, có giữ `settings.json.bak`.
- Đọc lỗi hoặc JSON hỏng thì khôi phục từ `.bak` và báo người dùng. **Không bao giờ** ghi đè bằng config rỗng.
- Log ở `%AppData%\Soundboard\logs\`.

---

## 6. Model dữ liệu chuẩn

```csharp
public sealed record SoundItem(
    Guid Id, string Name, string LibraryFile, string Color,
    float Volume, PlaybackKind Kind,            // Preloaded | Streamed (tính tự động)
    EditParams Edit);

public sealed record EditParams(
    int TrimStartMs, int? TrimEndMs, int FadeInMs, int FadeOutMs,
    float GainDb, IReadOnlyList<EffectConfig> Effects); // Effects: chỗ mở rộng

public sealed record EffectConfig(string EffectId, IReadOnlyDictionary<string, string> Params);

public sealed record Trigger(InputDevice Device, int Code, Modifiers Modifiers);

public sealed record Binding(
    Guid Id, Trigger Trigger, string ActionId, Guid? TargetId,
    TriggerMode Mode, bool PassThrough);

public enum TriggerMode { Press, Toggle, Hold, Restart }
public enum TriggerPhase { Down, Up }
```

Đổi tên hay xóa thuộc tính trong các model này là **breaking change**: bắt buộc phải có migration (mục 7.4).

---

## 7. LUẬT BẮT BUỘC (vi phạm = không được merge)

### 7.1 Phạm vi thay đổi
1. **Chỉ sửa những file cần thiết cho yêu cầu hiện tại.** Không refactor, đổi tên, format lại hay "dọn dẹp" code ngoài phạm vi.
2. Trước khi sửa, **liệt kê các file sẽ đụng tới và lý do**. Nếu chạm hơn 8 file hoặc chạm vào mục 7.6 (vùng nhạy cảm) thì phải trình bày kế hoạch và chờ xác nhận.
3. Không xóa code, test hay comment mà bạn không hiểu rõ mục đích.
4. Một thay đổi chỉ làm **một việc**. Sửa bug thì không kèm thêm tính năng.

### 7.2 Hợp đồng (contract) & tương thích
5. Interface public trong `Soundboard.Core` là **hợp đồng**. Chỉ được **thêm** member mới (kèm default implementation nếu có thể). Đổi signature hay xóa member thì phải có ADR + cập nhật mọi implementation + test.
6. Không thay đổi hành vi mặc định mà người dùng đang dựa vào (âm lượng, PassThrough, TriggerMode…) nếu không được yêu cầu.
7. Tính năng mới **chưa ổn định** phải nằm sau feature flag trong `AppSettings.Features` (mặc định tắt).

### 7.3 Luồng xử lý (threading) — vùng dễ gây lỗi nhất
8. **Audio callback** (`Read()` của `ISampleProvider`): **CẤM** cấp phát bộ nhớ (`new`, LINQ, string), `lock` lâu, I/O, logging, gọi UI hay `await`. Thay đổi tham số từ thread khác bằng `Volatile`/`Interlocked` hoặc hàng đợi lock-free.
9. **Hook callback** (`WH_*_LL`): phải trả về trong **< 1 ms**. Chỉ đẩy sự kiện vào `Channel<T>` rồi xử lý ở thread khác. Windows sẽ tự gỡ hook nếu callback chậm (LowLevelHooksTimeout). Luôn gọi `CallNextHookEx` khi `PassThrough = true`.
10. Cập nhật UI chỉ qua `Dispatcher` / `SynchronizationContext` của WPF.
11. Mọi tài nguyên audio/hook phải `Dispose` đúng cách. Thoát app phải gỡ hook và dừng WASAPI sạch sẽ.

### 7.4 Dữ liệu người dùng
12. **Không bao giờ** sửa hay xóa file gốc người dùng import.
13. Đổi cấu trúc `settings.json` thì phải tăng `schemaVersion` + viết migration trong `Storage/Migrations/` (`MigrateVnToVn+1`) + test migration với file JSON mẫu của phiên bản cũ (lưu ở `tests/.../Fixtures/`).
14. Lỗi khi đọc config **không được** làm mất dữ liệu (xem 5.7).

### 7.5 Chất lượng code
15. `Nullable` bật, `TreatWarningsAsErrors` bật (trong `Directory.Build.props`). Không dùng `#pragma warning disable` nếu không ghi lý do.
16. Không `catch (Exception)` rồi nuốt lỗi. Phải log, rồi xử lý hoặc ném tiếp.
17. Không hard-code đường dẫn, tên thiết bị hay số magic. Dùng hằng số có tên hoặc settings.
18. Đặt tên: `PascalCase` cho type/member, `_camelCase` cho field private, `I` đầu tên interface, hậu tố `Async` cho method async. Mỗi file một type public.
19. Comment giải thích **tại sao**, không giải thích **làm gì**.

### 7.6 Vùng nhạy cảm (phải trình bày kế hoạch trước khi sửa)
- `Soundboard.Audio/AudioEngine*`, `Mixer*`, `PttGate*`
- `Soundboard.Input/Hooks/*`
- `Soundboard.Storage/JsonSettingsStore*`, `Migrations/*`
- `Soundboard.Core/Abstractions/*`, `Models/*`
- `installer/*`, `Directory.Build.props`

### 7.7 Kiểm thử
20. Logic mới trong Core / Audio (không phụ thuộc thiết bị) / Storage **phải có unit test**.
21. Sửa bug thì **viết test tái hiện bug trước**, sau đó mới sửa.
22. Effect audio được test bằng cách so sánh mẫu: chạy effect trên tín hiệu sinh sẵn (sine, im lặng, xung) rồi kiểm tra kết quả số.
23. Không được xóa hay vô hiệu test đang có để "cho build qua".

---

## 8. Quy trình làm việc mỗi lần nhận yêu cầu

1. **Đọc** file này và các file liên quan.
2. **Tóm tắt** lại yêu cầu bằng 1–3 câu. Nếu mơ hồ thì hỏi.
3. **Lập kế hoạch**: các file sẽ sửa hoặc tạo, interface có bị ảnh hưởng không, có cần migration không, test nào sẽ viết.
4. **Thực hiện** đúng phạm vi.
5. **Kiểm tra**: `dotnet build -warnaserror` và `dotnet test` đều phải pass.
6. **Cập nhật** `CHANGELOG.md` (mục `Unreleased`), file này (nếu thêm tính năng hay luật), và ADR (nếu đổi kiến trúc).
7. **Báo cáo**: đã thay đổi gì, file nào, cách test thủ công, rủi ro còn lại.

### Definition of Done
- [ ] Build không warning, test pass.
- [ ] Không vi phạm mục 4.1 và mục 7.
- [ ] Có test cho logic mới / bug đã sửa.
- [ ] CHANGELOG đã cập nhật.
- [ ] Đã test thủ công trên thiết bị thật nếu chạm vào Audio/Input (ghi lại các bước đã test).

---

## 9. Git & phiên bản

- **SemVer** `MAJOR.MINOR.PATCH`, khai báo **một chỗ duy nhất** trong `Directory.Build.props` (`<Version>`). Installer đọc version từ đây.
- Commit theo **Conventional Commits**: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `build:`, `chore:`.
- Nhánh: `main` luôn build được. Làm việc trên `feat/...`, `fix/...`.
- Mỗi bản phát hành phải gắn tag `vX.Y.Z`.

---

## 10. Đóng gói thành file cài đặt (.exe)

### 10.1 Build
```powershell
dotnet publish src/Soundboard.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/publish
```
Bản self-contained nên máy cài **không cần** có sẵn .NET.

### 10.2 Installer (Inno Setup — `installer/Soundboard.iss`)
- Kết quả là `artifacts/Soundboard-Setup-{version}.exe`.
- Cài vào `{autopf}\Soundboard`. Tạo shortcut Start Menu, tùy chọn shortcut Desktop, tùy chọn "Khởi động cùng Windows".
- Kiểm tra VB-Cable. Nếu chưa có thì hiện thông báo kèm link trang chính thức của VB-Audio. **Không** đóng gói VB-Cable vào installer (vấn đề bản quyền phân phối lại).
- Gỡ cài đặt **giữ lại** `%AppData%\Soundboard` (thư viện sound + config). Chỉ xóa khi người dùng tick "Xóa cả dữ liệu".
- Có script `build-installer.ps1` chạy publish rồi gọi `ISCC.exe`, chỉ bằng một lệnh.
- (Tùy chọn sau này) Ký số file exe để giảm cảnh báo SmartScreen.

---

## 11. Lộ trình phát triển

| Giai đoạn | Nội dung | Tiêu chí hoàn thành |
|---|---|---|
| **P0 — Khung** | Solution, các project, DI, model, `ActionDispatcher`, `JsonSettingsStore`, logging, `Directory.Build.props` | Build + test pass. App mở được cửa sổ trống, lưu và đọc được settings |
| **P1 — Audio** | Thu mic, mixer, Output A/B, PttGate, limiter, phát 1 file | Đồng đội trên Discord/Valorant nghe được giọng + sound qua VB-Cable |
| **P2 — Input** | Hook bàn phím/chuột, binding, 4 TriggerMode, PassThrough | Phím đơn và tổ hợp đều hoạt động khi đang ở trong Valorant, game không mất phím |
| **P3 — UI chính** | Lưới sound, chọn thiết bị, slider, tray, setup lần đầu | Dùng app hoàn toàn không cần sửa file config bằng tay |
| **P4 — Editor** | Import + copy vào thư viện, waveform, trim, fade, gain, normalize, export | Chỉnh sửa áp dụng đúng lúc phát, file gốc không bị đổi |
| **P5 — Đóng gói** | Script publish, Inno Setup, v1.0.0 | Cài trên máy sạch chạy được, gỡ cài giữ dữ liệu |
| **P6+ — Mở rộng** | Ducking, playlist, pitch/reverb, voice changer, web remote / Stream Deck | Mỗi tính năng có feature flag + test |

**Chỉ làm giai đoạn kế tiếp khi giai đoạn trước đạt tiêu chí.**

---

## 12. Nhật ký quyết định (cập nhật khi đổi luật / stack)

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-11-22 | C# .NET 8 + WPF + NAudio | Hỗ trợ WASAPI tốt nhất, làm app Windows nhanh nhất, RAM ~80–200 MB |
| 2026-11-22 | Dùng VB-Cable, không tự viết driver | Vanguard chặn driver chưa ký; tránh phải phát triển kernel driver |
| 2026-11-22 | Low-level hook thay vì RegisterHotKey | Không nuốt phím của game, hỗ trợ Hold/PTT |
| 2026-11-22 | Chỉnh sửa không phá file (EditParams) | Sửa lại được nhiều lần, không mất chất lượng, an toàn cho file người dùng |
| 2026-11-22 | Inno Setup cho installer | Đơn giản, miễn phí, ra một file Setup.exe |
