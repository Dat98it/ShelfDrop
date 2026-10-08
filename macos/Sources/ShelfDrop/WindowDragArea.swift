import AppKit
import SwiftUI

/// Lets the user move the shelf by dragging any part of it that is not a control or a tile.
///
/// `isMovableByWindowBackground` alone does not work here: `NSHostingView` swallows the
/// mouse-down, so the window never gets the chance to start a move. Placing this view behind
/// the SwiftUI content means clicks on empty areas fall through to it, and it hands the
/// event to the window explicitly.
struct WindowDragArea: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView { WindowDragNSView() }
    func updateNSView(_ view: NSView, context: Context) {}
}

final class WindowDragNSView: NSView {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var mouseDownCanMoveWindow: Bool { true }

    override func mouseDown(with event: NSEvent) {
        window?.performDrag(with: event)
    }
}
