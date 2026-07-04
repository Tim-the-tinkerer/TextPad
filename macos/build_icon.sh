#!/bin/bash
set -e

cd "$(dirname "$0")"

if [ ! -f icon_base.png ]; then
  echo "Missing icon_base.png"
  exit 1
fi

ICONSET="$(mktemp -d)/icon.iconset"
mkdir -p "$ICONSET"
trap 'rm -rf "$(dirname "$ICONSET")"' EXIT

make_icon() {
  sips -s format png -z "$2" "$2" "$1" --out "$3" >/dev/null
}

make_icon icon_base.png 16  "$ICONSET/icon_16x16.png"
make_icon icon_base.png 32  "$ICONSET/icon_16x16@2x.png"
make_icon icon_base.png 32  "$ICONSET/icon_32x32.png"
make_icon icon_base.png 64  "$ICONSET/icon_32x32@2x.png"
make_icon icon_base.png 128 "$ICONSET/icon_128x128.png"
make_icon icon_base.png 256 "$ICONSET/icon_128x128@2x.png"
make_icon icon_base.png 256 "$ICONSET/icon_256x256.png"
make_icon icon_base.png 512 "$ICONSET/icon_256x256@2x.png"
make_icon icon_base.png 512 "$ICONSET/icon_512x512.png"
make_icon icon_base.png 1024 "$ICONSET/icon_512x512@2x.png"

iconutil -c icns "$ICONSET" -o AppIcon.icns

echo "Created AppIcon.icns"