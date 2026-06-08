# Cozy — first end-to-end setup (Windows PC + Android tablet)

Goal: move your PC mouse across the screen edge onto the tablet, and type into the
tablet's apps. This walks through it from nothing. Budget ~45–60 min the first time.

---

## Part A — The server (your Windows PC)

### 1. Install deskflow
The earlier `winget` install failed because it needs admin. Run an **elevated** PowerShell
(Start → type "PowerShell" → *Run as administrator*) and:
```powershell
winget install --id Deskflow.Deskflow --accept-package-agreements --accept-source-agreements
```
If it still fails, download the installer from <https://deskflow.org> and run it.

### 2. Find your PC's LAN IP (you'll type this into the tablet)
```powershell
(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.PrefixOrigin -eq 'Dhcp' }).IPAddress
```
Note the `192.168.x.x` (or `10.x.x.x`) address.

### 3. Configure deskflow as the server
1. Launch **Deskflow**. Choose **Server** (this PC has the keyboard/mouse).
2. Open the server configuration / screen layout.
3. Add a new screen; set its **name** to exactly what you'll use on the tablet
   (e.g. `android-tablet`). Drag it to the side of your PC screen where you want the
   cursor to cross over.
   - *Tip:* you can generate this layout visually in `cozy-layout-editor/index.html`
     and paste the exported `.conf`.
4. **For first bring-up, disable TLS/encryption** in deskflow settings (Settings →
   Security → turn off "Enable encryption"/TLS). This removes one moving part. Once it
   works you can turn TLS back on and tick "Use TLS" in the Cozy app.
5. Make sure **Windows Firewall** allows deskflow on the private network (you'll usually
   get a prompt the first time — click *Allow*). Port is **24800/TCP**.
6. Start the server. It should say it's waiting for clients.

---

## Part B — The client (your Android tablet)

### 4. Build the APK
On the PC:
1. Install **Android Studio** (<https://developer.android.com/studio>).
2. `File → Open` → select `C:\Users\theco\Dskflow-Cozy\cozy-client`.
3. Let Gradle sync and download the SDK (first time is slow).
4. Plug in the tablet with a USB cable. On the tablet, enable **Developer options**
   (Settings → About → tap "Build number" 7×) and **USB debugging**.
5. Press **Run ▶**. Android Studio installs and launches **Cozy** on the tablet.
   - No cable? Use `Build → Build APK`, copy the APK to the tablet, and install it.

### 5. Grant the two permissions (this is the no-root price of admission)
In the Cozy app:
1. Tap **Enable accessibility** → find **Cozy input** under Installed/Downloaded
   services → turn it **on**. (This is what draws the cursor and taps.)
2. Tap **Enable Cozy keyboard** → enable **Cozy keyboard** in the list. To actually
   type, you'll also *switch* to it (keyboard switcher) when a text field is focused.

### 6. Connect
1. Make sure the tablet is on the **same Wi-Fi** as the PC.
2. In Cozy: enter the PC's **IP**, port **24800**, the **client name** matching the
   server config (`android-tablet`), and **untick "Use TLS"** if you disabled
   encryption on the server.
3. Tap **Connect**. The status should go Connecting → Handshaking → **Connected ✓**.
4. On the PC, push your mouse off the edge toward the tablet's position in the layout.
   The Cozy cursor should appear on the tablet and follow your mouse. Click to tap.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Stuck on "Connecting…" | Wrong IP, firewall blocking 24800, or not same Wi-Fi. Test from PC: `Test-NetConnection <tabletIP> -Port 24800` is the wrong direction — instead confirm the server is listening and the tablet can reach the PC. |
| "Handshaking" then error | TLS mismatch. Either disable encryption on the server **and** untick "Use TLS", or keep both on. They must match. |
| Server log says "unknown client" | The client **name** in Cozy must exactly match the screen name in the server config. |
| Cursor shows but taps do nothing | Accessibility service got disabled (Android does this after some reboots/updates). Re-enable it. Also note password fields & many games block injected taps by design. |
| Typing doesn't work | You enabled the Cozy keyboard but didn't **switch** to it, or the target isn't a text field. Tap a text box, switch input method to "Cozy keyboard". |
| Movement feels laggy | Wi-Fi latency. Use 5 GHz, put the PC on Ethernet, keep both near the router. |

---

## What "working" looks like for v0.1
- ✅ Mouse crosses onto the tablet, cursor follows, click/long-press/drag/scroll work.
- ✅ Typing into normal text fields via the Cozy keyboard.
- ⛔ Not yet: password fields, DRM/secure apps, many games (accessibility limitation).
- ⛔ Not yet: per-display crossing on multi-monitor devices (needs the Cozy-enhanced
  server that consumes `cozy-layout.json`; the editor already exports it).
