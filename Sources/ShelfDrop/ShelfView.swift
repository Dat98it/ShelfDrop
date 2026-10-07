import SwiftUI

struct ShelfView: View {
    @ObservedObject var model: ShelfModel
    let onClose: () -> Void
    /// Opens the system share menu for the given items.
    let onShare: ([ShelfItem]) -> Void

    private let columns = [GridItem(.adaptive(minimum: 76, maximum: 90), spacing: 8)]

    var body: some View {
        VStack(spacing: 8) {
            header
            if model.items.isEmpty {
                emptyState
            } else {
                ScrollView {
                    LazyVGrid(columns: columns, spacing: 8) {
                        ForEach(model.items) { item in
                            ShelfItemView(
                                item: item,
                                onRemove: { model.remove(item.id) },
                                onShare: { onShare([item]) }
                            )
                        }
                    }
                    .padding(.vertical, 2)
                }
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(WindowDragArea())
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .strokeBorder(Color.accentColor, lineWidth: model.isDropTargeted ? 3 : 0)
        }
        .overlay(alignment: .bottomTrailing) {
            ResizeGrip()
                .frame(width: ShelfView.gripSize, height: ShelfView.gripSize)
                .padding(4)
                .help("Drag to resize")
        }
    }

    /// Size the panel opens with; the user can resize it from there.
    static let defaultSize = CGSize(width: 340, height: 260)
    static let minimumSize = CGSize(width: 310, height: 180)
    static let gripSize: CGFloat = 18

    private var header: some View {
        HStack(spacing: 8) {
            Text(model.items.isEmpty ? "Shelf" : "Shelf · \(model.items.count)")
                .font(.headline)
                .lineLimit(1)
                .fixedSize()
            Spacer(minLength: 0)
            if !model.items.isEmpty {
                shareAllButton
                dragAllHandle
                Button("Clear") { model.clear() }
                    .buttonStyle(.borderless)
                    .fixedSize()
            }
            Button(action: onClose) {
                Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
            .help("Close shelf and remove its items")
        }
    }

    /// Icon only: the header has to stay readable at the shelf's minimum width.
    private var shareAllButton: some View {
        Button { onShare(model.items) } label: {
            Image(systemName: "square.and.arrow.up")
                .font(.callout)
                .padding(.horizontal, 8)
                .padding(.vertical, 3)
                .background(.quaternary, in: Capsule())
        }
        .buttonStyle(.plain)
        .disabled(!model.items.contains { $0.sharingItem != nil })
        .help("Share all items…")
    }

    private var dragAllHandle: some View {
        Label("Drag all", systemImage: "square.stack.3d.up")
            .font(.callout)
            // Never wrap: SwiftUI will squeeze a label before it uses up free space in the row.
            .lineLimit(1)
            .fixedSize(horizontal: true, vertical: false)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background(.quaternary, in: Capsule())
            .overlay(DragSourceView(items: { model.items }))
            .help("Drag every item out at once")
    }

    private var emptyState: some View {
        VStack(spacing: 6) {
            Image(systemName: "tray.and.arrow.down")
                .font(.system(size: 34))
                .foregroundStyle(.secondary)
            Text("Drop files, images, links or text here")
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct ShelfItemView: View {
    let item: ShelfItem
    let onRemove: () -> Void
    let onShare: () -> Void

    var body: some View {
        VStack(spacing: 4) {
            Image(nsImage: item.thumbnail)
                .resizable()
                .scaledToFit()
                .frame(width: 56, height: 56)
                .opacity(item.isMissing ? 0.35 : 1)
                .overlay(DragSourceView(items: { [item] }))
            Text(item.isMissing ? "\(item.title) (missing)" : item.title)
                .font(.caption2)
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .frame(maxWidth: .infinity)
        }
        .padding(.vertical, 4)
        .overlay(alignment: .topLeading) {
            Button(action: onShare) {
                Image(systemName: "square.and.arrow.up.circle.fill")
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
            .disabled(item.isMissing)
            .help("Share…")
        }
        .overlay(alignment: .topTrailing) {
            Button(action: onRemove) {
                Image(systemName: "xmark.circle.fill")
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
            .help("Remove from shelf")
        }
    }
}
