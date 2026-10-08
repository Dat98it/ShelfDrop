import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem?
    private let shelf = ShelfController()
    private let launchAtLogin = LaunchAtLogin(service: SystemLoginItemService())
    private var launchAtLoginItem: NSMenuItem?

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
        item.button?.image = StatusIcon.make()
        item.button?.toolTip = "ShelfDrop"

        let menu = NSMenu()
        menu.addItem(withTitle: "Show/Hide Shelf", action: #selector(toggleShelf), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Clear Shelf", action: #selector(clearShelf), keyEquivalent: "").target = self
        menu.addItem(.separator())
        let loginItem = menu.addItem(withTitle: "Launch at Login", action: #selector(toggleLaunchAtLogin), keyEquivalent: "")
        loginItem.target = self
        loginItem.state = launchAtLogin.menuState
        launchAtLoginItem = loginItem
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit ShelfDrop", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        // The setting can change outside the app (System Settings), so refresh it each time the menu opens.
        menu.delegate = self
        item.menu = menu
        statusItem = item
    }

    @objc private func toggleShelf() {
        shelf.toggle()
    }

    @objc private func clearShelf() {
        shelf.clear()
    }

    @objc private func toggleLaunchAtLogin() {
        if case .failed(let message) = launchAtLogin.toggle() {
            let alert = NSAlert()
            alert.alertStyle = .warning
            alert.messageText = "Couldn't change Launch at Login"
            alert.informativeText = message
            // An accessory app is never frontmost, so bring it forward or the alert hides behind other windows.
            NSApp.activate()
            alert.runModal()
        }
        launchAtLoginItem?.state = launchAtLogin.menuState
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        launchAtLoginItem?.state = launchAtLogin.menuState
    }
}
