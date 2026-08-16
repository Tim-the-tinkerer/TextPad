#!/bin/bash
set -e
cd "$(dirname "$0")"

# Same entry point as the other Apps-Usefull projects: build, then launch.
# Pass --no-launch (or "build") to compile only.
LAUNCH=true
for arg in "$@"; do
  case "$arg" in
    --no-launch|build) LAUNCH=false ;;
  esac
done

bash ./build.sh

if [ "$LAUNCH" = true ]; then
  open macos/TextPad.app
fi
