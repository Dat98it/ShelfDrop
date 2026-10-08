import AppKit
import Foundation
import Testing
@testable import ShelfDrop

struct ShelfSizeStoreTests {
    private let isolated = IsolatedDefaults()
    private var store: ShelfSizeStore { ShelfSizeStore(defaults: isolated.defaults) }

    @Test func returnsNilWhenNothingWasSaved() {
        #expect(store.load() == nil)
    }

    @Test func roundTripsASize() {
        store.save(NSSize(width: 480, height: 330))
        #expect(store.load() == NSSize(width: 480, height: 330))
    }

    @Test func raisesTooSmallValuesToTheMinimum() {
        store.save(NSSize(width: 10, height: 10))
        #expect(store.load() == NSSize(width: ShelfView.minimumSize.width, height: ShelfView.minimumSize.height))
    }

    @Test func capsAbsurdlyLargeValues() {
        store.save(NSSize(width: 1e9, height: 1e9))
        let loaded = store.load()
        #expect(loaded == NSSize(width: ShelfSizeStore.maximumStoredLength, height: ShelfSizeStore.maximumStoredLength))
    }

    @Test func ignoresValuesOfTheWrongType() {
        isolated.defaults.set("wide", forKey: "shelfWidth")
        isolated.defaults.set("tall", forKey: "shelfHeight")
        #expect(store.load() == nil)
    }

    @Test func ignoresAHalfWrittenPreference() {
        isolated.defaults.set(400.0, forKey: "shelfWidth")  // no height
        #expect(store.load() == nil)
    }
}
