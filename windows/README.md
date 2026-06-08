# Cozy on Windows

**There is no separate Cozy app for Windows.** The PC runs stock **deskflow** as the
**server** — the machine whose physical keyboard and mouse you're sharing. Cozy only
adds the Android client; Windows uses deskflow unchanged.

## Install
Run in an **elevated** PowerShell (Run as administrator — deskflow's installer needs it):
```powershell
powershell -ExecutionPolicy Bypass -File install-cozy-windows.ps1
```
This installs deskflow via winget, prints your LAN IP, and opens the firewall for port
24800/TCP.

## Configure
1. Launch **Deskflow** → choose **Server**.
2. Add a screen for the tablet (name must match the client name you enter in the Cozy
   app) and any Mac; drag them into position. You can generate this layout in
   `..\cozy-layout-editor\index.html` and paste the exported `.conf`.
3. For the first connection, **disable encryption/TLS** (Settings → Security), and untick
   **Use TLS** in the Cozy app so both sides match.
4. Start the server.

This PC's IP on the current network is **192.168.50.33** (re-check with the script if your
network changes).

See `..\SETUP.md` for the complete end-to-end guide.
