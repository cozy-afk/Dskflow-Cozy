import SwiftUI

struct ContentView: View {
    @StateObject private var controller = DeskflowController()
    @StateObject private var handoff = HandoffManager()

    @AppStorage("serverIP") private var serverIP = "192.168.50.33"
    @AppStorage("port") private var port = 24800
    @AppStorage("screenName") private var screenName = "macbook"
    @AppStorage("tls") private var tls = true

    private let accent = Color(red: 0.357, green: 0.549, blue: 1.0)

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                // Header
                VStack(alignment: .leading, spacing: 2) {
                    Text("Cozy").font(.system(size: 32, weight: .bold))
                    Text("Share one keyboard & mouse with your PC and tablet.")
                        .foregroundStyle(.secondary)
                }

                // Status + action
                card {
                    HStack {
                        VStack(alignment: .leading, spacing: 6) {
                            HStack(spacing: 8) {
                                Circle()
                                    .fill(controller.isRunning ? Color.green : Color.secondary)
                                    .frame(width: 12, height: 12)
                                Text(controller.isRunning ? "Connected" : "Not connected")
                                    .font(.title3.weight(.semibold))
                            }
                            Text(controller.isRunning
                                 ? "Your PC's mouse can now cross onto this Mac."
                                 : "Connect to your PC to start sharing.")
                                .foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button(controller.isRunning ? "Disconnect" : "Connect") {
                            if controller.isRunning { controller.stop(); handoff.stop() }
                            else if handoff.enabled { handoff.startAsController(port: port, tls: tls) }
                            else { controller.start(serverIP: serverIP, port: port, screenName: screenName, tls: tls) }
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(accent)
                        .controlSize(.large)
                        .disabled(!controller.isInstalled)
                    }
                }

                if !controller.isInstalled {
                    card {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("One-time setup needed").font(.headline)
                            Text("Install the Cozy engine with the included script: macos/install-cozy-mac.sh, then grant Accessibility + Input Monitoring permissions.")
                                .foregroundStyle(.secondary)
                        }
                    }
                }

                // Settings
                card {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("Connection").font(.headline)
                        labeledField("PC IP address", text: $serverIP)
                        HStack {
                            VStack(alignment: .leading) {
                                Text("This Mac's name").font(.caption).foregroundStyle(.secondary)
                                TextField("macbook", text: $screenName).textFieldStyle(.roundedBorder)
                            }
                            VStack(alignment: .leading) {
                                Text("Port").font(.caption).foregroundStyle(.secondary)
                                TextField("24800", value: $port, format: .number.grouping(.never))
                                    .textFieldStyle(.roundedBorder).frame(width: 90)
                            }
                        }
                        Toggle("Use encryption (TLS)", isOn: $tls)
                    }
                }

                // Multi-controller (beta)
                card {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Multi-controller (beta)").font(.headline)
                        Text("Type on whichever machine you're at and it takes over. Pairs with the PC's Cozy. ~1s switch; untested across machines.")
                            .font(.caption).foregroundStyle(.secondary)
                        Toggle("Hand control between this Mac and the PC", isOn: $handoff.enabled)
                        labeledField("The PC's IP address", text: $handoff.peerIP)
                        Text(roleText).font(.caption).foregroundStyle(.secondary)
                    }
                }

                // Activity
                card {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Activity").font(.headline)
                        ScrollView {
                            Text(controller.log.isEmpty ? "No activity yet." : controller.log)
                                .font(.system(.caption, design: .monospaced))
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .textSelection(.enabled)
                        }
                        .frame(height: 140)
                    }
                }
            }
            .padding(24)
        }
        .onAppear { handoff.bind(controller) }
    }

    private var roleText: String {
        switch controller.role {
        case .server: return "Role: controlling (this Mac drives)"
        case .client: return "Role: receiving (the PC drives)"
        case .none: return "Role: idle"
        }
    }

    @ViewBuilder private func card<Content: View>(@ViewBuilder _ content: () -> Content) -> some View {
        content()
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 14).fill(Color(NSColor.controlBackgroundColor)))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.secondary.opacity(0.15)))
    }

    @ViewBuilder private func labeledField(_ label: String, text: Binding<String>) -> some View {
        VStack(alignment: .leading) {
            Text(label).font(.caption).foregroundStyle(.secondary)
            TextField(label, text: text).textFieldStyle(.roundedBorder)
        }
    }
}

#Preview { ContentView() }
