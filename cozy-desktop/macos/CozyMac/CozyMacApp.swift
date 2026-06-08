import SwiftUI

/// Cozy for macOS — a native SwiftUI app that hides deskflow.
/// The Mac joins the keyboard/mouse network (typically as a client of the PC).
@main
struct CozyMacApp: App {
    var body: some Scene {
        WindowGroup("Cozy") {
            ContentView()
                .frame(minWidth: 640, minHeight: 560)
        }
        .windowResizability(.contentSize)
    }
}
