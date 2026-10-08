import AppKit

/// The menu bar icon: the shelf itself in miniature, a rounded panel with a title bar and a row
/// of three tiles, echoing the app's own window.
///
/// It is drawn in code as a *template* image, so macOS tints it for light and dark menu bars and
/// for the highlighted state, and it stays sharp at any display scale. The system's "tray with an
/// arrow" symbol that this replaces looked too much like other drag-and-drop shelf apps.
enum StatusIcon {
    static let size = NSSize(width: 18, height: 18)

    // Geometry, in points, in the 18x18 canvas. The panel is centred in it.
    static let panel = NSRect(x: 1.0, y: 3.2, width: 16.0, height: 11.6)
    static let lineWidth: CGFloat = 1.4
    static let tileCount = 3

    static func make() -> NSImage {
        let image = NSImage(size: size, flipped: false) { _ in
            draw()
            return true
        }
        image.isTemplate = true
        image.accessibilityDescription = "ShelfDrop"
        return image
    }

    /// Draws in black on a transparent background; only the alpha matters for a template image.
    static func draw() {
        NSColor.black.setStroke()
        NSColor.black.setFill()

        // The shelf's outline, inset by half the line so the stroke stays inside `panel`.
        let outline = NSBezierPath(
            roundedRect: panel.insetBy(dx: lineWidth / 2, dy: lineWidth / 2),
            xRadius: 3.2, yRadius: 3.2
        )
        outline.lineWidth = lineWidth
        outline.stroke()

        // Content area: the same margin on the left and right, so the tiles sit centred.
        let left = panel.minX + 2.6
        let width = panel.width - 5.2

        // Title bar, left-aligned with the first tile.
        NSBezierPath(
            roundedRect: NSRect(x: left, y: panel.maxY - 4.0, width: 5.2, height: 1.4),
            xRadius: 0.7, yRadius: 0.7
        ).fill()

        // A row of tiles under it.
        let gap: CGFloat = 1.2
        let tileWidth = (width - gap * CGFloat(tileCount - 1)) / CGFloat(tileCount)
        for index in 0..<tileCount {
            let tile = NSRect(x: left + CGFloat(index) * (tileWidth + gap), y: panel.minY + 2.6, width: tileWidth, height: 3.6)
            NSBezierPath(roundedRect: tile, xRadius: 0.9, yRadius: 0.9).fill()
        }
    }
}
