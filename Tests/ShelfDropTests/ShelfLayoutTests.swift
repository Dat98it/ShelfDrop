import AppKit
import SwiftUI
import Testing
@testable import ShelfDrop

@MainActor
struct ShelfLayoutTests {
    /// Width the SwiftUI shelf wants when it holds `count` items: the widest row is the header
    /// with all its buttons, so this is the narrowest the shelf can be without squeezing it.
    private func idealWidth(itemCount count: Int) -> CGFloat {
        _ = NSApplication.shared
        let model = ShelfModel()
        model.add((0..<count).map { ShelfItem.text("item \($0)") })
        let hosting = NSHostingView(rootView: ShelfView(model: model, onClose: {}, onShare: { _ in }))
        return hosting.fittingSize.width
    }

    @Test func headerFitsAtTheMinimumWidthEvenWithManyItems() {
        for count in [1, 9, 99, 999] {
            let width = idealWidth(itemCount: count)
            #expect(width <= ShelfView.minimumSize.width, "header wants \(width)pt with \(count) items, minimum is \(ShelfView.minimumSize.width)pt")
        }
    }
}

@MainActor
struct ShelfHeaderWrapTests {
    private func allSubviews(of view: NSView) -> [NSView] {
        view.subviews + view.subviews.flatMap(allSubviews)
    }

    /// Height of the "Drag all" handle, found as the shortest drag-source overlay (tiles are 56pt tall).
    private func dragAllHeight(at size: CGSize) -> CGFloat? {
        _ = NSApplication.shared
        let model = ShelfModel()
        model.add([.text("one"), .text("two"), .text("three"), .text("four")])
        let hosting = NSHostingView(rootView: ShelfView(model: model, onClose: {}, onShare: { _ in }))
        hosting.sizingOptions = []
        hosting.frame = NSRect(origin: .zero, size: size)
        let window = NSWindow(contentRect: hosting.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.contentView = hosting
        hosting.layoutSubtreeIfNeeded()
        RunLoop.current.run(until: Date().addingTimeInterval(0.2))
        hosting.layoutSubtreeIfNeeded()
        return allSubviews(of: hosting).compactMap { $0 as? DragSourceNSView }.map(\.frame.height).min()
    }

    @Test func dragAllStaysOnOneLineAtTheMinimumWidth() throws {
        let roomy = try #require(dragAllHeight(at: ShelfView.defaultSize))
        let tight = try #require(dragAllHeight(at: CGSize(width: ShelfView.minimumSize.width, height: ShelfView.defaultSize.height)))
        // If the label wrapped, the handle would be about twice as tall.
        #expect(tight == roomy, "Drag all is \(tight)pt tall at the minimum width vs \(roomy)pt when there is room")
    }
}
