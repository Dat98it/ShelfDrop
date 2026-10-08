import AppKit
import Foundation
import Testing
@testable import ShelfDrop

@MainActor
struct ShelfPositionTests {
    private let isolated = IsolatedDefaults()
    private var store: ShelfPositionStore { ShelfPositionStore(defaults: isolated.defaults) }

    private func makeController() -> ShelfController {
        _ = NSApplication.shared
        return ShelfController(defaults: isolated.defaults)
    }

    private func topLeft(of panel: NSPanel) -> NSPoint {
        NSPoint(x: panel.frame.minX, y: panel.frame.maxY)
    }

    private func wait(_ seconds: TimeInterval) {
        RunLoop.current.run(until: Date().addingTimeInterval(seconds))
    }

    /// Simulates the user dragging the shelf somewhere: the panel's origin changes outside of
    /// the controller's own placement, and AppKit posts its move notification.
    private func userMove(_ panel: NSPanel, toTopLeft target: NSPoint) {
        panel.setFrameOrigin(NSPoint(x: target.x, y: target.y - panel.frame.height))
        wait(0.05)
    }

    private func expectTopLeft(_ panel: NSPanel, near target: NSPoint, sourceLocation: SourceLocation = #_sourceLocation) {
        let actual = topLeft(of: panel)
        #expect(abs(actual.x - target.x) < 1, "x: \(actual.x) vs \(target.x)", sourceLocation: sourceLocation)
        #expect(abs(actual.y - target.y) < 1, "y: \(actual.y) vs \(target.y)", sourceLocation: sourceLocation)
    }

    // MARK: - Default behaviour

    @Test func neverMovedShelfOpensCentredOnTheCursorEachTime() {
        let controller = makeController()

        controller.show(near: NSPoint(x: 700, y: 700))
        #expect(abs(controller.panel.frame.midX - 700) < 1)
        #expect(abs(controller.panel.frame.midY - 700) < 1)

        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 900))
        #expect(abs(controller.panel.frame.midX - 1500) < 1)
        #expect(abs(controller.panel.frame.midY - 900) < 1)

        // Placing it ourselves must not be mistaken for the user choosing a spot.
        wait(0.6)
        #expect(store.load() == nil)
    }

    @Test func aMoveNotificationThatDoesNotMoveTheCornerIsNotTheUserMovingIt() {
        // AppKit can deliver a move notification for our own placement after the panel is already
        // visible. It must not turn the cursor-centred spot into the "remembered" one.
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))
        NotificationCenter.default.post(name: NSWindow.didMoveNotification, object: controller.panel)
        wait(0.6)

        #expect(store.load() == nil)
        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 1000))
        #expect(abs(controller.panel.frame.midX - 1500) < 1)
        #expect(abs(controller.panel.frame.midY - 1000) < 1)
    }

    // MARK: - Remembering

    @Test func movedShelfReopensAtThatSpotInsteadOfUnderTheCursor() {
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))
        let target = NSPoint(x: 300, y: 600)
        userMove(controller.panel, toTopLeft: target)

        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 1000))  // the cursor is somewhere else entirely

        expectTopLeft(controller.panel, near: target)
    }

    @Test func movedShelfIsRestoredAfterRelaunch() {
        let first = makeController()
        first.show(near: NSPoint(x: 700, y: 700))
        let target = NSPoint(x: 420, y: 650)
        userMove(first.panel, toTopLeft: target)
        wait(0.6)  // let the debounced save run

        // "Relaunch": a brand-new controller reading the same preferences.
        let relaunched = makeController()
        relaunched.show(near: NSPoint(x: 1800, y: 1100))

        expectTopLeft(relaunched.panel, near: target)
    }

    @Test func aMoveMadeJustBeforeQuittingIsStillSaved() {
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))
        let target = NSPoint(x: 350, y: 640)
        userMove(controller.panel, toTopLeft: target)
        #expect(store.load() == nil)  // still waiting on the debounce timer

        controller.flushPendingSaves()  // what applicationWillTerminate does

        let saved = store.load()
        #expect(saved != nil)
        #expect(abs((saved?.x ?? 0) - target.x) < 1)
        #expect(abs((saved?.y ?? 0) - target.y) < 1)
    }

    @Test func eachMoveOverwritesThePreviousOne() {
        let controller = makeController()
        controller.show(near: NSPoint(x: 700, y: 700))
        userMove(controller.panel, toTopLeft: NSPoint(x: 300, y: 600))
        userMove(controller.panel, toTopLeft: NSPoint(x: 500, y: 800))
        wait(0.6)

        controller.hide()
        controller.show(near: NSPoint(x: 1500, y: 1000))
        expectTopLeft(controller.panel, near: NSPoint(x: 500, y: 800))
    }

    // MARK: - Safety

    @Test func rememberedSpotOnAMissingScreenFallsBackToTheCursor() {
        store.save(NSPoint(x: -90_000, y: 90_000))  // no display is anywhere near here
        let controller = makeController()

        controller.show(near: NSPoint(x: 700, y: 700))

        #expect(abs(controller.panel.frame.midX - 700) < 1)
        #expect(abs(controller.panel.frame.midY - 700) < 1)
    }

    @Test func rememberedSpotAtTheScreenEdgeIsPulledFullyOnScreen() throws {
        let probe = makeController()
        probe.show(near: NSPoint(x: 700, y: 700))
        let visible = try #require(probe.panel.screen?.visibleFrame)
        probe.hide()

        // Top-left corner almost at the bottom-right of the screen: the panel would hang off it.
        store.save(NSPoint(x: visible.maxX - 5, y: visible.minY + 30))
        let controller = makeController()
        controller.show(near: NSPoint(x: 100, y: 100))

        #expect(visible.insetBy(dx: -0.5, dy: -0.5).contains(controller.panel.frame))
    }
}
