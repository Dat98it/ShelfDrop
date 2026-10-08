import AppKit
import SwiftUI

/// A hover flag a view can own with `@StateObject`.
///
/// Used instead of `@State` on purpose: in the current SDK `@State` is a macro whose plugin ships
/// only with Xcode, so it does not compile with just the Command Line Tools.
final class HoverState: ObservableObject {
    @Published var isHovering = false
}

/// Reports whether the mouse is over the view it is attached to (as a `.background`).
///
/// SwiftUI's `onHover` depends on the window being key, which this non-activating panel rarely is.
/// A tracking area with `.activeAlways` works regardless, and the view is invisible to clicks.
struct HoverReader: NSViewRepresentable {
    let onChange: (Bool) -> Void

    func makeNSView(context: Context) -> HoverNSView {
        let view = HoverNSView()
        view.onChange = onChange
        return view
    }

    func updateNSView(_ view: HoverNSView, context: Context) {
        view.onChange = onChange
    }
}

final class HoverNSView: NSView {
    var onChange: (Bool) -> Void = { _ in }

    /// Never the target of a click: everything under it keeps working as if it were not there.
    override func hitTest(_ point: NSPoint) -> NSView? { nil }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(
            rect: .zero, options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: self
        ))
    }

    override func mouseEntered(with event: NSEvent) { onChange(true) }
    override func mouseExited(with event: NSEvent) { onChange(false) }
}
