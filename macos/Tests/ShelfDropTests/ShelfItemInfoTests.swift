import AppKit
import Foundation
import Testing
import UniformTypeIdentifiers
@testable import ShelfDrop

struct ShelfItemInfoTests {
    private func size(_ bytes: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }

    private func makeTempDirectory() throws -> URL {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("shelfdrop-info-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: - Kind labels

    @Test func kindLabelsAreShortAndHumanReadable() {
        func label(_ type: UTType?, dir: Bool = false, app: Bool = false, ext: String = "") -> String {
            FileFacts.kindLabel(for: type, isDirectory: dir, isApplication: app, fileExtension: ext)
        }
        #expect(label(.png) == "Image")
        #expect(label(.pdf) == "PDF")
        #expect(label(.mpeg4Movie) == "Video")
        #expect(label(.mp3) == "Audio")
        #expect(label(.zip) == "Archive")
        #expect(label(.plainText) == "Text")
        #expect(label(.swiftSource) == "Code")
        #expect(label(nil, dir: true) == "Folder")
        // An .app bundle is also a directory, but "App" is the more useful answer.
        #expect(label(.application, dir: true, app: true) == "App")
        #expect(label(nil, ext: "xyz") == "XYZ")
        #expect(label(nil) == "File")
    }

    // MARK: - Describing real files

    @Test func aPlainFileReportsItsKindAndSize() throws {
        let dir = try makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let file = dir.appendingPathComponent("note.txt")
        try Data(repeating: 0x61, count: 2048).write(to: file)

        let item = ShelfItem.file(file)

        #expect(item.kindLabel == "Text")
        #expect(item.byteCount == 2048)
        #expect(!item.isImageFile)
        #expect(item.detail == "Text · \(size(2048))")
    }

    @Test func aFolderHasNoSize() throws {
        let dir = try makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }

        let item = ShelfItem.file(dir)

        #expect(item.kindLabel == "Folder")
        #expect(item.byteCount == nil)
        #expect(item.detail == "Folder")
    }

    @Test func anImageFileIsFlaggedForPhotoStyleThumbnails() throws {
        let dir = try makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let image = NSImage(size: NSSize(width: 8, height: 8), flipped: false) { rect in
            NSColor.systemBlue.setFill(); rect.fill(); return true
        }
        let file = dir.appendingPathComponent("pic.png")
        try NSBitmapImageRep(data: image.tiffRepresentation!)!.representation(using: .png, properties: [:])!.write(to: file)

        let item = ShelfItem.file(file)

        #expect(item.isImageFile)
        #expect(item.kindLabel == "Image")
    }

    @Test func aMissingFileStillGetsAKindFromItsExtensionButNoSize() {
        let gone = URL(fileURLWithPath: "/tmp/shelfdrop-gone-\(UUID().uuidString).pdf")

        let item = ShelfItem.file(gone)

        #expect(item.isMissing)
        #expect(item.kindLabel == "PDF")
        #expect(item.byteCount == nil)
    }

    @Test func linksAndTextAreLabelledWithoutASize() {
        #expect(ShelfItem.link(URL(string: "https://example.com")!).detail == "Link")
        #expect(ShelfItem.text("hello").detail == "Text")
    }

    // MARK: - Header summary

    @Test func summaryCountsItemsAndAddsUpFileSizes() throws {
        let dir = try makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let a = dir.appendingPathComponent("a.bin"); try Data(count: 1000).write(to: a)
        let b = dir.appendingPathComponent("b.bin"); try Data(count: 3000).write(to: b)

        #expect(ShelfItem.summary(of: []) == "Empty")
        #expect(ShelfItem.summary(of: [.text("one")]) == "1 item")
        #expect(ShelfItem.summary(of: [.text("one"), .link(URL(string: "https://example.com")!)]) == "2 items")
        #expect(ShelfItem.summary(of: [.file(a), .file(b), .text("x")]) == "3 items · \(size(4000))")
    }
}
