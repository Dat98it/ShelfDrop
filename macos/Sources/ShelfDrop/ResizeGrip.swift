import AppKit
import SwiftUI

/// Bottom-right handle that resizes the shelf panel.
///
/// The top-left corner stays put and the bottom-right follows the mouse, so dragging
/// right/down grows the shelf and left/up shrinks it. Width and height change together
/// with the drag, each clamped to the panel's `minSize` and to the screen it is on.
struct ResizeGrip: NSViewRepresentable {
    func makeNSView(context: Context) -> ResizeGripNSView { ResizeGripNSView() }
    func updateNSView(_ view: ResizeGripNSView, context: Context) {}
}

final class ResizeGripNSView: NSView {
    private var startFrame = NSRect.zero
    private var startMouse = NSPoint.zero
    private var didResize = false

    // Same constraints as the other interactive overlays: works on a non-activating panel
    // without a priming click, and must not be mistaken for a window-move handle.
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var mouseDownCanMoveWindow: Bool { false }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.tertiaryLabelColor.setStroke()
        let path = NSBezierPath()
        path.lineWidth = 1.5
        path.lineCapStyle = .round
        // Three diagonal ticks hugging the bottom-right corner.
        for offset in stride(from: CGFloat(5), through: 13, by: 4) {
            path.move(to: NSPoint(x: bounds.maxX - offset, y: bounds.minY + 2))
            path.line(to: NSPoint(x: bounds.maxX - 2, y: bounds.minY + offset))
        }
        path.stroke()
    }

    // Cursor rects only fire for the key window of the active app; this app is never active,
    // so use an always-active tracking area instead.
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: .zero, options: [.cursorUpdate, .activeAlways, .inVisibleRect], owner: self))
    }

    override func cursorUpdate(with event: NSEvent) {
        if #available(macOS 15.0, *) {
            NSCursor.frameResize(position: .bottomRight, directions: .all).set()
        } else {
            NSCursor.crosshair.set()
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard let window else { return }
        startFrame = window.frame
        startMouse = window.convertPoint(toScreen: event.locationInWindow)
        didResize = false
    }

    override func mouseUp(with event: NSEvent) {
        // A plain click on the grip must not persist anything (the panel may be showing a
        // size that was shrunk to fit the screen rather than the user's preference).
        guard didResize, let panel = window as? ShelfPanel else { return }
        didResize = false
        panel.onUserResizeEnded?(panel.frame.size)
    }

    override func mouseDragged(with event: NSEvent) {
        guard let window else { return }
        // Screen coordinates, not window ones: the window is resizing under the mouse.
        let mouse = window.convertPoint(toScreen: event.locationInWindow)
        let proposed = NSSize(
            width: startFrame.width + (mouse.x - startMouse.x),
            height: startFrame.height - (mouse.y - startMouse.y)  // screen y points up
        )

        let limit = (window.screen ?? NSScreen.main)?.visibleFrame.size ?? NSSize(width: 1200, height: 900)
        let size = NSSize(
            width: min(max(proposed.width, window.minSize.width), max(limit.width, window.minSize.width)),
            height: min(max(proposed.height, window.minSize.height), max(limit.height, window.minSize.height))
        )
        // Keep the top-left corner anchored.
        let origin = NSPoint(x: startFrame.minX, y: startFrame.maxY - size.height)
        window.setFrame(NSRect(origin: origin, size: size), display: true)
        didResize = true
    }
}
