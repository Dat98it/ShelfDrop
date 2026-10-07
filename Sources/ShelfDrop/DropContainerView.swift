import AppKit

/// Root view of the shelf panel. It wraps the SwiftUI content and acts as the drag destination
/// for the whole panel: AppKit walks up from the view under the cursor until it finds a view
/// that registered for dragged types, so SwiftUI subviews need no drop handling of their own.
@MainActor
final class DropContainerView: NSView {
    var onTargetChange: ((Bool) -> Void)?
    var onReceive: (([ShelfItem]) -> Void)?

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        registerForDraggedTypes(PasteboardImporter.acceptedTypes)
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        onTargetChange?(true)
        return .copy
    }

    override func draggingExited(_ sender: NSDraggingInfo?) {
        onTargetChange?(false)
    }

    override func draggingEnded(_ sender: NSDraggingInfo) {
        onTargetChange?(false)
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        onTargetChange?(false)
        PasteboardImporter.importItems(from: sender.draggingPasteboard) { [weak self] items in
            self?.onReceive?(items)
        }
        return true
    }
}
