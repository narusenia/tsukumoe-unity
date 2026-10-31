#!/bin/sh
# Builds tsukumoe-ffi in a seimei checkout and copies the shared library into the package.
# usage: scripts/build-plugin.sh [path-to-seimei]   (default: ../seimei)
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
seimei=${1:-$here/../seimei}
cargo build --manifest-path "$seimei/Cargo.toml" -p tsukumoe-ffi --release
case $(uname -s) in
  Darwin) cp "$seimei/target/release/libtsukumoe_ffi.dylib" "$here/package/Runtime/Plugins/macOS/" ;;
  *) echo "only macOS is wired up so far" >&2; exit 1 ;;
esac
