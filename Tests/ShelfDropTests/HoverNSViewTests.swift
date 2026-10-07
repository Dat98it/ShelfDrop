import AppKit
import Testing
@testable import ShelfDrop

@MainActor
struct HoverNSViewTests {
    private func event(_ type: NSEvent.EventType) -> NSEvent {
        NSEvent.enterExitEvent(
            with: type, location: .zero, modifierFlags: [], timestamp: 0, windowNumber: 0,
            context: nil, eventNumber: 0, trackingNumber: 0, userData: nil
        )!
    }

    @Test func reportsTheMouseEnteringAndLeaving() {
        let view = HoverNSView()
        var states: [Bool] = []
        view.onChange = { states.append($0) }

        view.mouseEntered(with: event(.mouseEntered))
        view.mouseExited(with: event(.mouseExited))

        #expect(states == [true, false])
    }

    @Test func neverCapturesClicks() {
        let view = HoverNSView()
        view.frame = NSRect(x: 0, y: 0, width: 100, height: 100)
        let container = NSView(frame: view.frame)
        container.addSubview(view)

        // Whatever is underneath must receive the click, as if the tracker was not there.
        #expect(view.hitTest(NSPoint(x: 50, y: 50)) == nil)
    }

    @Test func installsAnAlwaysActiveTrackingArea() {
        let view = HoverNSView()
        view.frame = NSRect(x: 0, y: 0, width: 100, height: 100)
        view.updateTrackingAreas()

        let area = view.trackingAreas.first
        #expect(view.trackingAreas.count == 1)
        // activeAlways is what makes it work in a window that is never key.
        #expect(area?.options.contains(.activeAlways) == true)
        #expect(area?.options.contains(.mouseEnteredAndExited) == true)
    }
}
