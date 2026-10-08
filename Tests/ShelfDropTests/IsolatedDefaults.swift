import Foundation

/// A throwaway `UserDefaults` for tests: it lives only in memory, so tests never touch real
/// preferences and never leave a file behind.
///
/// An earlier version used a real, uniquely named suite and removed it afterwards. That emptied
/// the domain but the preferences daemon (`cfprefsd`) kept re-creating an empty 42-byte `.plist`
/// in ~/Library/Preferences a few seconds later, one per test, more than a thousand in all.
/// Deleting the file from the test cannot win that race, so nothing is ever written to disk.
///
/// Hold it as a stored property of the test suite struct (Swift Testing creates one instance per test).
final class IsolatedDefaults {
    let defaults: UserDefaults = InMemoryDefaults()
}

/// A `UserDefaults` that keeps its values in a dictionary. It overrides every accessor the app and
/// its tests use; none of them reaches the real storage, so the unused suite behind it is never created on disk.
final class InMemoryDefaults: UserDefaults {
    private var storage: [String: Any] = [:]

    convenience init() {
        self.init(suiteName: "shelfdrop.in-memory.\(UUID().uuidString)")!
    }

    override init?(suiteName suitename: String?) {
        super.init(suiteName: suitename)
    }

    override func object(forKey defaultName: String) -> Any? { storage[defaultName] }
    override func set(_ value: Any?, forKey defaultName: String) { storage[defaultName] = value }
    override func set(_ value: Double, forKey defaultName: String) { storage[defaultName] = value }
    override func set(_ value: Float, forKey defaultName: String) { storage[defaultName] = value }
    override func set(_ value: Int, forKey defaultName: String) { storage[defaultName] = value }
    override func set(_ value: Bool, forKey defaultName: String) { storage[defaultName] = value }
    override func set(_ url: URL?, forKey defaultName: String) { storage[defaultName] = url }
    override func removeObject(forKey defaultName: String) { storage[defaultName] = nil }
    override func removePersistentDomain(forName domainName: String) { storage.removeAll() }
    override func synchronize() -> Bool { true }
}
