import AppKit

/// Floating window that hosts the shelf. It never activates the app, so showing it in the
/// middle of a drag does not steal focus from whatever the user is dragging out of.
final class ShelfPanel: NSPanel {
    /// Called when the user finishes resizing with the grip, with the resulting size.
    /// Not called for programmatic resizes (e.g. fitting a smaller screen).
    var onUserResizeEnded: ((NSSize) -> Void)?

    init(size: NSSize) {
        super.init(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.nonactivatingPanel, .borderless],
            backing: .buffered,
            defer: false
        )
        isFloatingPanel = true
        level = .floating
        hidesOnDeactivate = false
        isReleasedWhenClosed = false
        isMovableByWindowBackground = true
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        minSize = ShelfView.minimumSize
        // Visible on every Space and on top of full-screen apps.
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]
    }

    // Borderless windows refuse key status by default, which would leave SwiftUI buttons dead.
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
