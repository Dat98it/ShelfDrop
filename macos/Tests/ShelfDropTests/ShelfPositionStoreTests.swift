import AppKit
import Foundation
import Testing
@testable import ShelfDrop

struct ShelfPositionStoreTests {
    private let isolated = IsolatedDefaults()
    private var store: ShelfPositionStore { ShelfPositionStore(defaults: isolated.defaults) }

    @Test func returnsNilWhenNothingWasSaved() {
        #expect(store.load() == nil)
    }

    @Test func roundTripsAPoint() {
        store.save(NSPoint(x: 312.5, y: 640))
        #expect(store.load() == NSPoint(x: 312.5, y: 640))
    }

    @Test func keepsNegativeCoordinatesFromDisplaysLeftOfOrBelowTheMainOne() {
        store.save(NSPoint(x: -1800, y: -200))
        #expect(store.load() == NSPoint(x: -1800, y: -200))
    }

    @Test func ignoresAbsurdValues() {
        store.save(NSPoint(x: 1e12, y: 5))
        #expect(store.load() == nil)
    }

    @Test func ignoresValuesOfTheWrongType() {
        isolated.defaults.set("left", forKey: "shelfTopLeftX")
        isolated.defaults.set("top", forKey: "shelfTopLeftY")
        #expect(store.load() == nil)
    }

    @Test func ignoresAHalfWrittenPreference() {
        isolated.defaults.set(400.0, forKey: "shelfTopLeftX")  // no y
        #expect(store.load() == nil)
    }
}
