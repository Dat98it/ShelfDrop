import AppKit
import Testing
@testable import ShelfDrop

@MainActor
struct StatusIconTests {
    /// The icon rendered at `scale` pixels per point, as RGBA.
    private func render(scale: Int) -> NSBitmapImageRep {
        _ = NSApplication.shared
        let pixels = Int(StatusIcon.size.width) * scale
        let rep = NSBitmapImageRep(
            bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4,
            hasAlpha: true, isPlanar: false, colorSpaceName: .calibratedRGB, bytesPerRow: 0, bitsPerPixel: 0
        )!
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        StatusIcon.make().draw(in: NSRect(x: 0, y: 0, width: pixels, height: pixels))
        NSGraphicsContext.restoreGraphicsState()
        return rep
    }

    private func alpha(_ rep: NSBitmapImageRep, _ x: Int, _ y: Int) -> CGFloat {
        rep.colorAt(x: x, y: y)?.alphaComponent ?? 0
    }

    /// Margins, in pixels, between the drawn pixels and the edges of the canvas.
    private func margins(_ rep: NSBitmapImageRep) -> (left: Int, right: Int, top: Int, bottom: Int)? {
        var minX = Int.max, maxX = -1, minY = Int.max, maxY = -1
        for y in 0..<rep.pixelsHigh {
            for x in 0..<rep.pixelsWide where alpha(rep, x, y) > 0.1 {
                minX = min(minX, x); maxX = max(maxX, x); minY = min(minY, y); maxY = max(maxY, y)
            }
        }
        guard maxX >= 0 else { return nil }
        return (minX, rep.pixelsWide - 1 - maxX, minY, rep.pixelsHigh - 1 - maxY)
    }

    @Test func isATemplateImageSoMacOSTintsItForLightAndDarkMenuBars() {
        let icon = StatusIcon.make()
        #expect(icon.isTemplate)
        #expect(icon.size == NSSize(width: 18, height: 18))
        #expect(icon.accessibilityDescription == "ShelfDrop")
    }

    @Test func drawsSomethingAtEveryScreenScale() throws {
        for scale in [1, 2, 3] {
            let rep = render(scale: scale)
            var inked = 0
            for y in 0..<rep.pixelsHigh {
                for x in 0..<rep.pixelsWide where alpha(rep, x, y) > 0.5 { inked += 1 }
            }
            let coverage = Double(inked) / Double(rep.pixelsWide * rep.pixelsHigh)
            // Not blank and not a solid block: an outline with a few tiles covers a fair share of the canvas.
            #expect((0.12...0.45).contains(coverage), "\(scale)x: \(Int(coverage * 100))% of the pixels are drawn")
        }
    }

    @Test func isBlackOnTransparentBecauseOnlyTheAlphaOfATemplateMatters() throws {
        let rep = render(scale: 2)
        for y in 0..<rep.pixelsHigh {
            for x in 0..<rep.pixelsWide {
                let color = try #require(rep.colorAt(x: x, y: y))
                if color.alphaComponent > 0.5 {
                    #expect(color.redComponent < 0.1 && color.greenComponent < 0.1 && color.blueComponent < 0.1,
                            "pixel (\(x), \(y)) is not black")
                }
            }
        }
    }

    @Test func isCentredInItsCanvasSoItSitsLevelWithTheOtherMenuBarIcons() throws {
        for scale in [1, 2] {
            let m = try #require(margins(render(scale: scale)))
            // Within one pixel: odd widths cannot be split evenly.
            #expect(abs(m.left - m.right) <= 1, "\(scale)x: left margin \(m.left)px, right \(m.right)px")
            #expect(abs(m.top - m.bottom) <= 1, "\(scale)x: top margin \(m.top)px, bottom \(m.bottom)px")
        }
    }

    @Test func staysInsideTheMenuBarHeight() throws {
        let m = try #require(margins(render(scale: 2)))
        // The glyph must not touch the edges, where the menu bar would crop it.
        #expect(m.left >= 1 && m.right >= 1 && m.top >= 1 && m.bottom >= 1)
    }
}
