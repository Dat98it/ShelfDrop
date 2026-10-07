import AppKit
import UniformTypeIdentifiers

/// One thing sitting on the shelf. Files are kept as references to the original location
/// (nothing is copied), except for payloads that only exist as data (images, promised files),
/// which are written to a temp folder first so they behave like files everywhere.
struct ShelfItem: Identifiable {
    enum Kind {
        case file(URL)
        case link(URL)
        case text(String)
    }

    let id = UUID()
    let kind: Kind
    let title: String
    let thumbnail: NSImage

    static func file(_ url: URL) -> ShelfItem {
        ShelfItem(kind: .file(url), title: url.lastPathComponent, thumbnail: Thumbnails.forFile(url))
    }

    static func link(_ url: URL) -> ShelfItem {
        ShelfItem(kind: .link(url), title: url.host ?? url.absoluteString, thumbnail: Thumbnails.symbol("link"))
    }

    static func text(_ string: String) -> ShelfItem {
        let firstLine = string.split(whereSeparator: \.isNewline).first.map(String.init) ?? string
        return ShelfItem(kind: .text(string), title: String(firstLine.prefix(40)), thumbnail: Thumbnails.symbol("text.alignleft"))
    }

    /// What gets written to the drag pasteboard when this item is dragged out.
    var pasteboardWriter: NSPasteboardWriting {
        switch kind {
        case .file(let url), .link(let url): return url as NSURL
        case .text(let string): return string as NSString
        }
    }

    /// What the share sheet receives for this item, or nil if it cannot be shared right now
    /// (a file that was moved or deleted after being added).
    var sharingItem: Any? {
        switch kind {
        case .file(let url): return isMissing ? nil : url as NSURL
        case .link(let url): return url as NSURL
        case .text(let string): return string as NSString
        }
    }

    /// True when a referenced file was moved or deleted after being added.
    var isMissing: Bool {
        guard case .file(let url) = kind else { return false }
        return !FileManager.default.fileExists(atPath: url.path)
    }
}

enum Thumbnails {
    static func forFile(_ url: URL) -> NSImage {
        if let type = UTType(filenameExtension: url.pathExtension), type.conforms(to: .image),
           let source = CGImageSourceCreateWithURL(url as CFURL, nil) {
            let options: [CFString: Any] = [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: 192,
            ]
            if let cgImage = CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary) {
                return NSImage(cgImage: cgImage, size: NSSize(width: cgImage.width / 2, height: cgImage.height / 2))
            }
        }
        return NSWorkspace.shared.icon(forFile: url.path)
    }

    static func symbol(_ name: String) -> NSImage {
        let image = NSImage(systemSymbolName: name, accessibilityDescription: nil) ?? NSImage()
        return image.withSymbolConfiguration(.init(pointSize: 40, weight: .regular)) ?? image
    }
}

enum TempStorage {
    static let root = FileManager.default.temporaryDirectory.appendingPathComponent("ShelfDrop", isDirectory: true)

    static func makeDirectory() -> URL {
        let dir = root.appendingPathComponent(UUID().uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    /// The shelf is not persisted in the MVP, so leftovers from a previous run are garbage.
    static func cleanUp() {
        try? FileManager.default.removeItem(at: root)
    }

    /// Deletes the temp copies behind `items`. Only files inside `root` are ever removed:
    /// everything else on a shelf is a reference to a file the user owns.
    static func discard(_ items: [ShelfItem]) {
        let fileManager = FileManager.default
        // Trailing slash so a sibling like ".../ShelfDropOther" does not match by prefix.
        let rootPrefix = root.resolvingSymlinksInPath().path + "/"

        for item in items {
            guard case .file(let url) = item.kind else { continue }
            let resolved = url.resolvingSymlinksInPath()
            guard resolved.path.hasPrefix(rootPrefix) else { continue }
            try? fileManager.removeItem(at: resolved)

            // Each drop gets its own folder; remove it once its last file is gone.
            let folder = resolved.deletingLastPathComponent()
            if folder.path.hasPrefix(rootPrefix),
               (try? fileManager.contentsOfDirectory(atPath: folder.path))?.isEmpty == true {
                try? fileManager.removeItem(at: folder)
            }
        }
    }
}
