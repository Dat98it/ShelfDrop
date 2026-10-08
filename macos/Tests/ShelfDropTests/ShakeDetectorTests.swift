import Foundation
import Testing
@testable import ShelfDrop

struct ShakeDetectorTests {
    /// Feeds x positions at a fixed interval and reports whether any sample triggered a shake.
    private func run(_ xs: [CGFloat], interval: TimeInterval, detector: inout ShakeDetector) -> Bool {
        var triggered = false
        for (i, x) in xs.enumerated() {
            if detector.feed(x: x, at: Double(i) * interval) { triggered = true }
        }
        return triggered
    }

    @Test func fastBackAndForthTriggers() {
        var detector = ShakeDetector()
        // 100pt strokes every 0.1s: right, left, right, left.
        #expect(run([0, 100, 0, 100, 0], interval: 0.1, detector: &detector))
    }

    @Test func straightLineDoesNotTrigger() {
        var detector = ShakeDetector()
        let xs = stride(from: CGFloat(0), through: 1000, by: 10).map { $0 }
        #expect(!run(xs, interval: 0.016, detector: &detector))
    }

    @Test func slowBackAndForthDoesNotTrigger() {
        var detector = ShakeDetector()
        // Same strokes as the fast case but 2s apart: reversals fall outside the window.
        #expect(!run([0, 100, 0, 100, 0], interval: 2.0, detector: &detector))
    }

    @Test func smallJitterDoesNotTrigger() {
        var detector = ShakeDetector()
        // 5pt wobble is below minStrokeDistance, so no stroke is ever established.
        let xs: [CGFloat] = (0..<40).map { $0 % 2 == 0 ? 0 : 5 }
        #expect(!run(xs, interval: 0.02, detector: &detector))
    }

    @Test func triggersOnlyOncePerShake() {
        var detector = ShakeDetector()
        var count = 0
        let xs: [CGFloat] = [0, 100, 0, 100, 0, 100, 0]
        for (i, x) in xs.enumerated() {
            if detector.feed(x: x, at: Double(i) * 0.1) { count += 1 }
        }
        // 3 reversals trigger at index 4; the remaining samples only start a new, incomplete shake.
        #expect(count == 1)
    }

    @Test func resetClearsProgress() {
        var detector = ShakeDetector()
        // Two reversals so far: one more would normally trigger.
        for (i, x) in [CGFloat(0), 100, 0, 100].enumerated() {
            let triggered = detector.feed(x: x, at: Double(i) * 0.1)
            #expect(!triggered)
        }
        detector.reset()
        // After reset, a single reversal must not complete the shake.
        #expect(!run([0, 100, 0], interval: 0.1, detector: &detector))
    }
}
