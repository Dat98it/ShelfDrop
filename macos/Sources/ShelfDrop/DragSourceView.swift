import AppKit
import SwiftUI

/// Transparent overlay that turns a mouse drag into an `NSDraggingSession`.
///
/// SwiftUI's `.onDrag` can only carry one item, so dragging a whole group out of the shelf
/// needs AppKit. `items` is evaluated when the drag starts: pass a single item for a tile,
/// or every item for the "Drag all" handle.
struct DragSourceView: NSViewRepresentable {
    let items: () -> [ShelfItem]

    func makeNSView(context: Context) -> DragSourceNSView {
        let view = DragSourceNSView()
        view.items = items
        return view
    }

    func updateNSView(_ view: DragSourceNSView, context: Context) {
        view.items = items
    }
}

final class DragSourceNSView: NSView, NSDraggingSource {
    var items: () -> [ShelfItem] = { [] }
    private var mouseDownEvent: NSEvent?

    // The shelf is a non-activating panel; clicks must work without a prior activating click,
    // and a drag that starts here must not move the window.
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var mouseDownCanMoveWindow: Bool { false }

    override func mouseDown(with event: NSEvent) {
        mouseDownEvent = event
    }

    override func mouseDragged(with event: NSEvent) {
        guard let down = mouseDownEvent else { return }
        let dx = event.locationInWindow.x - down.locationInWindow.x
        let dy = event.locationInWindow.y - down.locationInWindow.y
        guard hypot(dx, dy) > 4 else { return }
        mouseDownEvent = nil

        let dragged = items()
        guard !dragged.isEmpty else { return }

        let origin = convert(down.locationInWindow, from: nil)
        let size: CGFloat = 48
        let draggingItems = dragged.enumerated().map { index, item -> NSDraggingItem in
            let draggingItem = NSDraggingItem(pasteboardWriter: item.pasteboardWriter)
            // Fan the items out slightly so a group reads as a stack.
            let offset = CGFloat(min(index, 4)) * 6
            let frame = NSRect(x: origin.x - size / 2 + offset, y: origin.y - size / 2 - offset, width: size, height: size)
            draggingItem.setDraggingFrame(frame, contents: item.thumbnail)
            return draggingItem
        }
        beginDraggingSession(with: draggingItems, event: down, source: self)
    }

    func draggingSession(_ session: NSDraggingSession, sourceOperationMaskFor context: NSDraggingContext) -> NSDragOperation {
        // Copy only: with .move/.generic Finder could relocate the user's original files.
        .copy
    }
}
