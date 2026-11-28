---
name: release-bump
description: Open and close AstroCast releases. Bumps Version, AssemblyVersion, FileVersion and InformationalVersion in every versioned .csproj with an audited all-or-nothing script, verifies the bump at the built AstroCast.Presentation.dll (the version both heads' window titles show), writes the bump commit, and places the annotated v{X.Y.Z} tag on the release's last commit. Use this whenever a commit is about to be made in the AstroCast repo and the version may equal the latest tag (CLAUDE.md's per-commit check), whenever the user says bump, new version, open a release, 1.3.4, minor bump, or asks why the csprojs disagree on a version, and whenever a release is being closed - "tag it", "cut v1.3.3", "the last 1.3.3 branch has landed", "release is done".
---

# Release bump

A release here has three parts: a **bump commit** that opens it, the branches that
land into it, and an **annotated tag on its last commit** that closes it. This skill
does the two ends. The middle is ordinary work.

Every step below exists because something went wrong without it: a version table that
was wrong twice, a bump flattened to one field form, a tag one commit behind the tree
it named, a window title nobody checked. The scripts do the mechanical checks, so
report what they print. Don't re-derive it by hand.

Paths are relative to the repository root. On Windows run the PowerShell script
with `powershell` or `pwsh`; on macOS with `pwsh`.

## 0. Is a bump due?

```bash
git tag -l 'v*' --sort=-v:refname | head -1
grep -o '<Version>[^<]*' src/AstroCast/AstroCast.csproj
```

- **Version equals the latest tag:** the last release is closed, and this commit opens
  the next one. Bump.
- **Version ahead of the tag** (e.g. 1.3.3 against `v1.3.2`): a release is open and
  branches are still landing into it. Don't bump. Commit normally.
- **Version behind the tag:** something was reverted or a tag is misplaced. Stop and
  show the user both values.

**Which number.** Patch by default. Minor and major are the user's call, so propose
one only if they raise it. A minor marks a capability threshold: 1.3.0 was the first
release that could be handed to someone (signed, notarized, accepted by Gatekeeper).
2.0.0 is reserved for Avalonia replacing WPF on Windows, with the data migration that
implies. For a minor or major bump, the commit message says why.

## 1. Bump

Close every running `AstroCast` or `AstroCast.Avalonia` process first. A running head
locks its output and the build reports `MSB3027`/`MSB3021`, which reads like a code
failure and isn't one.

```bash
pwsh -NoProfile -File .claude/skills/release-bump/scripts/Bump-Version.ps1 -NewVersion 1.3.4
```

Add `-DryRun` to see what would change without writing anything. Leave out
`-NewVersion` to audit only, which reports the one version all the projects agree
on. The script:

- **Finds the projects from the tree:** every `*.csproj` outside `bin/`, `obj/`, `.git/`
  and `.claude/` that holds a `<Version>`. It does not use CLAUDE.md's table, which
  has been wrong twice. If the count differs from the table, the table needs a row.
  Say so; the bump itself is still right.
- **Audits everything before writing anything.** Each project must hold each of the
  four elements exactly once, with no uncommitted change. All of them must agree on one
  current version: X.Y.Z in `Version` and `InformationalVersion`, X.Y.Z.0 in
  `AssemblyVersion` and `FileVersion`. The new version must be the next patch, minor
  or major, so a typo like 1.3.40 is refused. Any failure: exit 1, nothing written.
- **Replaces only those four elements' text.** Line endings (CRLF on Windows, LF on
  the Mac) and encoding are untouched. It then reads every file back from disk and
  counts the two forms separately. A bump that flattened all four fields to one form
  would still total 32 and still build, which is why a total alone proves nothing.
  It also checks that `git diff --numstat` shows 4 and 4 in every file.
- **Exit 2** means files were written but failed the read-back. Run the
  `git checkout -- ...` line it prints before doing anything else.

**If the audit says the projects disagree,** show the user the odd ones out and don't
patch them by hand. The likely cause is Automatic Versions: in Visual Studio it bumps
only the projects registered with it, and `AstroCast.Avalonia` and `AstroCast.UiTests`
are not registered. Ask the user which version is right; a drift is a fact about the
history, not a typo to tidy.

## 2. Verify at the artifact

The `.csproj` text is only a claim. What the user sees is the window title and status
bar, from `MainViewModel.GetAppVersion()`, which reads `InformationalVersion` from
`AstroCast.Presentation.dll` and trims any `+<hash>`. Each head deploys its own copy,
so read every copy.

**Build** with `--no-incremental`:

- **Windows:** `dotnet build --no-incremental` at the root builds all eight projects.
- **macOS:** the solution build stops at once on the WPF head with `NETSDK1100`
  (Windows targeting). Build the three projects that cover the other seven instead:

  ```bash
  dotnet build src/AstroCast.Cli --no-incremental
  dotnet build tests/AstroCast.Tests --no-incremental
  dotnet build tests/AstroCast.UiTests --no-incremental
  ```

  The WPF head's copy is read on Windows. Say so in the commit message; don't imply it
  was read here.

Compare the distinct warning codes with the last build. `xUnit1031` is the known one,
and a new code is a finding to report.

**Test** both suites, as separate runs, and report them separately (never summed):

```bash
dotnet test tests/AstroCast.Tests
dotnet test tests/AstroCast.UiTests
```

**Read each head's copy**, one process per copy:

```bash
find src/AstroCast/bin/Debug src/AstroCast.Avalonia/bin/Debug -name AstroCast.Presentation.dll 2>/dev/null
dotnet run .claude/skills/release-bump/scripts/read-app-version.cs -- <path> --expect 1.3.4
```

(PowerShell: `Get-ChildItem src/AstroCast/bin/Debug, src/AstroCast.Avalonia/bin/Debug -Recurse -Filter AstroCast.Presentation.dll`.)

The reader invokes the real `GetAppVersion()` rather than reimplementing it, and checks
all four values. A copy built before the bump reads the old version and fails, so a
stale build can't pass. Ignore `bin/Release` copies: they are left over from packaging
and predate the bump. Exit 1 is a mismatch; find out why before committing.

## 3. Commit

The bump is its own commit, containing only the `.csproj` changes:
`chore: bump version to X.Y.Z`, with a reason after a comma for a minor or major bump.
Commit only as part of a commit the user asked for. If they asked only for the bump,
stop after step 2 and show the diff and the readings.

Body, after the recent bump commits (`git show 20ffcf9` is a good model):

```
chore: bump version to 1.3.4

Opens release 1.3.4. Follows <what landed just before, with commit hashes,
and what it changed, in a sentence or two>.

Verified on <the Windows machine | macOS>: <build scope> with <warning codes> the
only warning code(s); AstroCast.Tests <n>/<n> and AstroCast.UiTests <m>/<m>
after this bump. <N> csprojs, <4N> fields: three-component 1.3.4 <2N>,
four-component 1.3.4.0 <2N>, 4/4 lines in each file; <both heads' copies |
the Avalonia head's copy> of AstroCast.Presentation.dll read 1.3.4 by
GetAppVersion(), with AssemblyVersion and FileVersion 1.3.4.0.

Not tagged: v1.3.4 is tagged after the last 1.3.4 branch lands.

<the session's Co-Authored-By trailer>
```

Use the numbers the scripts and test runs printed, not the template's placeholders or
CLAUDE.md's figures.

## 4. Close the release: tag

The tag goes on the **last commit of the release**, usually the docs commit that
records the final landing ("docs: record ..., completing release X.Y.Z"). It goes on
the bump commit only when nothing else belongs to the release. This was settled at
`v1.0.243`, because `v1.0.242` was tagged straight after its bump and a docs commit
landed after it, so checking out that tag gives a tree one commit behind what was
verified and shipped. On the release head, `git checkout v{X.Y.Z}` reproduces exactly
what was built and tested, including the CLAUDE.md that describes it.

1. **Ask the user to confirm the release is complete.** Only they know whether more
   branches belong to it ("v1.3.3 is tagged after the last 1.3.3 branch lands").
2. **Check the head:**
   - you are on the commit to be tagged (normally `master` after the last landing)
     and the tree is clean;
   - every project agrees on X.Y.Z: run the bump script with no `-NewVersion`
     (audit only) and read its "Current" line;
   - `git tag -l vX.Y.Z` is empty;
   - both test suites passed **at this commit**. The tag's whole point is that its
     tree was verified.
3. **Tag and verify:**

   ```bash
   git tag -a "vX.Y.Z" -m "Release vX.Y.Z"
   git cat-file -t vX.Y.Z                                  # tag (annotated, not lightweight)
   test "$(git rev-parse 'vX.Y.Z^{commit}')" = "$(git rev-parse HEAD)" && echo on-head
   ```

4. **Push only when the user asks** (`git push origin vX.Y.Z`). A pushed tag is
   immutable by convention here: never move or re-create one. Tags at or before
   `v1.0.242` sit on their bump commits and stay there.

## Reference

| Item | Value |
|---|---|
| Tag format | `v{Major}.{Minor}.{Patch}`, annotated, message `Release v{X.Y.Z}` |
| Field forms | `Version`, `InformationalVersion`: X.Y.Z. `AssemblyVersion`, `FileVersion`: X.Y.Z.0 |
| Shared metadata | `Directory.Build.props` (Company, Product, Copyright). Not versioned; leave it alone |
| Installer | `BuildInstaller.ps1` reads the published EXE's version for WiX. The MSI's `UpgradeCode` never changes |
| macOS DMG | A DMG built before a bump carries the old version. Rebuild and re-notarize before handing it on |
