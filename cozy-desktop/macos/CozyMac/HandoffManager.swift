import Foundation

/// Orchestrates multi-controller handoff on the Mac, mirroring the Windows side:
/// watch for physical input here → become the controller (server) and claim;
/// when the peer claims → become a client of the peer. ~700ms switch, 1.5s anti-thrash.
@MainActor
final class HandoffManager: ObservableObject {
    @Published var enabled = false
    @Published var peerIP = "192.168.50.33"   // the PC, by default

    private let input = LocalInputMonitor()
    private let peer = PeerCoordinator()
    private weak var controller: DeskflowController?
    private var port = 24800
    private var tls = false
    private var lastSwitch: TimeInterval = 0

    func bind(_ c: DeskflowController) { controller = c }

    private func peerNames() -> [String] {
        peer.peerName.isEmpty ? ["windows-pc"] : [peer.peerName]
    }

    /// Press Connect with multi-controller on: this Mac begins as the controller.
    func startAsController(port: Int, tls: Bool) {
        guard let c = controller else { return }
        self.port = port
        self.tls = tls

        peer.onPeerClaim = { [weak self] name, ip in Task { @MainActor in self?.becomeClient(name: name, ip: ip) } }
        peer.onLog = { [weak self] s in Task { @MainActor in self?.controller?.note(s) } }
        input.onPhysicalActivity = { [weak self] in self?.onLocalActivity() }

        peer.start(myName: c.macName, peerHost: peerIP)
        input.start()
        c.startServer(peers: peerNames(), port: port, tls: tls)
        peer.broadcastClaim()
        c.note("Multi-controller on — type on this Mac to keep control; type on the PC to hand it over.")
    }

    func stop() {
        input.stop()
        peer.stop()
    }

    private func onLocalActivity() {
        guard enabled, let c = controller, c.isRunning, c.role != .server else { return }
        let now = Date().timeIntervalSince1970
        if now - lastSwitch < 1.5 { return }
        lastSwitch = now
        c.note("You're using this Mac — taking control here.")
        peer.broadcastClaim()
        c.stop()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.7) { [weak self] in
            guard let self, let c = self.controller else { return }
            c.startServer(peers: self.peerNames(), port: self.port, tls: self.tls)
        }
    }

    private func becomeClient(name: String, ip: String) {
        guard enabled, let c = controller, c.isRunning else { return }
        let now = Date().timeIntervalSince1970
        if now - lastSwitch < 1.5 { return }
        lastSwitch = now
        c.note("\(name) took control — this Mac will receive from \(ip).")
        c.stop()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.7) { [weak self] in
            guard let self, let c = self.controller else { return }
            c.start(serverIP: ip, port: self.port, screenName: c.macName, tls: self.tls)
        }
    }
}
