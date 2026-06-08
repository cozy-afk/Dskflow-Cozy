import Foundation

/// Hides deskflow on macOS. Finds the deskflow client binary and runs it as a
/// background process that connects to the PC server. The user only ever sees Cozy.
@MainActor
final class DeskflowController: ObservableObject {
    @Published var isRunning = false
    @Published var log: String = ""

    private var process: Process?

    /// Common install locations for the deskflow client binary on macOS.
    func findClientBinary() -> String? {
        let candidates = [
            "/Applications/Deskflow.app/Contents/MacOS/deskflow-client",
            "/Applications/Deskflow.app/Contents/MacOS/deskflow-core",
            "/opt/homebrew/bin/deskflow-client",
            "/usr/local/bin/deskflow-client",
        ]
        return candidates.first { FileManager.default.fileExists(atPath: $0) }
    }

    var isInstalled: Bool { findClientBinary() != nil }

    /// Connect this Mac to the PC server as a client named `screenName`.
    func start(serverIP: String, port: Int, screenName: String, tls: Bool) {
        guard !isRunning else { return }
        guard let bin = findClientBinary() else {
            append("Cozy engine not found. Run the setup first.")
            return
        }

        let p = Process()
        p.executableURL = URL(fileURLWithPath: bin)
        // Classic client invocation: foreground, named, pointed at server:port.
        p.arguments = ["-f", "--no-tray", "--name", screenName, "\(serverIP):\(port)"]

        let pipe = Pipe()
        p.standardOutput = pipe
        p.standardError = pipe
        pipe.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            if let s = String(data: data, encoding: .utf8), !s.isEmpty {
                Task { @MainActor in self?.append(s) }
            }
        }
        p.terminationHandler = { [weak self] _ in
            Task { @MainActor in
                self?.isRunning = false
                self?.append("Disconnected.")
            }
        }

        do {
            append("Connecting to \(serverIP):\(port) as \"\(screenName)\"…")
            try p.run()
            process = p
            isRunning = true
        } catch {
            append("Failed to start: \(error.localizedDescription)")
        }
    }

    func stop() {
        process?.terminate()
        process = nil
        isRunning = false
    }

    private func append(_ s: String) {
        log += s.hasSuffix("\n") ? s : s + "\n"
    }
}
