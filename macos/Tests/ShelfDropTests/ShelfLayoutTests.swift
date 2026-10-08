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

@MainActor
struct ShelfHeaderOverflowTests {
    private let isolated = IsolatedDefaults()

    @Test func headerControlsStayInsideThePanelEvenWithTheLongestSubtitle() throws {
        _ = NSApplication.shared
        let controller = ShelfController(defaults: isolated.defaults, sharePresenter: FakeSharePresenter())
        // As long as a subtitle realistically gets: "1000 items · 12.3 GB".
        controller.model.add((0..<999).map { ShelfItem.text("item \($0)") })
        controller.model.add([ShelfItem(
            kind: .text("big"), title: "big", thumbnail: NSImage(),
            kindLabel: "File", byteCount: 12_300_000_000, isImageFile: false
        )])
        controller.panel.setContentSize(ShelfView.minimumSize)
        controller.show(near: NSPoint(x: 700, y: 700))
        PanelProbe.settle(controller.panel)

        let width = controller.panel.frame.width
        let buttons = PanelProbe.headerButtonRects(in: controller.panel)  // Share, Clear, Close
        try #require(buttons.count == 3)

        // Nothing pushed past the right padding, and nothing overlapping its neighbour.
        #expect(buttons[2].maxX <= width - ShelfStyle.padding + 1, "Close ends at \(buttons[2].maxX) in a \(width)pt panel")
        #expect(buttons[0].maxX <= buttons[1].minX)
        #expect(buttons[1].maxX <= buttons[2].minX)
        let pill = PanelProbe.allViews(in: try #require(controller.panel.contentView))
            .compactMap { $0 as? DragSourceNSView }
            .map(PanelProbe.windowRect(of:))
            .first { abs($0.height - ShelfStyle.controlHeight) < 0.5 }
        #expect((pill?.maxX ?? .infinity) <= buttons[0].minX, "Drag all overlaps the Share button")
    }
}

@MainActor
struct ShelfScrollTests {
    private func allSubviews(of view: NSView) -> [NSView] {
        view.subviews + view.subviews.flatMap(allSubviews)
    }

    /// The grid must not be cut off by a scroller. SwiftUI's scroll view here uses a legacy scroller
    /// that, when shown, takes 17pt from the visible width while the grid is still laid out at the
    /// full width, which clipped the last column. Hiding the scroller avoids that.
    ///
    /// Caveat: whether a test process gets the legacy or the overlay scroller style depends on what ran
    /// before it, and with overlay there is nothing to clip. Run on its own
    /// (`swift test --filter ShelfScrollTests`) this fails without the fix; in a full run it may pass either way.
    @Test func theItemGridIsNotClippedWhenTheShelfScrolls() throws {
        _ = NSApplication.shared
        let model = ShelfModel()
        model.add((0..<30).map { ShelfItem.text("item \($0)") })  // far more than fit, so it scrolls
        let hosting = NSHostingView(rootView: ShelfView(model: model, onClose: {}, onShare: { _ in }))
        hosting.sizingOptions = []
        hosting.frame = NSRect(origin: .zero, size: ShelfView.defaultSize)
        let window = NSWindow(contentRect: hosting.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.contentView = hosting
        hosting.layoutSubtreeIfNeeded()
        RunLoop.current.run(until: Date().addingTimeInterval(0.3))
        hosting.layoutSubtreeIfNeeded()

        let scrollView = try #require(allSubviews(of: hosting).compactMap { $0 as? NSScrollView }.first)
        let document = try #require(scrollView.documentView)
        #expect(scrollView.contentSize.width >= document.frame.width,
                "visible width \(scrollView.contentSize.width)pt is narrower than the grid (\(document.frame.width)pt): the last column is cut off")
    }
}
