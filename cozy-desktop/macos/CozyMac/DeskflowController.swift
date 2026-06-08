import Foundation

/// Hides deskflow on macOS. Finds the deskflow-core engine and runs it as a
/// background CLIENT that connects to the PC server. The user only sees Cozy.
///
/// Verified against deskflow 1.26 (CLI confirmed on the Windows side): the engine
/// is driven by `deskflow-core client -s <settingsFile> --new-instance`, where the
/// settings file is a Qt-INI with the server address, name, port and TLS flag.
@MainActor
final class DeskflowController: ObservableObject {
    enum Role { case none, server, client }

    @Published var isRunning = false
    @Published var role: Role = .none
    @Published var log: String = ""

    private var process: Process?

    /// This Mac's screen name in deskflow.
    var macName: String { Self.sanitize(Host.current().localizedName ?? "macbook") }

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

    /// Write a server config (this Mac in the middle, each peer on an edge) + settings.
    private func writeServerSettings(peers: [String], port: Int, tls: Bool) -> URL {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Cozy", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)

        let sides = ["left", "right", "up", "down"]
        let opp = ["left": "right", "right": "left", "up": "down", "down": "up"]
        var screens = "section: screens\n\t\(macName):\n"
        var links = "section: links\n\t\(macName):\n"
        for (i, peer) in peers.enumerated() { screens += "\t\(peer):\n"; links += "\t\t\(sides[i % 4]) = \(peer)\n" }
        for (i, peer) in peers.enumerated() { links += "\t\(peer):\n\t\t\(opp[sides[i % 4]]!) = \(macName)\n" }
        let conf = screens + "end\n" + links + "end\n"
        let confURL = dir.appendingPathComponent("cozy-server.conf")
        try? conf.write(to: confURL, atomically: true, encoding: .utf8)

        let ini = """
        [core]
        coreMode=server
        computerName=\(macName)
        port=\(port)
        [security]
        tlsEnabled=\(tls ? "true" : "false")
        [server]
        externalConfig=true
        externalConfigFile=\(confURL.path)
        """
        let url = dir.appendingPathComponent("cozy-server-settings.conf")
        try? ini.write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    /// Connect this Mac to the PC server as a client.
    func start(serverIP: String, port: Int, screenName: String, tls: Bool) {
        let name = Self.sanitize(screenName)
        let settings = writeSettings(serverIP: serverIP, port: port, screenName: name, tls: tls)
        launch(args: ["client", "-s", settings.path, "--new-instance"],
               summary: "Connecting to \(serverIP):\(port) as \"\(name)\"…", role: .client)
    }

    /// Become the controller: run as server so the PC + tablets receive from this Mac.
    func startServer(peers: [String], port: Int, tls: Bool) {
        let settings = writeServerSettings(peers: peers, port: port, tls: tls)
        launch(args: ["server", "-s", settings.path, "--new-instance"],
               summary: "Controlling from this Mac on port \(port).", role: .server)
    }

    private func launch(args: [String], summary: String, role: Role) {
        guard !isRunning else { return }
        guard let bin = findEngineBinary() else {
            append("Cozy engine not found. Run macos/install-cozy-mac.sh first.")
            return
        }
        let p = Process()
        p.executableURL = URL(fileURLWithPath: bin)
        p.arguments = args
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
            Task { @MainActor in self?.isRunning = false; self?.role = .none; self?.append("Engine stopped.") }
        }
        do {
            append(summary)
            try p.run()
            process = p
            isRunning = true
            self.role = role
        } catch {
            append("Failed to start: \(error.localizedDescription)")
        }
    }

    func stop() {
        process?.terminate()
        process = nil
        isRunning = false
        role = .none
    }

    /// Public log hook for the handoff manager.
    func note(_ s: String) { append(s) }

    private func append(_ s: String) {
        log += s.hasSuffix("\n") ? s : s + "\n"
    }
}
