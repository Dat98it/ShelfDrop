import AppKit

/// Watches for system-wide drags that carry content and reports shakes while one is active.
///
/// How it works: AppKit writes the payload of every drag into the `.drag` pasteboard, so its
/// `changeCount` bumps when a real drag session starts (plain text selection or window resizing
/// does not touch it). We snapshot the count on every mouse-down, and a later `leftMouseDragged`
/// with a different count means "a drag with content just started".
@MainActor
final class DragMonitor {
    var onDragBegan: (() -> Void)?
    var onDragEnded: (() -> Void)?
    var onShake: ((NSPoint) -> Void)?

    private let dragPasteboard = NSPasteboard(name: .drag)
    private var monitors: [Any] = []
    private var lastSeenChangeCount: Int
    private var shake = ShakeDetector()
    private var endPollTimer: Timer?
    private(set) var isDragging = false

    init() {
        lastSeenChangeCount = dragPasteboard.changeCount
    }

    func start() {
        guard monitors.isEmpty else { return }
        let add = { (mask: NSEvent.EventTypeMask, handler: @escaping (NSEvent) -> Void) in
            if let monitor = NSEvent.addGlobalMonitorForEvents(matching: mask, handler: handler) {
                self.monitors.append(monitor)
            }
        }
        add(.leftMouseDown) { [weak self] _ in
            MainActor.assumeIsolated { self?.handleMouseDown() }
        }
        add(.leftMouseDragged) { [weak self] event in
            let x = NSEvent.mouseLocation.x
            let time = event.timestamp
            MainActor.assumeIsolated { self?.handleMouseDragged(x: x, time: time) }
        }
        add(.leftMouseUp) { [weak self] _ in
            MainActor.assumeIsolated { self?.endDrag() }
        }
    }

    func stop() {
        monitors.forEach(NSEvent.removeMonitor)
        monitors.removeAll()
        endDrag()
    }

    // MARK: - Event handling

    private func handleMouseDown() {
        lastSeenChangeCount = dragPasteboard.changeCount
    }

    private func handleMouseDragged(x: CGFloat, time: TimeInterval) {
        if !isDragging {
            guard dragPasteboard.changeCount != lastSeenChangeCount,
                  !(dragPasteboard.types ?? []).isEmpty else { return }
            beginDrag()
        }
        if shake.feed(x: x, at: time) {
            NSLog("[ShelfDrop] shake detected")
            onShake?(NSEvent.mouseLocation)
        }
    }

    private func beginDrag() {
        isDragging = true
        lastSeenChangeCount = dragPasteboard.changeCount
        shake.reset()
        NSLog("[ShelfDrop] drag began, types: %@", (dragPasteboard.types ?? []).map(\.rawValue).joined(separator: ", "))
        onDragBegan?()

        // A drop onto our own shelf is delivered to our app, which global monitors never see,
        // so the mouse-up can be missed. Poll the button state as a safety net.
        let timer = Timer(timeInterval: 0.1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated {
                if NSEvent.pressedMouseButtons & 1 == 0 { self?.endDrag() }
            }
        }
        RunLoop.main.add(timer, forMode: .common)
        endPollTimer = timer
    }

    private func endDrag() {
        endPollTimer?.invalidate()
        endPollTimer = nil
        guard isDragging else { return }
        isDragging = false
        shake.reset()
        NSLog("[ShelfDrop] drag ended")
        onDragEnded?()
    }
}
