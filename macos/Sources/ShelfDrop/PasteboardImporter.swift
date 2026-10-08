import AppKit

/// Turns whatever a drag put on the pasteboard into shelf items.
///
/// The first kind that yields something wins, in order of fidelity:
/// real files, promised files (Mail, Photos, browsers), raw images, web links, text.
enum PasteboardImporter {
    /// Pasteboard types the shelf accepts as a drop destination.
    static var acceptedTypes: [NSPasteboard.PasteboardType] {
        [.fileURL, .URL, .string, .png, .tiff]
            + NSFilePromiseReceiver.readableDraggedTypes.map { NSPasteboard.PasteboardType($0) }
    }

    @MainActor
    static func importItems(from pasteboard: NSPasteboard, completion: @escaping ([ShelfItem]) -> Void) {
        let fileURLs = pasteboard.readObjects(
            forClasses: [NSURL.self],
            options: [.urlReadingFileURLsOnly: true]
        ) as? [URL] ?? []
        if !fileURLs.isEmpty {
            completion(fileURLs.map(ShelfItem.file))
            return
        }

        let receivers = pasteboard.readObjects(forClasses: [NSFilePromiseReceiver.self]) as? [NSFilePromiseReceiver] ?? []
        if !receivers.isEmpty {
            receivePromisedFiles(receivers, completion: completion)
            return
        }

        if let image = (pasteboard.readObjects(forClasses: [NSImage.self]) as? [NSImage])?.first,
           let url = writeTemporaryPNG(image) {
            completion([.file(url)])
            return
        }

        let webURLs = pasteboard.readObjects(
            forClasses: [NSURL.self],
            options: [.urlReadingFileURLsOnly: false]
        ) as? [URL] ?? []
        if !webURLs.isEmpty {
            completion(webURLs.map(ShelfItem.link))
            return
        }

        let strings = (pasteboard.readObjects(forClasses: [NSString.self]) as? [String]) ?? []
        completion(strings.filter { !$0.isEmpty }.map(ShelfItem.text))
    }

    // MARK: - Helpers

    @MainActor
    private static func receivePromisedFiles(_ receivers: [NSFilePromiseReceiver], completion: @escaping ([ShelfItem]) -> Void) {
        let destination = TempStorage.makeDirectory()
        let group = DispatchGroup()
        var received: [URL] = []

        for receiver in receivers {
            group.enter()
            // The callback runs on the operation queue we pass, here the main queue,
            // so appending to `received` needs no extra locking.
            receiver.receivePromisedFiles(atDestination: destination, options: [:], operationQueue: .main) { url, error in
                if let error {
                    NSLog("[ShelfDrop] file promise failed: %@", error.localizedDescription)
                } else {
                    received.append(url)
                }
                group.leave()
            }
        }

        group.notify(queue: .main) {
            MainActor.assumeIsolated {
                completion(received.map(ShelfItem.file))
            }
        }
    }

    private static func writeTemporaryPNG(_ image: NSImage) -> URL? {
        guard let tiff = image.tiffRepresentation,
              let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) else { return nil }
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd 'at' HH.mm.ss"
        let url = TempStorage.makeDirectory().appendingPathComponent("Image \(formatter.string(from: Date())).png")
        do {
            try png.write(to: url)
            return url
        } catch {
            NSLog("[ShelfDrop] could not write image: %@", error.localizedDescription)
            return nil
        }
    }
}
