# Cozy on macOS

**There is no separate Cozy app to install on a Mac.** The Cozy *client* (the new
code in this repo) is Android-only, because it's built on Android's accessibility
input APIs. A Mac joins the same keyboard/mouse network as a stock **deskflow** peer.

## Install
```bash
bash install-cozy-mac.sh
```
This installs Homebrew (if needed) and deskflow, then prints the configuration steps.

## The macOS permissions that always trip people up
macOS blocks input injection by default. After installing, go to
**System Settings → Privacy & Security** and enable **Deskflow** under **both**:
- **Accessibility**
- **Input Monitoring**

Without both, the cursor won't cross onto/off the Mac.

## Roles
- **PC has the keyboard/mouse** (most common): run the Mac as a **client**, pointed at
  the PC server `192.168.50.33:24800`.
- **Mac has the keyboard/mouse**: run the Mac as the **server**, and the PC + tablet
  connect to it.

## Layout
Arrange where the Mac sits relative to the PC and tablet in deskflow's GUI, or generate
a `.conf` from `../cozy-layout-editor/index.html` and load it.
