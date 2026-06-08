# Cozy Desktop — the redesigned apps

Native, friendly desktop apps that replace deskflow's UI. **deskflow still does the hard
input-engine work, but it's completely hidden** — the apps launch, configure, and monitor
it as a background process. You only ever see Cozy.

| App | Stack | Role | Status |
|-----|-------|------|--------|
| `windows/CozyDesktop` | **WPF / .NET 8** (native, Fluent-styled) | PC = **server** | ✅ builds → `Cozy-Windows-0.1.0.exe` in Releases |
| `macos/CozyMac` | **SwiftUI** (native) | Mac = **client** | ⚠️ source only — build in Xcode (no Mac here to compile) |

## Why deskflow is still under the hood
The genuinely hard part of a software KVM is OS-level input hooking + edge detection
(Win32 `SetWindowsHookEx`, macOS `CGEventTap`, cursor warping, countless edge cases).
deskflow has solved that over years. Cozy keeps that engine and replaces everything the
user sees: a clean one-window flow — pick where the tablet sits, press **Start Sharing**.

## Windows app
- **Run it:** download `Cozy-Windows-0.1.0.exe` from Releases and double-click. It's
  self-contained (no .NET install needed).
- First launch shows a **one-time "Set up Cozy"** button that installs the engine
  (deskflow) for you (accept the admin prompt).
- Then: choose the tablet's side, press **Start Sharing**. The app writes the deskflow
  config and runs the server invisibly; the Activity panel shows live status.
- **Build from source:**
  ```powershell
  dotnet publish cozy-desktop/windows/CozyDesktop -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  ```

## macOS app
SwiftUI source in `macos/CozyMac/`. To build:
1. Open Xcode → **New Project → macOS → App** (SwiftUI, name it Cozy).
2. Replace the generated `App` / `ContentView` with the three files in `macos/CozyMac/`.
3. In *Signing & Capabilities*, the app will request **Accessibility** + **Input
   Monitoring** at runtime (required for input).
4. Build & run. It connects to the PC server (default `192.168.50.33:24800`).
   Install the engine first with `macos/install-cozy-mac.sh`.

## Wiring note (engine CLI)
`DeskflowController` launches the engine with classic synergy/barrier-style flags
(`-f --no-tray --name … --address :port -c config`). deskflow's exact flag names can vary
by version — if the engine starts but nothing connects, check the Activity log and adjust
the argument string in `DeskflowController` (one place, clearly commented).
