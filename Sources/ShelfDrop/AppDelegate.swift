import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private let shelf = ShelfController()

    func applicationDidFinishLaunching(_ notification: Notification) {
        TempStorage.cleanUp()
        shelf.start()
        setUpStatusItem()

        // Debug aid: open the shelf at launch without having to shake a drag into existence.
        if CommandLine.arguments.contains("--show-shelf"), let screen = NSScreen.main {
            shelf.show(near: NSPoint(x: screen.frame.midX, y: screen.frame.midY))
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        // A move made in the last moments may still be waiting on its debounce timer.
        shelf.flushPendingSaves()
    }

    private func setUpStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.image = NSImage(
            systemSymbolName: "tray.and.arrow.down",
            accessibilityDescription: "ShelfDrop"
        )

        let menu = NSMenu()
        menu.addItem(withTitle: "Show/Hide Shelf", action: #selector(toggleShelf), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Clear Shelf", action: #selector(clearShelf), keyEquivalent: "").target = self
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit ShelfDrop", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        item.menu = menu
        statusItem = item
    }

    @objc private func toggleShelf() {
        shelf.toggle()
    }

    @objc private func clearShelf() {
        shelf.clear()
    }
}
