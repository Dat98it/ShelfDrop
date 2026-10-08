import AppKit
import SwiftUI

/// Wires the pieces together: drag monitor -> shelf panel <-> shelf model.
@MainActor
final class ShelfController {
    let model = ShelfModel()
    private let monitor = DragMonitor()
    let panel: ShelfPanel  // internal so tests can drive it with real mouse events
    private let sizeStore: ShelfSizeStore
    private let positionStore: ShelfPositionStore
    /// The size the user last chose. The panel may temporarily be smaller than this when the
    /// current screen cannot fit it, so this (not the panel's frame) is what gets persisted.
    private var preferredSize: NSSize
    /// Where the user last dragged the shelf (its top-left corner), or nil if they never did.
    /// Until then the shelf opens centred on the cursor.
    private var rememberedTopLeft: NSPoint?
    /// Top-left corner the panel is expected to be at, i.e. where we last put it (or where the
    /// user last dropped it). A move notification that does not differ from this is not a user move.
    private var expectedTopLeft: NSPoint?
    private var pendingPositionSave: Timer?
    private let sharePresenter: SharePresenting
    /// Items the user picked a share service for. Their temp copies may still be read by that
    /// service (an AirDrop transfer can outlive the shelf), so closing the shelf must not delete them.
    private var sharedItemIDs: Set<ShelfItem.ID> = []

    var isShelfVisible: Bool { panel.isVisible }

    init(defaults: UserDefaults = .standard, sharePresenter: SharePresenting? = nil) {
        // Created here rather than as a default argument: default arguments are not main-actor isolated.
        self.sharePresenter = sharePresenter ?? SystemSharePresenter()
        sizeStore = ShelfSizeStore(defaults: defaults)
        positionStore = ShelfPositionStore(defaults: defaults)
        preferredSize = sizeStore.load() ?? NSSize(width: ShelfView.defaultSize.width, height: ShelfView.defaultSize.height)
        rememberedTopLeft = positionStore.load()

        let size = preferredSize
        panel = ShelfPanel(size: size)

        let container = DropContainerView(frame: NSRect(origin: .zero, size: size))
        container.onTargetChange = { [model] isTargeted in model.isDropTargeted = isTargeted }
        container.onReceive = { [model] items in model.add(items) }

        let hosting = NSHostingView(rootView: ShelfView(
            model: model,
            onClose: { [weak self] in self?.close() },
            onShare: { [weak self] items in self?.share(items) }
        ))
        hosting.sizingOptions = []
        hosting.frame = container.bounds
        hosting.autoresizingMask = [.width, .height]
        container.addSubview(hosting)
        panel.contentView = container

        panel.onUserResizeEnded = { [weak self] size in self?.userResized(to: size) }
        NotificationCenter.default.addObserver(
            forName: NSWindow.didMoveNotification, object: panel, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.panelMoved() }
        }
        monitor.onShake = { [weak self] point in self?.show(near: point) }
        monitor.onDragEnded = { [weak self] in self?.dragEnded() }
    }

    func start() {
        monitor.start()
    }

    func toggle() {
        if panel.isVisible {
            hide()
        } else {
            show(near: NSEvent.mouseLocation)
        }
    }

    func clear() {
        model.clear()
    }

    func hide() {
        panel.orderOut(nil)
    }

    /// The shelf's "x" button: hide the panel and forget everything on it.
    /// Items are only references to files, so the originals are never touched;
    /// only the temp copies this app made itself are deleted, except those that were just
    /// shared: a share service may still be reading them, so they are left for the next launch's cleanup.
    func close() {
        let discarded = model.items.filter { !sharedItemIDs.contains($0.id) }
        sharedItemIDs.removeAll()
        model.clear()
        hide()
        TempStorage.discard(discarded)
    }

    /// Opens the system share menu for `items`, anchored at the mouse (where the user just clicked).
    /// Files that no longer exist are left out; if nothing is left there is nothing to share.
    func share(_ items: [ShelfItem]) {
        let shareable = items.filter { $0.sharingItem != nil }
        guard !shareable.isEmpty, let view = panel.contentView else { return }

        // Anchor a small rect under the mouse; fall back to the panel's centre if the mouse is
        // elsewhere (e.g. the action was triggered from the keyboard).
        let mouseInView = view.convert(panel.convertPoint(fromScreen: NSEvent.mouseLocation), from: nil)
        let anchor = view.bounds.contains(mouseInView)
            ? NSRect(x: mouseInView.x - 1, y: mouseInView.y - 1, width: 2, height: 2)
            : NSRect(x: view.bounds.midX, y: view.bounds.midY, width: 1, height: 1)

        sharePresenter.present(items: shareable.compactMap(\.sharingItem), relativeTo: anchor, of: view) { [weak self] chosen in
            if chosen { self?.sharedItemIDs.formUnion(shareable.map(\.id)) }
        }
    }

    /// Drops a position change that is still waiting on its debounce timer without writing it.
    /// Used when uninstalling, so the app does not write its settings back after deleting them.
    func cancelPendingSaves() {
        pendingPositionSave?.invalidate()
        pendingPositionSave = nil
    }

    /// Writes a position change that is still waiting on its debounce timer. Call before quitting.
    func flushPendingSaves() {
        guard let pending = pendingPositionSave else { return }
        pending.invalidate()
        pendingPositionSave = nil
        if let topLeft = rememberedTopLeft { positionStore.save(topLeft) }
    }

    /// Shows the shelf where the user last put it, or centred on `point` if they never moved it
    /// (or that spot is no longer on any screen). Always kept fully inside the visible screen area.
    func show(near point: NSPoint) {
        guard !panel.isVisible else { return }

        // The remembered spot only counts while some screen still contains it (a display may
        // have been unplugged since). Probe just inside the corner: the exact edge is "outside".
        let remembered = rememberedTopLeft.flatMap { topLeft in
            screen(containing: NSPoint(x: topLeft.x + 1, y: topLeft.y - 1)).map { (topLeft: topLeft, screen: $0) }
        }
        let screen = remembered?.screen ?? screen(containing: point) ?? NSScreen.main

        // Open at the user's preferred size, shrunk if this screen cannot fit it. Shrinking is
        // only for display: it is never saved, so a smaller screen does not overwrite the preference.
        var size = preferredSize
        if let visible = screen?.visibleFrame {
            size.width = min(size.width, visible.width - 16)
            size.height = min(size.height, visible.height - 16)
        }
        if size != panel.frame.size { panel.setContentSize(size) }

        var origin: NSPoint
        if let remembered {
            origin = NSPoint(x: remembered.topLeft.x, y: remembered.topLeft.y - size.height)
        } else {
            origin = NSPoint(x: point.x - size.width / 2, y: point.y - size.height / 2)
        }
        if let visible = screen?.visibleFrame {
            origin.x = min(max(origin.x, visible.minX + 8), visible.maxX - size.width - 8)
            origin.y = min(max(origin.y, visible.minY + 8), visible.maxY - size.height - 8)
        }
        panel.setFrameOrigin(origin)
        expectedTopLeft = NSPoint(x: panel.frame.minX, y: panel.frame.maxY)
        // Not makeKeyAndOrderFront: the drag in progress must keep working.
        panel.orderFrontRegardless()
    }

    private func screen(containing point: NSPoint) -> NSScreen? {
        NSScreen.screens.first { NSMouseInRect(point, $0.frame, false) }
    }

    private func userResized(to size: NSSize) {
        preferredSize = size
        sizeStore.save(size)
    }

    /// Called for every move of the panel, including the ones this controller causes itself.
    /// Only a move that leaves the top-left corner somewhere new counts as the user moving it,
    /// which also means resizing (top-left stays fixed) is not mistaken for a move.
    private func panelMoved() {
        guard panel.isVisible, let expected = expectedTopLeft else { return }
        let topLeft = NSPoint(x: panel.frame.minX, y: panel.frame.maxY)
        guard abs(topLeft.x - expected.x) >= 1 || abs(topLeft.y - expected.y) >= 1 else { return }

        expectedTopLeft = topLeft
        rememberedTopLeft = topLeft

        // A drag fires many move notifications; write once things settle. A run-loop timer in
        // the common modes still fires while the window is being dragged (event-tracking mode).
        pendingPositionSave?.invalidate()
        let save = Timer(timeInterval: 0.4, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated { self?.flushPendingSaves() }
        }
        RunLoop.main.add(save, forMode: .common)
        pendingPositionSave = save
    }

    /// An empty shelf that was only opened for a drag that went elsewhere should not linger.
    private func dragEnded() {
        // Give a drop onto the shelf time to land before judging whether it is empty.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
            guard let self, self.model.items.isEmpty, !self.model.isDropTargeted else { return }
            self.hide()
        }
    }
}
