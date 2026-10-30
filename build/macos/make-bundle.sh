#!/bin/bash
#
# make-bundle.sh — build AstroCast.app, a double-clickable macOS application
# bundle wrapping the Avalonia head.
#
# WHY THIS IS A SHELL SCRIPT AND NOT AN MSBUILD TARGET
# ----------------------------------------------------
# The obvious alternative is an `AfterTargets="Publish"` target inside
# AstroCast.Avalonia.csproj. That csproj is built by Windows too, so such a
# target would have to be conditioned on the host OS, and a mis-conditioned
# target is exactly the class of silent cross-platform failure this repository
# has been bitten by repeatedly (see the Verification Methodology section of
# CLAUDE.md). A shell script that Windows never executes cannot regress the WPF
# head, and that is provable by inspection rather than by testing. It is also
# independently runnable and debuggable, which an MSBuild target embedded in a
# publish pipeline is not.
#
# WHY SELF-CONTAINED, NOT FRAMEWORK-DEPENDENT
# --------------------------------------------
# An app launched from Finder inherits none of the shell's environment. The
# apphost locates the shared runtime through DOTNET_ROOT, then
# /etc/dotnet/install_location*, then /usr/local/share/dotnet. On a machine
# whose .NET lives in a per-user install (~/.dotnet) none of those exist, and a
# framework-dependent bundle fails at launch with "You must install .NET to run
# this application" before a single line of our code runs. Measured, not
# assumed: a framework-dependent publish run under `env -i` reports exactly
# that. Self-contained removes the question.
#
# Trimming is deliberately NOT enabled. Reflection is load-bearing on the
# shipping path: sixteen sites read AssemblyInformationalVersionAttribute, and
# EventTypeHelper resolves enum DescriptionAttributes by name
# (typeof(EventType).GetField(...)) to populate the event-type combo box. The
# trimmer strips that metadata silently, and the failure would surface as an
# empty dropdown at runtime rather than as a build error.
#
# USAGE
#   build/macos/make-bundle.sh [-o OUTPUT_DIR] [-c CONFIGURATION] [-r RID]
#
# Defaults: OUTPUT_DIR=build/macos/dist, CONFIGURATION=Release, RID=osx-arm64
#
set -euo pipefail

# ---------------------------------------------------------------------------
# Locate the repository root from this script's own location, so the script
# works regardless of the caller's working directory.
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CONFIGURATION="Release"
RID="osx-arm64"
OUTPUT_DIR="$REPO_ROOT/build/macos/dist"

# The bundle identifier is permanent. Code signing and notarization key off it,
# so changing it later invalidates the identity of every previously shipped
# build. Backed by a domain actually controlled by the author, which is the
# convention Apple's notary service expects.
BUNDLE_ID="com.sthornhill.astrocast"

APP_NAME="AstroCast"
PROJECT="$REPO_ROOT/src/AstroCast.Avalonia/AstroCast.Avalonia.csproj"
PRESENTATION_PROJECT="$REPO_ROOT/src/AstroCast.Presentation/AstroCast.Presentation.csproj"

# A path that must not exist. AstroCast.Presentation embeds the file its
# CartoKeyFile property names as the built-in CARTO key, by default the
# profile's .astrocast/carto-key.txt; publishing with CartoKeyFile naming this
# path embeds nothing, so no bundle carries a key and every installed copy reads
# its key from carto-key.txt (public builds ship no key, CLAUDE.md, Base Map).
# Checked after the publish below.
NO_KEY_FILE="$REPO_ROOT/build/macos/no-carto-key-here"

# The apphost derives the names of its sidecar files from its own filename
# (AstroCast.Avalonia.runtimeconfig.json, .deps.json), so the executable is NOT
# renamed to "AstroCast". The user-visible name comes from CFBundleName, which
# is what the menu bar and Finder display.
EXECUTABLE_NAME="AstroCast.Avalonia"

# The .icns basename inside Contents/Resources. CFBundleIconFile names it
# without the extension.
ICON_NAME="AstroCast"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--output)        OUTPUT_DIR="$2"; shift 2 ;;
    -c|--configuration) CONFIGURATION="$2"; shift 2 ;;
    -r|--rid)           RID="$2"; shift 2 ;;
    -h|--help)
      sed -n '/^# USAGE/,/^#$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

fail() { echo "error: $*" >&2; exit 1; }
step() { echo; echo "==> $*"; }

# ---------------------------------------------------------------------------
# Guard: host platform.
# ---------------------------------------------------------------------------
[[ "$(uname -s)" == "Darwin" ]] || fail "this script builds a macOS bundle and must run on macOS (found $(uname -s))"

# ---------------------------------------------------------------------------
# Guard: no running instance.
#
# A build whose output directory is held open by a running app reports
# link/copy errors that are indistinguishable at a glance from a compilation
# failure, and the natural response is to go looking for a code mistake that
# does not exist. CLAUDE.md records losing real time to exactly this. Fail
# early and say what to do instead.
# ---------------------------------------------------------------------------
if pgrep -x "$EXECUTABLE_NAME" >/dev/null 2>&1; then
  fail "an instance of $EXECUTABLE_NAME is running; quit it before building (a locked output directory produces errors that look like compilation failures)"
fi

# ---------------------------------------------------------------------------
# Guard: the no-key path does not exist, or the publish would embed it as the
# CARTO key.
# ---------------------------------------------------------------------------
[[ ! -e "$NO_KEY_FILE" ]] || fail "$NO_KEY_FILE exists; it must not, or the publish would embed it as the built-in CARTO key"

# ---------------------------------------------------------------------------
# Version.
#
# Read by evaluating the projects rather than by parsing csproj text, and the
# publish below runs from these same projects moments later, so the value
# cannot be stale relative to what lands in the bundle.
#
# AstroCast.Presentation is the authority. MainViewModel.GetAppVersion() reads
# *that* assembly's InformationalVersion to render the window title and status
# bar, so taking the bundle version from anywhere else would let Finder's
# Get Info and the application's own title bar disagree about which version is
# running. If the two projects ever drift apart, that is a versioning mistake
# rather than something to paper over, so it stops the build.
# ---------------------------------------------------------------------------
step "Resolving version"
VERSION="$(dotnet msbuild "$PRESENTATION_PROJECT" -getProperty:InformationalVersion -nologo | tr -d '[:space:]')"
AVALONIA_VERSION="$(dotnet msbuild "$PROJECT" -getProperty:InformationalVersion -nologo | tr -d '[:space:]')"

[[ -n "$VERSION" ]] || fail "could not read InformationalVersion from AstroCast.Presentation"

if [[ "$VERSION" != "$AVALONIA_VERSION" ]]; then
  fail "version drift: AstroCast.Presentation is $VERSION but AstroCast.Avalonia is $AVALONIA_VERSION.
       Both must be bumped together (CLAUDE.md, Git Commit and Tagging Process)."
fi

# CFBundleShortVersionString accepts at most three dot-separated integers.
[[ "$VERSION" =~ ^[0-9]+(\.[0-9]+){0,2}$ ]] || fail "version '$VERSION' is not a valid CFBundleShortVersionString"

echo "    version $VERSION (AstroCast.Presentation, matched by AstroCast.Avalonia)"

# ---------------------------------------------------------------------------
# Bundle layout.
# ---------------------------------------------------------------------------
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"
CONTENTS="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS/MacOS"
RESOURCES_DIR="$CONTENTS/Resources"

# Refuse to delete anything that is not recognisably a previous output of this
# script. The check is deliberately conservative: the only paths removed are an
# existing bundle carrying our own identifier, or nothing at all.
if [[ -e "$APP_BUNDLE" ]]; then
  if [[ -f "$CONTENTS/Info.plist" ]] \
     && /usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "$CONTENTS/Info.plist" 2>/dev/null | grep -qx "$BUNDLE_ID"; then
    step "Removing previous bundle at $APP_BUNDLE"
    rm -rf "$APP_BUNDLE"
  else
    fail "$APP_BUNDLE exists but is not a bundle this script produced (CFBundleIdentifier is not $BUNDLE_ID); refusing to delete it"
  fi
fi

mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

# ---------------------------------------------------------------------------
# Publish straight into Contents/MacOS.
#
# Everything the app needs sits beside the executable, which is what keeps
# AppPaths.EphemerisDirectory correct without a source change:
# AppContext.BaseDirectory resolves to Contents/MacOS/ inside a bundle, so
# ephe/ lands at Contents/MacOS/ephe exactly as it does beside a bin/ build.
#
# ephe/ and the Swiss Ephemeris native library reach the publish output through
# None items declared in AstroCast.Core.csproj, which flow transitively across
# the ProjectReference. Verified below rather than assumed.
# ---------------------------------------------------------------------------
step "Publishing $CONFIGURATION/$RID (self-contained) into the bundle"
dotnet publish "$PROJECT" \
  --configuration "$CONFIGURATION" \
  --runtime "$RID" \
  --self-contained true \
  --output "$MACOS_DIR" \
  -p:CartoKeyFile="$NO_KEY_FILE" \
  --nologo

# ---------------------------------------------------------------------------
# No built-in CARTO key — checked in the published assembly, not assumed.
#
# The embedded resource's name, AstroCast.Presentation.CartoKey, is ASCII in
# the assembly's metadata, so it counts 1 with a key embedded and 0 without
# (the C# constant of the same name is UTF-16 and does not match). grep -c
# exits 1 when it prints 0, hence the || true; a grep error prints nothing and
# fails the comparison.
# ---------------------------------------------------------------------------
step "Checking that no CARTO key was embedded"
PRESENTATION_DLL="$MACOS_DIR/AstroCast.Presentation.dll"
[[ -f "$PRESENTATION_DLL" ]] || fail "AstroCast.Presentation.dll did not survive publish"
KEY_COUNT="$(LC_ALL=C grep -a -c 'AstroCast.Presentation.CartoKey' "$PRESENTATION_DLL" || true)"
[[ "$KEY_COUNT" == "0" ]] || fail "AstroCast.Presentation.dll carries the AstroCast.Presentation.CartoKey resource (count ${KEY_COUNT:-unreadable}); a bundle must ship no CARTO key"
echo "    no built-in CARTO key in AstroCast.Presentation.dll; the user supplies carto-key.txt"

# ---------------------------------------------------------------------------
# Minimum system version — measured from the shipped binaries, not asserted.
#
# dyld enforces each Mach-O's LC_BUILD_VERSION minos at load time, so the
# bundle's real floor is the highest floor among the binaries it carries.
# Deriving it here means the plist self-corrects: when a dependency is rebuilt
# against an older deployment target, the next build lowers the floor
# automatically instead of leaving a hand-maintained number to drift.
#
# As of this writing the floor is 15.0, set jointly by libQuestPdfSkia.dylib,
# libqpdf.dylib (both from the QuestPDF package, and not ours to rebuild) and
# libswe.dylib, which slice 5a.2 rebuilt against an explicit 15.0 target. It had
# previously been compiled with no -mmacosx-version-min and so inherited the
# build machine's OS, raising the whole bundle to 26.0 on its own. Lowering the
# floor further now requires QuestPDF's binaries to drop below 15.0.
#
# Two things this loop learned the hard way:
#
#   * Match on Mach-O *content*, not on a "*.dylib" glob. The runtime ships
#     `createdump`, which has no extension; an earlier version of this scan
#     matched by name and silently omitted it. It sits at 12.0 so nothing
#     downstream changed, but a census that quietly skips binaries is exactly
#     the shape of error that let the 26.0 floor hide in the first place.
#
#   * Report every binary at the maximum, not just the first one reached. The
#     comparison below only updates on a strictly greater value, so with several
#     binaries tied at the floor it would name whichever the walk happened to
#     reach first — reading as though that one were the constraint when it is
#     merely one of several.
# ---------------------------------------------------------------------------
step "Measuring minimum system version from Mach-O load commands"
minos_of() {
  otool -l "$1" 2>/dev/null \
    | awk '/LC_BUILD_VERSION/{f=1} f&&/minos/{print $2; exit}'
}

FLOOR=""
FLOOR_SETTERS=()
MACHO_COUNT=0
while IFS= read -r binary; do
  file -b "$binary" 2>/dev/null | grep -q "Mach-O" || continue
  m="$(minos_of "$binary")"
  [[ -n "$m" ]] || continue
  MACHO_COUNT=$((MACHO_COUNT + 1))
  if [[ -z "$FLOOR" ]]; then
    FLOOR="$m"; FLOOR_SETTERS=("$(basename "$binary")")
  elif [[ "$m" == "$FLOOR" ]]; then
    FLOOR_SETTERS+=("$(basename "$binary")")
  elif [[ "$(printf '%s\n%s\n' "$FLOOR" "$m" | sort -t. -k1,1n -k2,2n | tail -1)" == "$m" ]]; then
    FLOOR="$m"; FLOOR_SETTERS=("$(basename "$binary")")
  fi
done < <(find "$MACOS_DIR" -type f)

[[ -n "$FLOOR" ]] || fail "could not determine a minimum system version from the published binaries"
if [[ ${#FLOOR_SETTERS[@]} -eq 1 ]]; then
  echo "    LSMinimumSystemVersion $FLOOR (of $MACHO_COUNT Mach-O binaries; set by ${FLOOR_SETTERS[0]})"
else
  # "${arr[*]}" joins on the FIRST character of IFS only, so IFS=', ' would
  # separate with a bare comma. Join explicitly instead.
  JOINED="$(printf '%s, ' "${FLOOR_SETTERS[@]}")"
  echo "    LSMinimumSystemVersion $FLOOR (of $MACHO_COUNT Mach-O binaries; set jointly by ${#FLOOR_SETTERS[@]}: ${JOINED%, })"
fi

# ---------------------------------------------------------------------------
# Icon.
#
# Rebuilt from the committed 1024 master on every bundle build rather than
# copied from a stored .icns, so the artwork in the bundle cannot drift from the
# artwork in the repository. make-icon.sh is cheap and verifies its own output.
# ---------------------------------------------------------------------------
step "Building the icon"
"$SCRIPT_DIR/make-icon.sh" -o "$RESOURCES_DIR/$ICON_NAME.icns" 2>&1 \
  | sed -n 's/^    /      /p' | tail -14
[[ -f "$RESOURCES_DIR/$ICON_NAME.icns" ]] || fail "make-icon.sh produced no $ICON_NAME.icns"
echo "    Resources/$ICON_NAME.icns  $(stat -f%z "$RESOURCES_DIR/$ICON_NAME.icns") bytes"

# ---------------------------------------------------------------------------
# Info.plist — generated on every build, never hand-maintained.
#
# CFBundleIconFile is written WITHOUT the .icns extension. Both forms work on
# current macOS, but the extensionless form is the documented convention and is
# what every system bundle uses.
# ---------------------------------------------------------------------------
step "Writing Info.plist"
COPYRIGHT="$(dotnet msbuild "$PROJECT" -getProperty:Copyright -nologo | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"

cat > "$CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleIdentifier</key>
	<string>$BUNDLE_ID</string>
	<key>CFBundleName</key>
	<string>$APP_NAME</string>
	<key>CFBundleDisplayName</key>
	<string>$APP_NAME</string>
	<key>CFBundleExecutable</key>
	<string>$EXECUTABLE_NAME</string>
	<key>CFBundleIconFile</key>
	<string>$ICON_NAME</string>
	<key>CFBundleShortVersionString</key>
	<string>$VERSION</string>
	<key>CFBundleVersion</key>
	<string>$VERSION</string>
	<key>LSMinimumSystemVersion</key>
	<string>$FLOOR</string>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.reference</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSHumanReadableCopyright</key>
	<string>$COPYRIGHT</string>
</dict>
</plist>
PLIST

plutil -lint "$CONTENTS/Info.plist" >/dev/null || fail "generated Info.plist is not valid"

# ---------------------------------------------------------------------------
# The executable bit is what separates an application from a folder as far as
# LaunchServices is concerned.
# ---------------------------------------------------------------------------
chmod +x "$MACOS_DIR/$EXECUTABLE_NAME"

# ---------------------------------------------------------------------------
# Verify the bundle rather than trusting the publish.
# ---------------------------------------------------------------------------
step "Verifying bundle contents"

[[ -x "$MACOS_DIR/$EXECUTABLE_NAME" ]] || fail "executable missing or not executable: $MACOS_DIR/$EXECUTABLE_NAME"
file -b "$MACOS_DIR/$EXECUTABLE_NAME" | grep -q "Mach-O" || fail "$EXECUTABLE_NAME is not a Mach-O executable"

[[ -f "$MACOS_DIR/libswe.dylib" ]] || fail "libswe.dylib did not survive publish; the engine cannot compute without it"
[[ -f "$MACOS_DIR/THIRD-PARTY-NOTICES.md" ]] || fail "THIRD-PARTY-NOTICES.md did not survive publish; the bundle must ship its third-party notices"
[[ -f "$MACOS_DIR/LICENSE" ]] || fail "LICENSE did not survive publish; the bundle must ship AstroCast's licence"

EPHE_COUNT="$(find "$MACOS_DIR/ephe" -type f 2>/dev/null | wc -l | tr -d ' ')"
[[ "$EPHE_COUNT" == "7" ]] || fail "expected 7 files in ephe/, found $EPHE_COUNT"

# A self-contained publish must carry its own runtime; if this is missing the
# bundle would fall back to searching for a shared runtime and fail from Finder.
[[ -f "$MACOS_DIR/libcoreclr.dylib" ]] || fail "libcoreclr.dylib missing — publish was not self-contained"

# Cross-check the version written into the plist against the versions the SDK
# recorded from the assemblies it actually published.
DEPS="$MACOS_DIR/$EXECUTABLE_NAME.deps.json"
[[ -f "$DEPS" ]] || fail "missing $DEPS"
python3 - "$DEPS" "$VERSION" <<'PY' || fail "published assembly versions disagree with the version written to Info.plist"
import json, sys
deps, version = sys.argv[1], sys.argv[2]
data = json.load(open(deps))
found = False
for libs in data["targets"].values():
    for name in libs:
        if name.startswith("AstroCast.Presentation/"):
            found = True
            if name.split("/", 1)[1] != version:
                print(f"  deps.json reports {name}, expected AstroCast.Presentation/{version}", file=sys.stderr)
                sys.exit(1)
sys.exit(0 if found else 1)
PY

echo "    executable   $(file -b "$MACOS_DIR/$EXECUTABLE_NAME")"
echo "    libswe.dylib present"
echo "    THIRD-PARTY-NOTICES.md present"
echo "    LICENSE present"
echo "    ephe/        $EPHE_COUNT files"
echo "    self-contained runtime present"
echo "    deps.json agrees with Info.plist version $VERSION"

# LaunchServices treats a directory as an application when it has the .app
# extension, a CFBundlePackageType of APPL, and an executable at the path named
# by CFBundleExecutable. Ask the system rather than inferring it.
if command -v mdls >/dev/null 2>&1; then
  KIND="$(mdls -name kMDItemContentType -raw "$APP_BUNDLE" 2>/dev/null || true)"
  echo "    content type $KIND"
fi

BUNDLE_SIZE="$(du -sh "$APP_BUNDLE" | cut -f1 | tr -d ' ')"

step "Built $APP_BUNDLE"
echo "    version      $VERSION"
echo "    identifier   $BUNDLE_ID"
echo "    minimum OS   $FLOOR"
echo "    size         $BUNDLE_SIZE"
echo "    base map     no built-in key; the user supplies carto-key.txt"
echo
echo "The bundle is unsigned. Gatekeeper will refuse it on first launch until it"
echo "is signed and notarized; until then, open it once via Finder's right-click"
echo "> Open, which offers an override that double-clicking does not."
