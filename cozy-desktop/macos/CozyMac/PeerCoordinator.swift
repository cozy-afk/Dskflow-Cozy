import Foundation
import Network

/// Control channel between Cozy desktops (PC ↔ Mac) for multi-controller handoff.
/// Mirrors the Windows PeerCoordinator: listens on a control port, dials the peer,
/// and exchanges HELLO/CLAIM lines. When a peer claims control, we yield.
final class PeerCoordinator {
    static let controlPort: UInt16 = 24811

    var onPeerClaim: ((_ peerName: String, _ peerIp: String) -> Void)?
    var onLog: ((String) -> Void)?
    private(set) var peerName: String = ""

    private var listener: NWListener?
    private var connections: [NWConnection] = []
    private var myName = ""
    private var peerHost: String?

    func start(myName: String, peerHost: String?) {
        self.myName = myName
        self.peerHost = (peerHost?.isEmpty ?? true) ? nil : peerHost
        startListener()
        if let h = self.peerHost { add(NWConnection(host: NWEndpoint.Host(h),
                                                     port: NWEndpoint.Port(rawValue: Self.controlPort)!,
                                                     using: .tcp)) }
    }

    private func startListener() {
        do {
            let l = try NWListener(using: .tcp, on: NWEndpoint.Port(rawValue: Self.controlPort)!)
            l.newConnectionHandler = { [weak self] conn in self?.add(conn) }
            l.start(queue: .global())
            listener = l
        } catch { onLog?("coordinator listen failed: \(error)") }
    }

    private func add(_ conn: NWConnection) {
        connections.append(conn)
        conn.start(queue: .global())
        send(conn, "HELLO \(myName)")
        receive(conn)
    }

    private func receive(_ conn: NWConnection) {
        conn.receive(minimumIncompleteLength: 1, maximumLength: 4096) { [weak self] data, _, isComplete, _ in
            if let data = data, let s = String(data: data, encoding: .utf8) {
                for line in s.split(separator: "\n") { self?.handle(String(line), conn) }
            }
            if isComplete { conn.cancel() } else { self?.receive(conn) }
        }
    }

    private func handle(_ line: String, _ conn: NWConnection) {
        let parts = line.split(separator: " ").map(String.init)
        guard parts.count >= 2 else { return }
        switch parts[0] {
        case "HELLO":
            peerName = parts[1]
        case "CLAIM" where parts[1] != myName:
            // For a 2-machine setup each side configures the other's IP, so peerHost
            // is the correct address to receive from.
            let ip = peerHost ?? ""
            onLog?("peer \"\(parts[1])\" claimed control")
            onPeerClaim?(parts[1], ip)
        default:
            break
        }
    }

    func broadcastClaim() { for c in connections { send(c, "CLAIM \(myName)") } }

    private func send(_ conn: NWConnection, _ s: String) {
        conn.send(content: (s + "\n").data(using: .utf8), completion: .contentProcessed { _ in })
    }

    func stop() {
        listener?.cancel()
        listener = nil
        connections.forEach { $0.cancel() }
        connections.removeAll()
    }
}
