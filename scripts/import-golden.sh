#!/bin/sh
# Copies seimei's parity cases, models and native output (the golden files) into
# package/Tests/Fixtures/parity/. Run it again whenever seimei's cases change.
# usage: scripts/import-golden.sh [path-to-seimei]   (default: ../seimei)
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
seimei=${1:-$here/../seimei}
dest=$here/package/Tests/Fixtures/parity
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
"$seimei/tests/parity/export.sh" "$tmp"
rm -rf "$dest"
mkdir -p "$dest/cases" "$dest/models" "$dest/golden"
cp "$seimei"/tests/parity/cases/*.json "$dest/cases/"
cp "$tmp"/models/*.tmod "$dest/models/"
cp "$tmp"/*.rgba "$tmp"/*.json "$dest/golden/"
