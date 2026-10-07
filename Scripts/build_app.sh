#!/usr/bin/env bash
# Build ShelfDrop with SwiftPM and assemble a signed .app bundle (no Xcode needed).
# Usage: Scripts/build_app.sh [release|debug]
set -euo pipefail

cd "$(dirname "$0")/.."
CONFIG="${1:-release}"
APP="build/ShelfDrop.app"

swift build -c "$CONFIG"
BIN="$(swift build -c "$CONFIG" --show-bin-path)/ShelfDrop"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/ShelfDrop"
cp Resources/Info.plist "$APP/Contents/Info.plist"

# Ad-hoc signature: enough to run on the machine that built it.
codesign --force --sign - "$APP"

echo "Built $APP"
