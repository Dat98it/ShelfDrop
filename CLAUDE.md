# ShelfDrop

Một "kệ" tạm để gom file từ nhiều nơi rồi kéo cả nhóm đi một lần (lắc chuột khi đang kéo thì kệ hiện ra). Repo có hai bản độc lập, mỗi bản trong thư mục riêng; file này chỉ ghi những điều **không thấy được từ code** nhưng dễ làm sai.

| Thư mục | Nội dung | Tài liệu |
|---|---|---|
| `macos/` | Bản macOS: Swift, AppKit + SwiftUI, build bằng SwiftPM, không cần Xcode | [README.md, phần macOS](README.md#macos) |
| `windows/` | Bản Windows: C# / .NET 8 / WPF | [README.md, phần Windows](README.md#windows) |
| (gốc) | `README.md` (**tài liệu duy nhất của dự án**, có phần riêng cho từng nền tảng), `LICENSE`, `CLAUDE.md` này, `.github/` (CI của bản Windows), `.gitignore` | |

**Chỉ có một `README.md`, ở thư mục gốc**: tài liệu của cả hai bản nằm trong đó (đừng tạo lại README riêng trong `macos/` hay `windows/`; sửa phần tương ứng trong README gốc). **Mọi lệnh Swift và script của bản macOS chạy trong thư mục `macos/`** (`cd macos` trước), mọi lệnh `dotnet` của bản Windows chạy trong `windows/`. Đường dẫn trong phần macOS bên dưới tính từ `macos/`.

## Lệnh thường dùng (bản macOS, trong `macos/`)

```bash
swift build                              # biên dịch
swift test                               # chạy bộ test
Scripts/build_app.sh [release|debug] [universal]   # ghép build/ShelfDrop.app, ký ad-hoc
Scripts/make_dmg.sh                      # build universal rồi tạo build/ShelfDrop-<version>.dmg
swift Scripts/make_icon.swift            # vẽ lại icon: ghi Resources/AppIcon.icns
open build/ShelfDrop.app --args --show-shelf     # mở sẵn kệ để xem giao diện
```

## Môi trường: chỉ có Command Line Tools, không có Xcode

- **Không có XCTest**, nên test dùng **Swift Testing** (`import Testing`).
- **Không dùng `@State`** của SwiftUI: trong SDK hiện tại nó là macro mà plugin chỉ có trong Xcode, nên không build được. Dùng `@StateObject` (xem `HoverState` trong `HoverReader.swift`).
- Lần `swift test` đầu tiên sau khi sửa source hay báo `plugin for module 'TestingMacros' not found`. Đó là lỗi môi trường, **chạy lại** (đôi khi vài lần) là được.
- Test tạo và hiển thị panel thật nên cần phiên đăng nhập có giao diện. Panel có thể nhấp nháy trên màn hình lúc chạy.
- Kiểm tra ký hiệu trong file nhị phân bằng `nm`, không phải `strings`: tên Swift bị mã hóa nên `strings` không thấy.

## Viết test

- **Không dùng `UserDefaults` thật trong test.** Dùng `IsolatedDefaults` (một `UserDefaults` chạy trong bộ nhớ). Vùng cấu hình thật từng để lại hàng nghìn file `.plist` rỗng trong `~/Library/Preferences`, vì `cfprefsd` ghi lại chúng vài giây sau khi test xong, bất kể xóa thế nào.
- Các thao tác thật (Thùng rác, Login Items, hộp thoại chia sẻ) đều đi qua protocol hoặc closure để test thay bằng đối tượng giả (`LoginItemService`, `SharePresenting`, `UninstallEffects`). Test không được xóa app, cấu hình hay file nào thật.
- Tìm nút bằng `PanelProbe` (đọc từ bố cục thật) thay vì tọa độ cố định, để chỉnh giao diện không làm hỏng test.
- Với logic an toàn, nên phá thử (đảo điều kiện, bỏ một dòng) để chắc test thật sự bắt được lỗi.

## Những cái bẫy đã gặp

- **Sửa phiên bản trong `macos/Resources/Info.plist` bằng sửa văn bản tối thiểu** (hai dòng `CFBundleShortVersionString` và `CFBundleVersion`). `PlistBuddy` viết lại cả file nên diff rất ồn.
- **`NSScrollView` của SwiftUI** với thanh cuộn kiểu cũ làm lưới bị cắt cột cuối, nên danh sách đang ẩn thanh cuộn (`.scrollIndicators(.hidden)`).
- **Mục Uninstall** chỉ bật khi app là một `.app` có đúng định danh `local.shelfdrop.app`, không nằm trên ổ chỉ-đọc, và được phép xóa. Chạy `swift run` thì "bundle" là cả thư mục build; chốt này để không bao giờ chuyển nó vào Thùng rác.
- Cấu hình dùng chung cho mọi bản sao app trên máy (cùng định danh), và Launch at Login ghi nhớ **bản đang chạy**. Bản trong `build/` bị script xóa và tạo lại mỗi lần build, nên đừng bật Launch at Login từ đó; chạy bản trong `/Applications`.

## Git

- Remote `origin` là `git@github.com-dat98it:Dat98it/ShelfDrop.git` (`github.com-dat98it` là bí danh SSH trong `~/.ssh/config`), nhánh `main`.
- Danh tính commit của repo này là `Dat98it <dat98.it@gmail.com>`. **Đặt ở mức repo** (`git config --local user.name` / `user.email`) và kiểm tra `git config user.email` trước khi commit: đừng tin cấu hình git toàn cục của máy.
- **Không thêm trailer `Co-Authored-By`** vào commit.
- Commit message viết bằng tiếng Anh, theo từng thay đổi lô-gic (mã nguồn kèm test, README, tăng phiên bản tách riêng). Chuỗi giao diện bằng tiếng Anh; README và ghi chú phát hành bằng tiếng Việt.

## Quy trình phát hành (bản macOS)

Khi được yêu cầu "commit, push và tạo release vX.Y.Z kèm file .dmg":

1. Xem lại thay đổi đang chờ và `git log vTRƯỚC..`; commit theo từng phần.
2. Tăng phiên bản trong `macos/Resources/Info.plist` (tăng cả build number), chạy toàn bộ test, commit `Bump version to X.Y.Z`, push.
3. `Scripts/make_dmg.sh`. Gắn file `.dmg` lên, kiểm tra phiên bản, `codesign --verify --deep --strict`, `lipo -archs`, icon, ký hiệu bằng `nm`, và chạy thử bản app lấy ra. Cây thư mục phải sạch và không file nguồn nào mới hơn `.dmg`.
4. Viết ghi chú phát hành bằng tiếng Việt: có gì mới, cách cài, mục Gatekeeper, lưu ý, SHA-256 và link so sánh `vTRƯỚC...vX.Y.Z`. Hash trong ghi chú phải đúng bằng hash của file `.dmg`.
5. `gh release create vX.Y.Z macos/build/ShelfDrop-X.Y.Z.dmg --target "$(git rev-parse HEAD)" --latest --notes-file ...`.
6. Kiểm lại: tag trỏ đúng commit, `digest` của file đính kèm trùng file ở máy, `/releases/latest` trỏ về bản mới.

Chỉ phát hành khi được yêu cầu, và chỉ khi đã được cho biết số phiên bản.

## Gatekeeper và cài đặt

App chỉ ký ad-hoc, chưa notarize. Bản tải về mang cờ cách ly nên lần mở đầu macOS hiện hộp thoại chặn; **nút "Move to Trash" trên hộp thoại đó đã từng xóa mất bản đã cài**. Hướng dẫn người dùng bấm **Done** rồi **Open Anyway** trong System Settings → Privacy & Security, hoặc `xattr -dr com.apple.quarantine /Applications/ShelfDrop.app`. Chỉ bỏ cờ cách ly khi người dùng đồng ý rõ ràng.

## Bản Windows (`windows/`)

C# / .NET 8 / WPF, cùng repo. Xem [phần Windows trong README.md](README.md#windows) cho cách hoạt động; ở đây chỉ ghi điều dễ làm sai:

- **Máy này không có Windows.** Chỉ có thể: build mọi dự án và chạy test của `ShelfDrop.Core.Tests` (260+ test) tại chỗ, còn lại (test lớp Windows, chạy thử chương trình, trình cài đặt) chỉ chạy được trên GitHub Actions (`.github/workflows/windows.yml`, máy `windows-latest`). Nói rõ cái nào đã chạy thật trên Windows, cái nào chưa, và đừng tuyên bố một hành vi Windows "chạy đúng" chỉ vì nó biên dịch được.
- **.NET 8 SDK cài bằng Homebrew, keg-only.** Mỗi lệnh phải đặt `DOTNET_ROOT=/opt/homebrew/opt/dotnet@8/libexec` và `PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"` (hoặc dùng một script bọc nhỏ). Gỡ bằng `brew uninstall dotnet@8`.
- **Không có XAML.** SDK trên Mac không có `Microsoft.NET.Sdk.WindowsDesktop` nên `UseWPF` không build được ngoài Windows. Dự án dùng `FrameworkReference` tới WPF/WinForms và dựng toàn bộ giao diện bằng code C#, để trình biên dịch kiểm tra được ngay trên Mac. Đừng thêm file `.xaml`.
- **Mọi quyết định nằm trong `ShelfDrop.Core`** (không gọi API Windows, test được trên Mac). `ShelfDrop.App` chỉ nối đối tượng thật vào. Thêm hành vi thì viết ở Core kèm test trước.
- **Cảnh báo = lỗi** (`TreatWarningsAsErrors`). Chạy `dotnet build windows/ShelfDrop.Windows.sln -c Release` trước khi báo xong.
- **Hook chuột phải trả về nhanh**: chỉ nuôi `DragTracker`; việc nặng (mở cửa sổ) chạy sau qua `Dispatcher.BeginInvoke`, nếu không Windows sẽ gỡ hook.
- **Test cần Windows không được chạm vào máy người chạy**: registry thử dùng khóa riêng `Software\ShelfDropTests\<guid>`, test dùng chuột giả lập chỉ chạy khi `SHELFDROP_INTERACTIVE_TESTS=1` (CI đặt biến này).
- Icon Windows: chạy `swift Scripts/make_icon.swift --windows` **trong `macos/`** (ghi `windows/src/ShelfDrop.App/Assets/ShelfDrop.ico`). Script nằm ở `macos/` vì nó vẽ bằng AppKit, chỉ chạy được trên Mac.
- Phát hành bản Windows dùng tag riêng `win-vX.Y.Z` để không đụng tới các bản macOS `vX.Y.Z`. Trình cài đặt chỉ tạo được trên CI: tải artifact `ShelfDrop-Setup` của lần chạy tương ứng bằng `gh run download`, tính SHA-256, rồi `gh release create win-vX.Y.Z` (không dùng `--latest`, để `/releases/latest` vẫn trỏ về bản macOS).

## Khi chạy lệnh trên máy này

- Các bước xóa thư mục hoặc dùng biến trong `rm` hay bị công cụ chặn. Viết `"${VAR:?}"` thay vì `$VAR`, và đừng xóa thư mục đang là thư mục làm việc (đổi sang chỗ khác trước).
- `ls ~/.Trash` bị macOS chặn (`Operation not permitted`), nên không dùng nó để kiểm tra Thùng rác.
- Xác nhận trước với người dùng khi làm việc có thể xóa dữ liệu, thay bản đang chạy, hoặc bỏ qua một cơ chế bảo mật.
