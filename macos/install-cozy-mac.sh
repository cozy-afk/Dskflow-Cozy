#!/usr/bin/env bash
# Cozy — macOS setup.
#
# There is NO custom Cozy app for macOS: the Mac participates in the same
# keyboard/mouse network as a stock *deskflow* peer (client or server). This
# script installs deskflow and points you at the next steps.
#
# Usage:  bash install-cozy-mac.sh
set -euo pipefail

echo "== Cozy macOS setup =="

# 1. Homebrew
if ! command -v brew >/dev/null 2>&1; then
  echo "Homebrew not found. Installing it (you'll be prompted for your password)…"
  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
  # make brew available in this shell for both Apple Silicon and Intel layouts
  eval "$([ -x /opt/homebrew/bin/brew ] && /opt/homebrew/bin/brew shellenv || /usr/local/bin/brew shellenv)"
fi

# 2. deskflow
echo "Installing deskflow…"
if brew install --cask deskflow 2>/dev/null; then
  echo "Installed deskflow via Homebrew cask."
else
  echo "Cask install failed (cask may not exist yet)."
  echo "Download the macOS .dmg manually from: https://github.com/deskflow/deskflow/releases"
  open "https://github.com/deskflow/deskflow/releases" || true
fi

cat <<'NEXT'

== Next steps ==
1. Launch Deskflow (Applications).
2. Grant the macOS permissions it asks for:
   System Settings → Privacy & Security →
     • Accessibility   → enable Deskflow
     • Input Monitoring → enable Deskflow
   (macOS requires both for software KVMs to inject/read input.)
3. Decide this Mac's role:
   • If the keyboard/mouse you want to share is on the Windows PC →
       run the Mac as a CLIENT and enter the PC's IP: 192.168.50.33
   • If the Mac has the keyboard/mouse → run it as the SERVER instead.
4. Arrange screen positions in Deskflow's layout (or import the .conf exported
   from cozy-layout-editor/index.html).

The Android tablet uses the separate Cozy APK — see ../SETUP.md.
NEXT
echo "Done."
