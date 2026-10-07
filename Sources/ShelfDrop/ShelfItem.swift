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
    /// Short human label for what this is: "Image", "PDF", "Folder", "Link", "Text"...
    let kindLabel: String
    /// Size of a plain file; nil for folders, links and text.
    let byteCount: Int64?
    /// True for image files, which get a photo-style thumbnail.
    let isImageFile: Bool

    static func file(_ url: URL) -> ShelfItem {
        let facts = FileFacts.describe(url)
        return ShelfItem(
            kind: .file(url), title: url.lastPathComponent, thumbnail: Thumbnails.forFile(url),
            kindLabel: facts.kindLabel, byteCount: facts.byteCount, isImageFile: facts.isImage
        )
    }

    static func link(_ url: URL) -> ShelfItem {
        ShelfItem(
            kind: .link(url), title: url.host ?? url.absoluteString, thumbnail: Thumbnails.symbol("link"),
            kindLabel: "Link", byteCount: nil, isImageFile: false
        )
    }

    static func text(_ string: String) -> ShelfItem {
        let firstLine = string.split(whereSeparator: \.isNewline).first.map(String.init) ?? string
        return ShelfItem(
            kind: .text(string), title: String(firstLine.prefix(40)), thumbnail: Thumbnails.symbol("text.alignleft"),
            kindLabel: "Text", byteCount: nil, isImageFile: false
        )
    }

    /// Second line of a tile: the kind and, for files, the size ("Image · 24 KB").
    var detail: String {
        [kindLabel, byteCount.map { ByteCountFormatter.string(fromByteCount: $0, countStyle: .file) }]
            .compactMap { $0 }
            .joined(separator: " · ")
    }

    /// Header subtitle for a whole shelf: "7 items · 1.2 MB". Sizes only count plain files.
    static func summary(of items: [ShelfItem]) -> String {
        guard !items.isEmpty else { return "Empty" }
        var parts = ["\(items.count) \(items.count == 1 ? "item" : "items")"]
        let total = items.compactMap(\.byteCount).reduce(0, +)
        if total > 0 { parts.append(ByteCountFormatter.string(fromByteCount: total, countStyle: .file)) }
        return parts.joined(separator: " · ")
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

/// Kind and size of a file, for the second line of its tile.
enum FileFacts {
    struct Facts {
        var kindLabel: String
        var byteCount: Int64?
        var isImage: Bool
    }

    static func describe(_ url: URL) -> Facts {
        let values = try? url.resourceValues(forKeys: [.isDirectoryKey, .isApplicationKey, .contentTypeKey, .fileSizeKey])
        let isDirectory = values?.isDirectory ?? false
        let type = values?.contentType ?? UTType(filenameExtension: url.pathExtension)
        return Facts(
            kindLabel: kindLabel(for: type, isDirectory: isDirectory, isApplication: values?.isApplication ?? false, fileExtension: url.pathExtension),
            byteCount: isDirectory ? nil : values?.fileSize.map(Int64.init),
            isImage: !isDirectory && (type?.conforms(to: .image) ?? false)
        )
    }

    static func kindLabel(for type: UTType?, isDirectory: Bool, isApplication: Bool, fileExtension: String) -> String {
        if isApplication { return "App" }
        if isDirectory { return "Folder" }
        if let type {
            if type.conforms(to: .image) { return "Image" }
            if type.conforms(to: .pdf) { return "PDF" }
            if type.conforms(to: .movie) { return "Video" }
            if type.conforms(to: .audio) { return "Audio" }
            if type.conforms(to: .archive) { return "Archive" }
            if type.conforms(to: .spreadsheet) { return "Sheet" }
            if type.conforms(to: .presentation) { return "Slides" }
            if type.conforms(to: .sourceCode) { return "Code" }
            if type.conforms(to: .text) { return "Text" }
        }
        return fileExtension.isEmpty ? "File" : fileExtension.uppercased()
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
