import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem?
    private let shelf = ShelfController()
    private let launchAtLogin = LaunchAtLogin(service: SystemLoginItemService())
    private var launchAtLoginItem: NSMenuItem?
    private let uninstaller = Uninstaller(loginItem: SystemLoginItemService())
    private var uninstallItem: NSMenuItem?

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
        let uninstall = menu.addItem(withTitle: "Uninstall ShelfDrop…", action: #selector(uninstallApp), keyEquivalent: "")
        uninstall.target = self
        uninstallItem = uninstall
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit ShelfDrop", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        // Uninstall is enabled or not by whether the app can safely remove itself from where it is.
        menu.autoenablesItems = false
        // The settings can change outside the app (System Settings), so refresh the menu each time it opens.
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

    @objc private func uninstallApp() {
        guard case .available = uninstaller.availability else { return }

        let confirmation = uninstaller.confirmation()
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = confirmation.title
        alert.informativeText = confirmation.message
        // The first button is the default one (Return), so the safe choice is the one a stray key press picks.
        alert.addButton(withTitle: confirmation.cancelButton)
        alert.addButton(withTitle: confirmation.confirmButton).hasDestructiveAction = true
        NSApp.activate()
        guard alert.runModal() == .alertSecondButtonReturn else { return }

        switch uninstaller.uninstall() {
        case .removed(let warnings):
            // The settings are gone now; make sure quitting does not write them back.
            shelf.cancelPendingSaves()
            if !warnings.isEmpty { showAlert(title: "ShelfDrop was moved to the Trash", message: warnings.joined(separator: "\n\n")) }
            NSApp.terminate(nil)
        case .failed(let message):
            showAlert(title: "Couldn't uninstall ShelfDrop", message: message)
        }
    }

    private func showAlert(title: String, message: String) {
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = title
        alert.informativeText = message
        NSApp.activate()
        alert.runModal()
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        launchAtLoginItem?.state = launchAtLogin.menuState
        switch uninstaller.availability {
        case .available:
            uninstallItem?.isEnabled = true
            uninstallItem?.toolTip = nil
        case .unavailable(let reason):
            uninstallItem?.isEnabled = false
            uninstallItem?.toolTip = reason
        }
    }
}
