import AppKit

/// Everything that really changes the machine, as separate steps, so tests can swap them out
/// and never remove a real app, setting or file.
struct UninstallEffects {
    var moveToTrash: (URL) throws -> Void
    var removeTemporaryFiles: () -> Void
    var removePreferences: () -> Void
    var scheduleFinalCleanup: () -> Void

    static func live(bundleIdentifier: String) -> UninstallEffects {
        UninstallEffects(
            // The Trash, not a permanent delete: the user can still put the app back.
            moveToTrash: { url in try FileManager.default.trashItem(at: url, resultingItemURL: nil) },
            removeTemporaryFiles: { TempStorage.cleanUp() },
            removePreferences: {
                let defaults = UserDefaults.standard
                defaults.removePersistentDomain(forName: bundleIdentifier)
                defaults.synchronize()
            },
            scheduleFinalCleanup: { FinalCleanup.schedule(bundleIdentifier: bundleIdentifier) }
        )
    }
}

/// Removes the settings file once the app has quit.
///
/// Clearing the settings from inside the app is not enough: the system's preferences daemon
/// writes an empty settings file back a few seconds after the app's last write, however
/// the app deletes it (measured on this macOS: 42 bytes, reappearing within about 5 seconds).
/// So a small shell command outlives the app, waits for that write, then deletes the file.
enum FinalCleanup {
    /// The domain and the path are passed as arguments ($1, $2) rather than pasted into the
    /// command, so nothing in them can be read as shell syntax.
    static func arguments(bundleIdentifier: String, preferencesFile: String) -> [String] {
        let cleanup = "/usr/bin/defaults delete \"$1\" 2>/dev/null; /bin/rm -f \"$2\""
        return ["-c", "sleep 5; \(cleanup); sleep 10; \(cleanup)", "sh", bundleIdentifier, preferencesFile]
    }

    static func schedule(bundleIdentifier: String) {
        let file = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Preferences/\(bundleIdentifier).plist").path
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = arguments(bundleIdentifier: bundleIdentifier, preferencesFile: file)
        process.standardInput = FileHandle.nullDevice
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        try? process.run()  // fire and forget: it keeps running after the app quits
    }
}

/// The "Uninstall ShelfDrop…" menu item: moves the app to the Trash and removes what it left behind.
@MainActor
final class Uninstaller {
    static let expectedBundleIdentifier = "local.shelfdrop.app"

    enum Availability: Equatable {
        case available
        case unavailable(String)
    }

    enum Outcome: Equatable {
        /// Done. `warnings` lists steps that did not work but did not stop the rest.
        case removed(warnings: [String])
        /// Nothing was removed (or what was done has been put back).
        case failed(String)
    }

    /// Facts about where the app lives. Injectable so tests need no read-only volume.
    struct Checks {
        var isOnReadOnlyVolume: (URL) -> Bool
        var isDeletable: (URL) -> Bool

        static let live = Checks(
            isOnReadOnlyVolume: { url in
                (try? url.resourceValues(forKeys: [.volumeIsReadOnlyKey]).volumeIsReadOnly) ?? false
            },
            isDeletable: { FileManager.default.isDeletableFile(atPath: $0.path) }
        )
    }

    struct Confirmation: Equatable {
        let title: String
        let message: String
        let confirmButton: String
        let cancelButton: String
    }

    let bundleURL: URL
    private let bundleIdentifier: String?
    private let loginItem: LoginItemService
    private let effects: UninstallEffects
    private let checks: Checks

    init(
        bundleURL: URL = Bundle.main.bundleURL,
        bundleIdentifier: String? = Bundle.main.bundleIdentifier,
        loginItem: LoginItemService,
        effects: UninstallEffects? = nil,
        checks: Checks = .live
    ) {
        self.bundleURL = bundleURL
        self.bundleIdentifier = bundleIdentifier
        self.loginItem = loginItem
        self.effects = effects ?? .live(bundleIdentifier: Self.expectedBundleIdentifier)
        self.checks = checks
    }

    /// Whether uninstalling from here is safe and possible. Checked before anything is touched.
    var availability: Availability {
        // The guards that keep this from ever removing something that is not ShelfDrop: a running
        // `swift run` build, for example, has a folder of binaries (not an .app) as its "bundle".
        guard bundleURL.pathExtension == "app" else {
            return .unavailable("This copy of ShelfDrop is not an installed app (it looks like a development build).")
        }
        guard bundleIdentifier == Self.expectedBundleIdentifier else {
            return .unavailable("This app does not look like ShelfDrop, so it will not be removed.")
        }
        guard !checks.isOnReadOnlyVolume(bundleURL) else {
            return .unavailable("ShelfDrop is running from a read-only disk. Drag it into Applications first.")
        }
        guard checks.isDeletable(bundleURL) else {
            return .unavailable("This account is not allowed to remove ShelfDrop from its current folder.")
        }
        return .available
    }

    /// What the confirmation alert says. Written by hand, so a test checks that it mentions every
    /// thing `uninstall()` does: what the user agrees to must be what happens.
    func confirmation() -> Confirmation {
        let location = (bundleURL.path as NSString).abbreviatingWithTildeInPath
        return Confirmation(
            title: "Uninstall ShelfDrop?",
            message: """
            ShelfDrop will quit and:

            • move itself (\(location)) to the Trash
            • turn off Launch at Login
            • delete its settings (shelf size and position) and temporary files

            Your own files are not touched: the shelf only holds references to them. \
            Until you empty the Trash you can still put the app back.
            """,
            confirmButton: "Uninstall",
            cancelButton: "Cancel"
        )
    }

    func uninstall() -> Outcome {
        if case .unavailable(let reason) = availability { return .failed(reason) }

        var warnings: [String] = []

        // 1. Stop it launching at login. Done first, while the app is still where macOS expects it.
        let loginItemWasOn = loginItem.status == .enabled || loginItem.status == .requiresApproval
        var loginItemTurnedOff = false
        if loginItemWasOn {
            do {
                try loginItem.unregister()
                loginItemTurnedOff = true
            } catch {
                warnings.append("Couldn't turn off Launch at Login (\(error.localizedDescription)). Remove it in System Settings → General → Login Items.")
            }
        }

        // 2. The app itself. The one step that can reasonably fail; if it does, undo step 1 and stop,
        //    leaving the settings alone so the app that stays behind keeps working as before.
        do {
            try effects.moveToTrash(bundleURL)
        } catch {
            if loginItemTurnedOff { try? loginItem.register() }
            return .failed("Couldn't move ShelfDrop to the Trash: \(error.localizedDescription)")
        }

        // 3. What it left behind.
        effects.removeTemporaryFiles()
        effects.removePreferences()
        effects.scheduleFinalCleanup()
        return .removed(warnings: warnings)
    }
}
