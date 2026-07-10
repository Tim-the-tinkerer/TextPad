#!/bin/bash
set -e
cd "$(dirname "$0")"
bash build.sh
open TextPad.app