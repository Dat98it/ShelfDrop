import AppKit
import Foundation
import Testing
@testable import ShelfDrop

/// Records what the uninstaller does instead of doing it.
@MainActor
final class EffectLog {
    struct TrashFailure: LocalizedError { var errorDescription: String? { "disk is full" } }

    var steps: [String] = []
    var trashError: Error?
    /// How many times the login item had been turned off at the moment the app was trashed.
    var loginItemTurnedOffBeforeTrash: Int?
    weak var login: FakeLoginItemService?

    var effects: UninstallEffects {
        UninstallEffects(
            moveToTrash: { [self] url in
                steps.append("trash:\(url.path)")
                loginItemTurnedOffBeforeTrash = login?.unregisterCalls
                if let trashError { throw trashError }
            },
            removeTemporaryFiles: { [self] in steps.append("temp") },
            removePreferences: { [self] in steps.append("prefs") },
            scheduleFinalCleanup: { [self] in steps.append("cleanup") }
        )
    }
}

@MainActor
struct UninstallerTests {
    private let installed = URL(fileURLWithPath: "/Applications/ShelfDrop.app")
    private let log = EffectLog()

    private func make(
        at url: URL? = nil,
        identifier: String? = Uninstaller.expectedBundleIdentifier,
        login: FakeLoginItemService,
        readOnly: Bool = false,
        deletable: Bool = true
    ) -> Uninstaller {
        log.login = login
        return Uninstaller(
            bundleURL: url ?? installed, bundleIdentifier: identifier, loginItem: login, effects: log.effects,
            checks: .init(isOnReadOnlyVolume: { _ in readOnly }, isDeletable: { _ in deletable })
        )
    }

    // MARK: - When it is allowed

    @Test func isAvailableForAnInstalledCopyOfTheApp() {
        let uninstaller = make(login: FakeLoginItemService(status: .disabled))
        #expect(uninstaller.availability == .available)
    }

    @Test func refusesAFolderThatIsNotAnAppBundle() {
        // What `swift run` gives as the "bundle": the folder holding the binary. Trashing it would delete the build.
        let uninstaller = make(at: URL(fileURLWithPath: "/Users/me/ShelfDrop/.build/debug"), login: FakeLoginItemService(status: .disabled))
        guard case .unavailable = uninstaller.availability else { Issue.record("a build folder must not be removable"); return }
    }

    @Test func refusesAnyOtherApp() {
        let other = make(at: URL(fileURLWithPath: "/Applications/Safari.app"), identifier: "com.apple.Safari", login: FakeLoginItemService(status: .disabled))
        let none = make(identifier: nil, login: FakeLoginItemService(status: .disabled))
        for uninstaller in [other, none] {
            guard case .unavailable = uninstaller.availability else { Issue.record("only ShelfDrop itself may be removed"); return }
        }
    }

    @Test func refusesAReadOnlyDiskBecauseTheTrashCannotTakeItFromThere() {
        let uninstaller = make(login: FakeLoginItemService(status: .disabled), readOnly: true)
        guard case .unavailable(let reason) = uninstaller.availability else { Issue.record("should be unavailable"); return }
        #expect(reason.contains("read-only"))
    }

    @Test func refusesWhenThisAccountCannotDeleteTheApp() {
        let uninstaller = make(login: FakeLoginItemService(status: .disabled), deletable: false)
        guard case .unavailable = uninstaller.availability else { Issue.record("should be unavailable"); return }
    }

    @Test func anUnavailableUninstallDoesNothingAtAll() {
        let login = FakeLoginItemService(status: .enabled)
        let uninstaller = make(identifier: nil, login: login)

        let outcome = uninstaller.uninstall()

        guard case .failed = outcome else { Issue.record("expected a refusal"); return }
        #expect(log.steps.isEmpty)
        #expect(login.unregisterCalls == 0)
    }

    @Test func aCopyRunningOutsideAnInstalledAppIsNeverRemovable() {
        // The default arguments look at the process running the tests, which is not an installed ShelfDrop.app.
        let uninstaller = Uninstaller(loginItem: FakeLoginItemService(status: .disabled))
        #expect(uninstaller.availability != .available)
    }

    // MARK: - Doing it

    @Test func removesTheAppThenWhatItLeftBehind() {
        let login = FakeLoginItemService(status: .enabled)
        let outcome = make(login: login).uninstall()

        #expect(outcome == .removed(warnings: []))
        #expect(log.steps == ["trash:/Applications/ShelfDrop.app", "temp", "prefs", "cleanup"])
    }

    @Test func turnsOffLaunchAtLoginBeforeTheAppMoves() {
        let login = FakeLoginItemService(status: .enabled)
        _ = make(login: login).uninstall()

        #expect(login.unregisterCalls == 1)
        // macOS looks the login item up by the app's location, so it has to go while the app is still there.
        #expect(log.loginItemTurnedOffBeforeTrash == 1)
    }

    @Test func alsoTurnsOffALoginItemThatIsWaitingForApproval() {
        let login = FakeLoginItemService(status: .requiresApproval)
        _ = make(login: login).uninstall()
        #expect(login.unregisterCalls == 1)
    }

    @Test func leavesLoginItemsAloneWhenItWasNeverOn() {
        let login = FakeLoginItemService(status: .disabled)
        let outcome = make(login: login).uninstall()

        #expect(outcome == .removed(warnings: []))
        #expect(login.unregisterCalls == 0)
        #expect(login.registerCalls == 0)
    }

    @Test func trashesExactlyTheAppBundleAndNothingAround_it() {
        let outcome = make(at: URL(fileURLWithPath: "/Users/me/Apps/ShelfDrop.app"), login: FakeLoginItemService(status: .disabled)).uninstall()
        #expect(outcome == .removed(warnings: []))
        #expect(log.steps.first == "trash:/Users/me/Apps/ShelfDrop.app")
    }

    // MARK: - When something goes wrong

    @Test func ifTheTrashFailsNothingElseIsRemovedAndLaunchAtLoginIsRestored() {
        let login = FakeLoginItemService(status: .enabled)
        log.trashError = EffectLog.TrashFailure()
        let outcome = make(login: login).uninstall()

        guard case .failed(let message) = outcome else { Issue.record("expected a failure"); return }
        #expect(message.contains("Trash") && message.contains("disk is full"))
        // The app stays, so its settings and temp files must stay too.
        #expect(log.steps == ["trash:/Applications/ShelfDrop.app"])
        // And it must keep launching at login, as before.
        #expect(login.registerCalls == 1)
        #expect(login.status == .enabled)
    }

    @Test func ifTheTrashFailsAndLaunchAtLoginWasOffItStaysOff() {
        let login = FakeLoginItemService(status: .disabled)
        log.trashError = EffectLog.TrashFailure()
        _ = make(login: login).uninstall()

        #expect(login.registerCalls == 0)
        #expect(login.status == .disabled)
    }

    @Test func aLoginItemThatWillNotTurnOffIsAWarningNotAStopper() {
        let login = FakeLoginItemService(status: .enabled)
        login.unregisterError = FakeLoginItemService.Failure()
        let outcome = make(login: login).uninstall()

        guard case .removed(let warnings) = outcome else { Issue.record("the app should still be removed"); return }
        #expect(warnings.count == 1)
        #expect(warnings.first?.contains("Login Items") == true)
        #expect(log.steps == ["trash:/Applications/ShelfDrop.app", "temp", "prefs", "cleanup"])
    }

    // MARK: - What the user is told

    @Test func theConfirmationSaysEverythingThatWillHappen() {
        let message = make(login: FakeLoginItemService(status: .disabled)).confirmation().message
        #expect(message.contains("Trash"))
        #expect(message.contains("Launch at Login"))
        #expect(message.contains("settings"))
        #expect(message.contains("temporary files"))
        #expect(message.contains("/Applications/ShelfDrop.app"))
        #expect(message.contains("Your own files are not touched"))
    }

    @Test func theConfirmationOffersCancelAndUninstall() {
        let confirmation = make(login: FakeLoginItemService(status: .disabled)).confirmation()
        #expect(confirmation.cancelButton == "Cancel")
        #expect(confirmation.confirmButton == "Uninstall")
    }
}

struct FinalCleanupTests {
    @Test func passesTheDomainAndPathAsArgumentsInsteadOfPastingThemIntoTheCommand() {
        let arguments = FinalCleanup.arguments(bundleIdentifier: "local.shelfdrop.app", preferencesFile: "/Users/me/Library/Preferences/local.shelfdrop.app.plist")

        #expect(arguments.count == 5)
        #expect(arguments[0] == "-c")
        #expect(arguments[3] == "local.shelfdrop.app")
        #expect(arguments[4] == "/Users/me/Library/Preferences/local.shelfdrop.app.plist")
        // The script refers to them as $1 and $2, and does not contain them itself.
        #expect(arguments[1].contains("\"$1\"") && arguments[1].contains("\"$2\""))
        #expect(!arguments[1].contains("local.shelfdrop") && !arguments[1].contains("/Users/"))
    }

    @Test func aPathWithSpacesOrQuotesCannotChangeWhatTheCommandDoes() {
        let nasty = "/Users/me/Library/Preferences/x\"; rm -rf ~; echo \".plist"
        let arguments = FinalCleanup.arguments(bundleIdentifier: "local.shelfdrop.app", preferencesFile: nasty)
        #expect(!arguments[1].contains("rm -rf"))
        #expect(arguments[4] == nasty)  // carried as plain data
    }

    @Test func onlyEverRemovesOneFileAndNeverUsesWildcardsOrRecursion() {
        let script = FinalCleanup.arguments(bundleIdentifier: "id", preferencesFile: "/x")[1]
        #expect(!script.contains("*") && !script.contains(" -r") && !script.contains("-rf"))
        #expect(script.contains("/bin/rm -f \"$2\""))
    }

    @Test func waitsForTheSettingsDaemonAndChecksAgainLater() {
        let script = FinalCleanup.arguments(bundleIdentifier: "id", preferencesFile: "/x")[1]
        #expect(script.hasPrefix("sleep 5;"))
        #expect(script.components(separatedBy: "sleep").count - 1 == 2)  // two passes
    }
}
