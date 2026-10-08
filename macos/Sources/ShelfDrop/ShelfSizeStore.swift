import AppKit

/// Remembers the size the user gave the shelf, across launches.
struct ShelfSizeStore {
    /// Guards against a corrupted preference producing an absurd window.
    static let maximumStoredLength: CGFloat = 10_000

    private let defaults: UserDefaults
    private let widthKey = "shelfWidth"
    private let heightKey = "shelfHeight"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    /// The stored size, or nil if the user has never resized the shelf (or the value is unusable).
    func load() -> NSSize? {
        guard let width = defaults.object(forKey: widthKey) as? Double,
              let height = defaults.object(forKey: heightKey) as? Double,
              width.isFinite, height.isFinite else { return nil }
        let minimum = ShelfView.minimumSize
        return NSSize(
            width: min(max(CGFloat(width), minimum.width), Self.maximumStoredLength),
            height: min(max(CGFloat(height), minimum.height), Self.maximumStoredLength)
        )
    }

    func save(_ size: NSSize) {
        defaults.set(Double(size.width), forKey: widthKey)
        defaults.set(Double(size.height), forKey: heightKey)
    }
}
