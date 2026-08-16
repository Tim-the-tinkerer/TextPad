#!/bin/bash
set -e

cd "$(dirname "$0")"

ICON_SOURCE="Resources/icon_base.png"
ICON_OUTPUT="Resources/AppIcon.icns"

if [ ! -f "$ICON_SOURCE" ]; then
  echo "Missing icon_base.png"
  exit 1
fi

ICONSET="$(mktemp -d)/icon.iconset"
mkdir -p "$ICONSET"
trap 'rm -rf "$(dirname "$ICONSET")"' EXIT

make_icon() {
  sips -s format png -z "$2" "$2" "$1" --out "$3" >/dev/null
}

make_icon "$ICON_SOURCE" 16  "$ICONSET/icon_16x16.png"
make_icon "$ICON_SOURCE" 32  "$ICONSET/icon_16x16@2x.png"
make_icon "$ICON_SOURCE" 32  "$ICONSET/icon_32x32.png"
make_icon "$ICON_SOURCE" 64  "$ICONSET/icon_32x32@2x.png"
make_icon "$ICON_SOURCE" 128 "$ICONSET/icon_128x128.png"
make_icon "$ICON_SOURCE" 256 "$ICONSET/icon_128x128@2x.png"
make_icon "$ICON_SOURCE" 256 "$ICONSET/icon_256x256.png"
make_icon "$ICON_SOURCE" 512 "$ICONSET/icon_256x256@2x.png"
make_icon "$ICON_SOURCE" 512 "$ICONSET/icon_512x512.png"
make_icon "$ICON_SOURCE" 1024 "$ICONSET/icon_512x512@2x.png"

iconutil -c icns "$ICONSET" -o "$ICON_OUTPUT"
chmod 644 "$ICON_OUTPUT"

echo "Created AppIcon.icns"
