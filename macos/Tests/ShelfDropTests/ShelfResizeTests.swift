import AppKit
import Foundation
import Testing
@testable import ShelfDrop

@MainActor
struct ShelfResizeTests {
    private let isolated = IsolatedDefaults()

    private func makeController() -> ShelfController {
        _ = NSApplication.shared
        return ShelfController(defaults: isolated.defaults)
    }

    private func settle(_ controller: ShelfController) {
        // A freshly shown panel has not laid out its SwiftUI content yet.
        controller.panel.contentView?.layoutSubtreeIfNeeded()
        RunLoop.current.run(until: Date().addingTimeInterval(0.2))
    }

    private func makeShownController() -> ShelfController {
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))
        settle(controller)
        return controller
    }

    /// Sends a real mouse event positioned at a *screen* point. The panel resizes while the
    /// mouse moves, so window coordinates must be recomputed from the panel's current frame
    /// each time, as a real event would be.
    private func mouse(_ type: NSEvent.EventType, _ panel: NSPanel, atScreen point: NSPoint) {
        let event = NSEvent.mouseEvent(
            with: type, location: panel.convertPoint(fromScreen: point), modifierFlags: [],
            timestamp: ProcessInfo.processInfo.systemUptime, windowNumber: panel.windowNumber,
            context: nil, eventNumber: 0, clickCount: 1, pressure: 1
        )!
        panel.sendEvent(event)
        RunLoop.current.run(until: Date().addingTimeInterval(0.02))
    }

    /// Screen position of the bottom-right grip's centre.
    private func gripCentre(of panel: NSPanel) -> NSPoint {
        // The grip sits 4pt of padding plus half its size in from the bottom-right corner.
        let inset = 4 + ShelfView.gripSize / 2
        return panel.convertPoint(toScreen: NSPoint(x: panel.frame.width - inset, y: inset))
    }

    /// Drags the bottom-right grip by (dx, dy) in screen points (dy > 0 moves the mouse up).
    private func dragGrip(of panel: NSPanel, dx: CGFloat, dy: CGFloat) {
        let start = gripCentre(of: panel)
        mouse(.leftMouseDown, panel, atScreen: start)
        for step in 1...5 {
            let fraction = CGFloat(step) / 5
            mouse(.leftMouseDragged, panel, atScreen: NSPoint(x: start.x + dx * fraction, y: start.y + dy * fraction))
        }
        mouse(.leftMouseUp, panel, atScreen: NSPoint(x: start.x + dx, y: start.y + dy))
    }

    // MARK: - Resizing

    @Test func draggingTheGripGrowsWidthAndHeightKeepingTopLeftFixed() {
        let controller = makeShownController()
        let before = controller.panel.frame

        dragGrip(of: controller.panel, dx: 100, dy: -60)  // right and down

        let after = controller.panel.frame
        #expect(after.width == before.width + 100)
        #expect(after.height == before.height + 60)
        #expect(after.minX == before.minX)
        #expect(after.maxY == before.maxY)
    }

    @Test func draggingTheGripShrinksButNotBelowTheMinimum() {
        let controller = makeShownController()
        let before = controller.panel.frame

        dragGrip(of: controller.panel, dx: -20, dy: 30)  // left and up, a modest shrink
        let shrunk = controller.panel.frame
        #expect(shrunk.width == before.width - 20)
        #expect(shrunk.height == before.height - 30)

        dragGrip(of: controller.panel, dx: -5000, dy: 5000)  // far past the minimum
        let clamped = controller.panel.frame
        #expect(clamped.width == ShelfView.minimumSize.width)
        #expect(clamped.height == ShelfView.minimumSize.height)
    }

    @Test func growingIsLimitedToTheVisibleScreen() throws {
        let controller = makeShownController()
        let visible = try #require(controller.panel.screen?.visibleFrame)

        dragGrip(of: controller.panel, dx: 50_000, dy: -50_000)

        let size = controller.panel.frame.size
        #expect(size.width <= visible.width)
        #expect(size.height <= visible.height)
    }

    @Test func chosenSizeSurvivesCloseAndShowAgain() {
        let controller = makeShownController()
        dragGrip(of: controller.panel, dx: 80, dy: -50)
        let resized = controller.panel.frame.size

        controller.close()
        controller.show(near: NSPoint(x: 900, y: 900))

        #expect(controller.panel.frame.size == resized)
    }

    @Test func contentFollowsThePanelSize() {
        let controller = makeShownController()
        dragGrip(of: controller.panel, dx: 120, dy: -80)
        controller.panel.contentView?.layoutSubtreeIfNeeded()

        // The hosting view fills the container, so the SwiftUI shelf is as large as the panel.
        let container = controller.panel.contentView
        #expect(container?.frame.size == controller.panel.frame.size)
        #expect(container?.subviews.first?.frame.size == controller.panel.frame.size)
    }

    // MARK: - Resizing vs. remembering the position

    @Test func resizingIsNotMistakenForMovingTheShelf() {
        let controller = makeShownController()
        dragGrip(of: controller.panel, dx: 90, dy: -70)  // top-left stays put while the bottom-right moves
        // Resizing changes the window's bottom-left origin, so AppKit may report it as a move.
        NotificationCenter.default.post(name: NSWindow.didMoveNotification, object: controller.panel)
        RunLoop.current.run(until: Date().addingTimeInterval(0.6))

        // The user only resized; the shelf must still follow the cursor next time.
        #expect(ShelfPositionStore(defaults: isolated.defaults).load() == nil)
        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 1000))
        #expect(abs(controller.panel.frame.midX - 1500) < 1)
        #expect(abs(controller.panel.frame.midY - 1000) < 1)
    }

    @Test func resizingAMovedShelfKeepsItsRememberedCorner() {
        let controller = makeShownController()
        let target = NSPoint(x: 320, y: 640)
        controller.panel.setFrameOrigin(NSPoint(x: target.x, y: target.y - controller.panel.frame.height))  // the user moves it
        RunLoop.current.run(until: Date().addingTimeInterval(0.1))

        dragGrip(of: controller.panel, dx: 110, dy: -60)
        RunLoop.current.run(until: Date().addingTimeInterval(0.6))

        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 1000))
        #expect(abs(controller.panel.frame.minX - target.x) < 1)
        #expect(abs(controller.panel.frame.maxY - target.y) < 1)
    }

    // MARK: - Persistence across launches

    @Test func firstLaunchUsesTheDefaultSize() {
        let controller = makeController()
        #expect(controller.panel.frame.size == ShelfView.defaultSize)
    }

    @Test func resizedShelfOpensAtThatSizeAfterRelaunch() {
        let first = makeShownController()
        dragGrip(of: first.panel, dx: 90, dy: -70)
        let resized = first.panel.frame.size
        #expect(resized != ShelfView.defaultSize)

        // "Relaunch": a brand-new controller reading the same preferences.
        let relaunched = makeController()
        #expect(relaunched.panel.frame.size == resized)

        relaunched.show(near: NSPoint(x: 800, y: 800))
        #expect(relaunched.panel.frame.size == resized)
    }

    @Test func eachResizeOverwritesThePreviousSavedSize() {
        let first = makeShownController()
        dragGrip(of: first.panel, dx: 90, dy: -70)
        dragGrip(of: first.panel, dx: -30, dy: 20)
        let latest = first.panel.frame.size

        #expect(makeController().panel.frame.size == latest)
    }

    @Test func clickingTheGripWithoutDraggingSavesNothing() {
        let controller = makeShownController()
        let grip = gripCentre(of: controller.panel)

        mouse(.leftMouseDown, controller.panel, atScreen: grip)
        mouse(.leftMouseUp, controller.panel, atScreen: grip)

        #expect(ShelfSizeStore(defaults: isolated.defaults).load() == nil)
    }

    @Test func fittingASmallScreenDoesNotOverwriteTheSavedSize() {
        // Seed a preference no screen can fit, then open the shelf.
        ShelfSizeStore(defaults: isolated.defaults).save(NSSize(width: 9000, height: 9000))
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))

        // It shrank to fit for display...
        let visible = controller.panel.screen?.visibleFrame ?? .zero
        #expect(controller.panel.frame.width <= visible.width)
        #expect(controller.panel.frame.height <= visible.height)
        // ...but the stored preference is untouched.
        #expect(ShelfSizeStore(defaults: isolated.defaults).load() == NSSize(width: 9000, height: 9000))
    }
}
