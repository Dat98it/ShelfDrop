import AppKit
import Foundation
import Testing
@testable import ShelfDrop

/// Stands in for macOS's login items so tests never change the real ones.
@MainActor
final class FakeLoginItemService: LoginItemService {
    struct Failure: LocalizedError { var errorDescription: String? { "boom" } }

    var status: LoginItemStatus
    /// What registering leaves the status as (macOS may ask the user to approve it first).
    var statusAfterRegister: LoginItemStatus = .enabled
    var registerError: Error?
    var unregisterError: Error?
    private(set) var registerCalls = 0
    private(set) var unregisterCalls = 0
    private(set) var openedSettings = 0

    init(status: LoginItemStatus) { self.status = status }

    func register() throws {
        registerCalls += 1
        if let registerError { throw registerError }
        status = statusAfterRegister
    }

    func unregister() throws {
        unregisterCalls += 1
        if let unregisterError { throw unregisterError }
        status = .disabled
    }

    func openSystemSettings() { openedSettings += 1 }
}

@MainActor
struct LaunchAtLoginTests {
    @Test func turnsOnWhenOff() {
        let service = FakeLoginItemService(status: .disabled)
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .enabled)
        #expect(service.registerCalls == 1)
        #expect(service.status == .enabled)
        #expect(login.menuState == .on)
    }

    @Test func turnsOffWhenOn() {
        let service = FakeLoginItemService(status: .enabled)
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .disabled)
        #expect(service.unregisterCalls == 1)
        #expect(login.menuState == .off)
    }

    @Test func sendsTheUserToSystemSettingsWhenApprovalIsNeeded() {
        let service = FakeLoginItemService(status: .requiresApproval)
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .needsApproval)
        #expect(service.openedSettings == 1)
        // Nothing to register or undo: only the user can approve it.
        #expect(service.registerCalls == 0)
        #expect(service.unregisterCalls == 0)
    }

    @Test func registeringCanEndUpNeedingApproval() {
        let service = FakeLoginItemService(status: .disabled)
        service.statusAfterRegister = .requiresApproval
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .needsApproval)
        #expect(service.registerCalls == 1)
        #expect(service.openedSettings == 1)
    }

    @Test func reportsAFailureToRegisterAndStaysOff() {
        let service = FakeLoginItemService(status: .disabled)
        service.registerError = FakeLoginItemService.Failure()
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .failed("boom"))
        #expect(login.menuState == .off)
        #expect(service.openedSettings == 0)
    }

    @Test func reportsAFailureToUnregisterAndStaysOn() {
        let service = FakeLoginItemService(status: .enabled)
        service.unregisterError = FakeLoginItemService.Failure()
        let login = LaunchAtLogin(service: service)

        #expect(login.toggle() == .failed("boom"))
        #expect(login.menuState == .on)
    }

    @Test func menuCheckMarkFollowsTheStatus() {
        func state(_ status: LoginItemStatus) -> NSControl.StateValue {
            LaunchAtLogin(service: FakeLoginItemService(status: status)).menuState
        }
        #expect(state(.enabled) == .on)
        #expect(state(.requiresApproval) == .mixed)
        #expect(state(.disabled) == .off)
        #expect(state(.unavailable) == .off)
    }

    @Test func anAppMacOSCannotFindIsStillOfferedARegistrationAttempt() {
        let service = FakeLoginItemService(status: .unavailable)
        let login = LaunchAtLogin(service: service)

        _ = login.toggle()

        #expect(service.registerCalls == 1)
    }
}
