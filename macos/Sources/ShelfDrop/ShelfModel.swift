import Foundation

@MainActor
final class ShelfModel: ObservableObject {
    @Published private(set) var items: [ShelfItem] = []
    /// True while a drag is hovering over the shelf, used to highlight it.
    @Published var isDropTargeted = false

    func add(_ newItems: [ShelfItem]) {
        items.append(contentsOf: newItems)
    }

    func remove(_ id: ShelfItem.ID) {
        items.removeAll { $0.id == id }
    }

    func clear() {
        items.removeAll()
    }
}
