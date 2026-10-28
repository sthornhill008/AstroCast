# Signing and notarizing AstroCast on macOS

**What `build/macos/package-macos.sh` does, why it differs from the procedure it
was ported from, and what a successful run looks like.**

| | |
|---|---|
| **Adapted from** | `APPLE_NOTARIZATION.md` — "The ChessSage Procedure" (itself adapted from PhotoName) |
| **Verified against** | AstroCast v1.2.9, macOS 26.5.2, Apple M5 Pro, 2026-08-10 |
| **Script** | `build/macos/package-macos.sh` |
| **Entitlements** | `build/macos/AstroCast.entitlements` |
| **Signing identity** | `Developer ID Application: STEVEN LE GRAND F THORNHILL (26669DP59A)` |
| **Team ID** | `26669DP59A` |
| **Notary profile** | `PhotoName-Notary` — account-level; notarizes any app |
| **Bundle identifier** | `com.sthornhill.astrocast` — lowercase, fixed at slice 5a, **must not change** |

---

## Why this document exists, and where it lives

The reference procedure lives at `~/astrocast-mac-setup/APPLE_NOTARIZATION.md`, outside version
control and unreachable from the Windows machine. That is the same shape of gap that let the macOS
26 deployment-target floor ship unnoticed in Phase 1: the working `libswe` build command existed
only in an out-of-repo report, while the tracked plan carried a different, flagless one. Nobody was
wrong; the authoritative copy simply was not the one in the repository.

So this document sits **in the repo, beside the script it describes** — `build/macos/SIGNING.md`,
next to `package-macos.sh`, `make-bundle.sh`, `make-icon.sh` and `build-libswe.sh`. It is versioned
with the script, diffs when the script changes, and is visible from every clone.

`APPLE_NOTARIZATION.md` is **not** replaced. Its §§1–9 are verified against a working script for two
other applications and remain accurate for them.

### Lineage — what transferred and what did not

Following the reference's own convention of recording what it was adapted from:

| Element | Transfers? |
|---|---|
| §3 identity discovery, notary profile | **verbatim** — the keychain lookup and `PhotoName-Notary` work unchanged |
| §5 two-pass design | **verbatim**, and it earned its keep (see *the staple trap* below) |
| §6 Stage A principle — assert the build before spending a cycle on it | **verbatim** |
| §6 Stage D bundle-root sweep | **verbatim** |
| §6 **Stage D nested-code loop** | **DOES NOT TRANSFER** — see below. This is the headline. |
| §6 Stage E `ditto` over `zip`, staple the `.app` | **verbatim** |
| §6 Stage F DMG, artifact-not-exit-code test, `hdiutil` fallback | **verbatim** |
| §6 Stage G both `spctl` forms, SHA-256 gate | **verbatim** |
| §10 (unverified .NET guidance) | partly superseded — see the dated addendum in that document |

---

## 1. The headline: 278 nested code objects, not 21

**The rule.** `codesign` seals `Contents/Resources/` as *resources*, but treats **everything in
`Contents/MacOS/` other than the main executable as nested code requiring its own signature.**

That is invisible for PyInstaller and SwiftPM, whose bundles keep `Contents/MacOS/` nearly empty. A
self-contained .NET publish lands its **entire payload** flat in that directory:

| Kind | Count |
|---|---:|
| Managed assemblies (`.dll`, PE32/PE32+) | 226 |
| Native dylibs | 20 |
| `createdump` (runtime helper, no extension) | 1 |
| Fonts (`.ttf`) | 18 |
| Ephemeris data (`.se1`) | 6 |
| `.pdb` (3), `.json` (2), `.txt` (2) | 7 |
| **Total nested code objects** | **278** |
| *(plus the main executable `AstroCast.Avalonia`)* | *1 — 279 files in all* |

The reference's loop matches `Mach-O` by content, which here selects **21** — the 20 dylibs and
`createdump`. It therefore misses **257** objects, and the bundle then refuses to sign, reporting
one at a time:

```
AstroCast.app: code object is not signed at all
In subcomponent: …/Contents/MacOS/Avalonia.Remote.Protocol.dll
```

and, once the 226 assemblies are signed, the next one:

```
In subcomponent: …/Contents/MacOS/ephe/seas_12.se1
```

Yes — a Swiss Ephemeris **data file**. Everything in that directory is in scope, including files
that are plainly not code.

The working predicate is therefore *location*, not content:

```sh
# Every regular file under Contents/MacOS EXCEPT the main executable.
find "$APP_BUNDLE/Contents/MacOS" -type f ! -name "$EXECUTABLE_NAME"
# …plus, defensively, any Mach-O living elsewhere under Contents/.
```

**Verification that the split is exactly as described:** after a successful signing run the bundle
carries **257** `com.apple.cs.CodeDirectory` extended attributes — one per non-Mach-O object. The
other 21 are Mach-O and carry their signatures embedded, so they need no attribute. 21 + 257 = 278.

---

## 2. Why signing data files is safe — verified, not asserted

`codesign` cannot embed a signature into a PE file or a data file, so it stores one in extended
attributes and **leaves the file's bytes untouched**:

```
com.apple.cs.CodeDirectory
com.apple.cs.CodeRequirements
com.apple.cs.CodeSignature
```

Measured on `Avalonia.Remote.Protocol.dll`, three ways:

| | bytes | sha256 (first 24) |
|---|---:|---|
| Signed copy in the bundle | 75,776 | `bd3be7c4f8eb98db8b9a022b` |
| Same file with its xattrs cleared | 75,776 | `bd3be7c4f8eb98db8b9a022b` |
| Unsigned copy from the publish output | 75,776 | `bd3be7c4f8eb98db8b9a022b` |

All three identical. The .NET runtime therefore loads exactly the bytes it was built with, and
strong-naming and assembly loading are unaffected.

The attributes **survive the `ditto -c -k --keepParent` round-trip** that notarization uploads —
confirmed by archiving a signed assembly, extracting it, and re-reading its signature. They also
survive the DMG, which carries an HFS+ filesystem.

---

## 3. The `xattr` asymmetry — a rule, not a preference

> **Clear extended attributes BEFORE the signing loop. Never after.**

Because 257 of the 278 signatures live *in* extended attributes, a post-signing `xattr -cr` strips
them and leaves a bundle that **looks signed and is not**. This is not theoretical — clearing the
attributes on a copy of a signed assembly destroys its signature outright while leaving the file
byte-identical:

```
codesign -dv with-xattrs.dll     → Identifier=…            (signed)
codesign -dv xattrs-cleared.dll  → code object is not signed at all
```

`package-macos.sh` runs `xattr -cr` once, immediately before the loop, and never again.

### The `com.apple.provenance` red herring

The reference's §9 prescribes `xattr -cr` for the *"resource fork, Finder information, or similar
detritus"* rejection. On macOS 26 it does not clear everything, and the leftovers look alarming:

- **287** `com.apple.provenance` attributes remained on the unsigned bundle after `xattr -cr`
  (290 on the signed bundle, which has more files).
- `xattr -d com.apple.provenance <file>` **exits 0 and does nothing** — the attribute is
  system-protected. A silent no-op, not an error.

It is **benign**. The three attributes `codesign` actually rejects were all at zero:

| Attribute | Count |
|---|---:|
| `com.apple.FinderInfo` | 0 |
| `com.apple.ResourceFork` | 0 |
| `com.apple.quarantine` | 0 |

Recorded so the next person does not spend time trying to remove something that cannot be removed
and does not matter.

---

## 4. Three diagnostic traps

Each one yields a confident, wrong answer.

### 4a. The error comes from `sign`, not `verify`

```
AstroCast.app: code object is not signed at all
In subcomponent: …/Avalonia.Remote.Protocol.dll
```

This is emitted by `codesign --sign`, which **exits 1**. It appears immediately above the
`codesign --verify` that normally follows it in a script, and reads exactly like a verification
complaint. It is not one: the signing genuinely failed and no signature was written. Capture the
exit code of the sign command explicitly before concluding anything about verification.

### 4b. `codesign -dv` cannot tell you what is in scope

`codesign -dv <file>` reports `code object is not signed at all` for **any** unsigned file, whether
or not `codesign` would ever have required a signature there. Running it across the bundle to
discover which files need signing answers a different question and produces a misleading map. Only
signing the bundle itself reveals the real scope, one complaint at a time.

### 4c. App Translocation moves the app you are testing

A quarantined app launched from anywhere other than `/Applications` is copied by macOS to a
randomised, read-only path and executed from there:

```
/private/var/folders/…/AppTranslocation/0EBE37F2-…/d/AstroCast.app/Contents/MacOS/AstroCast.Avalonia
```

A liveness check that greps for the path you launched finds nothing and reports failure while the
app is running perfectly. Match on the executable name, or on the log, not on the path you expected.

---

## 5. Entitlements: `allow-jit` alone

`build/macos/AstroCast.entitlements` grants exactly one key:

```xml
<key>com.apple.security.cs.allow-jit</key><true/>
```

Read back from the signature on the shipped bundle — not from the file on disk — that is the only
entitlement present.

| Key | Needed? |
|---|---|
| `com.apple.security.cs.allow-jit` | **yes** — the CoreCLR JIT writes a page then executes it, which the hardened runtime forbids by default |
| `com.apple.security.cs.allow-unsigned-executable-memory` | no |
| `com.apple.security.cs.disable-library-validation` | no |

**Why the third is unnecessary, and the condition under which it becomes necessary.** Library
validation requires every loaded library to be signed by the same Team ID as the host. That holds
here *because the inside-out loop signs all 278 nested objects with the same identity*. The two
facts are linked: weaken the signing loop — leave a nested dylib unsigned, or ship one signed by a
third party — and `disable-library-validation` immediately becomes required. If a future dependency
arrives pre-signed by someone else, expect to need it, and prefer re-signing the dependency to
granting the key.

**The rule, carried from the reference: grant only what an observed failure demands.** Each key
weakens the hardened runtime.

**The failure mode is recognisable and late.** A missing entitlement does not fail signing and does
not fail notarization. The app notarizes and staples cleanly, then crashes immediately on launch
with a codesigning or memory-protection error. That is why `package-macos.sh` has a `--sign-only`
mode: sign, launch, add one key, repeat — a loop that costs seconds. Discovering the same thing
through notarization costs minutes per attempt.

---

## 6. The staple trap: submit target ≠ staple target

`notarytool` needs a container to upload, so an `.app` goes up as a zip. But a ticket cannot be
attached to a zip:

```
Processing: …/AstroCast.zip
Stapler is incapable of working with ZIP archive files.
```

The ticket belongs on the `.app`, which is what the user actually runs.

**The reference gets this right** — its Stage E submits `$zip` and staples `$APP_BUNDLE`. The bug is
introduced by *refactoring*: folding submit-and-staple into one helper shared by the app and the
DMG, because for the DMG the two artifacts genuinely are the same file. `package-macos.sh` therefore
takes them as two separate parameters:

```sh
notarize_artifact "$zip" "$APP_BUNDLE" "app" …   # submit the zip, staple the bundle
notarize_artifact "$dmg" "$dmg"        "dmg" …   # for a DMG, the same file
```

**The recovery is the point of the resume path.** When this failed, the app had already been
*Accepted*. Re-running the whole pass would have meant a second notarization cycle for an artifact
Apple had already blessed. Instead: staple the already-issued ticket directly, then resume Stage F
with `--dmg`. That is exactly the failure §5 predicted the resume entry point would pay for.

---

## 7. Bundle layout — a decision, not an oversight

The tidy fix for §1 is to move the non-code out of `Contents/MacOS/` into `Contents/Resources/`,
which is the conventional macOS layout and would shrink the nested-code set from 278 to roughly 21.

**It is deliberately not done.** `ephe/` must remain resolvable from `AppContext.BaseDirectory`,
which inside a bundle is `Contents/MacOS/`, and `AstroCast.Core` resolves it that way — a Phase 1
decision that the bundle was built to satisfy without a source change. Moving the directory would
require either a source change in Core or a symlink, and would re-open slice 5a's exit criterion.

Signing 278 objects costs a few minutes of timestamped signatures and is entirely reliable. Changing
the layout is a bundle-layout slice with its own verification, not something to slip into a signing
change. **If you are tempted to "tidy" this, that is the reason it is the way it is.**

---

## 8. Ordering hazard: never rebuild after signing

`make-bundle.sh` regenerates the `.icns` on **every** run (slice 5b, so bundle artwork cannot drift
from repo artwork). Correct in itself — and destructive after signing, because any write into
`Contents/` invalidates the seal.

Worse: a write *after notarization* produces an artifact that passes every local check and fails on
a user's machine, because the ticket no longer matches what is on disk.

```
build → smoke → sign → notarize → staple → DMG        ← fixed order
```

**The enforcement is structural, not a comment in a header:**

- `package-macos.sh` contains **no call** to `make-bundle.sh`, `make-icon.sh` or `dotnet publish`.
- `require_bundle` **refuses to continue** when the bundle is missing, and says to run
  `make-bundle.sh` as a separate earlier step, rather than helpfully building one.
- `--dmg` refuses unless the app already validates as stapled, so the resume path cannot package an
  unnotarized app inside a notarized container.

Building is a separate, earlier, human-invoked step. Adding a build call to the packaging script
reintroduces the hazard.

---

## 9. What a successful run looks like

Verified end to end on 2026-08-10, AstroCast v1.2.9.

### Notarization

| Artifact | Submission ID | Status |
|---|---|---|
| `AstroCast.zip` (staples the `.app`) | `38a69a41-fbc2-4f8f-8459-8921c2a54967` | **Accepted** |
| `AstroCast-v1.2.9-macOS-arm64.dmg` | `e7265640-270c-4ccd-9aae-0108f952d83e` | **Accepted** |

Both confirmed against Apple's own record with `xcrun notarytool info`, not only from the local logs.

### Signature

```
Identifier=com.sthornhill.astrocast
CodeDirectory … flags=0x10000(runtime)
Authority=Developer ID Application: STEVEN LE GRAND F THORNHILL (26669DP59A)
Authority=Developer ID Certification Authority
Authority=Apple Root CA
TeamIdentifier=26669DP59A
```

### Gatekeeper — both forms, because an app and a DMG are assessed differently

```
$ spctl -a -t exec -vvv AstroCast.app
AstroCast.app: accepted
source=Notarized Developer ID
origin=Developer ID Application: STEVEN LE GRAND F THORNHILL (26669DP59A)

$ spctl -a -t open --context context:primary-signature -vvv AstroCast-v1.2.9-macOS-arm64.dmg
…dmg: accepted
source=Notarized Developer ID
origin=Developer ID Application: STEVEN LE GRAND F THORNHILL (26669DP59A)
```

`xcrun stapler validate` succeeds on both.

### The DMG

| | |
|---|---|
| Name | `AstroCast-v1.2.9-macOS-arm64.dmg` |
| Size | **68,469,335 bytes** (65.3 MiB) |
| SHA-256 | `adbbb9f89064d9b12c13cae2cfe4345bfe21beb8511cf7998fff90ca859942e3` |
| Built by | `create-dmg` (styled, with volume icon) — the `hdiutil` fallback did not run |

`create-dmg` emits `hdiutil does not support internet-enable` as a warning; it is not a failure.
Per §6 of the reference, test for the **artifact**, not the exit code.

### The download path — the only test that exercises what a user receives

A copy of the DMG was given a browser quarantine flag and re-hashed against the original: identical,
which is the §8 post-upload gate that once caught an HTTP 200 delivering nothing.

Mounted; app dragged out; the dragged copy carried quarantine `0281;00000000;;`, remained stapled,
and `spctl` accepted it. Launching produced **one** dialog, from `CoreServicesUIAgent`:

> **"AstroCast" is an app downloaded from the Internet. Are you sure you want to open it?**
>
> This file was downloaded on an unknown date. **Apple checked it for malicious software and none
> was detected.**
>
> `[Cancel]` `[Open]`

**That is the success wording.** The sentence about Apple having checked it is what notarization
buys. An un-notarized app instead shows *"cannot be opened because Apple cannot check it for
malicious software"* and offers **no Open button**. If you see a consent dialog, notarization
worked; if you see a block, it did not.

After **Open**, macOS ran it under App Translocation (see §4c). From that randomised read-only path
it still:

- resolved `ephe/` **inside the translocated bundle**,
- found the database at the real user location, `~/Library/Application Support/AstroCast/`
  — not redirected,
- loaded **90 charts**,
- and computed **53 electional charts**.

That last check is not redundant. `swe_set_ephe_path` merely *stores* a path; the `.se1` files are
opened at calculation time. Only a real calculation proves they can be read from a translocated,
read-only image.

---

## 10. Report the size in bytes, not `du -h`

`du -h` reports **allocated blocks** and overstated this DMG as `81M` against a real 68,469,335
bytes (65.3 MiB) — a 25% discrepancy on an APFS volume.

Next to a SHA-256 taken over the real bytes, a size figure that disagrees with them is worse than no
size at all: it invites someone verifying a download to conclude the file is truncated. Use
`stat -f%z`, or `du -h -A` for the apparent size.

---

## Quick reference

```sh
# Pass 1 — test gate, assert the bundle, stop at the human smoke gate.
build/macos/make-bundle.sh          # build first, always a separate step
build/macos/package-macos.sh

# Tight loop for entitlements only: sign, then launch and observe.
build/macos/package-macos.sh --sign-only

# Pass 2 — sign, notarize + staple the app, DMG, verify.
build/macos/package-macos.sh --notarize

# Resume after a Stage F failure, against an already-stapled app.
build/macos/package-macos.sh --dmg
```

**Non-negotiables**

- Sign **everything under `Contents/MacOS/`**, not only the Mach-O binaries
- `xattr -cr` **before** the loop, never after
- `--options runtime` and `--timestamp` on every signature
- Sign **inside-out**: nested objects first, the bundle last
- `ditto -c -k --keepParent`, never `zip`
- Submit the zip, **staple the `.app`**
- Notarize **and staple** both the app and the DMG, separately
- Never rebuild, re-copy or `xattr -c` anything after signing
- Verify the artifact a user downloads, not the one you built
