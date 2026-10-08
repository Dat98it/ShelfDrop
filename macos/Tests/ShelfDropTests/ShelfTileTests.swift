import AppKit
import Foundation
import Testing
@testable import ShelfDrop

@MainActor
struct ShelfTileTests {
    private let isolated = IsolatedDefaults()

    private func makeShownController(items: [ShelfItem]) -> ShelfController {
        _ = NSApplication.shared
        let controller = ShelfController(defaults: isolated.defaults, sharePresenter: FakeSharePresenter())
        controller.model.add(items)
        controller.show(near: NSPoint(x: 700, y: 700))
        PanelProbe.settle(controller.panel)
        return controller
    }

    @Test func hoveringATileLetsYouRemoveIt() throws {
        let controller = makeShownController(items: [.text("one"), .text("two"), .text("three")])
        let cards = PanelProbe.cardRects(in: controller.panel)
        try #require(cards.count == 3)

        PanelProbe.hover(card: cards[1], in: controller.panel)
        PanelProbe.click(PanelProbe.removeButtonCentre(ofCard: cards[1]), in: controller.panel)

        let remaining = controller.model.items.map(\.title)
        #expect(remaining == ["one", "three"])
    }

    @Test func theRemoveButtonIsInertUntilTheMouseIsOverTheTile() throws {
        let controller = makeShownController(items: [.text("one"), .text("two")])
        let cards = PanelProbe.cardRects(in: controller.panel)
        try #require(cards.count == 2)

        PanelProbe.click(PanelProbe.removeButtonCentre(ofCard: cards[0]), in: controller.panel)

        #expect(controller.model.items.count == 2)
    }

    @Test func theCloseButtonClosesAndClearsTheShelf() throws {
        let controller = makeShownController(items: [.text("one"), .text("two")])
        let buttons = PanelProbe.headerButtonRects(in: controller.panel)
        try #require(buttons.count == 3)

        PanelProbe.click(PanelProbe.centre(of: buttons[2]), in: controller.panel)

        #expect(controller.model.items.isEmpty)
        #expect(!controller.isShelfVisible)
    }

    @Test func theClearButtonEmptiesTheShelfButLeavesItOpen() throws {
        let controller = makeShownController(items: [.text("one"), .text("two")])
        let buttons = PanelProbe.headerButtonRects(in: controller.panel)
        try #require(buttons.count == 3)

        PanelProbe.click(PanelProbe.centre(of: buttons[1]), in: controller.panel)

        #expect(controller.model.items.isEmpty)
        #expect(controller.isShelfVisible)
    }
}
