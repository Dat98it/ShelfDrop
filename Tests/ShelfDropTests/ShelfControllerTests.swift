import AppKit
import Foundation
import Testing
@testable import ShelfDrop

@MainActor
struct ShelfControllerTests {
    private let isolated = IsolatedDefaults()

    private func makeController() -> ShelfController {
        _ = NSApplication.shared
        return ShelfController(defaults: isolated.defaults)
    }

    /// Clicks at `point` (window coordinates, origin bottom-left) by sending a real mouse down/up pair.
    private func click(_ panel: NSPanel, at point: NSPoint) {
        for type in [NSEvent.EventType.leftMouseDown, .leftMouseUp] {
            let event = NSEvent.mouseEvent(
                with: type, location: point, modifierFlags: [], timestamp: ProcessInfo.processInfo.systemUptime,
                windowNumber: panel.windowNumber, context: nil, eventNumber: 0, clickCount: 1, pressure: 1
            )!
            panel.sendEvent(event)
            // SwiftUI runs button actions asynchronously; let it process the event.
            RunLoop.current.run(until: Date().addingTimeInterval(0.02))
        }
    }

    @Test func clickingTheCloseButtonRemovesItemsAndHidesPanel() {
        let controller = makeController()
        controller.model.add([.text("one"), .text("two")])
        controller.show(near: NSPoint(x: 400, y: 400))
        #expect(controller.isShelfVisible)

        // The "x" is the right-most header control. Probing the hit area showed it spans about
        // x 314-326 and 18-30pt below the top edge of the 340pt-wide shelf.
        let size = controller.panel.frame.size
        // A freshly shown panel has not laid out its SwiftUI content yet; clicks before that are lost.
        controller.panel.contentView?.layoutSubtreeIfNeeded()
        RunLoop.current.run(until: Date().addingTimeInterval(0.2))
        click(controller.panel, at: NSPoint(x: size.width - 20, y: size.height - 24))

        #expect(controller.model.items.isEmpty)
        #expect(!controller.isShelfVisible)
    }

    @Test func closeRemovesItemsAndHidesPanel() {
        let controller = makeController()
        controller.model.add([.text("one"), .text("two")])
        controller.show(near: NSPoint(x: 400, y: 400))
        #expect(controller.isShelfVisible)

        controller.close()

        #expect(controller.model.items.isEmpty)
        #expect(!controller.isShelfVisible)
    }

    @Test func closeDoesNotDeleteTheReferencedFile() throws {
        let file = FileManager.default.temporaryDirectory.appendingPathComponent("shelfdrop-keep-\(UUID().uuidString).txt")
        try "keep me".write(to: file, atomically: true, encoding: .utf8)
        defer { try? FileManager.default.removeItem(at: file) }

        let controller = makeController()
        controller.model.add([.file(file)])
        controller.close()

        #expect(controller.model.items.isEmpty)
        #expect(FileManager.default.fileExists(atPath: file.path))
    }

    // MARK: - Temp copies

    private func makeTempCopy(named name: String = "copy.txt", in folder: URL? = nil) throws -> URL {
        let directory = folder ?? TempStorage.makeDirectory()
        let url = directory.appendingPathComponent(name)
        try "temp".write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    @Test func closeDeletesTempCopiesAndTheirFolder() throws {
        let copy = try makeTempCopy()
        let folder = copy.deletingLastPathComponent()

        let controller = makeController()
        controller.model.add([.file(copy)])
        controller.close()

        #expect(!FileManager.default.fileExists(atPath: copy.path))
        #expect(!FileManager.default.fileExists(atPath: folder.path))
    }

    @Test func closeKeepsFolderWhileItStillHoldsOtherFiles() throws {
        let first = try makeTempCopy(named: "a.txt")
        let folder = first.deletingLastPathComponent()
        let stranger = try makeTempCopy(named: "not-on-shelf.txt", in: folder)
        defer { try? FileManager.default.removeItem(at: folder) }

        let controller = makeController()
        controller.model.add([.file(first)])
        controller.close()

        #expect(!FileManager.default.fileExists(atPath: first.path))
        #expect(FileManager.default.fileExists(atPath: stranger.path))
    }

    @Test func closeNeverDeletesFilesOutsideTheTempRoot() throws {
        // A sibling whose name merely starts with the temp root's name must not match by prefix.
        let lookalike = TempStorage.root.deletingLastPathComponent()
            .appendingPathComponent("\(TempStorage.root.lastPathComponent)Other-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: lookalike, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: lookalike) }
        let outside = try makeTempCopy(named: "mine.txt", in: lookalike)

        // A symlink inside the temp root that points at a user file must not delete its target.
        let target = try makeTempCopy(named: "target.txt", in: lookalike)
        let link = TempStorage.makeDirectory().appendingPathComponent("link.txt")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: target)

        let controller = makeController()
        controller.model.add([.file(outside), .file(link)])
        controller.close()

        #expect(FileManager.default.fileExists(atPath: outside.path))
        #expect(FileManager.default.fileExists(atPath: target.path))
    }

    @Test func hideKeepsItems() {
        let controller = makeController()
        controller.model.add([.text("keep")])
        controller.show(near: NSPoint(x: 400, y: 400))

        controller.hide()

        #expect(controller.model.items.count == 1)
    }
}
