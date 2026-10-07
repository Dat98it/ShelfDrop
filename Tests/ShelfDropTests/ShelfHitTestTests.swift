import AppKit
import SwiftUI
import Testing
@testable import ShelfDrop

/// The shelf is moved by dragging its empty areas and resized by its corner grip. Both only
/// work if a click lands on the right AppKit view instead of being swallowed by the SwiftUI
/// hosting view.
@MainActor
struct ShelfHitTestTests {
    private func makeHostedShelf(items: [ShelfItem] = []) -> NSHostingView<ShelfView> {
        _ = NSApplication.shared
        let model = ShelfModel()
        model.add(items)
        let hosting = NSHostingView(rootView: ShelfView(model: model, onClose: {}))
        hosting.sizingOptions = []
        hosting.frame = NSRect(origin: .zero, size: NSSize(width: ShelfView.defaultSize.width, height: ShelfView.defaultSize.height))
        let window = NSWindow(contentRect: hosting.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.contentView = hosting
        hosting.layoutSubtreeIfNeeded()
        return hosting
    }

    /// Hit-tests a point given in the hosting view's own top-left-origin coordinates.
    /// `hitTest` expects the *superview's* coordinate space (bottom-left origin), so convert.
    private func hit(_ hosting: NSHostingView<ShelfView>, x: CGFloat, y: CGFloat) -> NSView? {
        hosting.hitTest(hosting.convert(NSPoint(x: x, y: y), to: hosting.superview))
    }

    @Test func emptyAreaOfEmptyShelfStartsWindowDrag() {
        let hosting = makeHostedShelf()
        // Bottom-left corner: nothing but padding and background.
        #expect(hit(hosting, x: 30, y: ShelfView.defaultSize.height - 30) is WindowDragNSView)
    }

    @Test func headerGapStartsWindowDrag() {
        let hosting = makeHostedShelf()
        // Between the "Shelf" title and the close button, near the top.
        #expect(hit(hosting, x: ShelfView.defaultSize.width / 2, y: 22) is WindowDragNSView)
    }

    @Test func bottomRightCornerHitsTheResizeGrip() {
        let hosting = makeHostedShelf()
        let inset = 4 + ShelfView.gripSize / 2
        // The grip must sit above the window-drag area, or resizing would move the window instead.
        #expect(hit(hosting, x: ShelfView.defaultSize.width - inset, y: ShelfView.defaultSize.height - inset) is ResizeGripNSView)
    }

    @Test func tilesKeepTheirOwnDragSource() {
        let url = URL(fileURLWithPath: "/System/Library/CoreServices/Finder.app")
        let hosting = makeHostedShelf(items: [.file(url)])
        // Scan the grid area: some point must hit a tile's drag source, and tiles must not be
        // mistaken for window-drag areas.
        var sawTileSource = false
        for y in stride(from: 50, to: 200, by: 4) {
            for x in stride(from: 12, to: 120, by: 4) {
                if hit(hosting, x: CGFloat(x), y: CGFloat(y)) is DragSourceNSView { sawTileSource = true }
            }
        }
        #expect(sawTileSource)
    }
}
