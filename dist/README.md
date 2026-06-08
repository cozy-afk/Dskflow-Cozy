# Cozy — install files

| File | Platform | What it is |
|------|----------|-----------|
| `Cozy-0.1.0-debug.apk` | **Android** | The Cozy client app, ready to install (debug-signed). 5.4 MB. |
| `install-android.ps1` | Android (via PC) | One-shot installer over USB using the bundled `adb`. |
| `../macos/install-cozy-mac.sh` | **macOS** | Installs & configures deskflow on the Mac (no custom app needed there). |

## Install on Android — two ways

**A. Over USB from the PC (easiest):**
```powershell
powershell -ExecutionPolicy Bypass -File dist\install-android.ps1
```
First enable **Developer options → USB debugging** on the tablet (Settings → About → tap
"Build number" 7×), plug in USB, and accept the trust prompt.

**B. Sideload:** copy `Cozy-0.1.0-debug.apk` to the tablet (USB, Drive, email) and tap it.
Allow "install from unknown sources" if prompted.

After install, open **Cozy** and:
1. *Enable accessibility* → turn on "Cozy input".
2. *Enable Cozy keyboard* (for typing).
3. Enter the PC server IP **192.168.50.33**, port **24800**, a client name, **Connect**.

See `../SETUP.md` for the full server-side setup and troubleshooting.

## Install on macOS
```bash
bash macos/install-cozy-mac.sh
```
Then grant **Accessibility** + **Input Monitoring** to Deskflow in System Settings, and run it
as a client of the PC (or as the server). See `../macos/README.md`.

---
### Notes
- This is a **debug** APK (signed with the auto-generated debug key) — fine for personal use.
  A `release` build (`gradlew assembleRelease` with your own signing key) is for distribution.
- Rebuild any time with:
  `./.tools/gradle-8.10.2/bin/gradle.bat -p cozy-client :app:assembleDebug` (JAVA_HOME set to the JDK 17).
