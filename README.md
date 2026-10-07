# ShelfDrop

Ứng dụng menu bar cho macOS: một "kệ" tạm thời để gom file từ nhiều nơi rồi kéo cả nhóm đi một lần. Lấy cảm hứng từ Dropover, viết bằng Swift (AppKit + SwiftUI), không cần Xcode để build.

Khi đang kéo một file, **lắc chuột ngang** và kệ nổi lên ngay dưới con trỏ. Thả file vào kệ, chuyển sang app hoặc thư mục khác, kéo thêm file nữa, rồi kéo cả nhóm ra chỗ đích.

## Tính năng

- **Lắc để mở kệ** khi đang kéo file, ảnh, link hoặc văn bản. Kệ không cướp focus của app bạn đang dùng, và được cấu hình để nổi trên mọi Space và cả app fullscreen.
- **Gom nhiều loại dữ liệu:** file, ảnh kéo từ trình duyệt, link, văn bản, và file "hứa" (file promise) từ Mail, Photos...
- **Kéo ra ngoài** từng item hoặc cả nhóm bằng nút **Drag all**. Chỉ thao tác copy, không bao giờ di chuyển file gốc của bạn.
- **Dọn dẹp:** nút `x` trên từng tile gỡ một item, **Clear** gỡ tất cả, và nút `x` của kệ đóng kệ, xóa toàn bộ item cùng các bản sao tạm app đã tạo.
- **Di chuyển và đổi cỡ:** kéo vào chỗ trống của kệ để di chuyển, kéo tay nắm ở góc dưới phải để phóng to hoặc thu nhỏ (tối thiểu 260×180, tối đa bằng vùng hiển thị của màn hình).
- **Nhớ kích thước và vị trí** kệ, kể cả sau khi thoát app.
- **Tự ẩn** nếu kệ còn trống khi cú kéo kết thúc ở chỗ khác.
- Chỉ có icon trên menu bar (Show/Hide Shelf, Clear Shelf, Quit), không có icon Dock.

## Yêu cầu

- macOS 14 trở lên (theo [Package.swift](Package.swift)).
- Swift 5.9 trở lên. **Command Line Tools là đủ, không cần Xcode.**

> Mới được thử trên macOS 27.0 với Swift 6.4. Chưa thử trên macOS 14 và 15.

## Build và chạy

```bash
Scripts/build_app.sh          # build release, ghép build/ShelfDrop.app, ký ad-hoc
open build/ShelfDrop.app
```

Dùng `Scripts/build_app.sh debug` để build bản debug. Muốn cài vào máy:

```bash
cp -R build/ShelfDrop.app /Applications/
```

App chưa có chức năng tự chạy khi đăng nhập. Nếu cần, thêm thủ công trong **System Settings → General → Login Items**.

## Đóng gói thành file .dmg

```bash
Scripts/make_dmg.sh           # tạo build/ShelfDrop-<version>.dmg
```

Script build bản **universal** (chạy được cả Mac Apple Silicon lẫn Intel) rồi tạo file `.dmg` gồm app và shortcut `Applications`. Người nhận mở file, kéo `ShelfDrop` vào `Applications` là xong. File được tạo trong `build/` nên không bị đưa vào git. Số phiên bản lấy từ `CFBundleShortVersionString` trong [Resources/Info.plist](Resources/Info.plist).

### Cài trên máy khác

App chỉ được **ký ad-hoc**, chưa ký Developer ID và chưa notarize, nên **Gatekeeper sẽ chặn** lần mở đầu tiên trên máy khác (kiểm tra bằng `spctl` cho kết quả `rejected`). Bản `.dmg` hoạt động bình thường, chỉ là người dùng phải cho phép thủ công một lần:

- **macOS 15 trở lên:** mở app một lần (sẽ bị chặn), rồi vào **System Settings → Privacy & Security**, kéo xuống và bấm **Open Anyway**.
- **macOS 14:** chuột phải vào app, chọn **Open**.
- **Hoặc dùng terminal**, sau khi đã kéo app vào `Applications`:

  ```bash
  xattr -dr com.apple.quarantine /Applications/ShelfDrop.app
  ```

Muốn phát hành cho người khác mà không có cảnh báo này, cần ký bằng chứng chỉ **Developer ID** và **notarize** (yêu cầu tài khoản Apple Developer Program trả phí).

## Cách dùng

1. Bắt đầu kéo một file (từ Finder, trình duyệt, Mail...).
2. Giữ chuột và lắc nhanh sang trái phải. Kệ hiện ra.
3. Thả file vào kệ. Lặp lại với các file khác, ở bất kỳ app nào.
4. Kéo một tile ra ngoài để lấy một item, hoặc kéo **Drag all** để lấy tất cả.
5. Bấm `x` ở góc kệ khi xong.

Lần đầu kệ hiện giữa con trỏ. Nếu nó che file bạn cần, hãy kéo nó sang chỗ khác: từ lần sau kệ luôn hiện đúng chỗ đó.

### Xử lý sự cố

- **Lắc chuột mà kệ không hiện:** app theo dõi chuột trên toàn hệ thống. Nếu macOS đã hỏi quyền hoặc chặn, mở **System Settings → Privacy & Security**, cho phép ShelfDrop trong **Accessibility** hoặc **Input Monitoring**, rồi mở lại app.
- **Xem log:**

  ```bash
  /usr/bin/log stream --predicate 'process == "ShelfDrop" AND eventMessage CONTAINS "[ShelfDrop]"'
  ```

  Log ghi các sự kiện `drag began`, `shake detected` và `drag ended`.
- **Mở sẵn kệ để kiểm tra giao diện:** `open build/ShelfDrop.app --args --show-shelf`.

## Dữ liệu và cấu hình

| Thứ | Lưu ở đâu |
|---|---|
| Kích thước, vị trí kệ | `UserDefaults` của `local.shelfdrop.app`: `shelfWidth`, `shelfHeight`, `shelfTopLeftX`, `shelfTopLeftY` |
| Bản sao tạm (ảnh từ trình duyệt, file promise) | `$TMPDIR/ShelfDrop/`. Được xóa khi mở app và khi bấm `x` của kệ |
| Item trên kệ | Chỉ trong bộ nhớ, **không lưu khi thoát app** |

File kéo từ Finder được giữ dưới dạng **tham chiếu**, không sao chép. Nếu file gốc bị đổi tên hoặc xóa sau khi đã thả vào kệ, tile sẽ mờ đi và ghi `(missing)`.

Đặt lại về mặc định:

```bash
# Kích thước
defaults delete local.shelfdrop.app shelfWidth; defaults delete local.shelfdrop.app shelfHeight
# Vị trí (kệ quay về hiện giữa con trỏ)
defaults delete local.shelfdrop.app shelfTopLeftX; defaults delete local.shelfdrop.app shelfTopLeftY
```

## Phát triển

```bash
swift build        # biên dịch
swift test         # chạy bộ test
```

Test dùng **Swift Testing** vì Command Line Tools không có XCTest. Lưu ý khi chạy test:

- Test tạo và hiển thị panel thật, nên cần phiên đăng nhập có giao diện (không chạy được trên máy không có display). Panel có thể nhấp nháy trên màn hình trong lúc chạy.
- Lần `swift test` đầu tiên sau khi sửa source đôi khi báo `plugin for module 'TestingMacros' not found`. Đây là lỗi của môi trường, chạy lại là được.
- Test không đụng vào cấu hình thật: mỗi test dùng một vùng `UserDefaults` riêng và tự xóa khi xong.

### Cấu trúc

```
Package.swift
Resources/Info.plist            LSUIElement = true (app chỉ có menu bar)
Scripts/build_app.sh            build + ghép .app + ký ad-hoc (thêm đối số `universal` cho cả Intel)
Scripts/make_dmg.sh             build universal + đóng gói .dmg
Sources/ShelfDrop/
  main.swift, AppDelegate.swift       khởi động, status item, flush dữ liệu khi thoát
  DragMonitor.swift                   phát hiện drag toàn hệ thống + lắc chuột
  ShakeDetector.swift                 logic lắc thuần, dễ test
  ShelfController.swift               nối monitor, panel, model; đặt vị trí, kích thước
  ShelfPanel.swift                    NSPanel nổi, không kích hoạt app
  ShelfView.swift                     giao diện SwiftUI của kệ
  ShelfModel.swift, ShelfItem.swift   dữ liệu item, thumbnail, thư mục tạm
  PasteboardImporter.swift            đọc dữ liệu kéo vào (file, promise, ảnh, link, text)
  DropContainerView.swift             nhận drop cho cả panel
  DragSourceView.swift                bắt đầu kéo ra (1 item hoặc cả nhóm)
  WindowDragArea.swift                kéo chỗ trống để di chuyển kệ
  ResizeGrip.swift                    tay nắm đổi kích thước
  ShelfSizeStore.swift, ShelfPositionStore.swift   lưu kích thước, vị trí
Tests/ShelfDropTests/           49 test
```

### Cách hoạt động

- **Phát hiện đang kéo:** AppKit ghi dữ liệu của mỗi cú kéo vào pasteboard `.drag`, nên `changeCount` của nó tăng khi một drag thật bắt đầu (chọn text hay kéo cửa sổ thì không). App chụp `changeCount` mỗi lần nhấn chuột, và một `leftMouseDragged` sau đó có `changeCount` khác nghĩa là vừa có drag mang dữ liệu.
- **Lắc chuột:** `ShakeDetector` đếm số lần đổi chiều trên trục X. Cần ít nhất 3 lần đổi chiều trong 0.8 giây, mỗi nét dài ít nhất 40pt, để loại rung tay.
- **Kệ nổi không cướp focus:** `NSPanel` với `.nonactivatingPanel`, `level = .floating`, `.canJoinAllSpaces` và `.fullScreenAuxiliary`.
- **Kéo cả nhóm ra:** SwiftUI `.onDrag` chỉ mang được một item, nên phần này dùng `NSDraggingSession` với nhiều `NSDraggingItem` của AppKit, bọc bằng `NSViewRepresentable`.
- **Vì sao có `WindowDragArea` và `ResizeGrip`:** `NSHostingView` của SwiftUI nuốt thao tác chuột nên `isMovableByWindowBackground` không có tác dụng. Hai view AppKit nằm sau và trên nội dung SwiftUI lo việc di chuyển và đổi cỡ.

## Hạn chế đã biết

- Chỉ chạy trên macOS. Chưa thử trên macOS 14 và 15.
- Bản `.dmg` có cả slice Intel (x86_64) nhưng slice này mới chỉ được build, **chưa chạy thử** (máy phát triển không có Rosetta). Slice Apple Silicon đã chạy thử từ chính file `.dmg`.
- Item trên kệ không được giữ lại khi thoát app.
- **Clear** và nút `x` trên từng tile chỉ gỡ item khỏi kệ, không xóa bản sao tạm. Chỉ nút `x` của kệ làm việc đó (phần còn lại được dọn khi mở app lần sau).
- Thumbnail ảnh được tạo ngay lúc thả, nên thả cùng lúc nhiều ảnh rất lớn có thể làm giao diện khựng một chút.
- Chỉ nhận lắc theo chiều ngang. Độ nhạy chưa chỉnh được trong giao diện.
- Vị trí kệ lưu theo tọa độ toàn cục. Nếu màn hình đã lưu không còn nữa, kệ quay về hiện giữa con trỏ.

## Hướng phát triển

Ký Developer ID và notarize để cài không bị Gatekeeper chặn, upload lên dịch vụ cloud và lấy link chia sẻ, AirDrop, nén zip, Shortcuts, tự chạy khi đăng nhập, chỉnh độ nhạy lắc, lưu kệ giữa các lần chạy, mục menu để đặt lại vị trí kệ.

## Giấy phép

[MIT](LICENSE).
