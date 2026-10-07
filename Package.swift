// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "ShelfDrop",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "ShelfDrop",
            path: "Sources/ShelfDrop"
        ),
        .testTarget(
            name: "ShelfDropTests",
            dependencies: ["ShelfDrop"],
            path: "Tests/ShelfDropTests"
        ),
    ]
)
