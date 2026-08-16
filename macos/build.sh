#!/bin/bash
set -e

cd "$(dirname "$0")"

APP_NAME="TextPad"
BUILD_DIR=".build"
APP_DIR="$APP_NAME.app"

echo "Building TextPad for macOS..."

for script in build.sh build_icon.sh run.sh; do
  if [ -f "$script" ]; then
    chmod +x "$script"
  fi
done

if [ -f build_icon.sh ]; then
  if ! bash build_icon.sh; then
    if [ -f AppIcon.icns ]; then
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
  main.swift \
  CrashLogger.swift \
  SingleInstanceManager.swift \
  DocumentExport.swift \
  LargeFileSupport.swift \
  SafeFileReader.swift \
  AppDelegate.swift \
  AppHelp.swift \
  EditorPreferences.swift \
  EditorDocument.swift \
  DocumentFormat.swift \
  DocumentEncoding.swift \
  EncodingOptionsController.swift \
  FileChangeMonitor.swift \
  AutoSaveManager.swift \
  TextSearch.swift \
  InWindowFindBar.swift \
  ClosedTabManager.swift \
  DropReceivingView.swift \
  RichTextFormatting.swift \
  PlainTextEditing.swift \
  CurrentLineHighlightView.swift \
  EditorViewController.swift \
  SyntaxHighlighter.swift \
  LineNumberRuler.swift \
  FindReplaceController.swift \
  GoToLineController.swift \
  DocumentWindowController.swift \
  PreferencesWindowController.swift \
  BundledFonts.swift \
  -framework AppKit \
  -framework Foundation \
  -framework UniformTypeIdentifiers \
  -framework CoreText

mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

cp "$BUILD_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"
chmod +x "$APP_DIR/Contents/MacOS/$APP_NAME"
cp Info.plist "$APP_DIR/Contents/Info.plist"

if [ -f AppIcon.icns ]; then
  cp AppIcon.icns "$APP_DIR/Contents/Resources/AppIcon.icns"
  chmod 644 "$APP_DIR/Contents/Resources/AppIcon.icns"
fi

if [ -f Help.md ]; then
  cp Help.md "$APP_DIR/Contents/Resources/Help.md"
  chmod 644 "$APP_DIR/Contents/Resources/Help.md"
fi

if [ -d Fonts ]; then
  mkdir -p "$APP_DIR/Contents/Resources/Fonts"
  find Fonts -maxdepth 1 \( -name '*.ttf' -o -name '*.otf' \) -exec cp {} "$APP_DIR/Contents/Resources/Fonts/" \;
  chmod 644 "$APP_DIR/Contents/Resources/Fonts/"* 2>/dev/null || true
fi

chmod a+rX "$APP_DIR/Contents/Resources"

# Code signing
# Set SIGN_IDENTITY to a keychain identity (e.g. "Developer ID Application: Your Name (TEAMID)")
# to produce a distributable signature. Defaults to ad-hoc ("-") for local use.
SIGN_IDENTITY="${SIGN_IDENTITY:--}"
ENTITLEMENTS="TextPad.entitlements"

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