import AppKit
import Foundation
import Testing
@testable import ShelfDrop

/// Stands in for the system share menu: records what it was asked to present and answers
/// immediately as if the user had picked (or dismissed) a service.
@MainActor
final class FakeSharePresenter: SharePresenting {
    private(set) var presented: [[Any]] = []
    var userPicksAService = true

    func present(items: [Any], relativeTo rect: NSRect, of view: NSView, onChoose: @escaping (Bool) -> Void) {
        presented.append(items)
        onChoose(userPicksAService)
    }
}

@MainActor
struct ShelfShareTests {
    private let isolated = IsolatedDefaults()
    private let presenter = FakeSharePresenter()

    private func makeController() -> ShelfController {
        _ = NSApplication.shared
        return ShelfController(defaults: isolated.defaults, sharePresenter: presenter)
    }

    private func makeTempCopy(named name: String = "copy.txt") throws -> URL {
        let url = TempStorage.makeDirectory().appendingPathComponent(name)
        try "temp".write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    private func makeRealFile() throws -> URL {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("shelfdrop-share-\(UUID().uuidString).txt")
        try "mine".write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    // MARK: - The buttons

    private func makeShownController(items: [ShelfItem]) -> ShelfController {
        let controller = makeController()
        controller.model.add(items)
        controller.show(near: NSPoint(x: 700, y: 700))
        PanelProbe.settle(controller.panel)
        return controller
    }

    @Test func clickingTheHeaderShareButtonSharesEveryItem() throws {
        let controller = makeShownController(items: [.text("one"), .text("two"), .text("three")])
        // With items on the shelf the header has Share, Clear and Close, left to right.
        let buttons = PanelProbe.headerButtonRects(in: controller.panel)
        try #require(buttons.count == 3)

        PanelProbe.click(PanelProbe.centre(of: buttons[0]), in: controller.panel)

        #expect(presenter.presented.count == 1)
        #expect(presenter.presented.first?.count == 3)
    }

    @Test func clickingATilesShareButtonSharesOnlyThatItem() throws {
        let controller = makeShownController(items: [.text("one"), .text("two"), .text("three")])
        let cards = PanelProbe.cardRects(in: controller.panel)
        try #require(cards.count == 3)

        // The corner buttons only exist while the mouse is over the card.
        PanelProbe.hover(card: cards[0], in: controller.panel)
        PanelProbe.click(PanelProbe.shareButtonCentre(ofCard: cards[0]), in: controller.panel)

        #expect(presenter.presented.count == 1)
        let items = presenter.presented.first ?? []
        #expect(items.count == 1)
        #expect((items.first as? NSString) == "one")
    }

    @Test func aTilesShareButtonDoesNothingUntilTheMouseIsOverIt() throws {
        let controller = makeShownController(items: [.text("one"), .text("two")])
        let cards = PanelProbe.cardRects(in: controller.panel)
        try #require(cards.count == 2)

        // Same spot as above, but without hovering: it must not act like a hidden button.
        PanelProbe.click(PanelProbe.shareButtonCentre(ofCard: cards[0]), in: controller.panel)

        #expect(presenter.presented.isEmpty)
    }

    // MARK: - What gets shared

    @Test func eachKindOfItemBecomesTheRightSharingValue() throws {
        let file = try makeRealFile()
        defer { try? FileManager.default.removeItem(at: file) }
        let link = URL(string: "https://example.com/page")!

        #expect((ShelfItem.file(file).sharingItem as? NSURL) == file as NSURL)
        #expect((ShelfItem.link(link).sharingItem as? NSURL) == link as NSURL)
        #expect((ShelfItem.text("hello").sharingItem as? NSString) == "hello")
    }

    @Test func aFileThatNoLongerExistsCannotBeShared() {
        let gone = URL(fileURLWithPath: "/tmp/shelfdrop-definitely-not-here-\(UUID().uuidString).txt")
        #expect(ShelfItem.file(gone).sharingItem == nil)
    }

    @Test func shareHandsEveryShareableItemToThePresenterInOrder() throws {
        let file = try makeRealFile()
        defer { try? FileManager.default.removeItem(at: file) }
        let gone = URL(fileURLWithPath: "/tmp/shelfdrop-definitely-not-here-\(UUID().uuidString).txt")

        let controller = makeController()
        controller.share([
            .file(file),
            .file(gone),  // missing: must be left out
            .link(URL(string: "https://example.com")!),
            .text("note"),
        ])

        #expect(presenter.presented.count == 1)
        let items = presenter.presented[0]
        #expect(items.count == 3)
        #expect((items[0] as? NSURL) == file as NSURL)
        #expect((items[1] as? NSURL)?.absoluteString == "https://example.com")
        #expect((items[2] as? NSString) == "note")
    }

    @Test func sharingOnlyMissingFilesOrNothingDoesNotOpenTheMenu() {
        let gone = URL(fileURLWithPath: "/tmp/shelfdrop-definitely-not-here-\(UUID().uuidString).txt")
        let controller = makeController()

        controller.share([.file(gone)])
        controller.share([])

        #expect(presenter.presented.isEmpty)
    }

    // MARK: - Temp copies while a share may still be reading them

    @Test func aTempCopyThatWasSharedSurvivesClosingTheShelf() throws {
        let copy = try makeTempCopy()
        defer { try? FileManager.default.removeItem(at: copy.deletingLastPathComponent()) }
        let item = ShelfItem.file(copy)

        let controller = makeController()
        controller.model.add([item])
        controller.share([item])  // the fake picks a service
        controller.close()

        #expect(controller.model.items.isEmpty)
        // An AirDrop transfer can outlive the shelf, so the file must still be there.
        #expect(FileManager.default.fileExists(atPath: copy.path))
    }

    @Test func aTempCopyIsStillDeletedIfTheShareMenuWasDismissed() throws {
        let copy = try makeTempCopy()
        let item = ShelfItem.file(copy)
        presenter.userPicksAService = false

        let controller = makeController()
        controller.model.add([item])
        controller.share([item])
        controller.close()

        #expect(!FileManager.default.fileExists(atPath: copy.path))
    }

    @Test func onlyTheSharedTempCopiesAreKept() throws {
        let shared = try makeTempCopy(named: "shared.txt")
        let other = try makeTempCopy(named: "other.txt")
        defer { try? FileManager.default.removeItem(at: shared.deletingLastPathComponent()) }
        let sharedItem = ShelfItem.file(shared)

        let controller = makeController()
        controller.model.add([sharedItem, .file(other)])
        controller.share([sharedItem])  // share just one of the two
        controller.close()

        #expect(FileManager.default.fileExists(atPath: shared.path))
        #expect(!FileManager.default.fileExists(atPath: other.path))
    }
}
