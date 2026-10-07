import AppKit
import Foundation
@testable import ShelfDrop

/// Finds controls in a shown shelf panel from its real layout, so tests do not hard-code pixel
/// positions that break whenever the design is tweaked.
///
/// All rects are in the panel's window coordinates (origin bottom-left).
@MainActor
enum PanelProbe {
    static func allViews(in view: NSView) -> [NSView] {
        view.subviews + view.subviews.flatMap(allViews)
    }

    static func windowRect(of view: NSView) -> NSRect {
        view.convert(view.bounds, to: nil)
    }

    /// Lets SwiftUI lay out and settle after the panel was shown or its content changed.
    static func settle(_ panel: NSPanel, seconds: TimeInterval = 0.2) {
        panel.contentView?.layoutSubtreeIfNeeded()
        RunLoop.current.run(until: Date().addingTimeInterval(seconds))
        panel.contentView?.layoutSubtreeIfNeeded()
    }

    /// Header buttons, left to right. With items on the shelf: Share, Clear, Close.
    /// Every header button has the same fixed height; the tile cards are much taller.
    static func headerButtonRects(in panel: NSPanel) -> [NSRect] {
        guard let root = panel.contentView else { return [] }
        return allViews(in: root)
            .compactMap { $0 as? HoverNSView }
            .map(windowRect(of:))
            .filter { abs($0.height - ShelfStyle.controlHeight) < 0.5 }
            .sorted { $0.minX < $1.minX }
    }

    /// The item cards in reading order (top row first, left to right).
    static func cardRects(in panel: NSPanel) -> [NSRect] {
        guard let root = panel.contentView else { return [] }
        return allViews(in: root)
            .compactMap { $0 as? DragSourceNSView }
            .map(windowRect(of:))
            .filter { $0.height > 60 }
            .sorted { abs($0.maxY - $1.maxY) > 1 ? $0.maxY > $1.maxY : $0.minX < $1.minX }
    }

    /// Where a card's corner buttons sit: 4pt in from the corner, 20pt across.
    static func shareButtonCentre(ofCard card: NSRect) -> NSPoint {
        NSPoint(x: card.minX + 14, y: card.maxY - 14)
    }

    static func removeButtonCentre(ofCard card: NSRect) -> NSPoint {
        NSPoint(x: card.maxX - 14, y: card.maxY - 14)
    }

    /// Makes the mouse "enter" the card, as the tracking area would when the user rolls over it.
    static func hover(card: NSRect, in panel: NSPanel) {
        guard let root = panel.contentView,
              let tracker = allViews(in: root).compactMap({ $0 as? HoverNSView }).first(where: {
                  let r = windowRect(of: $0)
                  return abs(r.minX - card.minX) < 1 && abs(r.maxY - card.maxY) < 1 && abs(r.width - card.width) < 1
              }),
              let event = NSEvent.enterExitEvent(
                  with: .mouseEntered, location: .zero, modifierFlags: [], timestamp: 0,
                  windowNumber: panel.windowNumber, context: nil, eventNumber: 0, trackingNumber: 0, userData: nil
              ) else { return }
        tracker.mouseEntered(with: event)
        settle(panel, seconds: 0.15)
    }

    /// A real mouse down/up pair at `point`.
    static func click(_ point: NSPoint, in panel: NSPanel) {
        for type in [NSEvent.EventType.leftMouseDown, .leftMouseUp] {
            let event = NSEvent.mouseEvent(
                with: type, location: point, modifierFlags: [], timestamp: ProcessInfo.processInfo.systemUptime,
                windowNumber: panel.windowNumber, context: nil, eventNumber: 0, clickCount: 1, pressure: 1
            )!
            panel.sendEvent(event)
            // SwiftUI runs button actions asynchronously; let it process the event.
            RunLoop.current.run(until: Date().addingTimeInterval(0.03))
        }
    }

    static func centre(of rect: NSRect) -> NSPoint {
        NSPoint(x: rect.midX, y: rect.midY)
    }
}
