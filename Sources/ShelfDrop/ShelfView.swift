import SwiftUI

struct ShelfView: View {
    @ObservedObject var model: ShelfModel
    let onClose: () -> Void
    /// Opens the system share menu for the given items.
    let onShare: ([ShelfItem]) -> Void

    private let columns = [GridItem(.adaptive(minimum: 92, maximum: 112), spacing: 8)]

    /// Size the panel opens with; the user can resize it from there.
    static let defaultSize = CGSize(width: 360, height: 300)
    static let minimumSize = CGSize(width: 340, height: 190)
    static let gripSize: CGFloat = 18

    var body: some View {
        VStack(spacing: 10) {
            header
            if model.items.isEmpty {
                EmptyDropZone(isTargeted: model.isDropTargeted)
            } else {
                itemGrid
            }
        }
        .padding(ShelfStyle.padding)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(WindowDragArea())
        .background(chrome)
        .overlay(alignment: .bottomTrailing) {
            ResizeGrip()
                .frame(width: ShelfView.gripSize, height: ShelfView.gripSize)
                .padding(4)
                .help("Drag to resize")
        }
        .animation(.easeOut(duration: 0.15), value: model.isDropTargeted)
    }

    // MARK: - Window chrome

    /// The panel's body: frosted material with a hairline edge, and an accent wash and outline
    /// while something is being dragged over it.
    private var chrome: some View {
        let shape = RoundedRectangle(cornerRadius: ShelfStyle.panelRadius, style: .continuous)
        let highlighted = model.isDropTargeted && !model.items.isEmpty
        return shape
            .fill(.regularMaterial)
            .overlay(shape.fill(Color.accentColor.opacity(highlighted ? 0.10 : 0)))
            .overlay(shape.strokeBorder(Color.primary.opacity(0.10), lineWidth: 0.5))
            .overlay(shape.strokeBorder(Color.accentColor, lineWidth: 2).opacity(highlighted ? 1 : 0))
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 6) {
            VStack(alignment: .leading, spacing: 1) {
                Text("Shelf")
                    .font(.system(size: 14, weight: .semibold))
                Text(ShelfItem.summary(of: model.items))
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
            }
            .lineLimit(1)
            // First call on the free space, so the subtitle only truncates when it really must;
            // the controls on the right are fixed-size and keep theirs either way.
            .layoutPriority(1)

            Spacer(minLength: 8)

            if !model.items.isEmpty {
                dragAllPill

                Button { onShare(model.items) } label: {
                    Image(systemName: "square.and.arrow.up").frame(width: ShelfStyle.controlHeight)
                }
                .buttonStyle(ShelfButtonStyle())
                .disabled(!model.items.contains { $0.sharingItem != nil })
                .help("Share all items…")
                .accessibilityLabel("Share all items")

                Button { model.clear() } label: {
                    Text("Clear").padding(.horizontal, 9)
                }
                .buttonStyle(ShelfButtonStyle())
                .fixedSize()
                .help("Remove every item from the shelf (your files are not touched)")
                .accessibilityLabel("Clear shelf")
            }

            Button(action: onClose) {
                Image(systemName: "xmark").frame(width: ShelfStyle.controlHeight)
            }
            .buttonStyle(ShelfButtonStyle())
            .help("Close shelf and remove its items")
            .accessibilityLabel("Close shelf")
        }
    }

    /// Not a button: grab it and drag to take every item out at once.
    private var dragAllPill: some View {
        Label("Drag all", systemImage: "square.stack.3d.up.fill")
            .font(.system(size: 12, weight: .semibold))
            // Never wrap: SwiftUI will squeeze a label before it uses up free space in the row.
            .lineLimit(1)
            .fixedSize(horizontal: true, vertical: false)
            .padding(.horizontal, 10)
            .frame(height: ShelfStyle.controlHeight)
            .foregroundStyle(Color.accentColor)
            .background(Capsule().fill(Color.accentColor.opacity(0.16)))
            .overlay(DragSourceView(items: { model.items }))
            .help("Drag every item out at once")
            .accessibilityLabel("Drag all items")
    }

    // MARK: - Items

    private var itemGrid: some View {
        ScrollView {
            LazyVGrid(columns: columns, spacing: 8) {
                ForEach(model.items) { item in
                    ShelfItemView(
                        item: item,
                        onRemove: { model.remove(item.id) },
                        onShare: { onShare([item]) }
                    )
                    .transition(.scale(scale: 0.88).combined(with: .opacity))
                }
            }
            .padding(.vertical, 1)
            .animation(.snappy(duration: 0.22), value: model.items.map(\.id))
        }
        // Hidden on purpose: with a legacy (always visible) scroller SwiftUI lays the grid out at
        // the full width but clips it by the scroller's width, cutting off the last column.
        // Scrolling still works with the wheel and trackpad, and a half-visible row hints at it.
        .scrollIndicators(.hidden)
    }
}

// MARK: - Empty state

/// The drop zone shown while the shelf has nothing on it: it announces itself with a dashed
/// outline, and turns solid and accent-coloured when a drag is over it.
struct EmptyDropZone: View {
    let isTargeted: Bool

    var body: some View {
        let shape = RoundedRectangle(cornerRadius: 14, style: .continuous)
        ZStack {
            shape.fill(Color.accentColor.opacity(isTargeted ? 0.08 : 0))
            shape.strokeBorder(
                isTargeted ? Color.accentColor : Color.primary.opacity(0.22),
                style: StrokeStyle(lineWidth: isTargeted ? 2 : 1.2, dash: isTargeted ? [] : [6, 5])
            )
            // Drop the tip first when the shelf is short.
            ViewThatFits(in: .vertical) {
                content(showingTip: true)
                content(showingTip: false)
            }
            .padding(10)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func content(showingTip: Bool) -> some View {
        VStack(spacing: 10) {
            ZStack {
                Circle().fill(Color.primary.opacity(0.07)).frame(width: 44, height: 44)
                Image(systemName: isTargeted ? "arrow.down.circle.fill" : "tray.and.arrow.down")
                    .font(.system(size: 20, weight: .medium))
                    .foregroundStyle(isTargeted ? Color.accentColor : Color.secondary)
            }
            VStack(spacing: 2) {
                Text(isTargeted ? "Release to add" : "Drop anything here")
                    .font(.system(size: 13, weight: .semibold))
                Text("Files, images, links or text")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
            }
            if showingTip {
                Text("Tip: shake your mouse while dragging to open the shelf")
                    .font(.system(size: 10))
                    .foregroundStyle(.tertiary)
                    .multilineTextAlignment(.center)
            }
        }
        .frame(maxWidth: 290)
    }
}

// MARK: - Tile

struct ShelfItemView: View {
    let item: ShelfItem
    let onRemove: () -> Void
    let onShare: () -> Void

    @StateObject private var hover = HoverState()
    private var hovering: Bool { hover.isHovering }

    var body: some View {
        VStack(spacing: 6) {
            ItemThumbnail(item: item)
                .frame(height: 48)
            VStack(spacing: 1) {
                Text(item.title)
                    .font(.system(size: 11, weight: .medium))
                    .lineLimit(1)
                    .truncationMode(.middle)
                Text(item.isMissing ? "File not found" : item.detail)
                    .font(.system(size: 10))
                    .foregroundStyle(item.isMissing ? Color.orange : Color.secondary)
                    .lineLimit(1)
            }
            .frame(maxWidth: .infinity)
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 8)
        .frame(maxWidth: .infinity)
        .background(card)
        // The whole card is the drag handle; the buttons below sit on top of it.
        .overlay(DragSourceView(items: { [item] }))
        .overlay(alignment: .topLeading) {
            actionButton("square.and.arrow.up", help: "Share…", label: "Share item", disabled: item.isMissing, action: onShare)
        }
        .overlay(alignment: .topTrailing) {
            actionButton("xmark", help: "Remove from shelf", label: "Remove item", disabled: false, action: onRemove)
        }
        .background(HoverReader { hover.isHovering = $0 })
        .animation(.easeOut(duration: 0.12), value: hovering)
        .contextMenu {
            Button("Share…", action: onShare).disabled(item.isMissing)
            Button("Remove from Shelf", action: onRemove)
        }
    }

    private var card: some View {
        RoundedRectangle(cornerRadius: ShelfStyle.cardRadius, style: .continuous)
            .fill(Color.primary.opacity(hovering ? 0.10 : 0.05))
            .overlay(
                RoundedRectangle(cornerRadius: ShelfStyle.cardRadius, style: .continuous)
                    .strokeBorder(Color.primary.opacity(hovering ? 0.16 : 0.08), lineWidth: 0.5)
            )
    }

    /// Small round button in a corner of the card. Only there (and only clickable) while the
    /// mouse is over the card, so the grid stays calm; the context menu offers the same actions.
    private func actionButton(
        _ symbol: String, help: String, label: String, disabled: Bool, action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 9, weight: .bold))
                .foregroundStyle(.primary)
                .frame(width: 20, height: 20)
                .background(Circle().fill(.regularMaterial))
                .overlay(Circle().strokeBorder(Color.primary.opacity(0.15), lineWidth: 0.5))
        }
        .buttonStyle(.plain)
        .disabled(disabled)
        .help(help)
        .accessibilityLabel(label)
        .padding(4)
        .opacity(hovering ? 1 : 0)
        .allowsHitTesting(hovering)
    }
}

// MARK: - Thumbnail

private struct ItemThumbnail: View {
    let item: ShelfItem

    var body: some View {
        switch item.kind {
        case .file:
            Image(nsImage: item.thumbnail)
                .resizable()
                .scaledToFit()
                .clipShape(RoundedRectangle(cornerRadius: item.isImageFile ? 6 : 0, style: .continuous))
                .overlay {
                    if item.isImageFile {
                        RoundedRectangle(cornerRadius: 6, style: .continuous)
                            .strokeBorder(Color.primary.opacity(0.18), lineWidth: 0.5)
                    }
                }
                .shadow(color: .black.opacity(item.isImageFile ? 0.25 : 0), radius: 2, y: 1)
                .frame(maxWidth: 76, maxHeight: 48)
                .opacity(item.isMissing ? 0.35 : 1)
        case .link:
            GlyphTile(symbol: "link", tint: .blue)
        case .text:
            GlyphTile(symbol: "text.alignleft", tint: .orange)
        }
    }
}

/// A rounded, tinted square with a white symbol: the thumbnail for items that have no picture.
private struct GlyphTile: View {
    let symbol: String
    let tint: Color

    var body: some View {
        RoundedRectangle(cornerRadius: 11, style: .continuous)
            .fill(tint.gradient)
            .frame(width: 46, height: 46)
            .overlay(
                Image(systemName: symbol)
                    .font(.system(size: 20, weight: .semibold))
                    .foregroundStyle(.white)
            )
            .shadow(color: tint.opacity(0.35), radius: 3, y: 1)
    }
}
