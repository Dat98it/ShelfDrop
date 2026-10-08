import AppKit

/// Remembers where the user last put the shelf, across launches.
///
/// The anchor is the panel's top-left corner in global screen coordinates (y up), because that
/// is the corner that stays fixed when the shelf is resized.
struct ShelfPositionStore {
    private let defaults: UserDefaults
    private let xKey = "shelfTopLeftX"
    private let yKey = "shelfTopLeftY"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    /// The stored top-left corner, or nil if the user never moved the shelf (or the value is unusable).
    func load() -> NSPoint? {
        guard let x = defaults.object(forKey: xKey) as? Double,
              let y = defaults.object(forKey: yKey) as? Double,
              x.isFinite, y.isFinite, abs(x) < 1_000_000, abs(y) < 1_000_000 else { return nil }
        return NSPoint(x: x, y: y)
    }

    func save(_ topLeft: NSPoint) {
        defaults.set(Double(topLeft.x), forKey: xKey)
        defaults.set(Double(topLeft.y), forKey: yKey)
    }
}
