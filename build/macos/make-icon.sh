#!/bin/bash
#
# make-icon.sh — build AstroCast.icns from the 1024 master.
#
# Sits beside make-bundle.sh and build-libswe.sh, and follows the same rule they
# do: the recipe lives in the repository and verifies its own output, because a
# build input that lives outside version control is precisely what produced the
# Phase 1 libswe deployment-target defect.
#
# The master is committed at build/macos/icon/AstroCast-1024.png. The generated
# .iconset and .icns are build outputs and are not committed — they are
# reproducible from the master by running this script.
#
# THE FILL FRACTION IS A DELIBERATE CHOICE, NOT A DEFAULT
# --------------------------------------------------------
# The master's artwork occupies only ~60% of its canvas. Shipping macOS icons
# fill 83.6% — measured across Safari, Maps, Podcasts, App Store and Calculator,
# every one within 0.2% of the others — so the master used as-is renders visibly
# smaller than its neighbours in the Dock. icongen crops to the alpha bounding
# box and re-centres at FILL_PERCENT below.
#
# One consequence, accepted knowingly: the 1024 entry's 856px of artwork comes
# from 616px of master, a 1.39x magnification. Every other size is a genuine
# reduction, and each is resampled directly from the original crop rather than
# from the magnified 1024, so nothing else inherits that softness.
#
# USAGE
#   build/macos/make-icon.sh [-f FILL_PERCENT] [-o OUTPUT_ICNS]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

MASTER="$SCRIPT_DIR/icon/AstroCast-1024.png"
GENERATOR="$SCRIPT_DIR/icon/icongen.swift"
FILL_PERCENT="83.6"
OUTPUT="$SCRIPT_DIR/dist/AstroCast.icns"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -f|--fill)   FILL_PERCENT="$2"; shift 2 ;;
    -o|--output) OUTPUT="$2"; shift 2 ;;
    -h|--help)   sed -n '/^# USAGE/,/^set -euo/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' | head -n -1; exit 0 ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

fail() { echo "error: $*" >&2; exit 1; }
step() { echo; echo "==> $*"; }

[[ "$(uname -s)" == "Darwin" ]] || fail "builds a macOS .icns and must run on macOS (found $(uname -s))"
command -v iconutil >/dev/null || fail "iconutil not found"
command -v swiftc   >/dev/null || fail "swiftc not found; install the Xcode command line tools"
[[ -f "$MASTER" ]]    || fail "missing master artwork: $MASTER"
[[ -f "$GENERATOR" ]] || fail "missing generator: $GENERATOR"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
ICONSET="$WORK/AstroCast.iconset"

step "Compiling the generator"
swiftc -O "$GENERATOR" -o "$WORK/icongen" 2>&1 | sed 's/^/    /'

step "Rendering the iconset at ${FILL_PERCENT}% fill"
"$WORK/icongen" "$MASTER" "$ICONSET" "$FILL_PERCENT"

# ---------------------------------------------------------------------------
# Independent check of what actually landed on disk. icongen verifies its own
# arithmetic; this verifies the files, which is not the same thing — a write
# that silently failed or a name typo would pass the first and fail here.
# ---------------------------------------------------------------------------
step "Verifying the iconset on disk"
EXPECTED=(
  "icon_16x16.png:16"        "icon_16x16@2x.png:32"
  "icon_32x32.png:32"        "icon_32x32@2x.png:64"
  "icon_128x128.png:128"     "icon_128x128@2x.png:256"
  "icon_256x256.png:256"     "icon_256x256@2x.png:512"
  "icon_512x512.png:512"     "icon_512x512@2x.png:1024"
)
for entry in "${EXPECTED[@]}"; do
  name="${entry%%:*}"; want="${entry##*:}"
  f="$ICONSET/$name"
  [[ -f "$f" ]] || fail "missing $name"
  w="$(sips -g pixelWidth  "$f" | awk '/pixelWidth/{print $2}')"
  h="$(sips -g pixelHeight "$f" | awk '/pixelHeight/{print $2}')"
  [[ "$w" == "$want" && "$h" == "$want" ]] || fail "$name is ${w}x${h}, expected ${want}x${want}"
  [[ "$(sips -g hasAlpha "$f" | awk '/hasAlpha/{print $2}')" == "yes" ]] || fail "$name has no alpha channel"
  # No emitted file may exceed the master's dimensions. The master is 1024, so
  # this is what stops the set being padded out with fabricated detail.
  MW="$(sips -g pixelWidth "$MASTER" | awk '/pixelWidth/{print $2}')"
  (( want <= MW )) || fail "$name is ${want}px but the master is only ${MW}px — refusing to upscale the canvas"
done
echo "    10 files, all correct size, all carry alpha, none exceeds the ${MW}px master"

step "Building the .icns"
mkdir -p "$(dirname "$OUTPUT")"
iconutil -c icns "$ICONSET" -o "$OUTPUT"
[[ -f "$OUTPUT" ]] || fail "iconutil produced no output"

# The icns type codes, so a truncated or partial set is visible rather than
# merely suspected. ic04/ic05 are the 16/32 legacy pair; ic07..ic14 the modern
# 128 through 1024 entries.
step "Contents"
echo "    $(basename "$OUTPUT")  $(stat -f%z "$OUTPUT") bytes"
TYPES="$(iconutil -l "$OUTPUT" 2>/dev/null || true)"
if [[ -n "$TYPES" ]]; then
  echo "$TYPES" | sed 's/^/    /'
else
  # iconutil -l is not available on every release; fall back to reading the
  # four-character type codes out of the container directly.
  python3 - "$OUTPUT" <<'PY'
import struct, sys
data = open(sys.argv[1], 'rb').read()
assert data[:4] == b'icns', 'not an icns container'
off, end = 8, struct.unpack('>I', data[4:8])[0]
while off < end:
    t = data[off:off+4].decode('ascii', 'replace')
    n = struct.unpack('>I', data[off+4:off+8])[0]
    print(f"    {t}  {n-8:>9,} bytes")
    off += n
PY
fi

step "Built $OUTPUT"
