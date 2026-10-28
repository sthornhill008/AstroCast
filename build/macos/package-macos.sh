#!/bin/bash
#
# package-macos.sh — sign, notarize, staple and verify AstroCast.app, then
# package it as a signed, notarized DMG.
#
# Ported from the ChessSage procedure (~/astrocast-mac-setup/APPLE_NOTARIZATION.md),
# itself adapted from PhotoName. Stages D–G below transfer near-verbatim; the
# stage numbering and letter names are kept so the two scripts can be read
# against each other.
#
#
# THIS SCRIPT NEVER BUILDS THE BUNDLE — AND THAT IS A SAFETY PROPERTY
# ====================================================================
# make-bundle.sh regenerates the .icns on every run (slice 5b), so running it
# rewrites Contents/Resources. Any modification to Contents/ invalidates a code
# signature, so a build step anywhere after Stage D would silently destroy the
# signature — or worse, destroy it *after* notarization, leaving an artifact
# that passes every local check and fails on a user's machine.
#
# The enforcement is structural rather than a comment: this script contains no
# call to make-bundle.sh, make-icon.sh or dotnet publish, and `require_bundle`
# below REFUSES to continue when the bundle is missing rather than helpfully
# creating one. Building is a separate, earlier, human-invoked step. If you add
# a build call to this file you have reintroduced the hazard.
#
#
# THE TWO-PASS DESIGN (reference §5)
# ===================================
#   PASS 1   package-macos.sh
#            └─ test gate → assert bundle → [HUMAN SMOKE GATE] → exit 0
#
#                 ⇣  a person actually runs the app
#
#   PASS 2   package-macos.sh --notarize
#            └─ test gate → D sign → E notarize app → F DMG → G verify
#
#   RESUME   package-macos.sh --dmg
#            └─ Stage F + G only, against an already-notarized app
#
#   TIGHT    package-macos.sh --sign-only
#            └─ test gate → D sign → launch check.  Added for AstroCast: the
#               reference app needs no entitlements and this loop, but a .NET
#               app does, and §10.5 prescribes "sign, launch, add one
#               entitlement, repeat". Notarizing between attempts would make
#               that loop cost minutes instead of seconds.
#
# Nothing slow or irreversible happens until a human has confirmed the app
# launches. Notarization is neither fast nor free, and an app that notarizes
# perfectly and does not run is a fully-blessed broken artifact.
#
# USAGE
#   build/macos/package-macos.sh [--notarize | --dmg | --sign-only]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

APP_NAME="AstroCast"
BUNDLE_ID="com.sthornhill.astrocast"     # lowercase; fixed at slice 5a, keyed on by signing
NOTARY_PROFILE="PhotoName-Notary"        # account-level, notarizes any app
TEAM_ID="26669DP59A"
EXECUTABLE_NAME="AstroCast.Avalonia"     # apphost name; see make-bundle.sh
EXPECTED_MACHO=22                        # slice 5a.2 census, matched on content

OUT="$SCRIPT_DIR/dist"
APP_BUNDLE="$OUT/$APP_NAME.app"
ENTITLEMENTS="$SCRIPT_DIR/$APP_NAME.entitlements"
ICNS="$APP_BUNDLE/Contents/Resources/$APP_NAME.icns"

MODE="smoke"
case "${1:-}" in
  --notarize)  MODE="notarize" ;;
  --dmg)       MODE="dmg" ;;
  --sign-only) MODE="sign" ;;
  -h|--help)   sed -n '/^# USAGE/,/^set -euo/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' | head -n -1; exit 0 ;;
  "")          ;;
  *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
esac

die()  { echo "ERROR: $*" >&2; exit 1; }
info() { echo "    $*"; }
step() { echo; echo "==> $*"; }

[[ "$(uname -s)" == "Darwin" ]] || die "macOS only (found $(uname -s))"

# ---------------------------------------------------------------------------
# Identity, discovered rather than hard-coded (reference §3.1). A hard-coded
# string breaks silently when the certificate is renewed; discovery fails loudly.
# ---------------------------------------------------------------------------
IDENTITY="$(security find-identity -v -p codesigning 2>/dev/null \
  | grep 'Developer ID Application' | head -1 | sed -E 's/.*"(.+)".*/\1/' || true)"
[[ -n "$IDENTITY" ]] || die "No 'Developer ID Application' identity in the keychain.
       Check with: security find-identity -v -p codesigning"

# ---------------------------------------------------------------------------
# Refuse to run against a bundle this script did not find already built. See
# the header: creating one here would be the hazard.
# ---------------------------------------------------------------------------
require_bundle() {
  [[ -d "$APP_BUNDLE" ]] || die "No bundle at $APP_BUNDLE.
       Build it first, as a separate step:  build/macos/make-bundle.sh
       This script deliberately does not build — make-bundle.sh regenerates the
       icon, and any write into Contents/ after signing invalidates the seal."
}

version_of() {
  /usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" \
    "$APP_BUNDLE/Contents/Info.plist" 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Test gate. Runs in every pass, deliberately: pass 2 may run hours after pass
# 1, on a tree that has since been edited.
# ---------------------------------------------------------------------------
stage_test_gate() {
  step "Test gate"
  for proj in AstroCast.Tests AstroCast.UiTests; do
    local out
    out="$(cd "$REPO_ROOT" && dotnet test "tests/$proj" --nologo 2>&1)" \
      || { echo "$out" | tail -20; die "$proj failed — refusing to package."; }
    info "$(grep -oE 'Failed:[^,]*, Passed:[^,]*, Skipped:[^,]*, Total:[[:space:]]*[0-9]+' <<< "$out" | tail -1)  $proj"
  done
}

# ---------------------------------------------------------------------------
# Stage A' — assert the bundle rather than build it (reference §6 Stage A's
# principle: assert the build produced what you think it produced, before
# spending a notarization cycle on it).
# ---------------------------------------------------------------------------
stage_assert_bundle() {
  step "Asserting the bundle"
  require_bundle

  local exe="$APP_BUNDLE/Contents/MacOS/$EXECUTABLE_NAME"
  [[ -x "$exe" ]] || die "main executable missing or not +x: $exe"
  file -b "$exe" | grep -q 'Mach-O.*arm64' || die "main executable is not arm64"

  local id ver
  id="$(/usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "$APP_BUNDLE/Contents/Info.plist")"
  [[ "$id" == "$BUNDLE_ID" ]] || die "bundle identifier is '$id', expected '$BUNDLE_ID'.
       Signing and notarization key off this; it must not drift."
  ver="$(version_of)"
  [[ -n "$ver" ]] || die "no CFBundleShortVersionString in the plist"

  [[ -f "$ICNS" ]] || die "no icon at $ICNS"

  # Count Mach-O by CONTENT. An earlier census matched "*.dylib" by name and
  # silently omitted `createdump`, which ships in the runtime with no
  # extension. A wrong count means the publish changed and the signing loop's
  # coverage is no longer the verified one.
  local n
  n="$(find "$APP_BUNDLE/Contents" -type f -exec sh -c \
        'file -b "$1" 2>/dev/null | grep -q "Mach-O" && echo x' _ {} \; | wc -l | tr -d ' ')"
  [[ "$n" == "$EXPECTED_MACHO" ]] || die "found $n Mach-O binaries, expected $EXPECTED_MACHO.
       The publish changed. Re-verify the census before signing."

  info "version    $ver"
  info "identifier $id"
  info "executable arm64, +x"
  info "Mach-O     $n (as censused)"
  info "icon       $(basename "$ICNS")"
}

# ---------------------------------------------------------------------------
# Stage D — code-sign, inside-out.
# ---------------------------------------------------------------------------
stage_sign() {
  step "Stage D — signing inside-out"
  require_bundle
  [[ -f "$ENTITLEMENTS" ]] || die "missing entitlements file: $ENTITLEMENTS"

  # An interrupted prior run can leave a stale DMG or a create-dmg temp image
  # at the bundle root beside Contents/. Signing and notarization scan the
  # WHOLE bundle, so a stray artifact rides into the signature and the ticket.
  # A valid .app has nothing at its root except Contents.
  local stray
  for stray in "$APP_BUNDLE"/* "$APP_BUNDLE"/.[!.]*; do
    [[ -e "$stray" ]] || continue
    if [[ "$(basename "$stray")" != "Contents" ]]; then
      info "removing stray bundle-root item: $(basename "$stray")"
      rm -rf "$stray"
    fi
  done

  # Clear extended attributes (reference §9's "resource fork, Finder
  # information, or similar detritus"). Note: on macOS 14+ this leaves
  # com.apple.provenance behind — that attribute is system-protected and
  # cannot be removed, and codesign tolerates it. The ones that actually cause
  # the rejection are FinderInfo and ResourceFork, which this does clear.
  xattr -cr "$APP_BUNDLE"

  # Every nested code object EXCEPT the main executable — that one is signed as
  # part of signing the bundle. Discovery is by content, so this does not care
  # what produced the binaries or whether the inventory changed.
  #
  # THE PORTING SURPRISE, AND THE REASON THIS LOOP DIFFERS FROM THE REFERENCE.
  #
  # APPLE_NOTARIZATION.md's loop matches "Mach-O" alone. That is right for
  # PyInstaller and SwiftPM, whose bundles keep Contents/MacOS/ almost empty,
  # and wrong for .NET.
  #
  # codesign's default rules seal Contents/Resources/ as *resources* but treat
  # everything in Contents/MacOS/ other than the main executable as *nested
  # code* that must carry its own signature. A self-contained .NET publish
  # lands its entire payload flat in that directory — here 278 files: 226
  # managed assemblies, 20 dylibs, createdump, 18 fonts, 6 ephemeris data
  # files, 3 pdbs, 2 json and 2 txt. Every one of them is nested code as far as
  # codesign is concerned, including the ones that are plainly data.
  #
  # So the bundle signature fails until all of them are signed, one complaint
  # at a time:
  #
  #     AstroCast.app: code object is not signed at all
  #     In subcomponent: …/Contents/MacOS/Avalonia.Remote.Protocol.dll
  #     …then…            …/Contents/MacOS/ephe/seas_12.se1
  #
  # Two things about that message cost real time and are worth writing down.
  # It is emitted by the SIGN command, not by the --verify that follows, so it
  # reads like a verification complaint and is not one. And `codesign -dv` on
  # an individual data file reports the same wording whether or not codesign
  # would ever have required a signature there, so it cannot be used to probe
  # which files are in scope — only the bundle signing itself tells you that.
  #
  # Signing non-Mach-O files is safe. codesign cannot embed a signature into a
  # PE or a data file, so it stores one in extended attributes
  # (com.apple.cs.CodeDirectory and friends) and leaves the bytes untouched —
  # verified by hash. Those xattrs survive the `ditto` round-trip that
  # notarization uploads. It is also exactly why `xattr -cr` above must run
  # BEFORE this loop and must never run after: clearing attributes post-signing
  # would silently strip hundreds of signatures and leave a bundle that looks
  # signed and is not.
  #
  # The tidier alternative is to move the non-code into Contents/Resources/,
  # which is the conventional layout. It is deliberately not done here: ephe/
  # must stay resolvable from AppContext.BaseDirectory, which is Contents/MacOS/,
  # and Phase 1 resolves it that way in Core. Changing that is a bundle-layout
  # decision for its own slice, not something to slip into signing.
  local inner
  inner="$(find "$APP_BUNDLE/Contents/MacOS" -type f ! -name "$EXECUTABLE_NAME" || true)"
  # Defensive: any Mach-O living outside Contents/MacOS/ is nested code too.
  inner="$inner
$(find "$APP_BUNDLE/Contents" -type f -not -path "*/MacOS/*" \
     -exec sh -c 'file -b "$1" 2>/dev/null | grep -q "Mach-O" && echo "$1"' _ {} \; || true)"

  local count=0 macho=0 other=0
  while IFS= read -r f; do
    [[ -n "$f" ]] || continue
    if file -b "$f" 2>/dev/null | grep -q "Mach-O"; then macho=$((macho + 1)); else other=$((other + 1)); fi
    codesign --force --options runtime --timestamp --sign "$IDENTITY" "$f"
    count=$((count + 1))
  done <<< "$inner"
  info "signed $count nested code objects ($macho Mach-O, $other managed assemblies and data)"

  # The bundle last, with entitlements. Signatures nest: signing the container
  # before its contents invalidates the container.
  codesign --force --options runtime --timestamp \
           --entitlements "$ENTITLEMENTS" \
           --sign "$IDENTITY" "$APP_BUNDLE"
  info "signed the bundle (entitlements: $(basename "$ENTITLEMENTS"))"

  codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE" 2>&1 | sed 's/^/      /'

  # Capture ONCE, then match with here-strings. A `codesign -dvvv … | grep -q`
  # pipeline is a SIGPIPE trap under `set -o pipefail`: grep exits on first
  # match, codesign gets SIGPIPE, and the pipeline fails on success.
  local sig
  sig="$(codesign -dvvv "$APP_BUNDLE" 2>&1)"
  grep -q 'flags=.*runtime' <<< "$sig" \
    || die "hardened runtime flag not set after signing — notarization would reject this."
  info "hardened runtime: $(grep -o 'flags=[^ ]*' <<< "$sig" | head -1)"

  local ents
  ents="$(codesign -d --entitlements - --xml "$APP_BUNDLE" 2>/dev/null \
          | plutil -convert xml1 -o - - 2>/dev/null \
          | grep -oE 'com\.apple\.security\.cs\.[a-z-]+' | sort -u || true)"
  info "entitlements granted: ${ents//$'\n'/, }"
}

# ---------------------------------------------------------------------------
# Notary submit + parse + staple, shared by the app and the DMG.
# ---------------------------------------------------------------------------
# The artifact SUBMITTED and the artifact STAPLED are not always the same file,
# and conflating them is an easy mistake to make because for the DMG they are.
# notarytool needs a container to upload, so the app goes up as a zip — but a
# ticket cannot be attached to a zip:
#
#     Processing: …/AstroCast.zip
#     Stapler is incapable of working with ZIP archive files.
#
# The ticket belongs on the .app itself, which is what a user ends up running.
# Hence two separate parameters.
notarize_artifact() {
  local artifact="$1" staple_target="$2" label="$3" idfile="$4"
  local logf="$OUT/$label-notary.log"

  xcrun notarytool submit "$artifact" --keychain-profile "$NOTARY_PROFILE" --wait \
    2>&1 | tee "$logf" || true

  local subid status
  subid="$(grep -oE '\bid: [0-9a-f-]{36}' "$logf" | head -1 | awk '{print $2}' || true)"
  status="$(grep -oE '\bstatus: [A-Za-z ]+' "$logf" | tail -1 | sed 's/status: //' | xargs || true)"
  [[ -n "$subid" ]] || die "could not parse a submission ID out of $logf"
  echo "$subid" > "$idfile"
  info "submission $subid — $status"

  if [[ "$status" != "Accepted" ]]; then
    echo
    echo "Notary did not accept the $label. Detailed log:"
    xcrun notarytool log "$subid" --keychain-profile "$NOTARY_PROFILE" 2>&1 | sed 's/^/      /'
    die "$label notarization failed (submission $subid)."
  fi

  xcrun stapler staple   "$staple_target" 2>&1 | sed 's/^/      /'
  xcrun stapler validate "$staple_target" 2>&1 | sed 's/^/      /'
  info "stapled $(basename "$staple_target")"
}

# ---------------------------------------------------------------------------
# Stage E — notarize and staple the app alone. Shortest feedback loop; no DMG
# yet, so a rejection costs one cycle rather than two.
# ---------------------------------------------------------------------------
stage_notarize_app() {
  step "Stage E — notarize and staple the app"
  local zip="$OUT/$APP_NAME.zip"
  rm -f "$zip"
  # ditto, NOT zip. A plain `zip` can mangle the bundle and fail notarization
  # with an invalid-signature error. --keepParent preserves the .app wrapper.
  ditto -c -k --keepParent "$APP_BUNDLE" "$zip"
  info "archived $(du -h "$zip" | cut -f1)"
  # Submit the zip; staple the BUNDLE.
  notarize_artifact "$zip" "$APP_BUNDLE" "app" "$OUT/app-submission-id.txt"
}

# ---------------------------------------------------------------------------
# Stage F — DMG: build, sign, notarize, staple.
# ---------------------------------------------------------------------------
stage_dmg() {
  step "Stage F — DMG"
  require_bundle
  local ver dmg staging
  ver="$(version_of)"
  dmg="$OUT/$APP_NAME-v$ver-macOS-arm64.dmg"
  staging="$OUT/staging"

  # create-dmg insists the staging directory contains ONLY the .app.
  rm -rf "$staging"; mkdir -p "$staging"
  cp -R "$APP_BUNDLE" "$staging/"
  rm -f "$dmg" "$OUT"/rw.*.dmg

  if command -v create-dmg >/dev/null 2>&1; then
    create-dmg --volname "$APP_NAME" --window-size 540 380 --icon-size 128 \
               --icon "$APP_NAME.app" 140 190 --app-drop-link 400 190 \
               --volicon "$ICNS" "$dmg" "$staging" \
      || info "create-dmg exited non-zero — verifying / falling back below…"
  else
    info "create-dmg not installed — using hdiutil"
  fi

  # Test for the ARTIFACT, not the exit code. create-dmg's cosmetic AppleScript
  # step hits a Finder AppleEvent timeout (-1712) in detached sessions and
  # bails without writing the image, while also sometimes exiting non-zero on
  # success. hdiutil needs no Finder.
  if [[ ! -f "$dmg" ]]; then
    hdiutil detach "/Volumes/$APP_NAME" >/dev/null 2>&1 || true
    ln -sf /Applications "$staging/Applications"
    hdiutil create -volname "$APP_NAME" -srcfolder "$staging" -ov -format UDZO "$dmg" \
      | sed 's/^/      /'
  fi
  [[ -f "$dmg" ]] || die "no DMG was produced at $dmg"
  info "built $(basename "$dmg") ($(du -h -A "$dmg" | cut -f1 | tr -d ' '))"

  # NO --options runtime — a DMG takes no hardened runtime.
  codesign --force --timestamp --sign "$IDENTITY" "$dmg"
  info "signed the DMG"

  # For a DMG the two are the same file.
  notarize_artifact "$dmg" "$dmg" "dmg" "$OUT/dmg-submission-id.txt"
  echo "$dmg" > "$OUT/.last-dmg"
}

# ---------------------------------------------------------------------------
# Stage G — Gatekeeper verification and report. An app and a DMG are assessed
# differently; both forms are checked.
# ---------------------------------------------------------------------------
stage_verify() {
  step "Stage G — Gatekeeper verification"
  local dmg; dmg="$(cat "$OUT/.last-dmg" 2>/dev/null || true)"

  local app_assess
  app_assess="$(spctl -a -t exec -vvv "$APP_BUNDLE" 2>&1 || true)"
  grep -qi 'accepted' <<< "$app_assess" || { echo "$app_assess"; die "spctl REJECTED the app."; }
  info "app: $(grep -i 'accepted\|source=' <<< "$app_assess" | tr '\n' ' ')"
  grep -qi 'source=Notarized Developer ID' <<< "$app_assess" \
    || info "WARNING: app source is not 'Notarized Developer ID'."

  if [[ -n "$dmg" && -f "$dmg" ]]; then
    local dmg_assess
    dmg_assess="$(spctl -a -t open --context context:primary-signature -vvv "$dmg" 2>&1 || true)"
    grep -qi 'accepted' <<< "$dmg_assess" || { echo "$dmg_assess"; die "spctl REJECTED the DMG."; }
    info "dmg: $(grep -i 'accepted\|source=' <<< "$dmg_assess" | tr '\n' ' ')"
  fi

  step "Release"
  info "app        $APP_BUNDLE"
  [[ -n "$dmg" && -f "$dmg" ]] && {
    info "dmg        $dmg"
    # Apparent size, not `du -h`: du reports ALLOCATED BLOCKS, which on this
    # APFS volume overstates a 65 MiB DMG as 81M. The byte count is what a
    # user downloads and what the SHA-256 below is taken over.
    info "size       $(stat -f%z "$dmg") bytes ($(du -h -A "$dmg" | cut -f1 | tr -d ' '))"
    info "sha256     $(shasum -a 256 "$dmg" | cut -d' ' -f1)"
  }
  info "identity   $IDENTITY"
  info "team       $TEAM_ID"
  [[ -f "$OUT/app-submission-id.txt" ]] && info "app submission  $(cat "$OUT/app-submission-id.txt")"
  [[ -f "$OUT/dmg-submission-id.txt" ]] && info "dmg submission  $(cat "$OUT/dmg-submission-id.txt")"
}

# ---------------------------------------------------------------------------
mkdir -p "$OUT"
echo "AstroCast — macOS packaging (mode: $MODE)"
info "identity $IDENTITY"

case "$MODE" in
  smoke)
    stage_test_gate
    stage_assert_bundle
    step "HUMAN SMOKE GATE"
    cat <<GATE

    Launch this bundle from Finder and confirm it works BEFORE signing:

        $APP_BUNDLE

      1. it launches from Finder
      2. it shows its icon in the Dock and in Finder
      3. the saved-charts pane lists the full chart library
      4. it calculates a chart
      5. the Astrocartography map window opens

    Nothing is signed or uploaded until you run the next pass. Notarization is
    slow and every rejected submission is a round trip, and an app that
    notarizes perfectly and does not run is a fully-blessed broken artifact.

    When it checks out:

        build/macos/package-macos.sh --notarize

GATE
    ;;
  sign)
    stage_test_gate
    stage_assert_bundle
    stage_sign
    step "Signed, not notarized"
    info "Launch it now and confirm it still runs:  open '$APP_BUNDLE'"
    info "A missing entitlement shows up here — the app dies immediately with a"
    info "codesigning or memory-protection error. Add ONE key to"
    info "$(basename "$ENTITLEMENTS"), re-run, and repeat until it launches."
    ;;
  notarize)
    stage_test_gate
    stage_assert_bundle
    stage_sign
    stage_notarize_app
    stage_dmg
    stage_verify
    ;;
  dmg)
    stage_test_gate
    require_bundle
    # Resume path: the app must already be signed and stapled, or the DMG would
    # ship an unnotarized app inside a notarized container.
    xcrun stapler validate "$APP_BUNDLE" >/dev/null 2>&1 \
      || die "the app is not stapled — run --notarize first.
       --dmg resumes after a Stage F failure; it does not notarize the app."
    stage_dmg
    stage_verify
    ;;
esac
