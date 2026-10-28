#!/bin/bash
#
# build-libswe.sh — compile the Swiss Ephemeris native library for macOS arm64
# from the vendored C sources in lib/swisseph/src.
#
# WHY THIS SCRIPT EXISTS AT ALL
# ------------------------------
# Phase 1 built lib/swisseph/osx-arm64/libswe.dylib by typing a clang command by
# hand. The command was recorded in a report but never scripted, so the only
# copy of the recipe lived outside the repository — and the binary it produced
# carried a defect that took until Phase 5 to notice.
#
# THE DEPLOYMENT TARGET IS THE WHOLE POINT — DO NOT REMOVE IT
# ------------------------------------------------------------
# clang, given no -mmacosx-version-min, stamps the binary with the *build
# machine's* OS version as its minimum. Phase 1 compiled on macOS 26.5, so the
# dylib was stamped `minos 26.0`, and dyld enforces that at load time.
#
# The failure mode is nasty and worth spelling out: the .app bundle's real floor
# is the maximum LC_BUILD_VERSION across every Mach-O it ships, so this one file
# silently raised the whole application's requirement to macOS 26 — while
# Info.plist, the installer, and every other binary said otherwise. On macOS 15
# through 25 the app would launch, draw its window, and only then die when
# EphemerisService triggered the first P/Invoke and dyld refused to load the
# library. A crash after the UI appears, not a clean "this app requires a newer
# macOS" refusal.
#
# Rebuilding without this flag on a newer machine silently reintroduces exactly
# that, and nothing in the build or the test suite will complain: the goldens
# pass, because the compiled code is fine. Only a load-command census catches
# it.
#
# WHY 15.0 AND NOT LOWER
# -----------------------
# The floor is a maximum, so lowering this file below the highest
# package-supplied binary buys nothing. Census of the bundle as shipped:
#
#     minos 11.0 —  4 binaries
#     minos 12.0 — 15 binaries  (incl. the apphost, libcoreclr, createdump)
#     minos 15.0 —  3 binaries  (libQuestPdfSkia, libqpdf, and this library)
#
# libQuestPdfSkia and libqpdf arrive from the QuestPDF NuGet package already
# requiring 15.0 and are not ours to rebuild, so 15.0 is the lowest floor the
# bundle can actually achieve today. Building libswe at, say, 12.0 would leave
# the bundle's floor at 15.0 regardless. If QuestPDF's binaries ever drop below
# 15.0, lowering DEPLOYMENT_TARGET here is the follow-up.
#
# EQUIVALENCE
# -----------
# Changing only this flag changes only the load command. Measured on the
# 2026-08-10 rebuild: of 564,704 bytes, exactly 48 differ — one byte of
# LC_BUILD_VERSION minos, fifteen of the relinked LC_UUID, and thirty-two inside
# the linker's ad-hoc signature. Zero bytes of compiled code. The exported
# symbol set (177 symbols, 106 of them swe_*) and the undefined set (61, all
# libSystem) are byte-identical, and the CLI smoke test reproduces the Phase 1
# capture bit for bit.
#
# USAGE
#   build/macos/build-libswe.sh [-t DEPLOYMENT_TARGET] [-o OUTPUT]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

DEPLOYMENT_TARGET="15.0"
OUTPUT="$REPO_ROOT/lib/swisseph/osx-arm64/libswe.dylib"
SRC="$REPO_ROOT/lib/swisseph/src"

# The nine translation units Phase 1 established. This is not the whole of
# lib/swisseph/src: swetest.c, swemini.c, obama.c and swevents.c are sample
# programs with their own main(), and sweephe4.c / swephgen4.c belong to the
# separate ephemeris-file-generation tool. Adding any of them either fails the
# link with a duplicate main() or bloats the library with code nothing calls.
TRANSLATION_UNITS=(
  swecl.c swedate.c swehel.c swehouse.c swejpl.c
  swemmoon.c swemplan.c sweph.c swephlib.c
)

while [[ $# -gt 0 ]]; do
  case "$1" in
    -t|--target) DEPLOYMENT_TARGET="$2"; shift 2 ;;
    -o|--output) OUTPUT="$2"; shift 2 ;;
    -h|--help)   sed -n '/^# USAGE/,/^set -euo/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' | head -n -1; exit 0 ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

fail() { echo "error: $*" >&2; exit 1; }
step() { echo; echo "==> $*"; }

[[ "$(uname -s)" == "Darwin" ]] || fail "this builds a macOS dylib and must run on macOS (found $(uname -s))"
command -v clang >/dev/null || fail "clang not found; install the Xcode command line tools"

for tu in "${TRANSLATION_UNITS[@]}"; do
  [[ -f "$SRC/$tu" ]] || fail "missing translation unit: $SRC/$tu"
done

step "Compiling ${#TRANSLATION_UNITS[@]} translation units for arm64, deployment target $DEPLOYMENT_TARGET"
echo "    clang $(clang --version | head -1 | sed 's/^Apple //')"
echo "    SDK   $(xcrun --show-sdk-version)"

SOURCES=()
for tu in "${TRANSLATION_UNITS[@]}"; do SOURCES+=("$SRC/$tu"); done

mkdir -p "$(dirname "$OUTPUT")"

# -install_name @rpath/libswe.dylib matters: the library is loaded by
# NativeLibrary.TryLoad from beside the managed assemblies, not from a system
# path, so the install name must be rpath-relative or the load fails once the
# file moves into a publish output or an .app bundle.
clang -dynamiclib -arch arm64 -O2 \
  -mmacosx-version-min="$DEPLOYMENT_TARGET" \
  -install_name @rpath/libswe.dylib \
  -o "$OUTPUT" \
  "${SOURCES[@]}" \
  -lm

# ---------------------------------------------------------------------------
# Verify the result rather than trusting the compiler. Each check below failed
# to be run at least once in this project's history, or corresponds to a defect
# that shipped.
# ---------------------------------------------------------------------------
step "Verifying"

ACTUAL_MINOS="$(otool -l "$OUTPUT" | awk '/LC_BUILD_VERSION/{f=1} f&&/minos/{print $2; exit}')"
[[ "$ACTUAL_MINOS" == "$DEPLOYMENT_TARGET" ]] \
  || fail "LC_BUILD_VERSION minos is $ACTUAL_MINOS, expected $DEPLOYMENT_TARGET — the deployment target did not take"
echo "    minos              $ACTUAL_MINOS"

# The nine units must resolve each other completely; anything still undefined
# has to be satisfied by libSystem, and nothing else may be linked. A stray
# dependency here would not fail the build but would fail to load on a machine
# lacking the library.
NONSYSTEM_DEPS="$(otool -L "$OUTPUT" | tail -n +2 | grep -v "@rpath/libswe.dylib" | grep -vc "/usr/lib/libSystem" || true)"
[[ "$NONSYSTEM_DEPS" == "0" ]] || fail "links against something other than libSystem:
$(otool -L "$OUTPUT" | tail -n +2)"
echo "    links against      libSystem only"

EXPORTED="$(nm -gU "$OUTPUT" | wc -l | tr -d ' ')"
SWE_EXPORTED="$(nm -gU "$OUTPUT" | grep -c ' _swe_')"
echo "    exported symbols   $EXPORTED total, $SWE_EXPORTED swe_*"
[[ "$SWE_EXPORTED" -ge 106 ]] || fail "only $SWE_EXPORTED swe_* symbols exported, expected at least 106"

# The version is compiled in from sweph.h. If this ever changes, the sources
# changed — which is a different review from a flags change.
VERSION="$(python3 -c "
import ctypes, sys
lib = ctypes.CDLL('$OUTPUT')
buf = ctypes.create_string_buffer(256)
lib.swe_version(buf)
print(buf.value.decode())
" 2>/dev/null || echo "?")"
echo "    swe_version()      $VERSION"
[[ "$VERSION" == "2.10.03" ]] \
  || echo "    WARNING: expected Swiss Ephemeris 2.10.03; sources may have changed"

echo "    size               $(stat -f%z "$OUTPUT") bytes"
echo "    sha256             $(shasum -a 256 "$OUTPUT" | cut -d' ' -f1)"

step "Built $OUTPUT"
echo
echo "The bundle's floor is the maximum minos across all its binaries. After"
echo "rebuilding this library, re-run the census before believing the floor moved:"
echo
echo "  find build/macos/dist/AstroCast.app/Contents/MacOS -type f | while read -r f; do"
echo "    file -b \"\$f\" | grep -q Mach-O || continue"
echo "    printf '%s\\t%s\\n' \"\$(otool -l \"\$f\" | awk '/LC_BUILD_VERSION/{g=1} g&&/minos/{print \$2; exit}')\" \"\$(basename \"\$f\")\""
echo "  done | sort -t. -k1,1n -k2,2n"
echo
echo "Match on Mach-O content, not on a *.dylib glob — the runtime ships"
echo "'createdump', which has no extension and a 5a census missed for that reason."
