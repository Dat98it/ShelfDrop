import AppKit

// Top-level code runs on the main thread but is not main-actor isolated in Swift 5 mode.
MainActor.assumeIsolated {
    let app = NSApplication.shared
    let delegate = AppDelegate()  // kept alive by this scope for as long as app.run() blocks
    app.delegate = delegate
    app.setActivationPolicy(.accessory)
    app.run()
}
