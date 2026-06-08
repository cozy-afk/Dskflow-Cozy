# Cozy on macOS (Sonoma)

There are **two** macOS pieces, and which you want depends on your goal:

1. **The redesigned Cozy app** (native SwiftUI) — friendly UI, hides deskflow.
   Lives in `cozy-desktop/macos/`. You build it on your Mac (see below).
2. **The raw engine installer** (`install-cozy-mac.sh`) — just installs deskflow if
   you'd rather not use the Cozy app yet.

Either way the Mac participates as a stock deskflow peer under the hood — there's no
precompiled Mac binary in Releases because a Mac app can only be built on a Mac.

---

## Build the Cozy macOS app (one command)

You need **Xcode command-line tools** (`swift`). If missing: `xcode-select --install`.

**Option A — download the source package**
1. On the Releases page, download **`Cozy-macOS-source.zip`** and unzip it.
2. In Terminal:
   ```bash
   cd path/to/unzipped/macos
   bash build-mac.sh
   open Cozy.app
   ```

**Option B — clone the repo**
```bash
git clone https://github.com/cozy-afk/Dskflow-Cozy.git
cd Dskflow-Cozy/cozy-desktop/macos
bash build-mac.sh
open Cozy.app
```

`build-mac.sh` compiles the SwiftUI source and assembles a double-clickable **Cozy.app**.

## Install the engine + grant permissions
1. Install deskflow (the hidden engine):
   ```bash
   bash install-cozy-mac.sh
   ```
2. Sonoma blocks input control by default. In **System Settings → Privacy & Security**,
   enable **both Accessibility and Input Monitoring** for **Cozy** (and for **Deskflow**
   if it appears). Without both, the cursor won't cross onto the Mac.

## Use it
Open Cozy, enter the PC server's IP (**192.168.50.33**), port **24800**, a name for this
Mac, and Connect. Then move the PC's mouse toward the Mac.

> Heads-up (multi-device): the v0.1 Windows app currently puts one client in its layout
> (the "tablet name" field). To connect the **Mac**, set that field to this Mac's name —
> or use the visual layout editor's exported `.conf` to include the PC, tablet, and Mac
> together. Full multi-device config in one click is the next enhancement.
