#!/bin/bash
set -e

cd "$(dirname "$0")"

APP_NAME="TextPad"
BUILD_DIR=".build"
APP_DIR="$APP_NAME.app"
SOURCE_DIR="Sources"
RESOURCE_DIR="Resources"

echo "Building TextPad for macOS..."

for script in build.sh build_icon.sh run.sh; do
  if [ -f "$script" ]; then
    chmod +x "$script"
  fi
done

if [ -f build_icon.sh ]; then
  if ! bash build_icon.sh; then
    if [ -f "$RESOURCE_DIR/AppIcon.icns" ]; then
      echo "Warning: icon rebuild failed; using existing AppIcon.icns"
    else
      echo "Error: icon build failed and AppIcon.icns is missing"
      exit 1
    fi
  fi
fi

rm -rf "$APP_DIR" "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

ARCH="$(uname -m)"
case "$ARCH" in
  arm64)  TARGET="arm64-apple-macos11.0" ;;
  x86_64) TARGET="x86_64-apple-macos11.0" ;;
  *)
    echo "Error: unsupported architecture: $ARCH"
    exit 1
    ;;
esac

if ! command -v swiftc >/dev/null 2>&1; then
  echo "Error: swiftc not found. Install Xcode Command Line Tools:"
  echo "  xcode-select --install"
  exit 1
fi

echo "Target: $TARGET"

swiftc \
  -O \
  -target "$TARGET" \
  -o "$BUILD_DIR/$APP_NAME" \
  "$SOURCE_DIR/main.swift" \
  "$SOURCE_DIR/CrashLogger.swift" \
  "$SOURCE_DIR/SingleInstanceManager.swift" \
  "$SOURCE_DIR/DocumentExport.swift" \
  "$SOURCE_DIR/LargeFileSupport.swift" \
  "$SOURCE_DIR/SafeFileReader.swift" \
  "$SOURCE_DIR/AppDelegate.swift" \
  "$SOURCE_DIR/AppHelp.swift" \
  "$SOURCE_DIR/EditorPreferences.swift" \
  "$SOURCE_DIR/EditorDocument.swift" \
  "$SOURCE_DIR/DocumentFormat.swift" \
  "$SOURCE_DIR/DocumentEncoding.swift" \
  "$SOURCE_DIR/EncodingOptionsController.swift" \
  "$SOURCE_DIR/FileChangeMonitor.swift" \
  "$SOURCE_DIR/AutoSaveManager.swift" \
  "$SOURCE_DIR/TextSearch.swift" \
  "$SOURCE_DIR/InWindowFindBar.swift" \
  "$SOURCE_DIR/ClosedTabManager.swift" \
  "$SOURCE_DIR/DropReceivingView.swift" \
  "$SOURCE_DIR/RichTextFormatting.swift" \
  "$SOURCE_DIR/PlainTextEditing.swift" \
  "$SOURCE_DIR/CurrentLineHighlightView.swift" \
  "$SOURCE_DIR/EditorViewController.swift" \
  "$SOURCE_DIR/SyntaxHighlighter.swift" \
  "$SOURCE_DIR/LineNumberRuler.swift" \
  "$SOURCE_DIR/FindReplaceController.swift" \
  "$SOURCE_DIR/GoToLineController.swift" \
  "$SOURCE_DIR/DocumentWindowController.swift" \
  "$SOURCE_DIR/PreferencesWindowController.swift" \
  "$SOURCE_DIR/BundledFonts.swift" \
  -framework AppKit \
  -framework Foundation \
  -framework UniformTypeIdentifiers \
  -framework CoreText

mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

cp "$BUILD_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"
chmod +x "$APP_DIR/Contents/MacOS/$APP_NAME"
cp "$RESOURCE_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"

if [ -f "$RESOURCE_DIR/AppIcon.icns" ]; then
  cp "$RESOURCE_DIR/AppIcon.icns" "$APP_DIR/Contents/Resources/AppIcon.icns"
  chmod 644 "$APP_DIR/Contents/Resources/AppIcon.icns"
fi

if [ -f "$RESOURCE_DIR/Help.md" ]; then
  cp "$RESOURCE_DIR/Help.md" "$APP_DIR/Contents/Resources/Help.md"
  chmod 644 "$APP_DIR/Contents/Resources/Help.md"
fi

if [ -d "$RESOURCE_DIR/Fonts" ]; then
  mkdir -p "$APP_DIR/Contents/Resources/Fonts"
  find "$RESOURCE_DIR/Fonts" -maxdepth 1 \( -name '*.ttf' -o -name '*.otf' \) -exec cp {} "$APP_DIR/Contents/Resources/Fonts/" \;
  chmod 644 "$APP_DIR/Contents/Resources/Fonts/"* 2>/dev/null || true
fi

chmod a+rX "$APP_DIR/Contents/Resources"

# Code signing
# Set SIGN_IDENTITY to a keychain identity (e.g. "Developer ID Application: Your Name (TEAMID)")
# to produce a distributable signature. Defaults to ad-hoc ("-") for local use.
SIGN_IDENTITY="${SIGN_IDENTITY:--}"
ENTITLEMENTS="$RESOURCE_DIR/TextPad.entitlements"

echo "Signing $APP_DIR with identity: $SIGN_IDENTITY"
codesign --force --sign "$SIGN_IDENTITY" --entitlements "$ENTITLEMENTS" --timestamp "$APP_DIR/Contents/MacOS/$APP_NAME"
codesign --force --sign "$SIGN_IDENTITY" --entitlements "$ENTITLEMENTS" --timestamp "$APP_DIR"

if codesign --verify --deep --strict "$APP_DIR" 2>/dev/null; then
  echo "Signature verified."
else
  echo "Warning: signature verification failed."
fi

echo ""
echo "Build complete!"
echo "   App: $(pwd)/$APP_DIR"
echo ""
echo "Run with: open $APP_DIR"
if [ "$SIGN_IDENTITY" = "-" ]; then
  echo ""
  echo "Signed ad-hoc (local only). For distribution, install a Developer ID"
  echo "certificate and rebuild with:"
  echo "  SIGN_IDENTITY=\"Developer ID Application: ...\" ./build.sh"
fi
echo ""
