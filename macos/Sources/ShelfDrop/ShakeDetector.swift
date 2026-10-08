import CoreGraphics
import Foundation

/// Detects a "shake": quick back-and-forth movement along the X axis.
///
/// Feed it mouse positions while a drag is in progress. A stroke only counts
/// once it travels at least `minStrokeDistance`, which filters out hand tremor.
/// A shake is reported when `requiredReversals` direction changes happen
/// within `window` seconds.
struct ShakeDetector {
    struct Configuration {
        var minStrokeDistance: CGFloat = 40
        var requiredReversals = 3
        var window: TimeInterval = 0.8
    }

    var configuration: Configuration

    /// -1 = moving left, +1 = moving right, 0 = no stroke established yet.
    private var direction = 0
    /// Anchor before a stroke exists, then the furthest x reached in the current stroke.
    private var extremeX: CGFloat = 0
    private var hasAnchor = false
    private var reversalTimes: [TimeInterval] = []

    init(configuration: Configuration = Configuration()) {
        self.configuration = configuration
    }

    mutating func reset() {
        direction = 0
        extremeX = 0
        hasAnchor = false
        reversalTimes.removeAll()
    }

    /// Returns true when this sample completes a shake. State is reset afterwards
    /// so one shake triggers once.
    mutating func feed(x: CGFloat, at time: TimeInterval) -> Bool {
        guard hasAnchor else {
            extremeX = x
            hasAnchor = true
            return false
        }

        if direction == 0 {
            let delta = x - extremeX
            if abs(delta) >= configuration.minStrokeDistance {
                direction = delta > 0 ? 1 : -1
                extremeX = x
            }
            return false
        }

        let dir = CGFloat(direction)
        if x * dir > extremeX * dir {
            // Still travelling the same way: extend the stroke.
            extremeX = x
            return false
        }

        // Moving against the stroke: it is a reversal once it has gone far enough back.
        if (extremeX - x) * dir >= configuration.minStrokeDistance {
            direction = -direction
            extremeX = x
            reversalTimes.append(time)
            reversalTimes.removeAll { time - $0 > configuration.window }

            if reversalTimes.count >= configuration.requiredReversals {
                reset()
                return true
            }
        }
        return false
    }
}
