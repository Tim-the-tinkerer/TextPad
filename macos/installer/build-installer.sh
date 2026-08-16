#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

APP_NAME="TextPad"
APP_PATH="$ROOT/$APP_NAME.app"
DIST_DIR="$ROOT/dist"
PKG_WORK="$ROOT/installer/pkg-work"
DMG_WORK="$ROOT/installer/dmg-work"
SIGN_IDENTITY="${SIGN_IDENTITY:--}"
BUNDLE_ID="com.textpad.editor"

echo "TextPad macOS installer build"
echo "=============================="

# Build the app if missing or if REBUILD=1
if [ ! -d "$APP_PATH" ] || [ "${REBUILD:-0}" = "1" ]; then
  echo "Building $APP_NAME.app..."
  bash build.sh
fi

if [ ! -d "$APP_PATH" ]; then
  echo "Error: $APP_PATH not found after build."
  exit 1
fi

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP_PATH/Contents/Info.plist")"
BUILD="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$APP_PATH/Contents/Info.plist")"
ARCH="$(uname -m)"
case "$ARCH" in
  arm64)  ARCH_LABEL="arm64" ;;
  x86_64) ARCH_LABEL="x64" ;;
  *)
    echo "Error: unsupported architecture: $ARCH"
    exit 1
    ;;
esac

OUTPUT_BASE="$APP_NAME-$VERSION-mac-$ARCH_LABEL"
DMG_PATH="$DIST_DIR/$OUTPUT_BASE.dmg"
PKG_PATH="$DIST_DIR/$OUTPUT_BASE.pkg"

mkdir -p "$DIST_DIR"
rm -f "$DMG_PATH" "$PKG_PATH"
rm -rf "$PKG_WORK" "$DMG_WORK"
mkdir -p "$PKG_WORK" "$DMG_WORK"

echo "Version: $VERSION (build $BUILD)"
echo "Architecture: $ARCH_LABEL"
echo ""

# Ensure resource files (especially AppIcon.icns) are world-readable in installers.
chmod -R a+rX "$APP_PATH"

# --- PKG installer (copies to /Applications) ---
echo "Creating PKG installer..."

DIST_XML="$PKG_WORK/distribution.xml"
cat > "$DIST_XML" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="2">
    <title>TextPad $VERSION</title>
    <organization>com.textpad</organization>
    <domains enable_localSystem="true"/>
    <options customize="never" require-scripts="false" rootVolumeOnly="true"/>
    <choices-outline>
        <line choice="default">
            <line choice="com.textpad.editor"/>
        </line>
    </choices-outline>
    <choice id="default"/>
    <choice id="com.textpad.editor" visible="false">
        <pkg-ref id="$BUNDLE_ID"/>
    </choice>
    <pkg-ref id="$BUNDLE_ID" version="$VERSION" onConclusion="none">TextPad-component.pkg</pkg-ref>
    <welcome file="welcome.html" mime-type="text/html"/>
    <conclusion file="conclusion.html" mime-type="text/html"/>
</installer-gui-script>
EOF

cat > "$PKG_WORK/welcome.html" <<'EOF'
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <style>
    body { font-family: -apple-system, Helvetica, Arial, sans-serif; font-size: 13px; }
    h1 { font-size: 18px; }
  </style>
</head>
<body>
  <h1>Welcome to TextPad</h1>
  <p>This installer places TextPad in your <strong>Applications</strong> folder.</p>
  <p>TextPad is a lightweight text editor for plain text and rich text, with syntax highlighting, tabs, and large-file support.</p>
</body>
</html>
EOF

cat > "$PKG_WORK/conclusion.html" <<'EOF'
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <style>
    body { font-family: -apple-system, Helvetica, Arial, sans-serif; font-size: 13px; }
    h1 { font-size: 18px; }
  </style>
</head>
<body>
  <h1>Installation Complete</h1>
  <p>TextPad has been installed in <strong>Applications</strong>.</p>
  <p>Open TextPad from Launchpad or Spotlight. Press <strong>F1</strong> inside the app for help.</p>
</body>
</html>
EOF

PKG_ARGS=(
  --component "$APP_PATH"
  --install-location /Applications
  --identifier "$BUNDLE_ID"
  --version "$VERSION"
  --scripts "$ROOT/installer/scripts"
)
if [ "$SIGN_IDENTITY" != "-" ]; then
  PKG_ARGS+=(--sign "$SIGN_IDENTITY")
fi

pkgbuild "${PKG_ARGS[@]}" "$PKG_WORK/TextPad-component.pkg"

PRODUCT_ARGS=(
  --distribution "$DIST_XML"
  --package-path "$PKG_WORK"
  --version "$VERSION"
)
if [ "$SIGN_IDENTITY" != "-" ]; then
  PRODUCT_ARGS+=(--sign "$SIGN_IDENTITY")
fi

productbuild "${PRODUCT_ARGS[@]}" "$PKG_PATH"
echo "PKG: $PKG_PATH"

# --- DMG disk image (drag to Applications) ---
echo ""
echo "Creating DMG installer..."

DMG_STAGING="$DMG_WORK/staging"
mkdir -p "$DMG_STAGING"
ditto "$APP_PATH" "$DMG_STAGING/$APP_NAME.app"
ln -s /Applications "$DMG_STAGING/Applications"

rm -f "$DMG_PATH"
hdiutil create \
  -volname "TextPad $VERSION" \
  -srcfolder "$DMG_STAGING" \
  -ov \
  -format UDZO \
  -imagekey zlib-level=9 \
  "$DMG_PATH" >/dev/null
echo "DMG: $DMG_PATH"

# Optional: customize DMG window layout when Finder automation is available.
if [ "${DMG_LAYOUT:-0}" = "1" ]; then
  DMG_TEMP="$DMG_WORK/temp-layout.dmg"
  rm -f "$DMG_TEMP"
  hdiutil convert "$DMG_PATH" -format UDRW -o "$DMG_TEMP" >/dev/null

  MOUNT_DIR="$(hdiutil attach -readwrite -noverify -noautoopen "$DMG_TEMP" \
    | grep -o '/Volumes/.*' | head -1 || true)"
  if [ -n "$MOUNT_DIR" ]; then
    osascript <<APPLESCRIPT >/dev/null 2>&1 || true
tell application "Finder"
  tell disk "TextPad $VERSION"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set bounds of container window to {200, 120, 700, 420}
    set theViewOptions to icon view options of container window
    tell theViewOptions
      set arrangement to not arranged
      set icon size to 96
    end tell
    set position of item "$APP_NAME.app" of container window to {130, 170}
    set position of item "Applications" of container window to {410, 170}
    close
    open
    update without registering applications
    delay 1
  end tell
end tell
APPLESCRIPT
    hdiutil detach "$MOUNT_DIR" >/dev/null 2>&1 || true
    rm -f "$DMG_PATH"
    hdiutil convert "$DMG_TEMP" -format UDZO -imagekey zlib-level=9 -o "$DMG_PATH" >/dev/null
    rm -f "$DMG_TEMP"
    echo "DMG layout applied."
  else
    echo "Warning: could not mount DMG for layout; using default icon positions."
    rm -f "$DMG_TEMP"
  fi
fi

# Optional notarization hint
if [ "$SIGN_IDENTITY" = "-" ]; then
  echo ""
  echo "Installers are unsigned (local use)."
  echo "For distribution, rebuild with a Developer ID certificate:"
  echo "  SIGN_IDENTITY=\"Developer ID Application: Your Name (TEAMID)\" bash installer/build-installer.sh"
  echo ""
  echo "Users may need to right-click → Open the first time, or approve in System Settings."
fi

rm -rf "$PKG_WORK" "$DMG_WORK"

echo ""
echo "Installer build complete!"
echo "  DMG: $DMG_PATH"
echo "  PKG: $PKG_PATH"
echo ""