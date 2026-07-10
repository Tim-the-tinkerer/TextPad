#!/bin/bash
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
MACOS_DIR="$ROOT/macos"

bash "$ROOT/build.sh"
open "$MACOS_DIR/TextPad.app"