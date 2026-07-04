#!/bin/bash
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
MACOS_DIR="$ROOT/macos"
BUILD_SCRIPT="$MACOS_DIR/build.sh"

if [ ! -f "$BUILD_SCRIPT" ]; then
  echo "Error: macOS build script not found at $BUILD_SCRIPT"
  exit 1
fi

chmod +x "$BUILD_SCRIPT" 2>/dev/null || true
exec bash "$BUILD_SCRIPT" "$@"