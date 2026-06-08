#!/usr/bin/env bash
# Build the Cozy macOS app from source into a double-clickable Cozy.app.
#
# Prereqs (macOS Sonoma): Xcode command-line tools. If `swift` is missing, run:
#   xcode-select --install
#
# Usage:  bash build-mac.sh   →   then: open Cozy.app
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v swift >/dev/null 2>&1; then
  echo "Swift not found. Install Xcode command-line tools: xcode-select --install"
  exit 1
fi

echo "Building (release)…"
swift build -c release

BIN="$(swift build -c release --show-bin-path)/CozyMac"
APP="Cozy.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp "$BIN" "$APP/Contents/MacOS/Cozy"

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>            <string>Cozy</string>
  <key>CFBundleDisplayName</key>     <string>Cozy</string>
  <key>CFBundleIdentifier</key>      <string>com.cozy.mac</string>
  <key>CFBundleExecutable</key>      <string>Cozy</string>
  <key>CFBundlePackageType</key>     <string>APPL</string>
  <key>CFBundleShortVersionString</key> <string>0.1.0</string>
  <key>CFBundleVersion</key>         <string>1</string>
  <key>LSMinimumSystemVersion</key>  <string>13.0</string>
  <key>NSHighResolutionCapable</key> <true/>
  <key>NSPrincipalClass</key>        <string>NSApplication</string>
</dict>
</plist>
PLIST

# Ad-hoc sign so macOS lets it run and remembers its TCC (permission) grants.
codesign --force --deep --sign - "$APP" 2>/dev/null || true

echo ""
echo "Built ./$APP"
echo "Run it:   open Cozy.app"
echo ""
echo "First launch on Sonoma: grant BOTH of these to Cozy (and to Deskflow) in"
echo "System Settings -> Privacy & Security:  Accessibility  and  Input Monitoring."
