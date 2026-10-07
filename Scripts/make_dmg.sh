#!/usr/bin/env bash
# Build a universal ShelfDrop.app and wrap it in a drag-to-Applications disk image.
# Usage: Scripts/make_dmg.sh        ->  build/ShelfDrop-<version>.dmg
set -euo pipefail

cd "$(dirname "$0")/.."

Scripts/build_app.sh release universal

APP="build/ShelfDrop.app"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' Resources/Info.plist)"
DMG="build/ShelfDrop-$VERSION.dmg"

# Staging folder = what the user sees when the image is opened: the app next to an
# "Applications" shortcut, so installing is a single drag.
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"

rm -f "$DMG"
# hdiutil prints a deprecation warning on the newest macOS (in favour of `diskutil image create`),
# but it works and, unlike diskutil's new subcommand, exists on every macOS version.
hdiutil create -volname "ShelfDrop" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG" >/dev/null
hdiutil verify "$DMG" >/dev/null

echo "Created $DMG ($(du -h "$DMG" | cut -f1))"
