import AppKit
import Foundation
import Testing

/// Checks the files that make up the app bundle, straight from the repository.
struct AppBundleTests {
    private let root = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()

    private func infoPlist() throws -> [String: Any] {
        let data = try Data(contentsOf: root.appendingPathComponent("Resources/Info.plist"))
        return try #require(try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any])
    }

    @Test func infoPlistNamesAnIconThatShipsInResources() throws {
        let name = try #require(try infoPlist()["CFBundleIconFile"] as? String)
        let icon = root.appendingPathComponent("Resources/\(name).icns")
        #expect(FileManager.default.fileExists(atPath: icon.path), "CFBundleIconFile is \(name) but \(icon.lastPathComponent) is missing")
    }

    @Test func theIconHasEverySizeMacOSAsksFor() throws {
        let icon = try #require(NSImage(contentsOf: root.appendingPathComponent("Resources/AppIcon.icns")))
        let widths = Set(icon.representations.map(\.pixelsWide))
        // 16 to 512 points, each at 1x and 2x, means pixel widths from 16 up to 1024.
        for width in [16, 32, 64, 128, 256, 512, 1024] {
            #expect(widths.contains(width), "no \(width)px image in the icon (has \(widths.sorted()))")
        }
    }

    @Test func theAppStaysAMenuBarOnlyApp() throws {
        #expect(try infoPlist()["LSUIElement"] as? Bool == true)
    }
}
