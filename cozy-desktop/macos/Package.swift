// swift-tools-version:5.9
import PackageDescription

// Builds the Cozy macOS app as an SPM executable so it can be compiled from the
// command line (no manual Xcode project needed). `build-mac.sh` wraps this and
// assembles a double-clickable Cozy.app.
let package = Package(
    name: "CozyMac",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "CozyMac",
            path: "CozyMac"
        )
    ]
)
