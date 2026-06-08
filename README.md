# Cozy — one keyboard & mouse across PC, Mac, and Android

A project built **on top of [deskflow](https://github.com/deskflow/deskflow)** to share a single
keyboard and mouse across computers *and an Android tablet that keeps running its own apps* —
your cursor crosses the screen edge onto the tablet, just like deskflow does between desktops.

> **Status:** early scaffold. The PC↔Mac half works today with stock deskflow. The Android
> client and the layout editor in this repo are new and **not yet end-to-end tested** (no
> Android toolchain/device was available where they were written — see *Building* below).

## Why a fork wasn't enough
Deskflow officially supports **Windows, macOS, Linux** — not Android. Android won't let an app
inject mouse/keyboard into *other* apps without root or an **Accessibility Service**. So the only
new code we need is an **Android client**; the deskflow **server** already captures input, detects
edge-crossings, and arranges screens. We reuse it unchanged.

```
Windows PC (stock deskflow server)  ──TCP 24800, deskflow protocol──▶  Android tablet (this app)
  • captures kbd/mouse                                                   • overlay cursor
  • drag-to-arrange layout                                              • AccessibilityService taps
  • detects edge crossings                                              • (IME for typing — phase 2)
```

## What's in this repo
| Path | What it is | Runnable now? |
|------|-----------|---------------|
| `deskflow/` | Upstream deskflow source (the base/reference) | Build per upstream docs |
| `cozy-client/` | **New** Android client — speaks the deskflow protocol, injects via accessibility | Needs Android Studio |
| `cozy-layout-editor/index.html` | **New** drag-and-drop device/display grid editor → exports server config | ✅ open in any browser |

## The Android client (`cozy-client/`)
A byte-faithful Kotlin reimplementation of the deskflow **client** side:
- `protocol/` — wire codec + `DeskflowClient` (handshake, `QINF`→`DINF`, mouse/key/wheel decode, `CALV` keep-alive).
- `service/CozyAccessibilityService` — draws the overlay cursor and injects taps/long-press/drag/scroll via `dispatchGesture()`.
- `service/ConnectionService` — foreground service holding the TCP connection.
- `ui/MainActivity` — enter PC IP, enable accessibility, connect.

**Honest limits of the no-root accessibility path** (these are by design, not bugs):
- Password fields, DRM video, and many games (`FLAG_SECURE`) reject injected taps.
- Cursor motion is a synthesized overlay + discrete gestures — a hair less smooth than native.
- **Keyboard typing is phase 2**: full keysym→text needs a companion `InputMethodService`. Mouse
  (move/click/drag/scroll) is implemented first because that's the headline feature.

### Building it
1. Open `cozy-client/` in **Android Studio** (Giraffe+); let it download the Gradle/Android SDK.
2. Build & install on the tablet (USB debugging on).
3. On the tablet: open **Cozy** → *Enable accessibility* → turn on "Cozy input".
4. Enter the PC's LAN IP, keep port `24800`, set a client name, **Connect**.

## The layout editor (`cozy-layout-editor/index.html`)
Open it in a browser. Drag each **display** tile around a snapping grid to say where devices sit
relative to each other. Devices with **multiple displays** get one tile per display (shared colour),
and each display participates in the grid independently. It exports two things:
- **deskflow `.conf`** — one screen per device (collapses multi-display to the primary, because the
  stock protocol models one screen per machine). Paste into your deskflow server config.
- **Cozy layout JSON** — the *rich* per-display graph, for a future Cozy-enhanced server that can
  cross the cursor between individual displays (beyond deskflow's one-screen-per-device model).

## Setting up the server (PC)
1. Install deskflow on the PC (`winget install Deskflow.Deskflow`, accept the admin prompt).
2. Run it as **server**, add a client screen whose name matches the tablet's client name.
3. Arrange screens (deskflow's own GUI, or generate the `.conf` from the layout editor).

## Roadmap
- [ ] End-to-end test Android client against a live deskflow server
- [ ] Companion IME for full keyboard typing
- [ ] TLS (deskflow uses an SSL handshake by default — may need to disable TLS on the server for first bring-up)
- [ ] Cozy-enhanced server consuming the rich per-display layout
