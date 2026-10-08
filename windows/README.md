# ShelfDrop cho Windows

Bản Windows của ShelfDrop: một "kệ" tạm để gom file, ảnh, link và đoạn văn từ nhiều nơi rồi kéo cả nhóm đi một lần. **Lắc chuột khi đang kéo** thì kệ hiện ra ngay cạnh con trỏ. Cùng ý tưởng và cùng bộ tính năng với [bản macOS](../README.md), viết bằng C# / .NET 8 / WPF.

> **Trạng thái kiểm chứng.** Máy phát triển là Mac, không có Windows thật. Phần logic dùng chung được kiểm bằng test ngay trên Mac; phần dành riêng cho Windows (cửa sổ, hook chuột, kéo thả, chia sẻ, khay hệ thống, trình cài đặt) chỉ được build, test và chạy thử bằng một máy Windows tự động của GitHub Actions. Mục [Đã kiểm chứng đến đâu](#đã-kiểm-chứng-đến-đâu) ghi rõ cái nào đã chạy thật, cái nào chưa.

## Tính năng

- **Lắc chuột khi kéo để mở kệ.** Một hook chuột toàn hệ thống (`WH_MOUSE_LL`) nhận ra cú lắc ngang khi đang giữ nút trái. Kệ nổi trên mọi cửa sổ nhưng không giành focus nên cú kéo đang diễn ra không bị gián đoạn, và không hiện trên taskbar hay Alt+Tab.
- **Gom mọi thứ:** file và thư mục (giữ nguyên chỗ cũ, không sao chép), ảnh kéo từ trình duyệt, tệp đính kèm mail (file "ảo" do chương trình khác đưa), link web, đoạn văn.
- **Kéo ra** một item hoặc cả nhóm bằng nút **Drag all**. Chỉ *sao chép*, không bao giờ di chuyển file gốc. Link và văn bản lẫn trong nhóm được ghi thành file nhỏ `.url` / `.txt` để "Drag all" mang đủ mọi thứ.
- **Chia sẻ** từng item hoặc cả kệ qua hộp chia sẻ của Windows. Nếu không mở được, các item được chép vào clipboard và có thông báo.
- **Khay hệ thống:** nhấp để hiện/ẩn kệ; chuột phải ra menu *Show/Hide Shelf, Clear Shelf, Launch at Login, Uninstall ShelfDrop…, Quit*.
- **Di chuyển và đổi kích thước kệ** (kéo nền kệ, kéo góc dưới phải). Kích thước và vị trí được nhớ, kể cả khi màn hình thay đổi.
- **Giao diện sáng/tối** theo Windows, thumbnail thật của Explorer cho ảnh/video/tài liệu, nhận diện file đã bị xóa hoặc di chuyển.
- **Launch at Login** qua khóa Run của người dùng (hiện trong Settings → Apps → Startup). Nếu người dùng đã tắt nó ở đó, menu hiện dấu gạch và mở trang cài đặt.
- **Uninstall** ngay từ menu: tắt Launch at Login, chạy trình gỡ cài đặt, xóa cài đặt và file tạm. File của bạn không bị đụng tới.

## Cài đặt

Tải `ShelfDrop-Setup-<phiên bản>.exe` ở trang [Releases](https://github.com/Dat98it/ShelfDrop/releases) (các bản Windows có tag `win-v…`) và chạy. Không cần quyền quản trị: chương trình cài vào `%LocalAppData%\Programs\ShelfDrop` và xuất hiện trong Settings → Apps, nơi gỡ được như mọi ứng dụng khác.

**Lần chạy đầu có thể bị SmartScreen chặn** ("Windows protected your PC") vì file chưa được ký số. Chọn **More info → Run anyway**. Xem [giới hạn](#giới-hạn-đã-biết).

## Cách hoạt động

| Việc | Cách làm |
|---|---|
| Biết người dùng đang kéo | Windows không có "bảng kéo thả" toàn hệ thống để đọc như `NSPasteboard(name: .drag)` của macOS. Nên: nút trái xuống + con trỏ đi quá ngưỡng kéo = đang kéo. Cú nhấn bắt đầu *bên trong chính kệ* (di chuyển kệ, kéo item ra) bị bỏ qua. |
| Nhận ra cú lắc | `ShakeDetector`: ≥ 3 lần đổi chiều trục X trong 0,8 giây, mỗi nét dài ≥ 40 px (nhân theo DPI). Giống hệt bản macOS. |
| Kệ nổi mà không giành focus | `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW` + `Topmost`, `WM_MOUSEACTIVATE → MA_NOACTIVATE`. |
| Nhận file thả vào | Đích thả OLE của WPF. Đọc `CF_HDROP`, `FileGroupDescriptorW` + `FileContents` (file ảo), `PNG`/bitmap, `UniformResourceLocatorW`, văn bản Unicode. |
| Kéo ra | `DragDrop.DoDragDrop` với `DataObject` chứa danh sách file / link / văn bản. |
| Nhớ vị trí | Chỉ lưu khi người dùng *thả* cửa sổ (`WM_EXITSIZEMOVE`), nên cú đặt vị trí của chính chương trình không bị tính nhầm. Lưu ở dạng pixel vật lý trên màn hình ảo. |
| Lưu cài đặt | `%AppData%\ShelfDrop\settings.json`, ghi qua file tạm rồi đổi tên. File hỏng hay thiếu thì coi như chưa lưu gì. |
| Nhật ký | `%LocalAppData%\ShelfDrop\shelfdrop.log` (chỉ ghi sự kiện, không ghi nội dung kệ). Gửi file này khi báo lỗi. |

## Cấu trúc

```
windows/
  ShelfDrop.Windows.sln
  src/ShelfDrop.Core/       C# thuần, không gọi API Windows: chạy và test được trên mọi hệ điều hành
  src/ShelfDrop.App/        WPF + Win32: cửa sổ, hook chuột, kéo thả, chia sẻ, khay, registry
  tests/ShelfDrop.Core.Tests/   test của lõi (chạy được trên Mac)
  tests/ShelfDrop.App.Tests/    test cần Windows (mở cửa sổ thật, registry, chuột giả lập)
  installer/ShelfDrop.iss   trình cài đặt Inno Setup
  scripts/                  chạy thử chương trình đã build như người dùng thật, kiểm tra trình cài đặt
```

Mọi quyết định (khi nào là cú lắc, đặt kệ ở đâu, xóa gì khi đóng, gỡ cài đặt an toàn thế nào…) nằm trong `ShelfDrop.Core` và được test bằng đối tượng giả (`IShelfWindow`, `IScreenProvider`, `ILoginItemService`…). `ShelfDrop.App` chỉ nối các đối tượng thật vào.

Giao diện được dựng hoàn toàn bằng code C# (không có file XAML). Lý do thực tế: bộ công cụ XAML chỉ có trên Windows, còn viết bằng code thì trình biên dịch kiểm tra được mọi control và thuộc tính ngay trên Mac.

## Build và test

Cần [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd windows
dotnet build ShelfDrop.Windows.sln -c Release
dotnet test tests/ShelfDrop.Core.Tests          # chạy được trên Windows, macOS và Linux
dotnet test tests/ShelfDrop.App.Tests           # chỉ chạy được trên Windows
```

Trên macOS/Linux, `dotnet build` vẫn biên dịch được cả phần WPF (nhờ `EnableWindowsTargeting`) nhưng không chạy được.

Chạy với các cờ dùng cho ảnh chụp và kiểm tra: `ShelfDrop.exe --show-shelf` (mở kệ ngay), `--demo` (kèm vài item mẫu), `--screenshot <file.png>` (vẽ kệ ra ảnh rồi thoát).

Đóng gói một file `.exe` tự chứa (không cần cài .NET) rồi làm trình cài đặt:

```powershell
dotnet publish src/ShelfDrop.App -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
iscc /DAppVersion=0.1.0 /DSourceDir=..\publish installer\ShelfDrop.iss    # cần Inno Setup 6
```

## Tự động kiểm tra (GitHub Actions)

Mỗi lần đẩy mã có động tới `windows/`, [workflow `Windows`](../.github/workflows/windows.yml) chạy trên máy Windows của GitHub:

1. build cả solution (cảnh báo = lỗi);
2. chạy test của lõi và test của lớp Windows (chụp ảnh giao diện sáng/tối, trạng thái trống, trạng thái đang kéo vào, cỡ nhỏ nhất/rộng nhất → artifact `windows-results`);
3. đóng gói `ShelfDrop.exe` tự chứa;
4. **chạy thử bằng chuột giả lập** ([`scripts/smoke-test.ps1`](scripts/smoke-test.ps1)): lắc khi kéo để mở kệ, kệ trống tự đóng, kéo kệ rồi kiểm tra vị trí được lưu, đổi cỡ bằng góc kéo, mở lại đúng chỗ cũ, thả một file từ chương trình khác vào kệ, kéo item ra lại chương trình đó;
5. dựng trình cài đặt và **kiểm tra nó** ([`scripts/verify-installer.ps1`](scripts/verify-installer.ps1)): cài yên lặng, có mục Start menu và Settings → Apps, chạy bản đã cài, gỡ, không còn gì sót (file, cài đặt, mục khởi động cùng Windows).

## Đã kiểm chứng đến đâu

Cập nhật khi có kết quả từ máy Windows. **Đến nay:**

- ✅ Lõi C# (260 test) chạy xanh trên macOS; các điều kiện an toàn quan trọng đã được phá thử để chắc test bắt được lỗi.
- ✅ Toàn bộ mã (kể cả WPF, hook, WinRT share) biên dịch sạch, 0 cảnh báo.
- ✅ Cú pháp workflow và script PowerShell hợp lệ.
- ⏳ Mọi thứ chạy trên Windows thật (test lớp Windows, chạy thử bằng chuột giả lập, trình cài đặt): đang chờ kết quả đầu tiên từ GitHub Actions.

## Giới hạn đã biết

- **Chưa ký số**: SmartScreen sẽ cảnh báo ở lần chạy đầu. Ký số cần chứng chỉ trả phí.
- **Lắc khi đang giữ chuột ở bất cứ đâu đều tính là "kéo"** (không phân biệt kéo file với vẽ hay bôi đen chữ). Hậu quả nhẹ: kệ hiện ra và tự ẩn lại khi nhả chuột nếu trống. Hook chuột của chương trình thường **không thấy** thao tác trong cửa sổ chạy với quyền quản trị (cơ chế UIPI của Windows), nên lắc từ cửa sổ đó sẽ không mở kệ.
- **Không có ảnh mờ "đi theo con trỏ"** khi kéo item ra, chỉ có biểu tượng sao chép.
- **Kệ không hiện trên màn hình đang chạy ứng dụng toàn màn hình độc quyền** (một số game).
- Chỉ Windows 10 (1809) trở lên, 64-bit.
- Item trên kệ không được giữ lại khi thoát chương trình (giống bản macOS).
