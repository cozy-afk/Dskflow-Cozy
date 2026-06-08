import Foundation

/// Hides deskflow on macOS. Finds the deskflow-core engine and runs it as a
/// background CLIENT that connects to the PC server. The user only sees Cozy.
///
/// Verified against deskflow 1.26 (CLI confirmed on the Windows side): the engine
/// is driven by `deskflow-core client -s <settingsFile> --new-instance`, where the
/// settings file is a Qt-INI with the server address, name, port and TLS flag.
@MainActor
final class DeskflowController: ObservableObject {
    @Published var isRunning = false
    @Published var log: String = ""

    private var process: Process?

    /// Common install locations for the deskflow engine on macOS.
    func findEngineBinary() -> String? {
        let candidates = [
            "/Applications/Deskflow.app/Contents/MacOS/deskflow-core",
            "/opt/homebrew/bin/deskflow-core",
            "/usr/local/bin/deskflow-core",
        ]
        return candidates.first { FileManager.default.fileExists(atPath: $0) }
    }

    var isInstalled: Bool { findEngineBinary() != nil }

    /// deskflow screen names are lowercase, no spaces.
    static func sanitize(_ name: String) -> String {
        let mapped = name.lowercased().map { ($0.isLetter || $0.isNumber || $0 == "-" || $0 == "_") ? $0 : "-" }
        let s = String(mapped)
        return s.isEmpty ? "macbook" : s
    }

    /// Write the Qt-INI settings file the engine needs in client mode.
    private func writeSettings(serverIP: String, port: Int, screenName: String, tls: Bool) -> URL {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Cozy", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let ini = """
        [core]
        coreMode=client
        computerName=\(screenName)
        port=\(port)
        [client]
        remoteHost=\(serverIP)
        [security]
        tlsEnabled=\(tls ? "true" : "false")
        """
        let url = dir.appendingPathComponent("cozy-client-settings.conf")
        try? ini.write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    /// Connect this Mac to the PC server as a client.
    func start(serverIP: String, port: Int, screenName: String, tls: Bool) {
        guard !isRunning else { return }
        guard let bin = findEngineBinary() else {
            append("Cozy engine not found. Run macos/install-cozy-mac.sh first.")
            return
        }
        let name = Self.sanitize(screenName)
        let settings = writeSettings(serverIP: serverIP, port: port, screenName: name, tls: tls)

        let p = Process()
        p.executableURL = URL(fileURLWithPath: bin)
        p.arguments = ["client", "-s", settings.path, "--new-instance"]

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
            append("Connecting to \(serverIP):\(port) as \"\(name)\"…")
            append("Make sure this Mac's name is in the PC server's screen layout.")
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
