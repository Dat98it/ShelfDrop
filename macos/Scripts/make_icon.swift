#!/usr/bin/env swift
// Draws the ShelfDrop app icon and packs it into an .icns file (no design tool or Xcode needed).
//
// Run from the macos/ folder.
//
// Usage: swift Scripts/make_icon.swift [output.icns] [preview.png]
//   output.icns  default: Resources/AppIcon.icns
//   preview.png  optional: also write the 1024x1024 master, for looking at the design
//
//        swift Scripts/make_icon.swift --windows [output.ico] [preview.png]
//   Same artwork for the Windows app, drawn larger in its canvas (Windows icons have no
//   built-in margin) and packed as a multi-size .ico. default: ../windows/src/ShelfDrop.App/Assets/ShelfDrop.ico
//
// The icon is drawn in a 1024-unit design space (Apple's macOS icon grid: an 824-unit body
// centred in the canvas, leaving room for the shadow) and re-rendered at each size, so small
// sizes are drawn crisply instead of being shrunk from the big one.
import AppKit

func rgb(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat, _ a: CGFloat = 1) -> NSColor {
    NSColor(srgbRed: r, green: g, blue: b, alpha: a)
}

/// A symbol from SF Symbols, tinted and centred in `rect`.
func drawSymbol(_ name: String, in rect: NSRect, color: NSColor) {
    guard let symbol = NSImage(systemSymbolName: name, accessibilityDescription: nil) else { return }
    let configuration = NSImage.SymbolConfiguration(pointSize: rect.height, weight: .semibold)
        .applying(NSImage.SymbolConfiguration(paletteColors: [color]))
    guard let tinted = symbol.withSymbolConfiguration(configuration) else { return }
    let size = tinted.size
    let scale = min(rect.width / size.width, rect.height / size.height)
    let fitted = NSSize(width: size.width * scale, height: size.height * scale)
    tinted.draw(
        in: NSRect(x: rect.midX - fitted.width / 2, y: rect.midY - fitted.height / 2, width: fitted.width, height: fitted.height),
        from: .zero, operation: .sourceOver, fraction: 1
    )
}

/// One tile of the shelf: a rounded square with its own content, clipped to its shape.
func drawTile(_ rect: NSRect, _ content: (NSRect) -> Void) {
    let shape = NSBezierPath(roundedRect: rect, xRadius: 30, yRadius: 30)

    NSGraphicsContext.saveGraphicsState()
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.28)
    shadow.shadowOffset = NSSize(width: 0, height: -6)
    shadow.shadowBlurRadius = 12
    shadow.set()
    NSColor.white.setFill()
    shape.fill()
    NSGraphicsContext.restoreGraphicsState()

    NSGraphicsContext.saveGraphicsState()
    shape.addClip()
    content(rect)
    NSGraphicsContext.restoreGraphicsState()
}

func gradientFill(_ rect: NSRect, _ colors: [NSColor], angle: CGFloat) {
    NSGradient(colors: colors)!.draw(in: rect, angle: angle)
}

func drawIcon() {
    // Body of the icon.
    let body = NSRect(x: 100, y: 100, width: 824, height: 824)
    let bodyPath = NSBezierPath(roundedRect: body, xRadius: 186, yRadius: 186)

    NSGraphicsContext.saveGraphicsState()
    let bodyShadow = NSShadow()
    bodyShadow.shadowColor = NSColor.black.withAlphaComponent(0.38)
    bodyShadow.shadowOffset = NSSize(width: 0, height: -14)
    bodyShadow.shadowBlurRadius = 30
    bodyShadow.set()
    NSColor.black.setFill()
    bodyPath.fill()
    NSGraphicsContext.restoreGraphicsState()

    NSGraphicsContext.saveGraphicsState()
    bodyPath.addClip()
    NSGradient(
        colors: [rgb(0.55, 0.27, 0.96), rgb(0.27, 0.35, 0.99), rgb(0.04, 0.55, 1.00)],
        atLocations: [0, 0.52, 1], colorSpace: .sRGB
    )!.draw(in: body, angle: -58)
    // Warm glow from the top-right corner.
    let glowCentre = NSPoint(x: 800, y: 880)
    NSGradient(colors: [rgb(1.0, 0.42, 0.72, 0.65), rgb(1.0, 0.42, 0.72, 0)])!
        .draw(fromCenter: glowCentre, radius: 0, toCenter: glowCentre, radius: 520, options: [])
    // Light sweep across the top half.
    NSGradient(colors: [NSColor.white.withAlphaComponent(0.20), NSColor.white.withAlphaComponent(0)])!
        .draw(in: NSRect(x: body.minX, y: body.midY, width: body.width, height: body.height / 2), angle: -90)
    NSGraphicsContext.restoreGraphicsState()

    // Hairline edge.
    rgb(1, 1, 1, 0.28).setStroke()
    let edge = NSBezierPath(roundedRect: body.insetBy(dx: 2, dy: 2), xRadius: 184, yRadius: 184)
    edge.lineWidth = 4
    edge.stroke()

    // The shelf: a frosted panel holding six tiles.
    let panel = NSRect(x: 204, y: 294, width: 616, height: 436)
    let panelPath = NSBezierPath(roundedRect: panel, xRadius: 84, yRadius: 84)

    NSGraphicsContext.saveGraphicsState()
    let panelShadow = NSShadow()
    panelShadow.shadowColor = NSColor.black.withAlphaComponent(0.25)
    panelShadow.shadowOffset = NSSize(width: 0, height: -12)
    panelShadow.shadowBlurRadius = 26
    panelShadow.set()
    rgb(1, 1, 1, 0.22).setFill()
    panelPath.fill()
    NSGraphicsContext.restoreGraphicsState()

    NSGraphicsContext.saveGraphicsState()
    panelPath.addClip()
    NSGradient(colors: [rgb(1, 1, 1, 0.30), rgb(1, 1, 1, 0.10)])!.draw(in: panel, angle: -90)
    NSGraphicsContext.restoreGraphicsState()
    rgb(1, 1, 1, 0.55).setStroke()
    panelPath.lineWidth = 4
    panelPath.stroke()

    // Tiles: 3 columns x 2 rows.
    let tile: CGFloat = 146, gap: CGFloat = 34
    let gridWidth = 3 * tile + 2 * gap
    let gridHeight = 2 * tile + gap
    let originX = panel.midX - gridWidth / 2
    let originY = panel.midY - gridHeight / 2
    func rect(column: Int, row: Int) -> NSRect {   // row 0 is the top row
        NSRect(x: originX + CGFloat(column) * (tile + gap), y: originY + CGFloat(1 - row) * (tile + gap), width: tile, height: tile)
    }

    // Sunset photo.
    drawTile(rect(column: 0, row: 0)) { r in
        gradientFill(r, [rgb(0.99, 0.62, 0.33), rgb(0.93, 0.33, 0.50), rgb(0.36, 0.20, 0.60)], angle: 90)
        rgb(1.0, 0.95, 0.75).setFill()
        NSBezierPath(ovalIn: NSRect(x: r.minX + r.width * 0.56, y: r.minY + r.height * 0.40, width: r.width * 0.26, height: r.width * 0.26)).fill()
        rgb(0.12, 0.08, 0.22).setFill()
        let hill = NSBezierPath()
        hill.move(to: NSPoint(x: r.minX, y: r.minY))
        hill.line(to: NSPoint(x: r.minX, y: r.minY + r.height * 0.34))
        hill.curve(to: NSPoint(x: r.maxX, y: r.minY + r.height * 0.28),
                   controlPoint1: NSPoint(x: r.minX + r.width * 0.35, y: r.minY + r.height * 0.58),
                   controlPoint2: NSPoint(x: r.minX + r.width * 0.70, y: r.minY + r.height * 0.05))
        hill.line(to: NSPoint(x: r.maxX, y: r.minY))
        hill.close()
        hill.fill()
    }
    // Link.
    drawTile(rect(column: 1, row: 0)) { r in
        gradientFill(r, [rgb(0.36, 0.67, 1.0), rgb(0.12, 0.42, 0.98)], angle: 90)
        drawSymbol("link", in: r.insetBy(dx: 32, dy: 32), color: .white)
    }
    // Document.
    drawTile(rect(column: 2, row: 0)) { r in
        gradientFill(r, [rgb(1, 1, 1), rgb(0.88, 0.90, 0.95)], angle: 90)
        rgb(0.55, 0.60, 0.72).setFill()
        for (index, width) in [0.66, 0.66, 0.46].enumerated() {
            let y = r.maxY - r.height * (0.30 + 0.17 * CGFloat(index))
            NSBezierPath(roundedRect: NSRect(x: r.minX + r.width * 0.17, y: y, width: r.width * CGFloat(width), height: 11), xRadius: 5.5, yRadius: 5.5).fill()
        }
    }
    // Folder.
    drawTile(rect(column: 0, row: 1)) { r in
        gradientFill(r, [rgb(0.55, 0.82, 1.0), rgb(0.25, 0.60, 0.98)], angle: 90)
        drawSymbol("folder.fill", in: r.insetBy(dx: 30, dy: 34), color: .white)
    }
    // Text note.
    drawTile(rect(column: 1, row: 1)) { r in
        gradientFill(r, [rgb(1.0, 0.72, 0.30), rgb(0.97, 0.50, 0.14)], angle: 90)
        drawSymbol("text.alignleft", in: r.insetBy(dx: 34, dy: 36), color: .white)
    }
    // Mountain photo.
    drawTile(rect(column: 2, row: 1)) { r in
        gradientFill(r, [rgb(0.62, 0.86, 1.0), rgb(0.20, 0.45, 0.88), rgb(0.10, 0.20, 0.50)], angle: 90)
        rgb(0.08, 0.15, 0.30).setFill()
        let peaks = NSBezierPath()
        peaks.move(to: NSPoint(x: r.minX, y: r.minY))
        peaks.line(to: NSPoint(x: r.minX, y: r.minY + r.height * 0.34))
        peaks.line(to: NSPoint(x: r.minX + r.width * 0.28, y: r.minY + r.height * 0.62))
        peaks.line(to: NSPoint(x: r.minX + r.width * 0.50, y: r.minY + r.height * 0.40))
        peaks.line(to: NSPoint(x: r.minX + r.width * 0.74, y: r.minY + r.height * 0.70))
        peaks.line(to: NSPoint(x: r.maxX, y: r.minY + r.height * 0.36))
        peaks.line(to: NSPoint(x: r.maxX, y: r.minY))
        peaks.close()
        peaks.fill()
        rgb(1, 1, 0.95).setFill()
        NSBezierPath(ovalIn: NSRect(x: r.minX + r.width * 0.64, y: r.minY + r.height * 0.70, width: r.width * 0.16, height: r.width * 0.16)).fill()
    }
}

/// `zoom` > 1 draws the artwork larger about the centre of the canvas (the Windows icon).
func renderBitmap(pixels: Int, zoom: CGFloat = 1) -> NSBitmapImageRep {
    let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4,
        hasAlpha: true, isPlanar: false, colorSpaceName: .calibratedRGB, bytesPerRow: 0, bitsPerPixel: 0
    )!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    NSGraphicsContext.current?.imageInterpolation = .high
    let scale = NSAffineTransform()
    scale.translateX(by: CGFloat(pixels) / 2, yBy: CGFloat(pixels) / 2)
    scale.scale(by: CGFloat(pixels) / 1024 * zoom)
    scale.translateX(by: -512, yBy: -512)
    scale.concat()
    drawIcon()
    NSGraphicsContext.restoreGraphicsState()
    return rep
}

func render(pixels: Int, zoom: CGFloat = 1) -> Data {
    renderBitmap(pixels: pixels, zoom: zoom).representation(using: .png, properties: [:])!
}

// MARK: - Windows .ico

/// Small sizes as classic bitmap entries (every Windows API reads those), large ones as PNG.
func icoEntry(pixels: Int, zoom: CGFloat) -> Data {
    let png = render(pixels: pixels, zoom: zoom)
    guard pixels <= 48 else { return png }

    // Decode the PNG again so the pixels come out as plain, non-premultiplied RGBA.
    let rep = NSBitmapImageRep(data: png)!
    precondition(rep.pixelsWide == pixels && rep.bitsPerPixel == 32 && !rep.bitmapFormat.contains(.alphaFirst), "unexpected pixel layout")
    let premultiplied = !rep.bitmapFormat.contains(.alphaNonpremultiplied)
    let source = rep.bitmapData!

    var data = Data()
    func put32(_ v: Int) { var x = UInt32(truncatingIfNeeded: v).littleEndian; withUnsafeBytes(of: &x) { data.append(contentsOf: $0) } }
    func put16(_ v: Int) { var x = UInt16(truncatingIfNeeded: v).littleEndian; withUnsafeBytes(of: &x) { data.append(contentsOf: $0) } }
    put32(40); put32(pixels); put32(pixels * 2); put16(1); put16(32)   // BITMAPINFOHEADER: height covers the colour and mask halves
    put32(0); put32(pixels * pixels * 4); put32(0); put32(0); put32(0); put32(0)

    for y in stride(from: pixels - 1, through: 0, by: -1) {            // bottom-up
        for x in 0..<pixels {
            let p = source + y * rep.bytesPerRow + x * 4
            var (r, g, b, a) = (Int(p[0]), Int(p[1]), Int(p[2]), Int(p[3]))
            if premultiplied && a > 0 && a < 255 { r = min(255, r * 255 / a); g = min(255, g * 255 / a); b = min(255, b * 255 / a) }
            data.append(contentsOf: [UInt8(b), UInt8(g), UInt8(r), UInt8(a)])
        }
    }
    data.append(Data(count: ((pixels + 31) / 32) * 4 * pixels))         // AND mask: all zero, alpha does the work
    return data
}

func writeWindowsIcon(to path: String) throws {
    let sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    let zoom: CGFloat = 1.12   // the body then fills about 90% of the canvas
    let entries = sizes.map { ($0, icoEntry(pixels: $0, zoom: zoom)) }

    var ico = Data()
    func put16(_ v: Int) { var x = UInt16(v).littleEndian; withUnsafeBytes(of: &x) { ico.append(contentsOf: $0) } }
    func put32(_ v: Int) { var x = UInt32(v).littleEndian; withUnsafeBytes(of: &x) { ico.append(contentsOf: $0) } }
    put16(0); put16(1); put16(entries.count)
    var offset = 6 + 16 * entries.count
    for (pixels, entry) in entries {
        ico.append(contentsOf: [UInt8(pixels == 256 ? 0 : pixels), UInt8(pixels == 256 ? 0 : pixels), 0, 0])
        put16(1); put16(32); put32(entry.count); put32(offset)
        offset += entry.count
    }
    for (_, entry) in entries { ico.append(entry) }
    try FileManager.default.createDirectory(atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
    try ico.write(to: URL(fileURLWithPath: path))
    print("Wrote \(path) (\(entries.count) sizes, \(ico.count) bytes)")
}

// MARK: - Main

let arguments = CommandLine.arguments
if arguments.count > 1, arguments[1] == "--windows" {
    _ = NSApplication.shared
    try writeWindowsIcon(to: arguments.count > 2 ? arguments[2] : "../windows/src/ShelfDrop.App/Assets/ShelfDrop.ico")
    // Optionally also the large master, for marketing.
    if arguments.count > 3 { try render(pixels: 1024, zoom: 1.12).write(to: URL(fileURLWithPath: arguments[3])) }
    exit(0)
}
let output = arguments.count > 1 ? arguments[1] : "Resources/AppIcon.icns"
let preview = arguments.count > 2 ? arguments[2] : nil

_ = NSApplication.shared
let iconset = FileManager.default.temporaryDirectory.appendingPathComponent("ShelfDrop-\(UUID().uuidString).iconset")
try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
defer { try? FileManager.default.removeItem(at: iconset) }

// The names and sizes iconutil expects: each point size at 1x and 2x.
let sizes: [(name: String, pixels: Int)] = [
    ("icon_16x16", 16), ("icon_16x16@2x", 32),
    ("icon_32x32", 32), ("icon_32x32@2x", 64),
    ("icon_128x128", 128), ("icon_128x128@2x", 256),
    ("icon_256x256", 256), ("icon_256x256@2x", 512),
    ("icon_512x512", 512), ("icon_512x512@2x", 1024),
]
for (name, pixels) in sizes {
    try render(pixels: pixels).write(to: iconset.appendingPathComponent("\(name).png"))
}
if let preview { try render(pixels: 1024).write(to: URL(fileURLWithPath: preview)) }

let iconutil = Process()
iconutil.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
iconutil.arguments = ["-c", "icns", iconset.path, "-o", output]
try iconutil.run()
iconutil.waitUntilExit()
guard iconutil.terminationStatus == 0 else {
    FileHandle.standardError.write(Data("iconutil failed\n".utf8))
    exit(1)
}
print("Wrote \(output)")
