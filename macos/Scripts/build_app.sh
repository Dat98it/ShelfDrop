#!/usr/bin/env bash
# Build ShelfDrop with SwiftPM and assemble a signed .app bundle (no Xcode needed).
# Usage: Scripts/build_app.sh [release|debug] [universal]
#   universal  build for both Apple Silicon and Intel (needed for an app that runs on any Mac)
set -euo pipefail

cd "$(dirname "$0")/.."
CONFIG="${1:-release}"
APP="build/ShelfDrop.app"

ARCH_FLAGS=()
if [[ "${2:-}" == "universal" ]]; then
    ARCH_FLAGS=(--arch arm64 --arch x86_64)
fi

# ${ARCH_FLAGS[@]+...}: expand to nothing when empty (bash 3.2 on macOS fails on an empty array under set -u).
swift build -c "$CONFIG" ${ARCH_FLAGS[@]+"${ARCH_FLAGS[@]}"}
BIN="$(swift build -c "$CONFIG" ${ARCH_FLAGS[@]+"${ARCH_FLAGS[@]}"} --show-bin-path)/ShelfDrop"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/ShelfDrop"
cp Resources/Info.plist "$APP/Contents/Info.plist"
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

# Ad-hoc signature: enough to run on the machine that built it.
codesign --force --sign - "$APP"

echo "Built $APP ($(lipo -archs "$APP/Contents/MacOS/ShelfDrop"))"
