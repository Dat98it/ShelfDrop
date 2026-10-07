import Foundation

/// A throwaway `UserDefaults` domain so tests never touch real preferences.
/// Hold it as a stored property of the test suite struct: Swift Testing creates one instance
/// per test, so the domain is removed when that test finishes.
final class IsolatedDefaults {
    let name = "shelfdrop.tests.\(UUID().uuidString)"
    let defaults: UserDefaults

    init() {
        defaults = UserDefaults(suiteName: name)!
        defaults.removePersistentDomain(forName: name)
    }

    deinit {
        defaults.removePersistentDomain(forName: name)
    }
}
