import AppKit
import ServiceManagement

enum LoginItemStatus: Equatable {
    case enabled
    /// Registered, but macOS wants the user to allow it in System Settings → Login Items.
    case requiresApproval
    case disabled
    /// macOS cannot find this app to register (for example, it is not a proper app bundle).
    case unavailable
}

/// What the login-item logic needs from macOS. A protocol so tests never touch the real
/// login items of whoever runs them.
@MainActor
protocol LoginItemService: AnyObject {
    var status: LoginItemStatus { get }
    func register() throws
    func unregister() throws
    func openSystemSettings()
}

/// The real thing: registers the running app itself as a login item.
@MainActor
final class SystemLoginItemService: LoginItemService {
    var status: LoginItemStatus {
        switch SMAppService.mainApp.status {
        case .enabled: return .enabled
        case .requiresApproval: return .requiresApproval
        case .notRegistered: return .disabled
        case .notFound: return .unavailable
        @unknown default: return .unavailable
        }
    }

    func register() throws { try SMAppService.mainApp.register() }
    func unregister() throws { try SMAppService.mainApp.unregister() }
    func openSystemSettings() { SMAppService.openSystemSettingsLoginItems() }
}

/// The logic behind the "Launch at Login" menu item.
@MainActor
final class LaunchAtLogin {
    enum Outcome: Equatable {
        case enabled
        case disabled
        /// The user has to switch it on in System Settings; that pane was opened for them.
        case needsApproval
        case failed(String)
    }

    private let service: LoginItemService

    init(service: LoginItemService) {
        self.service = service
    }

    /// Check mark for the menu item: on when the app will launch at login, a dash while it
    /// waits for the user's approval, off otherwise.
    var menuState: NSControl.StateValue {
        switch service.status {
        case .enabled: return .on
        case .requiresApproval: return .mixed
        case .disabled, .unavailable: return .off
        }
    }

    @discardableResult
    func toggle() -> Outcome {
        switch service.status {
        case .enabled:
            do {
                try service.unregister()
                return .disabled
            } catch {
                return .failed(error.localizedDescription)
            }
        case .requiresApproval:
            // Already registered; only the user can approve it, so take them there.
            service.openSystemSettings()
            return .needsApproval
        case .disabled, .unavailable:
            do {
                try service.register()
            } catch {
                return .failed(error.localizedDescription)
            }
            if service.status == .requiresApproval {
                service.openSystemSettings()
                return .needsApproval
            }
            return .enabled
        }
    }
}
