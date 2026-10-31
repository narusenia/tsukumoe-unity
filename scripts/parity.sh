#!/bin/sh
# Runs every parity case in the Unity editor (batch mode, with graphics) and
# writes <case>.rgba and <case>.json to <out-dir>, for `tsukumoe parity` in seimei.
# usage: scripts/parity.sh <out-dir> [unity-editor-path]
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
out=$(cd "$(dirname "${1:?usage: scripts/parity.sh <out-dir>}")" && pwd)/$(basename "$1")
unity=${2:-/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity}
fx=$here/package/Tests/Fixtures/parity
"$unity" -batchmode -quit -projectPath "$here/project" -logFile - \
  -executeMethod Tsukumoe.Editor.ParityTool.Run \
  -tsukumoe-cases "$fx/cases" -tsukumoe-models "$fx/models" -tsukumoe-out "$out"
