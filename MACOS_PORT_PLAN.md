# AstroCast -- macOS Port Plan

- **Date:** 2026-07-05
- **Prepared by:** Claude Code (read-only codebase assessment)
- **Status:** **Phases 0-5 complete.** Phase 4 closed 2026-08-10 with the
  Returns panel, the ninth and last (`v1.2.7`) -- all nine panels translated and
  verified on Windows and macOS, `AstroCast.Tests` 2218/2218 on both,
  `AstroCast.UiTests` 82/82. **Phase 5 closed the same day at `v1.3.0`**: the
  `.app` bundle (5a), the macOS 15+ deployment-target floor (5a.2), the icon
  (5b), and signing/notarization/stapling (5c). **AstroCast is now a
  distributable macOS application** -- notarized, stapled, and accepted by
  Gatekeeper on a machine that has never seen the source. Remaining:
  **Phase 6** (parity verification and steady state) and the live Section 9
  findings, none of which the port was permitted to fix: items 6 to 12 and 14
  to 16 (item 3's naming is handled by the Phase 1 resolver, item 4 was fixed
  by O-7 in 1.3.3, and item 5 closes with Docs-2 in 1.3.4). Per-phase detail
  is in Section 6, which since Phase 5 also records every landing through
  release 1.3.4.
- **Repo state analyzed:** branch `fix/electional-engine-livereview-20260610`, v1.0.234, clean tree.

---

## 1. Executive Summary

AstroCast is a .NET 10 / C# 14 solution whose calculation engine (`AstroCast.Core`,
~38,300 lines, 76% of the application C#) already targets the cross-platform
`net10.0` TFM and contains **no Windows-specific APIs**. The only native dependency
is the Swiss Ephemeris C library, whose source is vendored in `lib/swisseph/src`
and compiles on macOS with a single `clang` command. The blockers are confined to
the WPF presentation layer (~11,700 C# lines + 2,450 XAML lines) and Windows-only
packaging (WiX MSI).

**Recommendation: port the UI to Avalonia 11**, extracting the existing view models
(which are already 95% framework-clean thanks to the H-1 decomposition work) into a
shared presentation project so the WPF app keeps working during the transition.
The decisive factor: the single third-party control in use, Mapsui (astrocartography
map), ships a first-party Avalonia package, and Steve already uses Avalonia +
MVVM in MyBridge. Roughly 90% of the C# carries over unchanged or with mechanical
edits; the genuine rewrite is the XAML views, ~1,050 lines of code-behind, and a
macOS bundling/signing script.

Suggested sequencing: get the CLI smoke test and the Core test suite green on the
Mac first (days, high confidence), then extract the presentation layer on Windows
(fully covered by existing tests), then build the Avalonia UI panel by panel.
First working macOS GUI build is estimated at **5-8 weeks of part-time sessions**,
with the headless engine running on the Mac inside the first week.

---

## 2. Current State

> **Note (2026-10-14, Docs-2).** Sections 2 and 3 are the dated assessment of
> 2026-07-05 and stand as written. Three of their statements no longer hold:
> houses are cast in the chart's own system, one of nine, Placidus by default
> (2.3); the 23 goldens cover the Markdown, JSON and series formatters but none
> covers `ChartPdfFormatter`, `ParanMarkdownFormatter` or the CLI's console
> output (2.3); and the Avalonia head writes its logs to `<DataDirectory>/logs`,
> `~/Library/Application Support/AstroCast/logs` on macOS (`AppPaths`), not to
> `~/Library/Logs/AstroCast/` (3.5).

### 2.1 Stack (confirmed from project files, not docs)

| Item | Value | Evidence |
|------|-------|----------|
| Language | C# 14 (`LangVersion 14`), nullable enabled | all four `.csproj` |
| Runtime | .NET 10 (LTS) | `TargetFramework net10.0` / `net10.0-windows` |
| Solution format | `.slnx` (XML solution, VS 2026) | `AstroCast.slnx` |
| UI framework | WPF (`UseWPF`, `OutputType WinExe`) | `src/AstroCast/AstroCast.csproj` |
| Native interop | Swiss Ephemeris via classic `DllImport` | `src/AstroCast.Core/Interop/SwissEphInterop.cs` |
| Database | SQLite via Microsoft.Data.Sqlite 10.0.3 | Core csproj |
| PDF | QuestPDF 2026.2.1 (Community license) | Core csproj |
| Logging | Serilog 4.3.1 (file sink in GUI, console sink in CLI) | csproj files |
| Tests | xUnit 2.9.3 + FluentAssertions 8.8.0, 156 test files / ~50,000 lines | `tests/AstroCast.Tests` |
| Versioning | Automatic Versions extension (Visual Studio only) bumps all four csproj version fields | `CLAUDE.md` |

### 2.2 Solution structure and dependencies

```
AstroCast.slnx
  src/AstroCast.Core     net10.0          250 files / 38,293 lines  (models, enums,
                                          36 service interfaces, ~58 services,
                                          25 formatters, SQLite repo, SwissEph interop)
  src/AstroCast          net10.0-windows   26 files / 11,657 lines + 3 XAML / 2,450 lines
                                          (WPF views, 12 view models, commands, dialogs)
  src/AstroCast.Cli      net10.0            1 file  /    220 lines  (J2000 smoke test)
  src/AstroCast.Installer                  WiX v4 .wxs sources (no csproj; built by
                                          BuildInstaller.ps1)
  tests/AstroCast.Tests  net10.0-windows  156 files / 49,990 lines  (references BOTH
                                          Core and the WPF project)
  lib/swisseph           src/ (vendored C sources), win-x64/swedll64.dll,
                                          linux-x64/ (README only, no binary)
  ephe/                  4 MB of .se1 ephemeris data + sefstars.txt (platform-neutral)
```

Dependency direction is clean and one-way: `AstroCast` (WPF) and `AstroCast.Cli`
depend on `AstroCast.Core`; Core depends on nothing in the solution. Composition
is manual DI in `App.xaml.cs` (376 lines) -- there is no DI container, and by
explicit project policy (docs/H-1_Decomposition_Pattern.md) no MVVM framework:
`ChildViewModelBase`, `RelayCommand`, `AsyncRelayCommand` are in-house.

The boundary between UI and domain logic is the single biggest asset for this
port: every calculation, formatter, and the repository sit behind interfaces in
Core, and the view models consume only those interfaces plus `IDialogService`
(which already lives in Core precisely to avoid WPF coupling).

### 2.3 What the application does

Personal-use astrological calculation engine (Swiss Ephemeris, tropical zodiac,
Placidus houses), per Ebertin's cosmobiology plus a large body of traditional
techniques. Feature areas, confirmed from the service catalog and formatters:

- **Natal/event charts:** 12 bodies, houses/angles (incl. Vertex), 9 aspect types
  with applying/separating, Ebertin midpoints and 45-degree-dial activations,
  declination aspects, antiscia, fixed stars and parans, 5-tier essential
  dignities, sect, planet conditions (combustion etc.), Moon conditions and
  void-of-course, Moon mansions, planetary hours, Arabic parts, stations.
- **Predictive:** transits (aspect finder, ingress/station detectors, midpoint
  scanner, timelines), secondary progressions, solar arc, profections, Firdaria,
  zodiacal releasing, solar/lunar/planetary returns, return+ingress timeline,
  planetary cycle timelines.
- **Mundane:** ingresses, lunations, eclipses, great conjunctions, synodic
  cycles, mundane aspects.
- **Electional:** scored candidate search with time-of-day / day-of-week
  constraints and malefic-avoidance scoring.
- **Relational:** synastry, composite charts.
- **Locational:** relocation, astrocartography with an interactive Mapsui map
  (CartoDB/OSM tiles) in a separate window.
- **Medical astrology:** temperament calculator, health profile generator.
- **Horary:** radicality checks, perfection analyzer.
- **I/O:** Markdown, JSON, PDF (QuestPDF), and console formatters -- all golden-
  tested; SQLite chart storage (input data only, positions always recomputed);
  location search via Nominatim HTTP + GeoTimeZone lat/lon-to-IANA lookup.

UI shape: one 950x600 window, nine panels navigated by Alt+1..9 key bindings,
plus the floating astrocartography map window.

### 2.4 How it is built and shipped on Windows today

- Build: Visual Studio 2026 or `dotnet build`; versions auto-incremented by the
  Automatic Versions VS extension; annotated git tag per release.
- Package: `BuildInstaller.ps1` publishes win-x64, generates WiX `Components.wxs`,
  and builds an MSI with Start Menu / Desktop shortcuts (HKCU registry values are
  used only as WiX shortcut KeyPaths -- the application itself never touches the
  registry).
- Distribution: local only (no remote repo; OneDrive is the backup).

> **Note (2026-10-01).** The three bullets above are the dated record of the
> original script and stand as written. Since `ec85d12` (committed 2026-08-10,
> rebased as `6ddb52a` and landed 2026-10-01) `BuildInstaller.ps1` publishes
> **self-contained** win-x64, harvests the publish tree **recursively** (one
> `Directory` element and one `ComponentGroup` per directory found, ids derived
> from the relative path), writes the MSI to a gitignored `dist\` beside the
> repository root instead of the Desktop, and builds with **WiX 6** (the `wix`
> CLI, 6.0.2 with `WixToolset.UI.wixext`, no `.wixproj`); the "WiX Toolset v4"
> row in 2.5 below is therefore WiX 6 as well. The repository also has a GitHub
> remote now (`origin` on the Maple and Mac clones), so "no remote repo" no
> longer holds; OneDrive remains the backup of the working copies.

### 2.5 Third-party dependency audit

| Package | Where | macOS status |
|---------|-------|--------------|
| Microsoft.Data.Sqlite 10.0.3 | Core | OK -- bundles native `e_sqlite3` for osx-arm64/osx-x64 |
| QuestPDF 2026.2.1 | Core | OK -- SkiaSharp-based, ships macOS natives; `ChartPdfFormatter` uses the default embedded font (no Windows font names) and sets the Community license in code |
| Serilog 4.3.1 / Sinks.File 6.0.0 / Sinks.Console 6.1.1 | Core/GUI/CLI | OK -- pure managed |
| GeoTimeZone (floating `Version="*"`) | WPF project | OK -- pure managed with embedded data. The floating version is a reproducibility risk (Section 9) |
| Mapsui.Wpf 5.0.2 (+ transitive BruTile, NetTopologySuite) | WPF project | **Windows-only package**, but the Mapsui family has a first-party `Mapsui.Avalonia` package; BruTile and NTS are cross-platform |
| xUnit / FluentAssertions / coverlet | Tests | OK |
| WiX Toolset v4 | Installer | Windows-only; replaced by .app/DMG tooling on macOS |
| swedll64.dll (Swiss Ephemeris 2.10.03) | native | Must be compiled for macOS from the vendored sources (Section 3.2) |

No other packages exist. There is no ASCOM, no serial-port or device I/O, no COM
interop, no Windows services, no tray/notification integration anywhere in the
solution -- AstroCast is pure computation + files + two HTTP endpoints (Nominatim
geocoding, map tile servers).

---

## 3. Portability Assessment

### 3.1 Runs on macOS unchanged

- **`AstroCast.Core` (38,293 lines).** Verified: zero `DllImport` outside the
  SwissEph wrapper, zero `Microsoft.Win32`/registry usage, zero `System.Drawing`,
  zero hardcoded drive letters or backslash paths (all path handling goes through
  `Path.Combine`), no `Process.Start`, no `#if WINDOWS`/`RuntimeInformation`
  branches. `TimezoneProvider` is already cross-platform-aware: it uses IANA IDs
  natively where the OS provides them (`TimeZoneInfo.HasIanaId`) and converts
  Windows IDs only when present, so it works identically on macOS.
- **`AstroCast.Cli` (220 lines).** Console smoke test that exercises the full
  engine, SQLite save/load, and all three export formats -- the ideal Phase 1
  validation harness on the Mac.
- **~135 of 156 test files** exercise Core only. The golden-file comparison
  already normalizes line endings (`GoldenFormatterTestBase.cs:146` replaces
  CRLF/CR with LF), so the golden suites are macOS-safe once the test project
  split (Section 3.6) is done.

### 3.2 The native library: Swiss Ephemeris

This is the one true native dependency, and it is in good shape:

- The C sources are vendored at `lib/swisseph/src`, so no download is needed.
  The Linux README documents the exact file list; the macOS build is now
  **scripted** — run `build/macos/build-libswe.sh` rather than typing a command.
  It compiles the same nine translation units Phase 1 established:

  ```
  clang -dynamiclib -arch arm64 -O2 \
      -mmacosx-version-min=15.0 \
      -install_name @rpath/libswe.dylib \
      -o lib/swisseph/osx-arm64/libswe.dylib \
      swecl.c swedate.c swehel.c swehouse.c swejpl.c swemmoon.c \
      swemplan.c sweph.c swephlib.c -lm
  ```

  **`-mmacosx-version-min` is load-bearing — never drop it.** Without it clang
  stamps the binary with the *build machine's* OS version as its minimum, and
  dyld enforces that at load. Phase 1 compiled on macOS 26.5 with no target, so
  the dylib carried `minos 26.0`; because a bundle's floor is the **maximum**
  `LC_BUILD_VERSION` across every Mach-O it ships, that one file silently raised
  the whole application's requirement to macOS 26 while `Info.plist` and every
  other binary said otherwise. The symptom is not a clean refusal to launch: on
  macOS 15–25 the app would start, draw its window, and only then die when the
  first P/Invoke reached `EphemerisService`. Nothing in the build or the test
  suite catches this — the goldens pass, because the compiled code is fine. Only
  a load-command census catches it. Fixed in
  `phase5/slice5a2-libswe-floor-20260810`.

  **Why 15.0 and not lower.** The floor is a maximum, so going below the highest
  package-supplied binary buys nothing: `libQuestPdfSkia` and `libqpdf` arrive
  from the QuestPDF package already requiring 15.0 and are not ours to rebuild.
  Everything else in the bundle sits at 11.0 or 12.0. If QuestPDF's binaries ever
  drop below 15.0, lower the target in the script.

  Changing only that flag changes only the load command: of 564,704 bytes exactly
  48 differ — 1 byte of `minos`, 15 of the relinked `LC_UUID`, 32 inside the
  linker's ad-hoc signature, and **zero bytes of compiled code**. The exported
  symbol set (177, of which 106 `swe_*`) and the undefined set (61, all
  libSystem) are unchanged, and the CLI smoke test reproduces the Phase 1 capture
  bit for bit. **Note the undefined count is 61 and not zero** -- an earlier
  Phase 1 claim of "zero undefined symbols" was never true and could not have
  been; the invariant that matters is that every one of them is satisfied by
  libSystem. See the slice 5a/5a.2 landing record in Section 6.

  Build on the M5 Mac for arm64 (see Section 8.1 on universal binaries).

- **One small Core code change is required.** `SwissEphInterop.cs:19` declares
  `private const string DllName = "swedll64"`. On macOS the runtime would probe
  for `swedll64.dylib` / `libswedll64.dylib`; on Linux for `swedll64.so` /
  `libswedll64.so`. Note that the existing Linux README's `libswe.so` would
  **not** resolve under these rules -- evidence the non-Windows path has never
  actually been exercised end to end. The clean fix is a
  `NativeLibrary.SetDllImportResolver` registration (a module initializer or a
  static constructor on the interop class) that maps `swedll64` to
  `swedll64.dll` on Windows, `libswe.dylib` on macOS, and `libswe.so` on Linux.
  This is roughly 30 lines including XML docs and fixes both platforms at once.
- The `.csproj` copy items reference only `win-x64/swedll64.dll`; conditional
  `ItemGroup`s (on `$([MSBuild]::IsOSPlatform('OSX'))` or RID) must be added so
  each platform copies its own binary.
- Threading and lifetime are already handled correctly for any platform: all
  `swe_*` calls are serialized behind a `SemaphoreSlim` in `EphemerisService`,
  and `swe_close()` runs on dispose.
- The 4 MB `ephe/` data files and `sefstars.txt` are platform-neutral bytes;
  they ship inside the app bundle as read-only resources.

### 3.3 Blocked: the WPF layer (must be rewritten per-framework)

| Item | Size | Port disposition |
|------|------|------------------|
| `MainWindow.xaml` | 2,408 lines | Rewrite in Avalonia XAML. Mechanical inventory: 14 `KeyBinding`s (Alt+1..9 navigation), 36 `Visibility=` usages (become `IsVisible`), 32 styles, 108 trigger lines (become Avalonia selectors/classes). Controls used are all basic -- the only list control is one `ListView` (becomes `ListBox`); **no** DataGrid, FlowDocument, RichTextBox, WebBrowser, or WindowsFormsHost anywhere |
| `AcgMapWindow.xaml(.cs)` | 34 + 503 lines | Swap `Mapsui.Wpf.MapControl` for `Mapsui.Avalonia.MapControl`. The layer-building code (BruTile tile source, NTS geometries, Mapsui styles) is framework-neutral and transfers nearly verbatim |
| `App.xaml.cs` | 376 lines | Re-implement as the Avalonia bootstrapper. The service wiring (35 Core services) is plain C# and copies over; the WPF-specific parts are the three exception-handler hookups and two `MessageBox` calls |
| `MainWindow.xaml.cs` | 65 lines | Window plumbing; rewrite |
| Converters (2 files) | 44 lines | `Visibility` converters; mostly disappear (Avalonia binds `bool` to `IsVisible` directly) |
| `WpfDialogService` | 23 lines | Re-implement `IDialogService` with Avalonia dialogs |
| Fonts | -- | `Segoe UI Emoji` and `Consolas` are Windows fonts; use a cross-platform stack (`Menlo`/`monospace`; Avalonia resolves system emoji) |

### 3.4 Reusable with targeted refactoring: the view-model layer (~10,600 lines)

The H-1 decomposition already produced 11 feature child VMs
(`ChartEntry`, `LocationSearch`, `Synastry`, `Transits`, `Electional`, `Returns`,
`Cycles`, `Mundane`, `ReturnIngressTimeline`, `Relocation`, `Astrocartography`)
plus coordinator `MainViewModel`. Framework coupling was measured by grep and is
narrow and known:

1. **`SaveFileDialog` -- ~30 call sites, all in `MainViewModel` export commands.**
   Every site follows the identical pattern (construct dialog, set filter and
   default name, `ShowDialog`, write file). Fix: add a
   `IFileDialogService` abstraction next to `IDialogService` in Core, implement
   per framework (WPF `SaveFileDialog`; Avalonia `StorageProvider`). Mechanical,
   repetitive. **As built (Phase 3 slice 3c + Phase 4 slice 0): 26 sites, not
   ~30, and a two-method shape** --
   `ShowSaveAsync(title, fileTypes, defaultFileName, defaultExt)` plus
   `ShowOpenAsync(title, fileTypes, defaultExt)` for the one database-import
   site. `defaultExt` is required, not optional: the medical report export
   supplies a suggested name with no extension and relies on the dialog to
   append one. `fileTypes` is `IReadOnlyList<FileDialogFileType>`, not a Win32
   filter string -- see section 5.
2. **UI-thread marshalling -- 2 sites.** `MainViewModel.OnPropertyChanged`
   (line 4696) and `ChildViewModelBase.OnPropertyChanged` (line 103) marshal via
   `Application.Current?.Dispatcher`. Fix: an `IUiDispatcher` abstraction
   (3 members: `CheckAccess`, `Post`) injected once; the existing null-dispatcher
   fallback for unit tests carries over as-is.
3. **`RelayCommand<T>.CanExecuteChanged`** subscribes to WPF's
   `CommandManager.RequerySuggested` (`RelayCommand.cs:371`). The non-generic
   `RelayCommand` and `AsyncRelayCommand` are already framework-free. Fix: give
   the generic variant the same manual `RaiseCanExecuteChanged` mechanism -- the
   codebase already routes refreshes through
   `ChildViewModelBase.RequestCommandStateRefresh`, so the plumbing exists.
4. **Message boxes** already go through Core's `IDialogService` (verified: zero
   direct `MessageBox` calls in `MainViewModel`; the only ones are in
   `App.xaml.cs`, which is rewritten anyway).
5. Everything else in the VM layer is portable today: `GeoTimeZone` lookups,
   Nominatim `HttpClient` + rate limiter, `OperationCancellationRegistry`,
   `NavigationPanel` enum, cancellation flows.

### 3.5 Data and file-system locations (small but mandatory change)

`App.xaml.cs` resolves everything relative to the executable:
logs at `BaseDirectory/logs` (line 57), ephemeris at `BaseDirectory/ephe`
(line 77), database at `BaseDirectory/astrocast.db` (line 199); the CLI does the
same. On macOS an app bundle is signed and effectively read-only (writing inside
`AstroCast.app` breaks the code signature and fails under app translocation), so
writable data must move to standard locations:

- Database: `~/Library/Application Support/AstroCast/astrocast.db`
- Logs: `~/Library/Logs/AstroCast/`
- Ephemeris: read-only inside the bundle (`Contents/Resources/ephe`)

Plan: introduce an `IAppPaths` (or static `AppPaths`) provider used by both heads.
Whether Windows keeps writing next to the EXE or also moves to `%LOCALAPPDATA%`
is an open question (Section 7, Q6; see also the related Windows observation in
Section 9).

### 3.6 Tests

`AstroCast.Tests` targets `net10.0-windows` with `UseWPF` solely because it
references the WPF project; 21 of 156 files test VMs/commands/WPF services (one,
`DailyHoroscopeWpfTests`, is genuinely WPF-bound). Plan: split into

- `AstroCast.Core.Tests` (`net10.0`) -- the ~135 Core files incl. all golden
  suites; runs on macOS and Windows.
- presentation/VM tests follow the extracted presentation project (`net10.0`);
- a small `net10.0-windows` remainder for WPF-specific tests.

Both Core and the WPF project declare `InternalsVisibleTo("AstroCast.Tests")`;
the split must update those attribute values -- a legitimate, required touch of
otherwise out-of-scope files.

### 3.7 Explicitly checked and found clean

- No registry access in application code (the only `Microsoft.Win32` using is
  WPF's `SaveFileDialog` namespace; the only registry writes are WiX shortcut
  KeyPaths in the installer).
- No hardcoded drive letters or path separators in `src` (`Path.Combine`
  throughout, per project rules).
- No COM interop, no Windows services, tray icons, toasts, or shell extensions.
- No hardware/device dependencies (no ASCOM/INDI/Alpaca relevance -- AstroCast is
  astrological calculation software, not telescope-control software; the Swiss
  Ephemeris is a pure math library).
- SQLite database file format is identical across platforms -- the existing
  `astrocast.db` can simply be copied to the Mac.

### 3.8 Reuse summary

| Bucket | Lines | Share of app C# | Disposition |
|--------|-------|-----------------|-------------|
| Core + CLI | 38,513 | 77% | Unchanged (plus ~30-line DllImport resolver and csproj copy-item conditions) |
| View-model layer | ~10,600 | 21% | Moves to a shared project; ~35 known WPF touchpoints replaced by 2 small abstractions |
| WPF code-behind, converters, dialog impl | ~1,050 | 2% | Rewritten per framework |
| XAML | 2,450 (separate) | -- | Rewritten in Avalonia XAML (mechanical mapping, no exotic controls) |
| Tests | 49,990 | -- | ~87% of files run cross-platform after the project split |

---

## 4. UI Strategy: Options and Recommendation

| Option | Fit for this codebase | Verdict |
|--------|----------------------|---------|
| **Avalonia UI** | Closest model to WPF: XAML, bindings, `ICommand`, styles, converters, multi-window. `Mapsui.Avalonia` exists for the one third-party control in use. Runs on Windows/macOS/Linux, so it can eventually replace the WPF head entirely. Steve already ships an Avalonia app (MyBridge, with CommunityToolkit.Mvvm), so the toolchain and idioms are familiar. Desktop-first community and good macOS window/menu support | **Recommended** |
| .NET MAUI | macOS support rides on Mac Catalyst; desktop (multi-window, menus, keyboard-centric density) is second-class for an information-dense tool like this; XAML dialect is farther from WPF; would still need a Mapsui swap (`Mapsui.Maui` targets mobile-first scenarios) | Not recommended |
| Uno Platform | WinUI XAML dialect (a rewrite either way), heavier stack, smaller desktop-app community; no advantage over Avalonia for a two-platform desktop tool | Not recommended |
| Native SwiftUI/AppKit | Forfeits all 10,600 VM lines and the C#-only maintenance story; two UI codebases in two languages for a single-developer personal project; nothing in this app (no deep OS integration) demands native UI | Not warranted |

Decision drivers specific to AstroCast: (a) the UI is forms + monospaced text
output + one map -- exactly the kind of UI that converts mechanically to
Avalonia; (b) Mapsui parity removes the only third-party-control risk; (c) the
H-1 architecture means the expensive logic already lives outside the views.

---

## 5. Recommended Target Architecture

```
AstroCast.Core            net10.0            unchanged (+ DllImport resolver)
AstroCast.Presentation    net10.0   NEW      ChildViewModelBase, 11 child VMs,
                                             MainViewModel, RelayCommand family,
                                             OperationCancellationRegistry,
                                             NavigationPanel, Nominatim client
AstroCast                 net10.0-windows    WPF views + WpfDialogService,
                                             WpfFileDialogService, WpfUiDispatcher
                                             (kept fully working during the port)
AstroCast.Avalonia        net10.0   NEW      Avalonia views + Avalonia impls of the
                                             same three service interfaces
AstroCast.Cli             net10.0            unchanged
AstroCast.Core.Tests      net10.0   SPLIT    cross-platform (goldens included)
AstroCast.Presentation.Tests net10.0 SPLIT   the current VM/command tests
AstroCast.Tests.Wpf       net10.0-windows    the few genuinely WPF tests
```

New abstractions (all in Core/Interfaces alongside `IDialogService`, all with
full XML docs per project standards):

- `IFileDialogService` -- async save and open prompts. As built:
  `Task<string?> ShowSaveAsync(title, fileTypes, defaultFileName, defaultExt)`
  and `Task<string?> ShowOpenAsync(title, fileTypes, defaultExt)`, both
  returning `null` on cancel. `fileTypes` is
  `IReadOnlyList<FileDialogFileType>` -- a plain-BCL Core type carrying display
  name, glob patterns, MIME types and Apple uniform type identifiers, the last
  because the macOS native panels are driven by UTIs rather than globs. The
  Avalonia head maps it to `FilePickerFileType`; the WPF head rebuilds the
  Win32 filter string from it. A `NullFileDialogService` supplies the
  no-picker-available case so the dependency can be non-nullable.
- `IUiDispatcher` -- `bool CheckAccess()`, `void Post(Action)`.
- `IAppPaths` (or extend `EphemerisOptions`-style configuration) -- database,
  log, and ephemeris directories per platform.

Native library layout per RID: `win-x64/swedll64.dll`, `osx-arm64/libswe.dylib`
(new), `linux-x64/libswe.so` (fixing the existing naming mismatch), selected by
the resolver; csproj copies conditionally.

Build on macOS: WPF projects cannot build there, so add a small
`AstroCast.Mac.slnx` (or build per-project) listing Core, Presentation, Cli,
Avalonia, and the two cross-platform test projects. `.slnx` is supported by the
`dotnet` CLI on all platforms.

The MCP server (Stage 7, still pending) is a stdio console app by design and is
unaffected -- it will be cross-platform by construction on this architecture.

---

## 6. Phased Work Plan

Effort is expressed in working sessions (one focused prompt-sized change each,
per the established workflow) plus rough calendar ranges. Estimates are honest
guesses, not commitments.

**Phase 0 -- Decisions and Mac environment (0.5-1 day)** -- **DONE 2026-07-05**
Resolve Section 7 questions Q1-Q3. On the MacBook: install .NET 10 SDK
(arm64), an IDE (see Q3), Xcode Command Line Tools (for `clang` and
`codesign`). Establish the repo-transfer/sync method (Q1) and line-ending
policy (Q2).
*Exit: `dotnet --info` healthy on macOS; repo present on the Mac.*

> **Phase 0 result (2026-07-05).** All exit criteria met; re-verifiable any
> time via `~/astrocast-mac-setup/verify-toolchain.sh` (11/11 checks, exit 0):
> - Xcode tools: full Xcode installed -- clang 21.0.0, git 2.50.1. Functional
>   proofs passed: arm64 dylib built with `clang -dynamiclib -O2` and ad-hoc
>   signed/verified (`codesign -s -`, `Signature=adhoc`, strict verify) --
>   exactly the Section 3.2 / 8.3 path Phase 1 depends on.
> - .NET SDK 10.0.301 osx-arm64, installed **user-local at `~/.dotnet`**
>   (Rider-style layout, not the Homebrew cask); `~/.zshrc` now exports
>   `DOTNET_ROOT` and prepends it to PATH. `dotnet new console` build + run
>   verified. Homebrew 6.0.6 and Node 22 also present.
> - IDEs: Rider 2026.1.4 (Toolbox-managed, non-commercial license) and
>   VS Code 1.127.0 with C# Dev Kit -- both installed.
> - Repo on the Mac: working clone at `~/Developer/AstroCast` (Q1);
>   `.gitattributes` written, zero-churn proven, not yet committed (Q2).
> - Deliberately still pending: `pwsh` (only needed by Phase 5's bundle
>   script -- `brew install --cask powershell`), Avalonia templates (Phase 4;
>   the Avalonia 11-vs-12 pin from Section 4 remains open).

**Phase 1 -- Native library + headless bring-up (1-2 days, ~3-5 sessions)**
Compile `libswe.dylib` (osx-arm64) from `lib/swisseph/src`; add the
`DllImportResolver` and conditional csproj copy items; run `AstroCast.Cli` on
the Mac. Validate: SwissEph version string 2.10.03, J2000/NYC positions match
Windows output digit-for-digit, SQLite save/list works, JSON/Markdown/PDF files
render (open the PDF -- font check).
*Exit: CLI smoke test green on macOS with identical numbers.*

**Phase 2 -- Test-suite split (1-2 days, ~2-4 sessions)**
Create `AstroCast.Core.Tests` (`net10.0`); move the ~135 Core-only files and
golden assets; update `InternalsVisibleTo`; run on both OSes.
*Exit: full Core suite passing on macOS and Windows.*

> **Phase 2 result (2026-07-25, Windows side; commit 2339c8e).** Executed as
> Option B-inverted per the Step A investigation: instead of moving the ~135
> neutral files, `AstroCast.Tests` was retargeted to `net10.0` in place and
> the 22 Windows-coupled files (17 VM test files + `TestDialogService`,
> 2 command tests, `OperationCancellationRegistryTests`,
> `DailyHoroscopeWpfTests`) moved via `git mv` into a new
> `AstroCast.Tests.Windows` (`net10.0-windows`, `UseWPF`). InternalsVisibleTo:
> the app's grant repoints to `AstroCast.Tests.Windows`; Core's is unchanged
> (the moved files use no Core internals). New `AstroCast.Mac.slnx` = Core +
> Cli + `AstroCast.Tests`. Proof: before/after `--list-tests` name lists
> byte-identical (2144 names); discovery 1886 + 258 = 2144; full Windows run
> 2144/2144, 0 skipped. Exit criterion pending the first Mac run:
> `dotnet test AstroCast.Mac.slnx` expecting exactly 1886/1886.

> **Phase 2 addendum (2026-07-25; commit bf9fd50).** First Mac run of the
> split suite: 1875/1886, zero calculation bugs. The 11 failures were two
> platform-divergence artifacts, fixed at the root rather than by
> regenerating goldens to match either platform: (1) nine embed helpers
> split sub-formatter output on '\n' while leaf formatters emit
> Environment.NewLine; the surviving '\r' produced a phantom blank line on
> Windows, baked into 10 markdown goldens -- replaced by a shared
> CR-stripping `MarkdownLineSplitter.SplitLines`; (2)
> `antisciaPoints[].longitude` was the JSON document's lone full-precision
> numeric, exposing the ~1e-12 degree arm64-vs-x64 libm delta -- now on the
> shared 4-decimal policy. `AssertGolden` hardened to report
> first-divergence line/column (FluentAssertions' diff renderer threw
> FormatException on JSON braces and had masked the antiscia divergence).
> Goldens regenerated on Windows via the sanctioned env var AFTER the code
> fix; 13 files changed (11 intended, plus 2 re-normalising stale
> "(as of ...)" literals -- pre-existing F-GR residue, kept so regeneration
> is idempotent). Windows: 2144/2144. Mac rerun pending: expected
> 1886/1886, byte-for-byte through the full documents.

**Phase 3 -- Presentation extraction, on Windows (1-2 weeks, ~8-12 sessions)**
Introduce `IFileDialogService`, `IUiDispatcher`, `IAppPaths` in Core. Create
`AstroCast.Presentation`; move `ChildViewModelBase` + child VMs + commands +
registry in H-1-style slices (zero behavior change per slice; existing VM tests
keep passing after each). Replace the ~30 `SaveFileDialog` blocks with the
abstraction; fix `RelayCommand<T>`; WPF app remains the daily driver throughout.
*Exit: WPF app byte-for-byte identical in behavior; presentation project builds
as `net10.0`; VM tests run on macOS.*

> **Phase 3 result (2026-07-26; branch `phase3/presentation-extraction-20260726`,
> code commit e649158).** Executed as "sequenced decouple in place, then one
> atomic move" per the Step A investigation (D:\Code\phase3-investigation.md),
> which found the plan's H-1-style move slices impossible
> (`ChildViewModelBase.Parent` is typed `MainViewModel`; the 20 files are one
> indivisible unit). Slices, each with the full Windows suite green:
> 3a `RelayCommand<T>` off `CommandManager` (8b360ff); 3b `IUiDispatcher`
> seam (0aa0707); 3c `IFileDialogService`, 26 dialog sites -- not ~30, and a
> 2-method + defaultExt shape, not the planned single method (a6ce5ac);
> 3d the collection work the plan had not budgeted: guard tests first,
> mutation-proven (f130491), `EnableCollectionSynchronization` routed through
> `IUiDispatcher` (43f9e71), `ICollectionView` timezone filters replaced by a
> stable `ObservableRangeCollection` raising one Reset (f1a271d + selection
> fix 76a71e1 + pre-existing editable-ComboBox click dead-end fixed in
> d2cf25a); 3e `AstroCast.Presentation` (net10.0) created, 20 files moved by
> `git mv` with ZERO source-logic edits, one XAML line changed, app version
> verified 1.0.236 on the built binary (9bbb4c3); 3f all 25 test files
> repatriated and `AstroCast.Tests.Windows` DELETED -- zero tests are
> Windows-only (e649158). `IAppPaths` deferred to Phase 5 (no path logic in
> the moving set). Proof: Windows 2175/2175 in a single net10.0 test project,
> 0 skipped; `--list-tests` name list byte-identical to the pre-move baseline
> (2175 names; suite grew 2144 -> 2175 during 3d's added coverage).
> `AstroCast.Mac.slnx` = Core + Cli + Presentation + Tests. Exit criterion
> pending the first Mac run: expected 2175/2175.*

**Phase 4 -- Avalonia UI -- COMPLETE 2026-08-10 (2-4 weeks, ~10-20 sessions)**
Scaffold `AstroCast.Avalonia`; port the `App.xaml.cs` wiring (incl. the three
exception safety nets, using Avalonia equivalents); implement the three platform
services; convert `MainWindow` panel by panel in this order: Chart Entry ->
Export -> Synastry -> Transits -> Returns -> Electional -> Medical -> Mundane/
Cycles/Timeline -> Relocation; then `AcgMapWindow` via `Mapsui.Avalonia`.
Keyboard navigation: Cmd+1..9 on macOS (Q5). Font stack swap. Verify every
panel against the WPF app side by side.
*Exit milestone: first fully functional macOS GUI build.* **Met 2026-08-10 at
`v1.2.7`** -- see the Phase 4 completion note after the slice 9 entry below.

> **Phase 4 slice 0 result (2026-07-27; branch
> `phase4/slice0-async-dialog-interfaces-20260727`, code commit 8c0a94b).**
> Async-ified both dialog seams ahead of any UI work, deliberately isolated so a
> verifiable refactor is not entangled with unverifiable new code. Run on
> Windows because changing the interfaces breaks `WpfFileDialogService` and
> `WpfDialogService`, which only compile there -- `AstroCast.Mac.slnx` excludes
> `src/AstroCast`, so the Mac would have gone green with the WPF head broken.
> Three changes: (1) `ShowSave`/`ShowOpen`/`Confirm` became
> `ShowSaveAsync`/`ShowOpenAsync`/`ConfirmAsync`, the WPF side wrapping its
> synchronous modal calls in `Task.FromResult` (no added blocking -- `ShowDialog`
> and `MessageBox.Show` are already the modal UI-thread call and have no async
> overload); (2) the Win32 pipe filter string became
> `IReadOnlyList<FileDialogFileType>`, with four predefined sets covering all 26
> sites; (3) `NullFileDialogService` replaced the `?.` pattern so the field is
> non-nullable. No `NullDialogService`: `_dialogService` was already required,
> non-nullable and null-guarded, and none of its 3 sites used `?.` -- the type
> would have been unused. Deferred to Phase 4 proper, where the Avalonia head
> needs a macOS production stand-in.
>
> Two corrections to the plan's assumptions, both recorded above: the shape is
> two methods with a required `defaultExt`, not one method with three
> parameters; and `await _service?.M()` on a `Task`-returning member *does*
> compile -- it throws `NullReferenceException` at runtime instead, which is why
> the null object is mandatory rather than stylistic.
>
> The medical report export's `"Markdown files (*.md)|*.md|All files (*.*)|*.*"`
> differs in casing and content from the `"Markdown Files ... Text Files ..."`
> used by the other 21 Markdown exports. That string is user-visible, so it is
> preserved verbatim as `FileDialogFileTypes.MarkdownLowercase` rather than
> collapsed.
>
> Proof: build 0 errors with the WPF head building; warning codes unchanged
> (`NU1903`, `xUnit1031`), none new; Windows 2175/2175, 0 skipped;
> `--list-tests` name list byte-identical to the pre-change baseline
> (`D:\Code\phase4-tests-baseline-2175.txt`), so no test was added, removed or
> renamed; and a filter-equivalence check over all 26 sites comparing the
> pre-change literals from `git HEAD` against the strings the real
> `WpfFileDialogService.BuildWin32Filter` now produces, reached by reflection
> over the built assembly -- 104 fields (filter, title, filename, defaultExt),
> **0 mismatches**. Mac re-verification pending: expected 2175/2175. Report:
> `D:\Code\phase4-slice0-report.md`.

> **Phase 4 slice 1 result (2026-07-27; branch
> `phase4/slice1-avalonia-shell-20260727`, code commit d2bca87).** The first new
> code of the port. `src/AstroCast.Avalonia` (net10.0, Avalonia 12.1.0) launches
> on macOS, wires the same service graph as the WPF head, and makes all nine
> panels navigable as empty placeholders. `src/AstroCast` and
> `AstroCast.Presentation` are untouched; the only change outside the two new
> projects is one new Core file, `NullDialogService`.
>
> `Avalonia.Skia` 12.1.0 is pinned explicitly although nothing renders through it
> yet, because `Mapsui.Avalonia12` declares a minimum of Avalonia 12.0.2 and
> without the pin the map slice would silently resolve a split renderer version.
> `AvaloniaUseCompiledBindingsByDefault` is set at project level so every future
> view inherits it; proved live by a deliberate typo yielding
> `AVLN2000: Unable to resolve property or method of name 'IsChartPanelVisibel'
> on type 'AstroCast.ViewModels.MainViewModel'` and `Build FAILED`.
>
> **Three corrections to earlier investigation findings.** (1) WPF's
> `DispatcherUnhandledException` *does* have an Avalonia equivalent --
> `Dispatcher.UIThread.UnhandledException`, same shape including a settable
> `Handled` -- so all three of the WPF head's exception safety nets port intact.
> (2) The WPF `Gesture="Alt+1"` syntax does **not** port: Avalonia's parser falls
> back to reading a bare digit as a *numeric* `Key` enum value, so `"Alt+1"`
> silently yields `Key.Cancel` and `"Alt+9"` yields `Key.HangulMode` -- valid
> gestures on the wrong keys, no parse error, no warning, and all nine shortcuts
> dead. Corrected to `Alt+D1..Alt+D9`. It was caught by one of the three seeded
> UI tests, not by the build or by launching. (3) `EqualityToVisibilityConverter`
> is dead in the WPF head (declared, never referenced) and was not ported.
>
> Paths diverge deliberately. `AppPaths` resolves database and logs under
> `Environment.SpecialFolder.ApplicationData` (`~/Library/Application Support` on
> macOS, `%APPDATA%` on Windows) because inside a signed `.app` bundle the
> <!-- Superseded: slice 1.5 replaced the single ApplicationData call with a
>      per-platform branch (%LOCALAPPDATA% on Windows, XDG on Linux), and slice 2
>      added the publisher folder, so Windows is now
>      %LOCALAPPDATA%\Thornhill Computer Software\AstroCast. See AppPaths.cs. -->

> executable's directory is read-only and replaced on update. `ephe/` stays
> beside the executable -- a read-only artefact `AstroCast.Core.csproj` already
> copies to any referencing project's output, so the head inherits the native
> library and data files with no csproj work. The WPF head keeps its
> `BaseDirectory` paths so existing data is not stranded, which means on Windows
> the two heads read **different databases**: a one-time migration is now a
> Phase 5 requirement, not an optional nicety.
>
> `AvaloniaUiDispatcher` is not a pass-through. `CheckAccess()` returns true only
> for the first thread that ever touched `Dispatcher.UIThread`, and `Post()` has
> no inline fast path -- with no application the action is queued and never runs,
> silently. Both violate the interface's headless contract, so it guards on
> `Application.Current is null`. `Post` passes `DispatcherPriority.Normal`
> explicitly (Avalonia's default `Default` is *below* `Render`/`DataBind`, where
> WPF's `BeginInvoke` defaults to `Normal`).
> `EnableCollectionSynchronization` is a documented no-op whose remarks state
> that it *removes* a guarantee rather than preserving one.
>
> The nine nav-highlight `DataTrigger`s collapsed to one selector, as predicted:
> parsing all nine and comparing setter tuples gave exactly one distinct
> signature, so each button binds `Classes.active` to its own
> `Is<Panel>PanelVisible` and a single `Button.nav.active` selector carries the
> setters. The no-content-host panel mechanism is reproduced exactly -- nine
> siblings each toggled by its own binding -- with `Visibility` +
> `BoolToVisibilityConverter` collapsing to a direct `IsVisible` bool.
> Avalonia has no `StatusBar`/`StatusBarItem`, so that became a docked `Border`
> with a three-column `Grid`.
>
> New `tests/AstroCast.UiTests` (net10.0, `OutputType=Exe`), separate because
> `Avalonia.Headless.XUnit` 12.x requires xunit v3 and `AstroCast.Tests` is on
> v2. Seeded with three tests only. They assign no `MainViewModel`: its 23
> service doubles live in `AstroCast.Tests`, unreachable across the v2/v3
> boundary. Extracting them into a referenceable library is a bounded follow-up.
>
> **This makes seven versioned csprojs**, not five: `AstroCast.Avalonia` is the
> sixth and `AstroCast.UiTests` the seventh. The house tagging process and
> `CLAUDE.md` both still say five and must be corrected at the Phase 4 landing,
> alongside the version bump and tag.
>
> Proof (macOS): build 0 errors; distinct warning codes unchanged (`NU1903`,
> `xUnit1031`), none new -- the count rises 10 to 14 only because two new
> projects each add `NU1903` restore warnings for the same SQLite advisory.
> Tests 2175/2175 existing plus 3/3 new, 0 skipped. The app launches, a real
> window appears, and startup completes end to end (ephemeris initialised,
> database created and migrated under Application Support, charts loaded).
> Driven headlessly against a real view model: all nine nav buttons and all nine
> keyboard shortcuts switch to the correct panel, with exactly one panel visible
> and exactly one button highlighted every time. Windows verification pending --
> the Avalonia head must build and launch there, and the WPF head must still
> build and pass its smoke test. Report:
> `~/astrocast-mac-setup/phase4-slice1-report.md`.

> **Phase 4 slice 2 follow-up work (agreed 2026-08-04; recorded at the
> 1.0.241 version bump, commit 31104a4).** The macOS verification of slice 2
> produced a complete, agreed work list for the next slice. It is recorded
> here so that it is not re-derived. Nothing in it is a defect in what landed;
> items 5 and 8 are corrections to slice 2's own documentation.
>
> 1. **Drop `.WithInterFont()`** (`src/AstroCast.Avalonia/Program.cs:40`).
>    Decision taken: use the system fonts -- Segoe UI on Windows, SF on
>    macOS -- rather than a bundled font. This is cross-cutting, so it wants
>    both-platform re-verification and a fresh Chart-panel side-by-side
>    rather than a spot check.
>
> 2. **Right margins on the docked form buttons.** `Now`
>    (`Margin="5,2,0,2"`, zero right margin) and `Lookup` are both shaved
>    flat at the 360 px column edge on macOS. This is geometry, not glyph
>    width, and will **not** be fixed by dropping Inter.
>
> 3. **Timezone chevron.** Add `AutoCompleteBox.InnerRightContent`. The field
>    currently looks like a plain text box while every sibling combo shows a
>    chevron.
>
> 4. **Re-tune the 92 px label column after the font change.** `UTC Offset`
>    already clears it comfortably under Inter on macOS, so the compensation
>    is over-wide today and becomes more so once the font changes.
>
> 5. **Correct the `ConfirmationWindow` remarks.** The comment claims Yes is
>    `IsDefault` and takes Enter. The focused `No` consumes Enter first, so
>    `IsDefault` is inert. The safety property is therefore *stronger* than
>    documented while the stated mechanism is false -- which is why the
>    comment is worth fixing rather than deleting.
>
> 6. **Focus ring on the confirmation dialog's buttons.** `No` is focused but
>    nothing is painted, so on a destructive prompt the user cannot see which
>    button Enter or Space will hit.
>
> 7. **Expect the Actions `WrapPanel` to stop wrapping under SF** (~383 pt of
>    buttons in a 360 pt column today). Re-check after the font change rather
>    than pre-adjusting.
>
> 8. **The empty-to-null UTI guard is unreachable from any current call
>    site**: all six predefined `FileDialogFileType` instances declare
>    non-empty MIME and UTI lists. Keep the guard -- it is correct defensive
>    code -- but correct the slice 2 note that describes it as fixing an
>    observable defect.

> **Phase 4 slice 3 result (2026-08-06; branch
> `phase4/slice3-export-panel-20260806`, code commit c286b92).** Translates the
> Export panel (`MainWindow.xaml` 2103-2234) and the saved-charts pane
> (2242-2403). The pane is chrome rather than one of the nine panels -- it is
> always visible -- so it lands with the panel that sits beside it. Seven panels
> remain as placeholders.
>
> Fidelity was checked mechanically rather than by eye: every `Command` binding
> and every bound property path in both regions was extracted from the WPF source
> and from the translation and compared. **18 export commands and 4 saved-charts
> commands, none missing and none extra; 37 bound paths on each side**, the only
> differences being the documented idiom changes.
>
> Idioms with no Avalonia equivalent, all three in the saved-charts pane: the
> sort-direction `DataTrigger` became a negated `Classes.descending` binding plus
> two selectors (Avalonia selectors cannot test a view-model property); the row
> commands' `RelativeSource AncestorType=ListView` became `$parent[ListBox]` with
> an explicit cast (compiled bindings cannot infer the ancestor's data-context
> type); and the two `Keywords` null/empty `DataTrigger`s collapsed to the
> built-in `StringConverters.IsNotNullOrEmpty`. `ListBox`, not `ListView`.
> Selection still does nothing, deliberately -- the WPF list binds no
> `SelectedItem` and has no handler, so a chart loads only via its Load button.
>
> The Export panel's seven group headings are **not** the `SectionHeader`
> resource; WPF styles them 11pt semibold grey inline against SectionHeader's
> 13pt bold blue, so reusing that class would have silently restyled them.
> `CommandParameter="pdfSave"` on Cancel PDF is load-bearing and now pinned: it
> is a plain string, invisible to compiled bindings, and a missing or misspelled
> value makes the button silently test `HasActiveOperation` instead -- enabling
> during an unrelated export.
>
> **Slice 3 broke a slice 2 test's premise, which is worth recording.**
> `ChartPanelFormGrids_ShareOneLabelColumnWidth` searched the whole window for
> grids containing a `formLabel`, which selected the five Chart-panel form grids
> only while that panel was the sole user of the class. The saved-charts filter
> grid uses `formLabel` too -- faithfully -- and has an `Auto` first column by
> design, so the window-wide count became six and the assertion failed on a grid
> the test was never about. Rescoped to `ChartPanelRoot`. Two named anchors,
> `ExportPanelRoot` and `SavedChartsPane`, were added so the new tests scope by
> name rather than repeat the mistake.
>
> Six new tests, and the two subtle ones were **mutation-tested rather than
> assumed**: removing the negation from `Classes.descending` fails the caption
> test, and repointing Load at `DeleteChartCommand` fails the row-command test.
>
> **A slice 2 comment was wrong, and correcting it retires planned work.** The
> search box's comment claimed Avalonia's `TextBox.Text` commits on `LostFocus`
> by default -- so that dropping `UpdateSourceTrigger=PropertyChanged` would
> leave `SearchText` a term behind -- and cited a macOS measurement. It does not
> reproduce. Deleting the attribute from the real binding changed nothing, and a
> side-by-side headless probe of two TextBoxes, one with the attribute and one
> without, showed **both** view-model properties updating while their box still
> held focus. Avalonia commits per keystroke by default; only WPF, whose default
> is `LostFocus`, ever needed the attribute. The comment further noted that the
> WPF markup carries it on **50** two-way bindings against this head's one, and
> called that "a separate slice" -- **it is not a defect and needs no slice**, so
> 49 sites of remedial work come off the plan. The test written to guard the
> attribute was vacuous (it passed with the attribute deleted) and was rewritten
> to state what it actually covers: the behaviour, not the mechanism.
>
> Proof (macOS): build 0 errors; warning codes unchanged (`NU1903`,
> `xUnit1031`), none new. Tests **2218/2218** existing and **31/31** UI, 0
> skipped. Both regions rendered through Skia and inspected -- seven grey group
> headings, eighteen buttons, the two Database Backup buttons enabled while the
> chart-dependent exports are correctly disabled with no chart calculated, three
> rows with Load and red Delete, and the Keywords line italic on the one seeded
> chart carrying keywords. The `CalendarDatePicker` button glyph renders as a
> stray digit headlessly; the already-shipped Chart panel renders identically, so
> it is a headless font artefact, not a slice 3 regression. Windows verification
> pending. Report: `~/astrocast-mac-setup/phase4-slice3-report.md`.

> **Windows verification of slice 3 (2026-08-07, Maple; master @ 4976e58).**
> Build 0 errors across **all eight projects including the WPF head** -- the
> check `AstroCast.Mac.slnx` structurally cannot make, since it excludes
> `net10.0-windows`. Warning codes unchanged (`NU1903`, `xUnit1031`), none new.
> Tests **2218/2218** and **31/31**, 0 skipped, with the discovered count equal
> to the executed count in both suites (theory cases 316, nested-class names 3),
> so no test host died mid-run. The WPF head still runs at v1.1.0 with its 90
> charts, switches panels, loads a chart, and enables the Export buttons exactly
> as before; `src/AstroCast/` is empty in the diff for the fourth slice running,
> so "the WPF head is unaffected" stays structural rather than inferred. The
> Export panel is a faithful translation: group heading colour `#666666` and
> panel title `#1B4F72` identical to WPF byte for byte, all eighteen captions
> present, nothing clipped, six of seven groups wrapping identically, and live
> enablement matching WPF exactly with a chart loaded. `Alt+7` is confirmed
> working by a real keypress -- an earlier `SendKeys` failure was synthetic-input
> delivery, not a keybinding defect, and should not be re-investigated.
>
> **The 6.5% rule: Avalonia measures text wider than WPF, and it is
> predictable.** Measured on one machine, same font family, same column width,
> from rendered pixels of both running heads: every content-sized button caption
> is **6.2-7.0% wider in Avalonia than in WPF** (mean ~6.6%). The delta scales
> with width, so this is text measurement -- Skia/HarfBuzz against DirectWrite --
> and not padding. The control that proves it: the three buttons pinned to a
> 100 px `MinWidth` floor measure **identically** in both heads, while every
> content-sized caption diverges.
> **(RETIRED -- see "Measurement reconciliation (2026-08-08)" below. The
> measurement stands; the attribution does not. It is a font-size difference, WPF
> 13.0 against Avalonia 14.0, and the two text engines agree to 0.14%. "Scales
> with width" rules out padding but does not separate text measurement from text
> size. The 6.5% figure is also machine-specific -- it is 14/12 = 16.7% at stock
> Windows text scaling -- so do not carry it into a screening threshold.)**
>
> **Screening rule for the seven remaining panels.** Before translating a panel,
> measure each WPF `WrapPanel` row. **Any row using more than ~94% of its
> available width will wrap differently in Avalonia.** Returns row 1 measured
> 337 px against a 337-346 px column (97-100%) and broke; row 3 at 330 px
> likewise. Every group with more slack translated identically. This turns wrap
> breakage from a surprise into something checkable before the work starts.
> **Take the numerator and denominator as defined under "Measurement
> reconciliation (2026-08-08)" below -- layout requirement over the stretched
> sibling's width, at a stated window size. Measuring ink against the column
> width understates utilisation by about one item margin, which is what let the
> Days row pass this screen.**
>
> Observed consequence: the Export panel's Returns group wraps to **5 rows in
> Avalonia against 3 in WPF**. **Left as-is for now.** If it proves a one-off, a
> local fix is right; if several panels hit the same wall, a global adjustment --
> a slightly wider content column, or marginally tighter button padding -- beats
> scattered patches. One instance is not enough to choose.
>
> **The `CalendarDatePicker` glyph is a Windows-specific defect, and the
> 2026-08-06 reading of it above was wrong.** That entry called it a headless
> font artefact. It is not: in the **running** Avalonia head on Windows the
> button renders a stray digit -- a dot above a "7" -- with no calendar shape at
> all. macOS renders it correctly in the running app, and the Mac saw the
> artefact only under headless Skia, where font resolution is
> known-unrepresentative. What that entry had right is that **slice 3 did not
> introduce it**: it affects the already-shipped Chart panel's Date field as well
> as slice 3's two new filter fields, so slice 3 only made it visible in two more
> places.
>
> Mechanism, and what has been ruled out. WPF is immune because its calendar icon
> is theme vector art; Avalonia's is a font glyph, which is why it can fall back
> at all. `Segoe Fluent Icons` (`C:\Windows\Fonts\SegoeIcons.ttf`) **is**
> installed, and the app declares **no** global `FontFamily` override anywhere in
> `src/AstroCast.Avalonia` -- so it is neither a missing font nor an app-level
> override. Hypothesis, **unconfirmed**: the Fluent theme's symbol font resource
> is not resolving to the icon font for this control, so the private-use
> codepoint falls through to an arbitrary fallback glyph. Confirm before choosing
> a fix -- the fix differs depending on whether the fault is the resource or its
> resolution. **Its own task, after this landing.**
>
> **Observability gap.** The Avalonia startup log records the ephemeris path but
> never the resolved database path; the Windows database location had to be
> recovered from the filesystem and the `IAppPaths` convention rather than read
> from the log. One line, worth adding when convenient.

> **Phase 4 slice 4 (2026-08-07; branch
> `phase4/slice4-electional-panel-20260807`).** Translates the Electional panel
> (`MainWindow.xaml` 1802-2061): 4 combo boxes, 9 text boxes, 8 check boxes, 5
> buttons, 2 date pickers, 2 WrapPanels, 17 form rows. Six panels remain as
> placeholders.
>
> **Exit criterion met.** The same search run through both heads returned the
> same five candidates in the same order with the same scores, and the two
> exported Markdown files are **92,302 bytes each and differ on exactly one
> line -- the `Generated:` timestamp**. Inputs: natal chart Andrew Windsor,
> BusinessLaunch, 2026-08-10 to 2026-08-14, Richmond VA (37.5407, -77.4360),
> America/New_York, 5 candidates, no time constraint. Candidates: 13 Aug 3:30 PM
> (73), 13 Aug 10:30 PM (73), 13 Aug 6:30 AM (72), 14 Aug 12:00 PM (72), 14 Aug
> 5:00 AM (68). All inside the requested range; the 5:00 AM and 6:30 AM results
> are the ones that need local-midnight handling to be right.
>
> **THE GLOBAL LABEL SURVEY -- the finding that outlives this slice.** All 79
> `FormLabel` TextBlocks across the nine WPF panels were measured in one pass
> before translating. 76 sit in `Grid.Column="0"` and constrain the label column;
> 3 do not and were excluded (Medical's "Health Context (optional)", 25
> characters, is a standalone caption in a StackPanel -- the naive "longest label"
> answer, and wrong). **Every one of the 24 panel form grids uses first column =
> 80.** Only the saved-charts filter differs (`Auto`).
>
> Ink measured through WPF's own text stack (`FormattedText`, Segoe UI SemiBold
> 12 DIP), because two labels cannot be measured from rendered pixels -- see
> below. Widest first: **Progressions 69.8**, **Relationship 67.5**, Mercury Rx
> 63.1, Natal Chart 63.0, Candidates 61.5, UTC Offset 59.3, Longitude 55.9.
>
> **WPF's own 80 px column already clips "Progressions".** Its text allowance is
> 80 - 8 = 72 px and the label needs ~73; at 10x zoom the trailing "s" is sliced
> vertically. "Relationship" (67.5) fits with about a pixel to spare. Both
> measured 72 px on screen and both stopped at exactly x=155 -- two different
> strings ending on the same pixel is the signature of a clip, and it is why the
> rendered-pixel technique had to be abandoned for these two. This is a
> pre-existing WPF defect in a panel not yet translated; it is not this slice's
> to fix.
>
> **The eventual shared column is 100, and the constraint lives in Synastry and
> Transits -- panels not yet translated.** Requirement = 1 px bearing + ink + 8 px
> `formLabel` margin. Measured directly on macOS: Synastry's "Relationship" needs
> **91.5** and Transits' "Progressions" **96.5**. 92 clears "Relationship" by
> about half a pixel and **overflows "Progressions" by ~4.5** -- so **100**,
> which leaves "Progressions" ~3.5 px, comparable to the 3.0 px `UTC Offset` has
> at 84 today. When Transits or Synastry lands, move the value across **every**
> panel in one edit. Two Avalonia panels starting their fields at different x is
> the visible defect slice 2b existed to fix.
>
> **This entry said 92 until the macOS verification measured it.** 92 came from
> projecting Windows ink through a scalar; both governing labels were then
> measured directly and neither figure survived. The corrected numbers above are
> measurements, not projections. Re-confirm both from rendered pixels once their
> panels exist -- they are the only two labels in the survey that have never been
> seen on screen in the Avalonia head.
>
> **RATIO PROJECTION IS UNUSABLE. DO NOT REPEAT IT.** The 1.0746 figure was
> derived from **rendered** Windows pixels (`UTC Offset`, WPF-Windows 67 ->
> Avalonia-macOS 72) while the survey's table was measured through WPF's
> `FormattedText` (same string: **59.33**). Those are two bases about **13%**
> apart, and multiplying one by a ratio built from the other is what produced 92.
> Worse, the spread within a single base is too wide to size a column at all:
> against the `FormattedText` figures the measured macOS/Windows ratio runs
> **1.1034 - 1.2136** across six strings, a 9.3% spread; against the rendered
> figures it runs **1.0492 - 1.0746** across three, and 1.0746 is the maximum of
> those, not the typical value. At ~70 px of label that is ±3 px of uncertainty --
> the entire margin under discussion.
>
> **The mechanism is glyph mix, not string length.** "Progressions" is dense in
> round lowercase (`o g r e s s i o n s`) which Helvetica Bold sets proportionally
> wider than Segoe UI SemiBold does; no single scalar captures that, which is why
> even the largest observed ratio still under-predicted it (84.7 projected vs 87.5
> measured). Length is not the problem: a 24-character control string measured to
> within 0.5 px.
>
> **Direct measurement replaces projection, and here is the harness.** Render the
> string with **SkiaSharp pinned to the version in the head's own `deps.json`**
> (3.119.4 at the time of writing -- read it, do not assume), at the device scale,
> in Helvetica weight 700, and take the ink extent by the same luminance rule the
> screenshot analysis uses (first and last column below 140 on white with
> `#333333` text). Validated against 27 labels measured in the running app: 15
> exact, 10 within ±0.5 px, 2 within ±1.0 px, **0.35 px mean error**, with a
> 24-character control string (`Avoid Mercury retrograde`, app 157.0 vs harness
> 156.5) confirming error does not accumulate with length. No Avalonia and no
> headless host is involved, so `Avalonia.Fonts.Inter` cannot leak in.
>
> **The governing label cannot be picked on Windows and scaled.** On Windows
> "Natal Chart" and "Mercury Rx" are joint-widest in this panel; on macOS the
> order is **Mercury Rx (75.0) > Candidates (74.5) > Natal Chart (73.5)**, with
> "Candidates" overtaking from outside the Windows top two. Slice 2c saw the same
> reordering in the Chart panel (`Keywords` was not in the Windows top three and
> placed third on macOS). Each panel's labels must therefore be **measured on
> macOS**, not selected on Windows and scaled -- picking the wrong string to size
> for is a failure mode the ratio never even gets a chance to fix.
>
> **`Mercury Rx` has exactly 0.0 px of slack in the shipped 84 px column.** Ink
> 75.0 + 1.0 bearing + 8.0 margin = 84.0 in an 84.0 column. It is **not** clipped:
> the per-column ink profile tapers cleanly to zero (`14, 11, 8, 5, 2, 0`) with no
> vertical wall, its ink right edge is device 303 against a clip boundary of 304,
> and the SkiaSharp harness measures the same 75.0 with unlimited width. But there
> is no room left -- renaming that label, or any edit that widens it by a pixel,
> clips it until the column moves. A comment beside it in `MainWindow.axaml` says
> so at the point of use.
>
> Note also that the projection under-predicted this very panel: it said Electional
> would need 82 (97.7% of 84); measurement says 84.0 (100%).
>
> **THE WRAP RULE HAS A LIMIT, AND THIS SLICE FOUND IT.** Screening predicted the
> Days row would wrap identically: 240 px used of 261 available in WPF, 91.9%,
> under the ~94% line. **It wrapped differently anyway** -- WPF breaks Mon-Fri /
> Sat-Sun (5+2), Avalonia breaks Mon-Thu / Fri-Sun (4+3).
> **(Superseded -- see "Measurement reconciliation (2026-08-08)" below. WPF's wrap
> is 5+2 only in a window tall enough to retract the vertical scrollbar; at the
> shipped 950x600 default it is 4+3, and the 240-of-261 figure compares an ink
> numerator against a column denominator. The true figure is 249 of 245.)**
> Measured: Avalonia fits
> four checkboxes in the same 240 px that WPF fits five, about 60 px per checkbox
> against 48, **+25%**. The rule under-predicted by a factor of four because it
> models *text* measurement, and a checkbox's width is mostly *chrome* -- Fluent's
> box glyph and internal padding are far larger than WPF's, and neither scales
> with font metrics. **The screening rule applies to text-dominated rows only.**
> For rows of checkboxes, radio buttons or any control with a fixed glyph, the
> chrome dominates and the rule does not apply. The button WrapPanel, which is
> text-dominated, was predicted correctly (71.9%, wraps identically).
>
> Constructs: 26 `ToolTip` -> `ToolTip.Tip`; 1 `Visibility` + converter ->
> `IsVisible`; 2 `DatePicker` -> `CalendarDatePicker`; 1 editable `ComboBox` ->
> `AutoCompleteBox`; 2 `DisplayMemberPath` -> `DisplayMemberBinding`; 1
> `Style.Triggers` -> the existing `invalidTimezone` class; 10
> `UpdateSourceTrigger` dropped. Zero `x:Static`, zero `ListView`, zero
> `RelativeSource`.
>
> The timezone `SelectedItem` binding is dropped, as in the Chart panel, and
> loses nothing: `ElectionalViewModel.TimeZone`'s setter already syncs
> `SelectedTimezone`, and `SelectedTimezone`'s setter only forwards back.
>
> **THE TIMEZONE PICKER SHIPPED BROKEN AND WAS FIXED ON THIS BRANCH.** Choosing a
> zone from the Electional drop-down displayed the chosen zone while
> `Electional.TimeZone` kept the fragment the user had typed, so the next search
> failed with `TimeZoneNotFoundException: The time zone ID 'York' was not found`.
> Only the drop-down path was affected -- typing a zone in full, and the location
> lookup setting it programmatically, both committed correctly, which is why the
> exit-criterion run (which used the lookup) passed.
>
> **Same family as slice 3d's selection loss: a collection `Reset` raised from
> inside the binding write.** `ElectionalViewModel.TimeZone`'s setter called
> `RefreshFilteredTimezones()` at the end of its own body. `SetProperty` raises
> `PropertyChanged` *before* it returns, so a refresh written there lands after
> the control has processed the text change -- replacing the bound collection out
> from under the drop-down's selection adapter mid-commit. Chart Entry was never
> affected because its refresh has always been a notification handler
> (`MainViewModel.OnChartEntryPropertyChanged`), which fires earlier in the same
> sequence. The fix moves Electional's refresh to `OnSelfPropertyChanged`, a
> self-subscription made in the constructor: `FilteredTimezones` moved onto this
> child under H-1.6, so routing it through the parent as Chart Entry does would
> re-introduce the cross-VM coupling that decomposition removed. `SelectedTimezone`
> needed no change. `RefreshFilteredTimezones` now has exactly one call site --
> `ResetToDefaults` used to call it a second time and now rides the same
> notification, as Chart Entry's field-bypass paths already did.
>
> **The obvious test for this passes on a broken build.** Assigning
> `AutoCompleteBox.SelectedItem` directly -- which is what
> `TimezonePickerTests.ChoosingFromTheDropDown_ReachesTheViewModel_AndSurvivesTheReset`
> does -- bypasses the selection adapter, and commits cleanly on both panels
> whether or not the defect is present. So does setting the popup `ListBox`'s own
> `SelectedItem`. The guard has to focus the box, type, and press `ArrowDown` +
> `Enter`, with a layout pass after typing so the drop-down's item containers
> exist; without that pass `ArrowDown` is a no-op on *every* panel and the test
> fails everywhere for the wrong reason. `TimezoneCommitTests` does this on both
> panels. Note the headless symptom is not identical to the running app's: on
> macOS the field showed the chosen zone while the view model kept the fragment,
> whereas headless cannot move the adapter onto the item at all. Same cause, same
> consequence, different intermediate detail -- so the running-app check is not
> optional for this one.
>
> **`TextBox.KeyBindings` with `Gesture="Enter"` works in the running app.** The
> WPF original's `TextBox.InputBindings` + `KeyBinding Key="Return"` on the
> location lookup is the only per-control input binding in the whole 2408-line
> WPF markup, so there was no precedent in this head. Verified live, not
> headless.
>
> **Six new tests, all four non-obvious ones mutation-checked.** Duplicating a
> day binding, removing `FilterMode="None"`, misspelling the cancellation key and
> deleting the Enter binding each failed exactly one test and nothing else.
> `CheckBox` derives from `ToggleButton` derives from `Button`, so a bare
> `OfType<Button>()` returns 13 controls in this panel rather than 5 -- the Export
> panel's equivalent test avoids this only because that panel has no checkboxes.
>
> **Two slice 2 tests broke on window-wide searches, the third time this pattern
> has appeared.** `EdgeDockedButtons_HaveASymmetricHorizontalMargin` used
> `Single(Content == "Lookup")` and `TimezonePicker_CarriesANonInteractiveChevron`
> used a bare `Single()` over AutoCompleteBoxes; the Electional panel legitimately
> adds a second of each. Both rescoped to `ChartPanelRoot`. **Any new
> panel-scoped test should scope by named anchor from the outset.**
>
> **A pre-existing export defect, found while running the exit criterion.**
> `ExportElectionalMarkdownAsync` builds its filename from `ChartEntry.Name` and
> calls `FileNameSanitiser.Sanitise`, which throws
> `ArgumentException: File name cannot be null or empty` **before any file picker
> is shown**, whenever no chart has been loaded into the Chart panel. Selecting a
> saved chart in the Electional panel's own Natal Chart dropdown does not populate
> `ChartEntry`, so the natural path -- pick a natal chart, set a range, export --
> fails with a status-bar error and no dialog. This is shared Presentation code
> reached identically from the Export panel's own button, so it is **not** slice
> 4's and **not** Avalonia-specific. Its own task.
>
> **A product observation, not port work.** There is no electional results view
> in either head. A search that returns five ranked candidates tells the user
> about one of them, in one status-bar line, and everything else is only in the
> exported Markdown. Building a results list would be new behaviour and is out of
> scope for the port, but it belongs on the list of candidate improvements once
> the port lands.
>
> Ride-alongs: the Avalonia startup log now records the resolved database path
> (it recorded only the ephemeris path); and the two `ZZ Slice2b/2c Test`
> fixtures were deleted through the saved-charts pane's own Delete buttons,
> confirm gate included, bringing that database to 90.
>
> **The two Windows databases still are not the same library.** Both hold 90
> charts, but they differ by two names: the Avalonia store has "Announce
> ChessSage app" and "Kathy Clymer" where the WPF store has "ChessSage announce"
> and "Slice2 Verify". Unaffected by the fixture deletion, and unrelated to this
> slice, but the "both heads hold the same library" premise is still false.
>
> Proof (Windows): build 0 errors, warning codes unchanged (`NU1903`,
> `xUnit1031`), none new. `AstroCast.Tests` **2218/2218** with the name list
> byte-identical to the pre-slice baseline (SHA256 `43EA6796...`); `UiTests`
> **37/37** (31 + 6), discovered equal to executed. `src/AstroCast/` empty in the
> diff for the fifth slice running. macOS verification pending. Report:
> `D:\Code\phase4-slice4-report.md`.

**Measurement reconciliation (2026-08-08) -- read this before screening any
remaining panel**
> Two slice 4 measurements of the same five Days checkboxes in the same WPF head
> disagreed: the inventory recorded 240 px needed of 261 available and a 5+2
> wrap; the landing recorded 249 of 245 and a 4+3 wrap. **Both were right about
> the window they were taken in.** Neither stated the variable that decides it.
> No production code changed; this entry is measurement and documentation only.
> Report: `D:\Code\measurement-reconciliation-report.md`.
>
> **THE MISSING VARIABLE IS WINDOW HEIGHT, NOT WIDTH.** The content
> `ScrollViewer` (`MainWindow.xaml:353`) sits in a **fixed 360 px** outer grid
> column (`:145`) and has `HorizontalScrollBarVisibility="Disabled"` with
> `VerticalScrollBarVisibility="Auto"`. So the field area does not follow window
> width -- the landing's sweep was right about that -- but it loses **17 px**
> (`SM_CXVSCROLL`) the moment the panel is too tall to fit and the vertical
> scrollbar appears. Measured in the running WPF head at 96 DPI:
>
> | Window | vscrollbar | Available | Needs 5 | Wrap |
> |---|---|---|---|---|
> | 950x600 | yes (17) | 245 | 249 | **4+3** |
> | 1100x600 | yes (17) | 245 | 249 | **4+3** |
> | 1300x600 | yes (17) | 245 | 249 | **4+3** |
> | 950x1200 | no | 262 | 249 | **5+2** |
> | 1300x1200 | no | 262 | 249 | **5+2** |
> | maximised | no | 262 | 249 | **5+2** |
>
> The requirement is **constant at 249** across all six; only the denominator
> moves. `342 - 17 - 80 = 245` and `342 - 0 - 80 = 262`, where 342 is
> `360 - 10 margin - 8 padding`. **The WPF Days-row baseline is therefore neither
> "5+2" nor "4+3" unconditionally: it is 4+3 whenever the vertical scrollbar is
> up, which at the shipped 950x600 default size it always is.** The Avalonia head
> wraps 4+3 at every size measured including maximised, because its ~36 px row
> pitch means its scrollbar is *never* retracted. The two heads agree at the
> default window size, and disagree only in a window tall enough to retract WPF's
> scrollbar.
>
> **THE STANDING TECHNIQUE.** For every remaining panel, all three numbers come
> from the running head through UI Automation, not from a screenshot and not from
> `DesiredSize`:
>
> 1. **Available width** = the `BoundingRectangle.Width` of a **stretched sibling
>    field** in the same column span with zero horizontal margin (any of the
>    `formTextBox` rows). Take the modal (left,width) among the panel's Edit
>    controls; several fields agreeing is the check that you picked the right one.
>    Record the window size and whether the vertical scrollbar is up, because
>    that is a 17 px term.
> 2. **Requirement for N items** = `sum(element BoundingRectangle.Width + right
>    margin)` over the first N children in layout order. Verify the margin rather
>    than assuming it: consecutive siblings' rects should be exactly `margin`
>    apart (8 px here).
> 3. **Predicted wrap** = the largest N whose requirement is `<=` available.
>
> Do **not** use `DesiredSize` (clamped by its container -- the circular
> assertion that made `FormLabels_FitInsideTheLabelColumn` unable to fail), and
> do not compare an ink numerator against a column denominator, which is what
> produced the 91.9% that passed the ~94% screen when the true figure is
> `249/245 = 101.6%`.
>
> **Worked example, WPF Days row at 950x600.** Checkbox rects (screen x, width):
> Mon 204/47, Tue 259/41, Wed 308/46, Thu 362/41, Fri 411/34. Consecutive gaps
> are 8, 8, 8, 8 -- the `Margin="0,0,8,2"` confirmed, not assumed. Element span
> Mon..Fri = `445 - 204 = 241`; requirement(5) = `241 + 8 = 249`. Stretched
> sibling (the Location `TextBox`) measures 245. `249 > 245`, so five do not fit
> and the row breaks after Thu: predicted **4+3**, observed **4+3**. At 950x1200
> the same sibling measures 262, `249 <= 262`, predicted **5+2**, observed
> **5+2**. Avalonia: requirement(4) = 249, requirement(5) = 301, sibling 258 ->
> predicted **4+3**, observed **4+3**. **Ten window states, ten correct
> predictions.**
>
> **WHERE THE 9 px WENT: ink is not the layout requirement.** The inventory's 240
> is a *rendered ink extent* -- first to last dark pixel across the five drawn
> checkboxes. The landing's 249 is the *layout extent*. They differ by the 8 px
> right margin of the fifth checkbox, which no glyph occupies, plus ~1 px of box
> inset. Confirmed directly on the Avalonia head, where both can be taken from
> one frame: element span 241, **rendered ink 239**, requirement **249** -- ink +
> 8 + 2. So `240 + 8 + 1 = 249`: one quantity, two definitions, no contradiction.
> Use the layout extent; the wrap is a layout outcome and ink systematically
> under-reports it by roughly one margin.
>
> **Chrome, per checkbox, separated from text.** WPF: `element width - advance` is
> constant at **20.1 px** (13 px box + gap + padding); plus the 8 px margin,
> **28.1 px** of every checkbox is not text. Avalonia: the per-checkbox ink runs
> all begin `0..19(20)` -- a 20 px Fluent box -- with text starting at x=28-29, so
> **~28.5 px** of chrome and **~36.5-37.5 px** fixed with the margin. That is
> where the "37.5 px per checkbox" figure belongs: **it is Avalonia's, not
> WPF's**, and it is not comparable to the inventory's 240, which is a WPF
> five-checkbox ink extent. Over five checkboxes the 8.4 px chrome difference is
> 42 of the 52 px by which Avalonia's requirement (301) exceeds WPF's (249).
>
> **THE LABEL SURVEY WAS MEASURED AT THE WRONG FONT SIZE.** `FormLabel` sets no
> `FontSize`, so it inherits `SystemFonts.MessageFontSize`. On Maple that is
> **13 DIP, not 12**: `TextScaleFactor` is **110**, so Windows scaled the system
> message font and its LOGFONT `lfHeight` is -13. Three independent confirmations:
> UI Automation reports the `Progressions` label slot as **75** and
> `Relationship` as **73** where every other label is clamped to 72, and
> `FormattedText` at SemiBold **13** gives **75.05** and **72.68** (SemiBold 12
> gives 69.28/67.08 and Bold 12 gives 71.94/69.93 -- neither fits); the nav rail's
> explicit `FontSize="9"` captions match advance within 1 px, confirming that an
> auto-sized slot width *is* the advance; and the per-checkbox chrome residual is
> constant only at 13 (19.9-20.4 px, against 21.1-22.4 at 12). **Every figure in
> the survey table is therefore 7.7% low.**
>
> **This is what vindicates the clipping observation.** At 12 DIP `Progressions`
> needs 69.3 into a 72 px allowance and the technique predicts no clip, which
> contradicts what was seen at 10x zoom. At 13 DIP it needs **75.05**, overflowing
> 72 by **3.0 px** -- clipped, exactly as observed -- while `Relationship` needs
> 72.68 and overflows by 0.7 px, which is why it "renders complete" to the eye.
> The observation was right and the measurement was wrong. **A technique that
> assumes the font size fails this known-answer case; read the resolved size from
> the running head.**
>
> **The two heads never rendered labels at the same size.** `.formLabel` in the
> Avalonia head sets no `FontSize` either, so it inherits FluentTheme's
> `ControlContentThemeFontSize` (14) -- theme-pinned, and *not* affected by
> Windows text scaling, which Avalonia ignores. So WPF-on-Maple labels are 13 and
> Avalonia labels are 14.
>
> **THE 6.5% RULE IS A FONT-SIZE ARTEFACT. RETIRE IT; DO NOT RE-TUNE IT.**
> Settled 2026-08-08 by the same-size control test, with no production change: the
> Export panel's 18 `ActionButton` captions were measured in both heads (identical
> style in each -- `Padding 12,5`, `MinWidth 100`, no `FontWeight`, no
> `FontSize`), and each head's widths regressed against per-em advance. Measured
> width = `fontSize * unitAdvance + chrome`, so the slope recovers the **resolved
> font size** and padding falls out as the intercept -- which means neither head
> has to be edited to share a size.
>
> | Head | slope, FormattedText basis | slope, SkiaSharp basis | R2 |
> |---|---:|---:|---:|
> | WPF | **13.017** | **12.999** | 0.9992 |
> | Avalonia | **14.056** | **14.036** | 0.9998 |
>
> (chrome pinned to the physical 26 px = 2 x 1 border + 2 x 12 padding; free-fit
> gives 12.79/13.63 with the intercept absorbing the difference, same conclusion.)
>
> Three things fall out at once:
>
> 1. **One advance vector fits both heads.** The same DirectWrite-derived unit
>    advances predict every fitted caption in *both* heads to within 1.4-2.7 px
>    across widths up to 198 px. If Skia/HarfBuzz measured text ~6.6% differently
>    from DirectWrite, Avalonia's widths could not be fitted by DirectWrite
>    advances and a single scalar; the residuals would be large and structured.
> 2. **The two engines agree to 0.14%.** Within each head the FormattedText-basis
>    and SkiaSharp-basis slopes differ by 0.14% (13.017 vs 12.999; 14.056 vs
>    14.036) -- two orders of magnitude below 6.6%.
> 3. **13 -> 14 predicts the observed 6.2-7.0% exactly, spread and all.** With
>    chrome shared at 26 px the width ratio is `(14u+26)/(13u+26)`, which *rises*
>    with caption length: 1.061 at `Export Markdown` (measured 136/128 = 1.0625)
>    and 1.066 at `Export Planetary Return MD` (measured 198/185 = 1.0703). The
>    recorded "the delta scales with width, so this is text measurement and not
>    padding" correctly ruled out padding but never separated text *measurement*
>    from text *size*. It is the size.
>
> The direct control agrees. At an **identical explicit `FontSize="9"`** the nine
> nav-rail captions fit slopes of **9.37 (WPF) against 9.12 (Avalonia)** on the
> FormattedText basis -- Avalonia's is if anything *lower* -- with the whole
> difference a constant ~1.5 px offset (intercept -1.44 against +0.13), not a
> scale factor. A constant-pixel offset and a percentage scale have opposite
> signatures across widths; this one is constant in pixels.
>
> Note also that the `MinWidth="100"` control that was offered as proof does not
> discriminate: a floored button is floored in both heads whatever the reason its
> text differs.
>
> **And the 6.5% figure is machine-specific, which is the real reason not to keep
> it.** WPF's 13 is `SystemFonts.MessageFontSize` under this machine's
> `TextScaleFactor` of 110. On a stock Windows box at 100% it is 12, the ratio
> becomes 14/12 = **16.7%** on text and roughly **14%** on a long caption -- more
> than double what was calibrated here. A screening rule carrying the 6.5% number
> would badly under-predict wrap breakage on any machine whose text scaling
> differs from Maple's.
>
> **Replace the ratio with a direct computation.** Avalonia caption width =
> `14 * unitAdvance(caption) + chrome`, with the unit advance from either
> instrument (they agree to 0.14%) and chrome from the style. Checked against all
> 18 measured captions: every one predicted within ~1 px, including the two the
> `MinWidth` floor catches. Nothing has to be projected from the WPF head at all,
> which removes the machine dependence along with the ratio.
>
> **THE TWO HARNESSES AGREE, SO THE macOS COLUMN FIGURES ARE TRUSTWORTHY.** WPF
> `FormattedText` and SkiaSharp 3.119.4 were run over 20 shared strings (the 17
> Electional labels plus `Progressions`, `Relationship`, `UTC Offset`), same
> family, same weight, same size, in one process:
>
> | Comparison | mean abs delta | max abs delta |
> |---|---|---|
> | advance vs advance | **0.15 px** | 0.61 px (`Type`) |
> | ink vs tight ink | **0.34 px** | 0.87 px (`Type`) |
>
> Both instruments resolve the same face (`SEGUISB.TTF` / `Segoe UI` weight 600).
> The residual concentrates on kern pairs (`Ty`, `UTC`), because SkiaSharp's
> unshaped `MeasureText` omits the GPOS kerning `FormattedText` applies -- so
> **SkiaSharp reads very slightly wide on kerned strings, which is the safe
> direction for sizing a column.** The two platforms' numbers are comparable and
> the survey's Windows column is sound *as an instrument reading*; its error was
> the font size parameter, not the harness.
>
> **100 STILL STANDS.** The `Progressions` requirement of 96.5-97.5 came from
> SkiaSharp on macOS, and that figure is not exposed to the 13-versus-12 error
> found here, because Avalonia's label size is theme-pinned at 14 on every
> platform rather than inherited from a machine setting. The instrument behind it
> is now cross-validated to sub-pixel agreement. On Windows the same label needs
> `75.05 + 8 = 83.1` in the WPF head and roughly 90 in the Avalonia head, both
> comfortably inside 100. Keep 100, and keep the plan's existing instruction to
> re-confirm from rendered pixels once the Transits panel exists.
>
> **HAZARD: `dotnet test` on another ref silently replaces the Avalonia head's
> binaries.** `AstroCast.UiTests` references `AstroCast.Avalonia`, so building or
> testing on any other ref rebuilds the head and Presentation into the same output
> directory. **It presents as the running app showing "Panel content arrives in a
> later slice"** -- which reads as a code regression rather than a build one, and
> can burn a debugging cycle. Detect it before believing anything:
>
> ```
> Get-FileHash src\AstroCast.Avalonia\bin\Debug\net10.0\AstroCast.Presentation.dll -Algorithm SHA256
> [Diagnostics.FileVersionInfo]::GetVersionInfo('...\AstroCast.Presentation.dll').ProductVersion
> Get-Process AstroCast.Avalonia | Select-Object -ExpandProperty Modules |
>   Where-Object ModuleName -like 'AstroCast*' | Select-Object ModuleName, FileName
> ```
>
> `ProductVersion` carries the commit (`1.2.1+3966179...`), and the live process's
> loaded-module list proves *which file* the running app actually mapped. Rebuild
> and re-verify by SHA after any cross-ref build.
>
> **HAZARD: `PrintWindow` returns a blank client area for the WPF head, and the
> existing check does not catch it.** On this machine `capture-window.ps1` reports
> `captureResult 0` and `lumRange 0..255` while the saved PNG shows an entirely
> white client -- the title bar and window border alone satisfy its
> "differingFromTopLeft" test. `PW_RENDERFULLCONTENT` does not redirect WPF's
> composited content here, and `Graphics.CopyFromScreen` fails outright with "The
> handle is invalid" because the automation host is containerised. The Avalonia
> head captures correctly (it composites through Skia), which is why this went
> unnoticed. **Any WPF pixel figure taken by screenshot on this machine must be
> validated by checking for ink inside the client region, not the whole frame** --
> and a blank frame scans as "no ink", which silently reads as a small
> measurement rather than a failure. The technique above avoids the problem
> entirely by taking WPF geometry from UI Automation.
>
> **What this changes for Transits.** The screening rule's input is now the layout
> requirement, not ink, so re-screen Transits' `WrapPanel` rows with the technique
> above rather than reusing any recorded ink figure. Record the window size and
> scrollbar state with every number. The 6.5% Avalonia-versus-WPF rule is under
> suspicion (see above) and should be treated as unvalidated until the same-size
> control test is run. The instrument used here is
> `D:\Code\uimeasure` (`UiMeasure app|text|skia`); it references no AstroCast
> assembly and attaches to whichever head is already built.

**Phase 4 slice 5 -- BUILT, NOT LANDED (2026-08-08): the Transits panel, and
the label column move to 100**
> Branch `phase4/slice5-transits-panel-20260808`, cut from the measurement-
> reconciliation docs branch tip (`d331494`, itself two docs-only commits above
> `master` = `3966179`), so the whole line fast-forwards at landing. Three code
> commits: the column move, the panel, the tests. `src/AstroCast/` diff vs
> master: **empty**, ninth slice running. Report:
> `D:\Code\phase4-slice5-report.md`.
>
> **THE COLUMN MOVE (84 -> 100) LANDED FIRST, AS ITS OWN COMMIT.** All six
> existing form grids (Chart five, Electional one) in one edit, plus the four
> comment blocks that described the 84 world: the Chart derivation (now gives
> the 100 arithmetic on both platforms and names the governor as
> "Progressions", not "UTC Offset"), the Electional preamble (the move no
> longer "belongs to the slice that brings Transits over" -- this was that
> slice), the Mercury Rx zero-slack comment (now records 16 px macOS / 18.4 px
> Windows of slack), and the Days-row screening comment (ink-over-column
> figures replaced by the settled layout-extent measurement).
> `ChartPanelLayoutTests.LabelColumnWidth` 84 -> 100 in the same commit; the
> historical "84.00" in the tautology-experiment record stays as written.
> **Verified from the running head, not only headless:** both panels' field
> columns start at x=224 (was 208), every form-label slot reads 92 = 100 - 8,
> zero labels at or over the clip boundary, and Mercury Rx -- previously
> zero-slack to the device pixel -- shows 21 px of rendered slack.
>
> **THE PANEL.** WPF `MainWindow.xaml:800-958` -> `TransitsPanelRoot`. Three
> features, as in the WPF head: Transits (two `CalendarDatePicker`s, two
> toggles, multiline context, four buttons), Daily Horoscope (one export
> button), Return & Ingress Timeline (years pair, two toggles, three buttons)
> -- the last binding a second child VM, so the panel carries TWO Cancel
> buttons distinguished only by their `CommandParameter` strings ("transits" /
> "returnIngressTimeline", both pinned by test against the OperationKeys
> constants). Born at 100, both grids. The two date pickers are sites six and
> seven of the known Windows `CalendarDatePicker` glyph defect (was five).
>
> **THE SCREENING PREDICTIONS ALL HELD.** Measured in the running WPF head at
> both window heights and computed directly for the Avalonia head
> (14 x unit advance + chrome -- never projected from WPF), then confirmed
> from the running app after translation:
>
> | Row | WPF req | Avalonia req | Available (W/A) | Both heads |
> |---|---:|---:|---|---|
> | Transits buttons (4) | 519 | 544 | 325-342 / 326 | **2+2** |
> | Daily Horoscope (1) | 196 | 209 | same | 1 row |
> | RIT checkboxes (2) | 148 | 173 | same | 1 row |
> | RIT buttons (3) | 352 | 360 | same | **2+1** |
>
> Both WPF wraps are height-independent (352 and 374 both exceed even the
> scrollbar-retracted 342), unlike the Days row. Vertical budget: the WPF
> panel runs ~582 px against a 561 px viewport at 950x600 -- **the WPF head
> already scrolls this panel** -- and the Avalonia head ~717 px, scrolling
> more. Same qualitative state. For the Returns slice (638 lines) expect the
> same shape: inflation changes how far it scrolls, not whether.
>
> **EXIT CRITERION MET -- identical transit calculation through both heads'
> own UIs.** Both heads driven identically through UI Automation patterns
> (Invoke/Value only, no synthetic input): load "Andrew Windsor" from the
> saved-charts pane, navigate to Transits, Calculate with the shared VM
> defaults (Today -> Today+1 month, minors off, progressions on), export both
> reports through each head's native save dialog. Transit MD: **899 lines,
> byte-count 39,720 in both, differing on exactly one line** (the Generated
> timestamp). Timeline MD: **610 lines, 29,494 bytes in both, one line**.
> Enablement parity demonstrated behaviourally: Calculate Transits sat
> disabled until the chart load flipped `HasResult` in each head.
>
> **"Progressions" measured on glass in this head for the first time:** 79 px
> of rendered ink ending at screen 203 against a clip boundary of 216 --
> **13 px of rendered slack** on Windows (rendered 79.0 vs 79.1 predicted
> from FormattedText geometry at 14). The macOS figure is the Mac's to
> confirm directly with the SkiaSharp harness.
>
> **Tests: UiTests 42 -> 47**, `AstroCast.Tests` 2218/2218 with the name list
> byte-identical (SHA `43EA6796...`, which also matches the slice 4 baseline
> file -- settling that file's provenance question). New:
> `TransitsPanelTests` (buttons command-bound; BOTH cancel keys pinned by
> automation name against the constants; automation names everywhere; the
> four toggles driven by focus+Space across both child VMs, asserting exactly
> one property moves) and `LabelColumnAlignmentTests` -- the cross-panel
> guard: every formLabel-bearing grid in every translated panel root (5+1+2)
> carries the one shared 100. Mutation-checked, four at once: repointed
> checkbox binding, "transits" -> "transitsX", a 96 grid, a deleted
> automation name -- exactly the four corresponding tests failed, 43 stayed
> green.
>
> **An instrument caveat found while verifying, worth keeping:** the
> label-ink scanner's stretched-sibling pick can land on a
> `CalendarDatePicker`'s *inner* text box (one pixel right of the field
> cell), which drags the scan window onto the field controls' left border
> column and reads every label row as full-width ink -- it presents as "every
> label clipped", when the truth (per-label rescan excluding the border
> column) is 13-61 px of slack. The UIA slot-width signature (an overflowing
> label reports a slot wider than column-minus-margin, as WPF's Progressions
> does at 75 vs 72) is the robust cross-check.
>
> **What the Mac must verify before landing:** (1) Chart + Electional +
> Transits at 100 under Helvetica -- labels unclipped, field columns at one
> x; (2) "Progressions" measured directly with the SkiaSharp harness against
> the predicted 96.5-97.5 need (expect ~2.5-3.5 px slack); (3) the two new
> `CalendarDatePicker` sites render the proper calendar glyph there (they
> show the stray digit on Windows, sites six and seven); (4) suites:
> `AstroCast.Tests` 2218/2218, `AstroCast.UiTests` 47/47; (5) the same
> two-export drive against the same chart if convenient -- the exports are
> platform-independent Core output, so byte-parity minus Generated is the
> expectation there too.

**Landing note (2026-08-08) -- the cross-platform export expectation in (5)
above was WRONG in one word, and it cost a landing stop: expect
CHARACTER-parity, not byte-parity.** The Mac's slice 5 exports came out 449 and
160 bytes smaller than Windows' at identical line counts, and the deltas are
exactly the files' Windows CRLF counts: the formatters mix Environment.NewLine
(the transit sections' AppendLine calls) with LF-normalized content (the
embedded natal section passes through MarkdownLineSplitter), so Windows emits
mixed CRLF/LF and macOS emits uniform LF for the same character stream --
39,720 - 449 = 39,271 and 29,494 - 160 = 29,334, the Mac's byte counts to the
byte. The golden harness normalizes line endings before comparing, so golden
parity can never see this class. Rules going forward: **compare cross-machine
exports at character level (normalize line endings first), and pin every
environmental input explicitly -- the date range especially, which defaults to
f(today) and only matched here because both drives happened to run the same
day.** The mixed-endings emission itself is a small pre-existing wart (one file,
two conventions) worth a register entry and its own cleanup task; it is not a
port defect, and both heads on one platform agree byte-for-byte.

**Phase 4 slice 6 -- LANDED (2026-08-08): the Medical panel, and the first
pre-existing defect the equality criterion could never have found**
> Branch `phase4/slice6-medical-panel-20260808`, two commits (translation,
> tests), fast-forwarded onto `master` at `2071cb6` and released as `v1.2.3`.
> `src/AstroCast/` diff vs master: **empty**, tenth slice running. Reports:
> `D:\Code\phase4-slice6-report.md` (build) and
> `D:\Code\phase4-slice6-landing-report.md` (landing).
>
> The smallest panel: `MainWindow.xaml:2063-2100` -> `MedicalPanelRoot`, seven
> elements, two of them interactive. Verified on both platforms -- Windows 0
> errors / 2218 name-list-identical / 51 UI tests, exports character-identical
> between heads at 34,815 bytes with an identical mixed line-ending census (154
> CRLF, 494 lone LF) and only the two wall-clock Generated stamps differing;
> macOS 0 errors / 2218 byte-identical / 51, panel fits without scrolling and
> wraps 3+3 exactly as Windows does.
> These are slice 6's figures; since 1.3.3 (N, O-7) the export's size and line endings differ.
>
> **`defaultExt` works in the Avalonia head.** The medical export is the only
> one of 25 `ShowSaveAsync` sites whose suggested filename carries no extension
> (`$"{LastResult.Input.Name}_Medical"` with `defaultExt: ".md"`), so it is the
> only place the append behaviour is load-bearing. Saving with nothing typed
> produced `..._Medical.md` in both heads. `AvaloniaFileDialogService` maps it
> to `FilePickerSaveOptions.DefaultExtension` through `NormaliseExtension`,
> which strips the leading dot -- the form Avalonia wants. First exercise of
> that path in this head.
>
> **The first stretched action button, and it did not stretch.** A WPF Button in
> a vertical StackPanel fills its parent (342 px measured); the Avalonia one
> auto-sized to 242 px and sat left. The *property* default is `Stretch` in both
> frameworks -- FluentTheme's Button style overrides it.
> `HorizontalAlignment="Stretch"` restores the WPF default; re-measured at 350
> of 350 on Windows and 349 of 350 on macOS. Every other action button in this
> head lives in a WrapPanel, which auto-sizes in both frameworks, which is why
> six slices ran before this could surface. Same class of fix as the
> `CalendarDatePicker` stretch settled in slices 2-4.
>
> **THE LABEL IS STACKED, AND THAT IS LOAD-BEARING.** "Health Context
> (optional)" is this panel's only `formLabel` and the widest in the translated
> set -- 161.4 px of ink at 14 DIP on Windows, 164.5 px on macOS. The WPF
> original stacks it full-width above the box instead of placing it in a form
> grid, and the translation does the same: a cell would need about 170 px and
> the shared 100 column would clip it by seventy. macOS confirmed one line with
> 184.5 px of headroom in a 350 px slot. `LabelColumnAlignmentTests` therefore
> registers this panel at **zero** form grids -- every other entry guards a
> column width, this one guards the decision not to have a column, so the
> plausible tidy-up that would clip the label fails a test instead of shipping.

**Product finding (2026-08-08) -- the health-context box is a dead input, in
both heads.** `MedicalHealthContext` has exactly two references in source: its
declaration (`MainViewModel.cs:1275`) and the WPF TextBox binding
(`MainWindow.xaml:2082`). Nothing reads it.
`HealthProfileGenerator.Generate(ChartResult)` and
`MedicalMarkdownFormatter.Format(ChartResult, HealthProfile, TimeProvider?)`
take no health-context parameter, and no test covers one. The panel's own
descriptive paragraph tells the user the value is "included verbatim in the
exported report header for AI-assisted interpretation" -- untrue in both heads
today.

Proven by negative control, not by inspection alone: the two heads were driven
again with deliberately different text -- `AAAA WPF CONTEXT ONLY AAAA` into the
WPF head, `ZZZZ totally different Avalonia text ZZZZ` into the Avalonia one --
and still produced byte-identical exports, with neither string present in
either file.

This is a **product fix for after the port**, not a port defect: it needs a
health-context parameter on the formatter, a change at the call site, and
golden regeneration. Same category as the electional starvation bug -- found
during the port, fixed separately. Carried into Section 9.

**Methodology (2026-08-08) -- what the equality criterion cannot see, and the
one assertion that fixes it.** Every panel's exit criterion so far has been
"both heads produce the same output". That is the right test for translation
fidelity and it has caught real binding defects. It is also **structurally
blind to pre-existing defects, because both heads can be identically wrong** --
and the dead input is the clean demonstration: the exports match no matter what
is typed, and would match even if the two heads were given different text.

The pattern holds across the whole port. Every pre-existing defect this work
has surfaced -- electional starvation, the UT-vs-local boundary, the JD
truncation, and now the dead input -- was found by reading code or by
side-investigation. Not one was found by the equality check.

**Strengthening for the remaining panels (Synastry, Relocation, Map, Returns):
add one content assertion per panel on top of the equality check -- does the
output actually contain what the panel claims it contains?** For Medical that
assertion would have been "the health-context string appears in the exported
report", which fails today and names its own cause. Equality proves the two
heads agree; only a content assertion proves they agree on something true.

**Phase 4 slice 7 -- LANDED (2026-08-08): the Synastry and Relocation panels,
and the discovery that this head has three content widths, not two**
> Branch `phase4/slice7-synastry-relocation-20260808`, three commits
> (translation, caption alignment, tests), fast-forwarded onto `master` at
> `50c0f97` and released as `v1.2.4`. `src/AstroCast/` diff vs master:
> **empty**, eleventh slice running. Report:
> `D:\Code\phase4-slice7-landing-report.md`.
>
> Two panels in one slice: 339 added lines of axaml and three new test files.
> Verified on both platforms -- Windows 0 errors / 2218 name-list-identical /
> 65 UI tests, all three wrap predictions confirmed on both running heads, 5 of
> 5 mutations killed; macOS 0 errors / 2218 byte-identical / 65, the tight
> Relocation row wrapping **2+2** (319.0 of 342.0 used, 23.0 spare) exactly as
> predicted, captions **centred** (28.5 measured against 28.75 predicted, three
> buttons agreeing to the device pixel), `Relationship` needing **91.5** of the
> 100 column with 8.5 px of slack as the widest label in both new panels, and
> the hint starting at the field column (device 352-353).

**Layout finding (2026-08-08) -- the Avalonia head has THREE content widths,
not two, and they are panel-dependent by construction.** Earlier slices treated
the panel content width as a per-platform constant to be looked up. It is not:
it is computed per panel, and this slice measured all three on both platforms.

| Platform | 342               | intermediate     | 350                         |
|----------|-------------------|------------------|-----------------------------|
| Windows  | Chart, Relocation | Transits **348** | Electional, Medical, Export |
| macOS    | Relocation        | Synastry **345** | Medical                     |

**Mechanism** (derived on Windows; the macOS figures are consistent with it):
WPF applies `Padding` as the presenter's **Margin**, which deflates measure and
arrange together. Avalonia applies it as the presenter's own **Padding**, and
`ScrollContentPresenter` measures its child at the full viewport (350) while
arranging at viewport minus padding (342); `StackPanel.ArrangeOverride` then
grants each child `max(finalSize, desired)`. So each panel's content width is
effectively `clamp(widest child's desired width, 342, 350)` -- panel-dependent
by construction, and platform-dependent because text measurement differs.

**And the Chart panel's width is data-dependent.** It flips 342 -> 350 around
the twentieth character typed into its first field, and takes every field
column with it. That is a live layout jump in the shipped head, not a cosmetic
inconsistency between panels.

**Correction (2026-08-08) -- the recorded caption claim was right in outcome
and wrong in mechanism.** What was written down was "Fluent left-aligns button
content; WPF centres". What actually happens: WPF centres via a **theme
setter**, while the property's registered metadata default reads `Left` -- so
inspecting the default confirms the wrong story, which is the trap. Fluent does
not set the property at all, leaving Avalonia's registered default of
`Stretch`, which spreads the caption TextBlock across the button so its glyphs
land on the left padding edge.

The fix follows from the real mechanism and not from the recorded one:
`HorizontalContentAlignment="Center"` on the button. A `TextAlignment` cannot
do it -- the TextBlock lives inside the button's template and is not reachable
from the button.

**The derivation then transferred to macOS exactly, taking only that platform's
own caption width as input.** Predicted offset `(100 - 38) / 2 = 31` on WPF and
`(100 - 42.5) / 2 = 28.75` on macOS; macOS measured **28.5** on three buttons,
all three agreeing to the device pixel. A mechanism that predicts the second
platform from one measurement is the standard to hold to here.

**Methodology (2026-08-08) -- a utilisation percentage is not a wrap predictor,
and this slice shows it cleanly.** The tight Relocation row measures **93.3%**
utilisation on macOS (319.0 of 342.0), sitting right against the retired 94%
screening threshold -- while being nowhere near a wrap change: adding the next
button would need **444.5** px against 342 available.

**Only "does the next item fit" predicts a wrap.** A percentage cannot, because
it compresses away the one quantity that decides the outcome -- the width of
the specific next item. That is the second percentage-shaped heuristic to fail
here, after the 6.5% rule, and it reinforces the direct-computation technique
already recorded.

**Five pre-existing defects found while reading these two panels, none fixed
(2026-08-08).** The content assertion adopted after slice 6 did its job:
asking whether each panel does what it claims surfaced five distinct defects,
every one of them present in the shipped WPF head and therefore none of them
port work. Carried into Section 9 as items 6-10.

1. **`UseNatalLocationForRelocationCommand` can never be enabled by calculating
   a chart.** Its predicate would allow it, but `HasResult`'s setter calls
   `RefreshExportCommandStates()` (`MainViewModel.cs:4148-4166`), whose list
   omits it; the only method that raises its `CanExecuteChanged` is
   `RefreshRelocationCommandStates()` (`:4440`), reached from the Relocation
   child's setters. So the only ways to wake the button are picking a chart
   from the dropdown or typing into the very Location field the button exists
   to fill. The Electional twin, `UseNatalLocationCommand`, **is** in the
   refresh list (via `RefreshElectionalCommandStates()` at `:4195`) -- which
   makes this an omission, not a design.
2. **Descriptive text promising behaviour that does not exist.** The Relocation
   hint and tooltip say to "leave empty to use the currently calculated chart",
   but the ComboBox is non-editable, `SavedCharts` carries no null sentinel,
   and `RelocationViewModel.Clear()` is never invoked from `ClearForm`. Once a
   chart is selected there is no UI path back to empty. Synastry's Person B box
   does not have this defect -- its `Clear()` is called. Same family as the
   dead health-context input from slice 6: interface text asserting something
   the code does not do.
3. **`AllSavedCharts` is a dead collection.** It is declared
   (`MainViewModel.cs:1008`), populated (`:4123-4126`), and bound by nothing in
   either head -- every chart picker in both `MainWindow.xaml` and
   `MainWindow.axaml` binds `SavedCharts`, four sites each, and `AllSavedCharts`
   has zero `ItemsSource` bindings. Two XML doc comments
   (`RelocationViewModel.cs:215`, `SynastryViewModel.cs:90`) and one axaml
   comment (`MainWindow.axaml:1487`) state that the Relocation and Synastry
   boxes use it; all three are wrong.
4. **Silent-failure coordinate input.** `LatitudeText`/`LongitudeText` raise no
   command refresh, are parsed only when a button is pressed, and report
   failure to the status bar. No range check exists anywhere in the path.
5. **Two UI-thread Swiss Ephemeris calls outside any cancellation token**, in
   `CalculateRelocationAsync` (before its `Task.Run`) and
   `UseNatalLocationForRelocation` (fully synchronous). Both freeze the UI
   while they run and neither can be cancelled, unlike the Synastry and
   Composite paths in the same view model.

**Also pre-existing, and left alone: a WPF label clip.** `Relationship`
overflows the shipped WPF head's 72 px label slot by about 0.7 px, reporting
**73** where every other label clamps to 72 -- the same UIA slot-width overflow
signature that identified the Transits `Progressions` clip. It is a WPF defect
rather than a translation defect, and the translated panel is clean: 13.7 px of
slack at the Avalonia head's 100 column on Windows, 8.5 px on macOS (91.5
needed). Recorded, not fixed.

**Phase 4 padding slice -- LANDED (2026-08-09): one content width,
and the overlay scroll bar stops sitting on the content**
> Branch `phase4/scrollviewer-padding-20260808`, cut from `master` at `e988b6b`
> (deliberately not from the unlanded dependency-hardening branch, since the two
> are independent). `src/AstroCast/` diff vs master: **empty**, twelfth slice
> running. Not a panel translation: the whole change is the panel host's right
> inset, two attributes in `MainWindow.axaml`. **It landed inside the Map
> slice's fast-forward** (the Map branch was cut from this branch's tip
> `74c497d`, so `master` at `3dc57bd` contains all six commits) and the branch
> was retired with it; its three commits are released as `v1.2.5`.
>
> The fix: the 8 px comes off the `ScrollViewer`'s `Padding` and goes on the
> panel-host `StackPanel`'s `Margin`, as **16**. Both departures from the WPF
> original are measured rather than stylistic, and the reasoning is recorded in
> a comment block above the ScrollViewer so that "tidy it back to match WPF"
> fails a test instead of shipping.
>
> Windows: build 0 errors, warning codes unchanged against master's baseline
> (`NU1903` x17 + `xUnit1031` x2 = 19, none new -- NU1903 is expected on this
> branch because it is cut from `master`, which does not carry the SQLite pin).
> `AstroCast.Tests` 2218/2218; `AstroCast.UiTests` **65 -> 68**.

**The three pre-flight measurements, all three answered before anything was
edited.** Taken in the headless harness with the real Fluent theme, which does
true Avalonia layout, and then confirmed on glass. Headless resolves Inter on
Windows where the app renders Segoe UI, so the absolute text-driven widths below
are headless figures; the scroll-bar geometry and the mechanism are
font-independent, and the outcome was re-measured in the running head.

1. **Does the expanded overlay scroll bar clip readable content at 342? YES, and
   the answer decides 16 over 8.** The bar occupies x=334..350: **16 px wide, and
   identical collapsed and expanded**, because expanding thickens the thumb
   inside a constant-width control rather than widening the control. So an 8 px
   inset leaves content 342 wide and every scrolling panel runs 8 px underneath
   the bar; a 16 px inset ends content at 334, exactly flush with the bar's left
   edge. **16 is the bar's measured width, not a doubled 8.** Measured overlaps
   before the change: Chart +8, Export +8, Transits +14, Electional +15.
2. **The WrapPanel re-screens across all seven translated panels cost exactly one
   wrap.** The Electional panel's first WrapPanel goes from **4+3 to 3+4** across
   the same two rows. Every other WrapPanel in every other panel keeps its
   pattern. And the change is caused by **removing the clamp, not by choosing
   16** -- an 8 px inset produces the identical 3+4 split, so the wider inset is
   free in wrap terms.
3. **The Chart panel's data-dependent width is gone.** Under the clamp it flipped
   342 -> 350 between 10 and 19 characters of name (headless/Inter; the recorded
   Segoe figure was "around the twentieth", consistent). After the change it is
   334 at every name length from 0 to 60, in **both** arranged and desired width
   -- not merely clamped stable but never reaching the ceiling. Confirmed on
   glass: a 60-character name leaves every Chart field at x=224 w=234, identical
   to a two-character name.

**Result, measured in the running head with Segoe UI.** All seven panels now
arrange to **334**. The Medical panel's health-context TextBox, the one control
that made the 350 width visible, measures **334** where it measured 350 before.
Chart, Transits and Electional field columns all measure **234**, which is 334
minus the shared 100 label column, where Chart's was 242. Content spans x=124 to
458 and the 16 px bar occupies 458 to 474: flush, zero overlap.

**And the WPF comparison, measured rather than assumed -- 334 is not a
regression against WPF, it is between WPF's two states.** WPF *reserves* layout
space for its scroll bar where Fluent overlays, so the WPF head's content width
is itself conditional: measured on the scrolling Chart panel, its field column is
245 against an 80 px label column, so **325** content -- exactly 342 minus the
17 px `SM_CXVSCROLL` it reserves. WPF is therefore 342 when a panel fits and 325
when it scrolls, while this head is now 334 unconditionally: 9 px *more* usable
width than WPF gives when scrolling, and never a width that changes under the
user.

**Three tests added (`PanelContentWidthTests`), and every assertion is
deliberately relative.** None names 334 or any absolute width, because headless
renders Inter, the app renders Segoe UI and macOS renders Helvetica, so an
absolute figure would pin the wrong font on two platforms out of three. What is
asserted is font-independent: that the panels agree with *each other*, that
content clears the *measured* bar position, and that the Chart panel's width does
not move with its data.

**Mutation-checked three ways, each mutation anchored to the specific attribute
it targets and confirmed to have landed there** (the slice 7 lesson: a mutation
that misses reports the assertion as weak when the mutation was):
- host `Margin` 16 -> 8: **only** the overlap test fails. One width still holds,
  which is exactly why two separate assertions are needed.
- `ScrollViewer` `Padding` restored, margin left at 16: **only** the one-width
  test fails. The clamp returns while content still clears the bar.
- full revert to the pre-slice markup: **all three** fail.

**What the Mac must verify before landing, and one specific way it could
legitimately fail.** (1) build 0 errors and no new warning codes; (2)
`AstroCast.Tests` 2218/2218 and `AstroCast.UiTests` 68/68; (3) all seven panels
at one content width on glass under Helvetica, with the Medical text box and the
Chart/Transits/Electional field columns agreeing; (4) the WrapPanel screens,
especially Electional's 3+4. **The honest risk: `TranslatedPanels_
AllShareOneContentWidth` can fail on macOS if some panel's widest child wants
more than the 334 available**, because Helvetica measures wider than Segoe UI and
the inset removed 8 px of slack that the clamp used to hide. That would not be a
false alarm -- it would be the test reporting a panel that overflows its content
width, which the clamp was previously concealing by silently widening that panel.
Fixing it would mean narrowing the offending control, not widening the inset
back.

**Phase 4 Map slice -- LANDED (2026-08-09): the astrocartography
panel and the first head that draws a map**
> Branch `phase4/map-panel-20260808`, cut from the padding branch tip `74c497d`
> rather than from `master`, because the Map panel lives in the panel host and
> had to be laid out against the width that will actually ship (334) rather than
> the pre-fix clamp widths. `src/AstroCast/` diff vs master: **empty**,
> thirteenth slice running. Fast-forwarded onto `master` at `3dc57bd` -- six
> commits, three padding and three map, no merge -- and released as `v1.2.5`.
> Report: `D:\Code\map-and-dependency-landing-report.md`.
>
> Four files: the `Mapsui.Avalonia12` 5.1.0 reference, the panel markup
> (`MainWindow.xaml` 1719-1799 of the WPF head), the map window, and the window
> lifecycle seam in the main window's code-behind. Windows: build 0 errors,
> warning codes unchanged against the padding branch's baseline (`NU1903` x17 +
> `xUnit1031` x2, none new), `AstroCast.Tests` 2218/2218, `AstroCast.UiTests`
> **68 -> 73**. Report: `D:\Code\phase4-map-slice-report.md`.

**The smoke test's prediction held, and the graph was read rather than the
warning count.** `Mapsui.Avalonia12` 5.1.0 asks for Avalonia and Avalonia.Skia
**12.0.2** and for SkiaSharp **3.119.4-preview.1.1**. Direct references win, so
the resolved graph is Avalonia.Skia **12.1.0** and stable SkiaSharp **3.119.4**,
with Mapsui, Mapsui.Nts, Mapsui.Rendering.Skia and Mapsui.Tiling all at 5.1.0,
BruTile 6.0.0, NetTopologySuite 2.6.0, and `SkiaSharp.NativeAssets.macOS`
3.119.4 present for Phase 5. The `Avalonia.Skia` pin established in slice 1 is
what makes that true, and its comment now says so. Restore introduced no new
warning code and no `NU1605` downgrade.

**Almost none of the 503-line map window is framework-specific, and that is the
main thing to know before editing it.** The layer building, projection, tile
source, label placement, colour table and crossing-report summary all speak
Mapsui's own API, including Mapsui's `Color`, `Brush` and `Pen` rather than
either UI framework's, so they are a near-verbatim copy of the WPF head's. Only
five things differ: the `Window` base class, the absence of XAML, WPF's
`StatusBar` becoming a `Border`, `Dispatcher.Invoke` becoming
`Dispatcher.UIThread`, and switching off Mapsui's log overlay.

**The map window has no XAML, and `AVLN3001` is why.** An `.axaml` version was
written first and warned that a XAML resource is only reachable through the
runtime loader if its type has a **public parameterless constructor**; this
window requires a view model. The alternatives were a parameterless constructor
leaving the view-model field null, or suppressing the warning project-wide.
Building the two-row tree in code costs a dozen lines, keeps the field
non-nullable, holds the zero-new-warnings line, and follows
`ConfirmationWindow`, which is already this head's idiom for a secondary window
that takes constructor arguments.

**`LoggingWidget.ShowLoggingInMap` must be set to `No`.** It is a *static*
property on `Mapsui.Widgets.InfoWidgets.LoggingWidget` whose default is
`OnlyInDebugMode`, so without this line the map draws a translucent log overlay
across itself in every Debug build. Mapsui.Wpf 5.0.2 predates the widget, so the
WPF head needs no equivalent.

**`Mapsui.Styles` is imported unaliased and Avalonia's UI types are qualified
instead**, which is backwards for a UI file and deliberate: both namespaces
define `Color`, `Brush` and `Pen`; four hundred lines use Mapsui's three and four
lines in the constructor use Avalonia's. Importing Mapsui plainly is what keeps
the ported code identical to the original.

**Verified on glass, both heads driven end to end.** The Avalonia map renders
CartoDB Voyager tiles with Latin place names, the ACG lines in their legend
colours, labelled callouts, the gold birth marker over the birth location and the
CARTO attribution, with no log overlay: 1123 distinct colours sampled across the
map area, and the frame **inspected** rather than merely counted. A physical
click over western France produced `Crossing Report: 46.75°N, 1.84°E    No
angular lines within 3°.` in the status strip, so `Map.Tapped`,
`SphericalMercator.ToLonLat`, `UpdateCrossingReport` and the one-line summariser
all work. **The click had to be delivered out of the Claude container**, as
synthetic input from inside it never reaches the window: the same limitation the
Mapsui probe and the slice 3 `SendKeys` finding recorded.

**Exit criterion met:** the two heads' ACG exports are **64,701 bytes each, 3465
lines each, differing in exactly one line** (the wall-clock `Generated:` stamp),
with a uniformly CRLF census on both.

**One cosmetic difference recorded and left alone: identical `Width` and `Height`
produce different windows, because WPF sizes the OUTER window and Avalonia sizes
the CLIENT area.** Measured: WPF 1200x800 outer, Avalonia 1216x839 outer from the
same 1200x800 values, a 16x39 chrome difference, so the Avalonia map is slightly
larger and its labels sit a few pixels lower. Matching WPF exactly would mean
hard-coding its frame arithmetic into this head.

**Five tests added; three mutation-checked, each anchored to the attribute it
targets.** Commands are asserted by reference (mutation: aim Export ACG MD at
`CalculateAcgCommand`); all sixteen toggles are driven individually because
sixteen near-identical checkboxes is where a copy-paste binds two to one property
(mutation: bind Mars to `ShowVenus`); and `LegendColours_
MatchTheColoursTheMapDrawsWith` pins a **cross-file invariant nothing enforced
before**, since the panel's twelve `Foreground` values and
`AcgMapWindow.GetPlanetColor` are two hand-maintained copies of one colour table
whose only previous protection was a comment (mutation: move the Sun swatch one
bit, `#FFD700` to `#FFD701`). The gating test asserts both directions across
`HasResult`. `MapPanelRoot` also joins `PanelContentWidthTests`, now eight panels,
and `LabelColumnAlignmentTests` at **zero** form grids, following Medical.

**What the Mac must verify before landing:** build 0 errors with no new warning
codes; 2218/2218 and 73/73; the map window opens and renders tiles and lines
under Helvetica; a tap updates the status strip; and the ACG export matches this
head's at character parity. The Mapsui native payload is the thing to watch,
since `SkiaSharp.NativeAssets.macOS` and `HarfBuzzSharp.NativeAssets.macOS` are
already in the graph via Avalonia.Skia, so the marginal native cost should be
zero and only managed DLLs should be added.

**Landing record (2026-08-09): the padding and Map slices released together as
`v1.2.5`, and the four things worth carrying forward.** The Mac verified the
combined branch before the fast-forward: package graph clean (Avalonia.Skia
12.1.0, stable SkiaSharp 3.119.4, no prerelease anywhere), `AstroCast.Tests`
2218/2218 name-list-identical, `AstroCast.UiTests` 73/73, all eight panels at one
content width of 334.0, the expanded overlay bar clearing content exactly, the
Chart panel constant from 0 to 60 characters of name, the ACG export reconciling
to the byte (61,237 + 3,464 CRLF = 64,701), and no new native library on any
macOS RID.

1. **334 on both platforms, and it is not a coincidence -- but it is also not
   what the tests proved.** The content width resolves to exactly **334.0** under
   Helvetica as it does under Segoe UI, and the reason is arithmetic rather than
   luck: **334 = viewport 350 minus host margin 16**. Both terms are
   font-independent -- 350 is the panel host's viewport, 16 is the Fluent overlay
   scroll bar's own constant width -- so Helvetica's wider metrics have nothing to
   act on. The number could only have differed if some panel's widest child
   wanted more than 334, which is the honest risk the slice recorded, and no
   panel did. The tests remain correct in asserting nothing absolute: they pin
   that the panels agree with each other, that content clears the *measured* bar,
   and that Chart does not move with its data. **The figure agreeing across
   platforms and the assertions being relative are independent facts** -- the
   number agrees for a reason unrelated to the reason it might have differed, and
   an absolute assertion would still be pinning the wrong font on two platforms
   out of three. Corroboration from the other end of the same arithmetic: the
   Chart, Transits and Electional field columns measure **234** on both
   platforms, which is 334 minus the shared **100** label column.
2. **The window-sizing framework difference now has three data points, and it
   generalises.** Identical `Width`/`Height` markup produces **WPF 1200x800
   outer, Avalonia-on-Windows 1216x839 outer, Avalonia-on-macOS 1200x828 outer**,
   because **WPF sizes the outer window and Avalonia sizes the client area**,
   with the chrome added on top differing per platform: Windows adds 16x39, macOS
   adds 0x28 (no side borders, a shorter title bar). The Avalonia client area is
   1200x800 in both cases, exactly as asked. This is not a map-window quirk --
   **it applies to any future sized window in this head**, so a window whose size
   matters should be specified as the client area it wants, and any comparison
   against WPF's numbers should expect the chrome delta rather than treat it as a
   regression.
3. **`AVLN3001`: a XAML resource is reachable through Avalonia's runtime loader
   only if its type has a public parameterless constructor.** That is the general
   rule behind the map window having no `.axaml`, and it is now hit twice, so it
   is this head's standing idiom rather than a one-off: **a secondary window that
   takes constructor arguments gets a code-built tree** -- `ConfirmationWindow`
   first, `AcgMapWindow` now. The alternatives both cost something real: a public
   parameterless constructor leaves the view-model field null and nullable, and
   suppressing the warning project-wide gives up the zero-new-warnings line.
   A dozen lines of code-built tree is cheaper than either.
4. **The `Avalonia.Skia 12.1.0` pin is proven load-bearing on both platforms, and
   the lesson is to read the graph, never the warning count.**
   `Mapsui.Avalonia12` 5.1.0 asks for Avalonia.Skia **12.0.2** and SkiaSharp
   **3.119.4-preview.1.1**; the direct pin wins, and the resolved graph is
   Avalonia.Skia **12.1.0** with **stable** SkiaSharp **3.119.4** -- verified
   independently on Windows and on macOS. The trap is that **a downgraded restore
   emits zero warnings**: NuGet reports `NU1605` only when a *project* reference
   forces a package below a direct reference, not when a direct reference holds a
   transitive request down, so nothing in the build output would have said the
   renderer had split versions. Without the pin the slice would have silently
   resolved a preview SkiaSharp against a 12.1.0 Avalonia. Warning counts are
   evidence about warnings; only the resolved graph is evidence about versions.

**Phase 4 slice 9 -- LANDED (2026-08-10): the Returns panel, the last
of the nine, and a brief that was wrong about its size**
> Branch `phase4/slice9-returns-panel-20260809`, cut from `master` at `0063431`
> (`v1.2.6`). Three commits: translation, tests, docs. `src/AstroCast/` diff vs
> master: **empty**, fourteenth slice running; nothing under `AstroCast.Core` or
> `AstroCast.Presentation` either. Windows: build 0 errors, `xUnit1031` the only
> warning code and `NU1903` still absent, `AstroCast.Tests` 2218/2218 with a
> name list byte-identical to master, `AstroCast.UiTests` **73 -> 82**.
> Fast-forwarded onto `master` at `320da11` -- three commits, no merge -- and
> released as `v1.2.7`. Report: `D:\Code\phase4-slice9-report.md`; landing
> record below.

**The slice brief described five features; the panel has ten.** It was written
from a screenshot that ended at Great Conjunctions, and the five sections below
that point -- Synodic Cycles, Ingress Charts, Lunation Charts, Eclipse
Calculator, Mundane Aspect Finder -- were absent from it entirely. Measured
against the markup the panel holds **36 buttons, 11 export commands and TWELVE
cancel keys**, roughly double the brief's budget. The read-only inventory caught
it before any markup was written, which is the entire purpose of that checkpoint;
the line count in the brief (638) was exactly right, so the error was invisible
from the outside.

**Three binding roots, and they do not follow the visual sections.** "Include
snapshots" sits inside Planetary Return but binds `Cycles.IncludeCycleSnapshots`;
Great Conjunctions and Synodic Cycles are `Cycles`; Ingress, Lunation, Eclipse
and Mundane Aspects are `Mundane`. Four sections of ten are not `Returns.*`.

**Three option collections are `static`, and a compiled binding cannot reach a
static through an instance path.** `ReturnsViewModel.AvailableReturnPlanets`,
`ReturnsViewModel.AvailableReturnFindModes` and
`MundaneViewModel.AvailableIngressPlanets` need `{x:Static}`; the other three
collections on the same two view models are instance properties and bind
normally. **The split is per property, not per view model** -- the same trap
slice 2 hit on `ChartEntryViewModel`, now confirmed to recur wherever a view
model mixes the two.

**In Avalonia `ToggleButton` derives from `Button`; in WPF it derives from
`ButtonBase`.** So `OfType<Button>()` returns check boxes in this head and does
not in WPF, and a button census carried over from WPF thinking counts two too
many here. Worth knowing before writing any control sweep in a test.

**Wrap screening: twelve of twelve predictions held**, computed as
`14 x advance + 26` and confirmed against the running WPF head. The pattern is
*not* the uniform 2+1 the brief assumed -- six of twelve rows differ: two are
lone check boxes, Ingress is 2+2+2, Lunation 2+2+1, and Eclipse and Mundane
Aspects fit **3 across** because all three captions floor at the shared
`MinWidth="100"`. Lunation is the one scrollbar-conditional row (2+3 at 342), but
unconditionally 2+2+1 in practice because the panel is far too tall to retract
the bar in either head.

**The panel scrolls in both heads and that is parity, not a defect**: WPF 2027 px
and Avalonia 2241 px against a ~515 px viewport, roughly four screens each. The
head-to-head ratio measured 1.106, against 1.136 derived from the Transits panel
-- close enough to use for planning, not close enough to quote as a constant.

**All eleven exports reconcile.** Driven end to end in both heads with identical
inputs, compared after line-ending normalisation: byte counts identical pairwise,
CRLF/lone-LF censuses identical, and **every differing line is a wall-clock
stamp**. Note these exports carry the stamp **twice** -- a `**Generated:**`
header and a `*Generated by AstroCast ...*` footer -- and a batch export carries
one footer per chart, so the four-chart cardinal-ingress file differs on **five**
lines. "Exactly one differing line" is the wrong exit test for this panel.

**Two automation facts the next driver will need.** The WPF head's buttons are
**not uniquely addressable by name** here ("Export MD" x4, "Find Conjunctions"
x2, "Export Series MD" x2, "Cancel" x12), so a first-match lookup silently drives
the wrong section; and **on Windows both heads clamp the reported
`BoundingRectangle` of anything scrolled outside the viewport** -- corrected in
the landing record below, which found macOS does not do this -- so several
different buttons report one identical rect and any geometry-scoped lookup
finds nothing. The Avalonia head is
addressable by unique automation name; the WPF head has to be addressed by tree
ordinal, which is stable under scrolling and needs no names to be unique. Section
headers are not unique text either -- `MainViewModel`'s operation display names
reuse "Great Conjunctions", "Synodic Cycles" and "Lunation Charts".

**Landing record (2026-08-10): released as `v1.2.7`, and five things worth
carrying forward.** The Mac verified the branch before the fast-forward: build 0
errors, `AstroCast.Tests` **2218/2218** with a byte-identical name list,
`AstroCast.UiTests` **82/82**, content width **334.0** and field column **234.0**
unchanged, the governing label `Start Date` measuring 74.0 inside the shared 100
column -- **26.0 px of slack**, with `Progressions` still the global governor at
97.0 -- all eleven exports reconciling with every differing line a wall-clock
stamp, and both new date-picker glyph sites pixel-identical to known-good ones.
Windows: 0 errors with `xUnit1031` the only warning code and `NU1903` absent,
2218/2218, 82/82, twelve of twelve wrap predictions holding. One measured
deviation, accepted: row 11 wraps 2+1 on macOS against 3 across on Windows.

**Seven of the eleven exports reconcile across platforms to the byte, and neither
report claimed it.** It falls out of the two independently-measured tables:
subtract the Windows CRLF count from the Windows byte count and the macOS figure
is reached exactly.

| Feature | Windows | minus CRLF | macOS |
|---|---:|---:|---:|
| Solar Return | 25,025 | 24,869 | **24,869** |
| Lunar Return | 24,386 | 24,234 | **24,234** |
| Planetary Return | 26,369 | 26,206 | **26,206** |
| Cycle Timeline | 1,307 | 1,267 | **1,267** |
| Ingress Series | 723 | 704 | **704** |
| Cardinal Ingresses | 73,861 | 72,294 | **72,294** |
| Lunation Series | 2,982 | 2,931 | **2,931** |

That set includes the 72 KB four-chart batch and all three chart-based exports --
the most complex outputs in the panel. **The four that do not reconcile used
different inputs, not divergent code**: Great Conjunctions (2000-2030 against
1800-2100), Eclipses (2026-2028 against 2025-2027), Synodic Cycles and Mundane
Aspects -- exactly the four features with user-typed year ranges. So the panel's
eleven exporters are verified **cross-platform**, not merely self-consistent
within each head. **Method note: pin the inputs explicitly before comparing
exports across platforms**, precisely as the slice 5 date-range lesson required.

**Row 11 wraps differently on macOS, and it is accepted rather than fixed.**
`Eclipse Calculator` wraps **2+1 under Helvetica** against Windows' **3 across**,
over budget by **0.5 logical px**, because `Find Eclipses` is 3.0 px wider than
`Find Aspects` in that font. Same-head-different-platform wrap differences are
unavoidable once font metrics differ: identical wrapping under Helvetica and
Segoe UI would require pinning widths, which is exactly what this head has
deliberately never done. It belongs in the same category as the macOS-governed
100 px label column and Fluent's taller row pitch. Reclaiming 0.5 px would cost
either a per-row special case or a cross-cutting style change, for a cosmetic
gain on one row of twelve.

**The screening technique named the fragile row correctly.** Rows 11 and 12 were
called out before the Mac ran as the two most likely to break first, because
their three-across fit depended on every caption flooring at `MinWidth="100"`.
Row 11 broke. Right row, different outcome: the prediction was about which row
had no margin left, and that part held.

**The `Generated:` stamp is calculation-time, not export-time.** Two exports of
the same computed result are **byte-identical** -- verified with files written
12 s apart carrying the same stamp. The consequence is a trap for the
verification method itself: **any parity test that re-exports without
recalculating compares identical files and passes vacuously.** Add it beside
line-ending normalisation in the export-verification procedure -- **recalculate
before re-exporting**, or the comparison proves only that the export path is
deterministic, which was never in question.

**Two hazard refinements, recorded in `CLAUDE.md` as well.**

1. **A `DatePicker` under the pointer consumes wheel events and mutates its
   value.** An eighth member of the synthetic-input family, and a new form of
   it: not a displaced click, not an autocompleting field, but *scrolling*
   silently changing state. Choose scroll points over inert regions -- the
   label-column strip contains only `TextBlock`s and worked reliably.
2. **Setting a `CalendarDatePicker`'s inner `PART_TextBox` through the
   accessibility API does not commit the binding.** The text updates while
   `SelectedDate` stays null; the value must be typed and committed with Return.
   This refines the read-back rule into its sharper form: **read the control's
   semantic value, not its display text.**

**Two corrections to earlier records.**

1. **Off-screen rect clamping is Windows-specific.** The slice 9 Windows report
   recorded that *both heads* clamp the reported `BoundingRectangle` of controls
   scrolled outside the viewport. On macOS they do not: labels at y=2558 against
   a viewport ending at 1030 report their true, distinct positions. The claim
   above is corrected to name the platform. The behaviour is a property of
   Windows UI Automation, not of either UI framework, so a driver written
   against it does not port.
2. **`ToggleButton` derives from `Button` in Avalonia and from `ButtonBase` in
   WPF**, so `OfType<Button>()` returns check boxes here and not there, and a
   button census carried over from WPF thinking counts wrong. Recorded in the
   entry above and promoted to `CLAUDE.md` at this landing, because it governs
   any control sweep written against this head rather than only this panel.

**PHASE 4 COMPLETE (2026-08-10).** All nine panels -- Chart Entry, Export,
Synastry, Transits, Returns, Electional, Medical, Mundane/Cycles/Timeline and
Relocation -- plus the astrocartography map window are translated and verified on
both platforms, spanning `v1.0.238` (the first release containing Phase 4 work,
slice 0) through `v1.2.7`. Slice 0 is the only one that modified the WPF head,
and had to: making `IFileDialogService` and `IDialogService` async broke
`WpfFileDialogService` and `WpfDialogService`, which compile only on Windows.
**Every slice after it -- fourteen consecutively -- left `src/AstroCast/`
untouched.** The exit milestone, a fully functional macOS GUI build, is met.

What remains is **Phase 5** (macOS fit and finish + packaging: `IAppPaths` macOS
locations, `AstroCast.ico` to `.icns`, the `.app` bundle with `DOTNET_ROOT`
handled, code signing and notarization per Q7) and **Phase 6** (parity
verification and steady state). Separately, the port accumulated a set of
**product findings it was not permitted to fix** -- the dead medical
health-context input, `UseNatalLocationForRelocationCommand`'s enablement gap,
the two uncancellable UI-thread ephemeris calls, the Relocation panel's
descriptive text, the dead `AllSavedCharts` collection, unvalidated coordinate
entry, the WPF `Relationship` label clip, the Windows date-picker glyph, and the
duplicated map-drawing code. They are itemised in Section 9 and want a decision
on Q4 (WPF retirement) before several of them can be scheduled.

**Phase 5 -- macOS fit and finish + packaging -- COMPLETE 2026-08-10 at `v1.3.0`
(3-5 days, ~4-6 sessions)**
`IAppPaths` macOS locations; convert `AstroCast.ico` to `.icns`; write
`BuildMacBundle.ps1` (pwsh runs on macOS) that does `dotnet publish -r osx-arm64
--self-contained`, assembles `AstroCast.app` (Info.plist, icns, ephe in
Resources, dylib in MacOS/), codesigns, and optionally produces a DMG. Signing
tier per Q7.
*Exit: double-clickable AstroCast.app on the M5 Mac; charts saved to
Application Support; logs in ~/Library/Logs.* **Met, and exceeded** -- the exit
criterion asked only for a double-clickable bundle; what shipped is a
**notarized, stapled, Gatekeeper-accepted DMG**. Four slices: 5a, 5a.2, 5b, 5c;
see their landing records below. The packaging script's own documentation is in
`build/macos/SIGNING.md`.

**Phase 5 slices 5a + 5a.2 -- LANDED TOGETHER (2026-08-10) as `v1.2.8`: the
`.app` bundle, and the deployment-target floor it exposed**
> Two independent branches with **disjoint file sets**, landed as one release
> because both must precede signing: a signature seals `Contents/`, so any
> bundle or binary change afterwards invalidates the seal and forces a fresh
> notarization cycle. `phase5/slice5a-app-bundle-20260810` @ `44b3661`
> (`build/macos/make-bundle.sh` new, `.gitignore`) fast-forwarded first;
> `phase5/slice5a2-libswe-floor-20260810` @ `4e0251b`
> (`build/macos/build-libswe.sh` new, the rebuilt `libswe.dylib`, this document)
> could not fast-forward afterwards and was **rebased locally** onto `master` --
> clean, no conflicts, all three blobs byte-identical across the rebase -- then
> fast-forwarded as `6885e90`. `src/` diff vs master: **empty** for both. The
> version is `1.2.8` and not a docs-only bump because 5a.2 changes a binary that
> ships inside the app and broadens its supported range from macOS 26+ to macOS
> 15+, which is a real difference to what a user can run.
>
> 5a: self-contained publish and bundle; launches from Finder, resolves `ephe/`
> from `Contents/MacOS/`, loads 90 charts, calculates, and exports Markdown
> identical to `dotnet run` once the calculation timestamp is masked. Native file
> dialog, Mapsui map window and background ephemeris work all function inside the
> bundle. 5a.2: `minos` 26.0 -> 15.0, bundle maximum now 15.0.
>
> Windows verification of the **combination**, which had never been built: build
> 0 errors with `xUnit1031` the only warning code and `NU1903` absent (run
> `--no-incremental`, since an incremental build reports 0 warnings when nothing
> recompiles); `AstroCast.Tests` **2218/2218**; `AstroCast.UiTests` **82/82**;
> the test-name list byte-identical (sha256 `43EA6796...`, 237,222 bytes) to the
> baselines going back to `v1.2.1`; `swedll64.dll` unchanged (sha256
> `0AA962A2...`, still last touched by the original vendoring commit `a88a0a8`);
> and the WPF head listing its **90** charts and calculating -- status line
> `Loaded 'Andrew Windsor' -- 12 positions, 28 aspects. Offset: GMT (UTC+0).`
> Report: `D:\Code\phase5-bundle-landing-report.md`.

**The floor hid because the compiled code was correct.** The mechanism and the
fix are recorded in Section 3.2 above; what belongs here is why five phases ran
without noticing. `libswe.dylib` was compiled in Phase 1 with no
`-mmacosx-version-min`, so clang stamped the build machine's OS as the minimum.
dyld enforces that at load, and the library loads on the **first P/Invoke** --
so the app starts, draws its window, and then dies inside `EphemerisService`. A
crash after the UI appears, not a clean refusal, which is a much harder symptom
to attribute. The build was clean and all 2218 goldens passed, because none of
that exercises a load command. **Only a load-command census finds it.**

**The documentation drift is the part to carry forward.** The command that
actually produced the shipped dylib lived in a report *outside the repository*,
while the in-repo plan carried a different, flagless one. Anyone following the
tracked documentation would have faithfully recreated the defect. That is now
closed by making the build a script rather than a prose command:
`build/macos/build-libswe.sh` **verifies rather than trusts** -- `minos` matches
the requested target, the binary links libSystem only, at least 106 `swe_*`
symbols are exported, and `swe_version()` still reports 2.10.03. It is
deterministic, producing the same sha256 across runs, so the artefact can be
compared rather than assumed.

**The 48-byte equivalence proof, and why the matching size is what made it
possible.** The two dylibs are both exactly 564,704 bytes, so `cmp -l` could
attribute *every* differing byte rather than merely observing that they differ:
1 byte of `minos` major (`0x1a` -> `0x0f`), 15 bytes of recomputed `LC_UUID`,
32 bytes of the linker's ad-hoc signature blob, and **zero bytes of compiled
code**. Set beside the goldens this gives **two independent arguments**: the
2218 goldens prove the *output* is unchanged, and the byte diff proves the
*codegen* is unchanged. The second was only available because the sizes matched
-- had they differed by even one byte, the byte-level argument would have
collapsed into "recompilation changed something" and only the goldens would have
remained.

**Two corrections to earlier records.**

1. **"The dylib linked with zero undefined symbols" was never true.** `nm -u`
   reports **61**, all satisfied by libSystem -- `_malloc`, `_sin`, `_fopen`,
   `___stack_chk_fail` -- both before and after the rebuild, and any correct
   library would have them. The Phase 1 interim report's phrasing was loose for
   "linked cleanly". **The meaningful invariant is *what* is undefined (libSystem
   only), not that the count is zero**, and that is what
   `build/macos/build-libswe.sh` now asserts. The original phrasing lives in the
   Mac-side Phase 1 interim report, outside this repository and not reachable
   from Maple, so it is corrected here rather than in place; correct it there
   too if that report is ever revised.
2. **Slice 5a's Mach-O census was one binary short -- 21 reported, 22 actual.**
   `createdump` ships in the runtime with **no file extension** and slipped a
   `*.dylib` glob. It sits at 12.0, so 5a's conclusion about the floor was
   unaffected, but a census that misses binaries is precisely the shape of error
   that let the 26.0 floor hide in the first place. **Match on `file`-reported
   Mach-O content, never on an extension glob.**

**Two notes for the remaining Phase 5 slices.**

1. ~~**The floor attribution under a tie is arbitrary.** `make-bundle.sh` updates
   its recorded floor only on a *strictly greater* value, so with three binaries
   now level at 15.0 it names whichever it reached first. The floor itself is
   correct; the wording "set by libswe.dylib" now reads misleadingly. Worth a
   wording fix in 5b.~~ **Done in 5b**, along with the Mach-O census fix; it now
   reports `15.0 (of 22 Mach-O binaries; set jointly by 3)`.
2. **Avalonia exposes no `kAXPressAction` on macOS.** Of 390 elements in the
   window subtree, exactly **4** advertise any action at all -- the window's
   `AXRaise` and the three OS traffic-light buttons. The guarded-AX-press
   mitigation `CLAUDE.md` prefers is therefore **unavailable for this app**. The
   working split is **CGEvent to act, AX to verify**, which still satisfies
   "assert which control acted" using a semantic instrument rather than
   screenshots. Promoted to `CLAUDE.md`.

**Phase 5 slice 5b -- LANDED (2026-08-10) as `v1.2.9`: the application icon, and
a hybrid that turned out to be impossible**
> Branch `phase5/slice5b-icon-20260810` @ `ab7cdf3`, one commit, cut from
> `master` at `3fc35c3` (`v1.2.8`). Four paths, **all under `build/macos/`**:
> `icon/AstroCast-1024.png` (new), `icon/icongen.swift` (new), `make-icon.sh`
> (new), `make-bundle.sh` (modified). `src/` diff vs master: **empty**. Nothing
> Windows builds reads any of them, so Maple landed this without testing it --
> the value of the slice is in the record, not in the Windows run. Verified on
> the Mac: Dock at maximum tile size crisp and matching its neighbours in visual
> weight; Finder's largest icon view sharp; Get Info correct at version 1.2.8;
> `.icns` **2,049,215 bytes** with all ten required types; 2218/2218 and 82/82
> with byte-identical name lists; the bundle launches, loads 90 charts and
> computes; **no icon-cache refresh needed**, because `make-bundle.sh` removes
> and recreates the bundle, which invalidates the cache as a side effect.
> Report: `D:\Code\phase5-icon-landing-report.md`.

**The two icon sources are different renders, so the planned hybrid was
impossible.** The slice was designed around combining the `.ico`'s hand-tuned
small sizes with the 1024 master for everything else. They are **not one design
at two sizes.** Rendered at matched framing the differences are structural rather
than resampling artefacts: a different apex on the "A", a thinner orbit swoosh
sitting higher, the blue dot at a different clock position, a thinner outer rim,
and a different extent on the gold glow. **RMSE 37.29, maximum channel
difference 255.** Both files were exported on 2026-03-03, so these are **sibling
renders, not old versus new**.

Had the hybrid shipped, the icon would have **visibly changed design as the user
scaled it in Finder**. Measuring before adopting the plan's own recommendation is
what caught it.

**And the hybrid was unnecessary as well as impossible: the `.ico`'s small
entries are resampled, not hand-tuned.** Tested against a downscale of the
`.ico`'s *own* 256 entry, so the revision difference between the two sources
could not confound the result. The structure is identical at every size --
same silhouette, same star placement, nothing redrawn, no thickened strokes --
with only contrast rising as size falls, which is the signature of a sharpening
pass in the resampler. **At 16 px neither source is legible as an "A"**: the
artwork simply does not survive to that size in recognisable form, from any
source, so no choice between them could have fixed it.

**Framing: shipping macOS icons fill 83.6% of their canvas; the master filled
60.2%.** Measured across five system apps -- Safari, Maps, Podcasts, App Store,
Calculator -- **all within 0.2% of 83.6%**. The master's artwork occupies
616x607 within its 1024 canvas, so used unreframed it would have rendered about
**28% undersized beside every neighbour in the Dock**: subtly wrong without being
identifiable, which is the worst kind of visual defect to ship. The `.ico` had the
opposite problem at **90.2%**, bleeding edge-to-edge at 16 px.

Reframed to 83.6%. **The trade, accepted knowingly:** the 1024 entry needs
**1.39x magnification**. But each smaller size is resampled from the *original
crop* rather than from the magnified 1024, so **only `ic10` carries any
softness**. Both alternatives were worse -- omitting `ic10` makes Finder's
largest Retina view upscale 2x from `ic09`, and not reframing means permanent
undersizing at every size. **Measure shipping icons rather than trusting the
published grid.**

**Two AX findings that generalise, both promoted to `CLAUDE.md`.**

1. **Verify the effect, not the report.** Finder's AppleScript `icon size`
   property accepted 512, **reported 512 back on read**, and changed nothing:
   captures at 64 and 256 were byte-identical, while the real slider read **24 of
   112** through the accessibility API. This **inverts** the slice 9 rule ("read
   the semantic value, not the display text") -- there the display lied and the
   value was truthful; here the property lied and the display was truthful. The
   reconciliation is that **a property and a display are both claims; only the
   observable outcome is authoritative.**
2. **AX value-writes do not run handlers; AX actions do.** Setting `AXValue` on
   the slider did nothing while `AXIncrement` worked -- the same shape as the
   `CalendarDatePicker` `PART_TextBox` finding from slice 9. **Two instances now,
   so it is an idiom rather than a coincidence.**

**Two tool hazards.**

1. **CGEvent unicode typing silently stopped working mid-session.** Clicks still
   registered and fields still took focus, but **no characters arrived** and the
   log showed no activity; `System Events ... keystroke` worked immediately in
   the same state. This is the **ninth** member of the synthetic-input family,
   and it is **invisible without reading the field back after every write**.
2. **`sips` crops from the centre whatever `--cropOffset` is given**, and cannot
   pad with a transparent margin at all (`--padColor` takes an opaque hex
   colour). That is why the generator is Swift rather than shell -- the job is
   not expressible with what macOS ships.

**macOS 26 wraps classic `.icns` in a system container.** On Tahoe the circular
badge is presented inside an automatic light rounded square, both in the Dock and
in Finder's `.app` tile. **This is not a defect in the asset** -- the raw `.icns`
renders uncontained beside it. A layered Icon Composer `.icon` asset would
replace the container with authored artwork. That is design work, **recorded as a
possible future slice, not decided.**

**The `make-bundle.sh` floor-measurement fixes from the 5a.2 landing are both
done here.** It now matches Mach-O binaries **on content rather than by name**
(the `*.dylib` glob had been omitting `createdump`), and it **names all binaries
at the floor** rather than whichever it reached first, reporting
`15.0 (of 22 Mach-O binaries; set jointly by 3)` -- **matching the 5a.2 census
exactly**, which is the corroboration that the census fix landed correctly.

**Phase 5 slice 5c -- LANDED (2026-08-10) as `v1.3.0`: signing, notarization and
stapling, and the first artifact that can be handed to someone**
> Branch `phase5/slice5c-signing-20260810`, two commits: `d6a2474` the packaging
> script and entitlements, `68ca847` the `SIGNING.md` documentation that landed
> after it. Three new files, **all under `build/macos/`**:
> `package-macos.sh`, `AstroCast.entitlements`, `SIGNING.md`. `src/` and
> `tests/` diffs vs master: **empty**. Nothing from `dist/` was committed --
> verified per artifact rather than by trusting the pattern: no `.dmg`, `.zip`,
> `.pkg`, `.log` or submission-ID file anywhere in the tree, with
> `build/macos/dist/` covered by `.gitignore`. The script authenticates via
> `notarytool --keychain-profile`, so **no credential is committed**.
>
> Verified on the Mac: **app Accepted** (`38a69a41-fbc2-4f8f-8459-8921c2a54967`),
> stapled, `spctl -a -t exec` reporting `source=Notarized Developer ID`;
> **DMG Accepted** (`e7265640-270c-4ccd-9aae-0108f952d83e`), stapled, `spctl -a
> -t open` accepted. **Both submission IDs were re-confirmed against Apple's
> record with `notarytool info`, not merely against local files** -- a stapled
> ticket on disk proves what was stapled, not what Apple currently says.
> Artifact: `AstroCast-v1.2.9-macOS-arm64.dmg`, **68,469,335 bytes**, SHA-256
> `adbbb9f8...9942e3`. Report: `D:\Code\phase5-signing-landing-report.md`.
>
> **`1.3.0` rather than `1.2.10`.** This is the first release that can be handed
> to someone -- signed, notarized, stapled and accepted by Gatekeeper on a
> machine that has never seen the source. Every prior `1.2.x` was a developer
> artifact. That is a **capability threshold**, which is what a minor bump marks.
> `2.0.0` stays reserved for Avalonia replacing WPF on Windows, with the data
> migration that implies.

**278 nested code objects, not 21 -- the working predicate is LOCATION, not
content.** `codesign` seals `Contents/Resources/` as resources, but treats
everything in `Contents/MacOS/` **other than the main executable** as nested code
requiring its own signature. A self-contained .NET publish lands its entire
payload flat in `Contents/MacOS/`: **226 managed assemblies, 20 native dylibs,
`createdump`, 18 fonts, 6 `.se1` ephemeris data files and 7 miscellaneous --
278 objects.** The reference procedure's Mach-O-only loop selects **21** and
misses **257**, after which the bundle refuses to sign one complaint at a time,
which reads as a sequence of unrelated faults rather than one wrong predicate.

**Independent confirmation from the artifact, reached from the other direction.**
The signed bundle carries exactly **257 `com.apple.cs.CodeDirectory` extended
attributes** -- one per non-Mach-O object -- while the other **21** are Mach-O
with signatures embedded in the binary. **21 + 257 = 278**, reconciling the
extended-attribute census against the file census by two independent routes.

**The `xattr` asymmetry: clear before signing, never after.** `codesign` cannot
embed a signature into a PE or data file, so it stores one in an extended
attribute and **leaves the file's bytes untouched** -- verified three ways: the
signed file, the signed file with xattrs cleared, and the unsigned original are
all **75,776 bytes with identical sha256**. Those attributes survive the `ditto`
round-trip and the DMG. **Therefore a post-signing `xattr -cr` silently strips
hundreds of signatures and leaves a bundle that looks signed and is not.**

Record the red herring with it: **`xattr -cr` leaves `com.apple.provenance`
behind** -- **287 on the unsigned bundle, 290 on the signed one**. It is
system-protected, `xattr -d` no-ops on it, and it is benign here because the
three attributes `codesign` actually rejects (`FinderInfo`, `ResourceFork`,
`quarantine`) were all **zero**. **State which bundle each count belongs to**: an
unqualified figure invites a future run to find a mismatch and chase a problem
that is not there.

**Entitlements: `allow-jit` alone, and the reason is connected to the signing
loop.** The reference's §10.3 offers three plausible keys for a .NET app; only
`allow-jit` was needed, and it is not optional for any JIT-compiling runtime
because the CoreCLR JIT writes native code into a page and then executes it.
**Library validation passes without `disable-library-validation` precisely
because the inside-out loop signs every nested binary with the same Team ID.**
Had any nested binary been left unsigned or foreign-signed, that third key would
have become necessary. **The two facts are linked** -- the minimal entitlement
set is a consequence of the complete signing pass, not an independent discovery,
and weakening one would force the other.

**The staple bug was ours, not the reference's** -- corrected during
re-verification and recorded in the corrected form. The 5c report initially
framed submit-target != staple-target as a gap in the reference. **It is not:**
§6 Stage E correctly submits the zip and staples the `.app`. The defect was
introduced by folding submit-and-staple into **one helper shared by both
artifacts** -- right for a DMG, wrong for an app, which uploads as a zip because
`notarytool` needs a container while `stapler` refuses zips. Kept as a **porting
warning rather than deleted**, because the refactor is the natural thing to do:
anyone porting this sees two near-identical blocks and unifies them.

**Three diagnostic traps.**

1. **The error is emitted by `sign`, not `verify`** -- the text is identical, and
   the natural reading is the wrong one.
2. **`codesign -dv` cannot probe scope.** It reports "not signed at all" for any
   unsigned file regardless of whether a signature would ever be *required*
   there, so it answers a different question than the one being asked. Only
   signing the bundle answers the real one.
3. **App Translocation.** A downloaded app runs from a randomised read-only path,
   so a liveness check that searches the original path finds nothing. **Verify
   the effect, not the path you expected it at** -- the same shape as the 5b
   Finder-slider finding.

**What success looks like at the download path.** This is the only test that
exercises what a user actually receives. One `CoreServicesUIAgent` consent dialog
whose text includes **"Apple checked it for malicious software and none was
detected"**, with an **Open** button -- against an un-notarized app's "cannot be
opened because Apple cannot check it" with **no** Open button. **A consent dialog
means notarization worked; a block means it did not.** Then App Translocation,
`ephe/` resolving inside the translocated bundle, the database found at the real
user location, **90 charts**, and **53 electional charts computed off the
read-only volume** -- which matters because `swe_set_ephe_path` only *stores* a
path, so **only a real calculation proves the `.se1` files are readable there.**

**Documentation placement, and why it generalises.** `build/macos/SIGNING.md`
sits in the repository **beside the script it describes**, not in
`~/astrocast-mac-setup/`. The reference procedure lives outside version control
and is unreachable from Maple -- **the same shape of gap that let the macOS 26
deployment-target floor ship unnoticed**, when the working `libswe` command
existed only in an out-of-repo report while the tracked plan carried a flagless
one. Nobody was wrong in either case; **the authoritative copy simply was not the
one in the repository.** `APPLE_NOTARIZATION.md` was **not** replaced -- its
§§1-9 remain verified for two other applications -- and received a dated addendum
instead.

**PHASE 5 COMPLETE (2026-08-10).** 5a (the `.app` bundle) -> 5a.2 (the
deployment-target floor) -> 5b (the icon) -> 5c (signing and notarization),
spanning `v1.2.8` through `v1.3.0`. The application is packaged, its supported
range is macOS 15+, it carries its icon, and it is signed, notarized and stapled.

What remains is **Phase 6** (parity verification and steady state) and the
**eleven live Section 9 findings** -- items 4 and 6-12, 14-16 -- **none of which
the port was permitted to fix.** (Items 1 and 2 are struck and 13 is resolved;
item 3 is a Linux-naming note closed as a side effect of Phase 1's resolver, and
item 5 is documentation drift, so neither is counted among the eleven. Item 15 is
a tooling gap recorded alongside the product defects rather than being one.)

**Landing record (2026-10-01): the station-classification fix as the first
commits of 1.3.1, and the installer fix rebased and landed behind it**
> `fix/station-classification-20261001` -- three commits, `2420482` -> `7db185a`
> -> `3bb5aff`, cut from `87f369a` (`v1.3.0`) -- fast-forwarded onto `master` at
> `3bb5aff`; the version bump 1.3.0 -> 1.3.1 followed as `748765b` (eight
> csprojs, 32 bytes, both heads' `AstroCast.Presentation.dll` reading 1.3.1.0 by
> reflection). Windows: build 0 errors with `xUnit1031` the only warning code and
> `NU1903` absent; `AstroCast.Tests` **2218 -> 2248** (three names removed, 33
> added, all in `StationFinderTests`, `StationServiceTests`,
> `ChartMarkdownFormatterTests` and `ChartJsonFormatterTests`); `AstroCast.UiTests`
> 82/82 with a byte-identical name list; each of the three commits green on its
> own (2243, 2248, 2248); the regenerated Synastry golden with its 1970 Los
> Angeles local time, and the DST and explicit-offset station tests, pass against
> Windows time-zone data. **Not tagged**: `v1.3.1` waits for the refranation
> branch. Report: `D:\Code\station-fix-maple-report.md`.
>
> `release/msi-1.3.0-20260810` -- the recursive-harvest, self-contained installer
> fix committed on 2026-08-10 as `ec85d12` and audited FINISHED
> (`D:\Code\msi-branch-audit-report.md`: the MSI it describes is the build
> installed and in daily use on Maple) -- was rebased onto `748765b` as `6ddb52a`
> (identical stable patch-id, author and message preserved), given this
> documentation commit, and fast-forwarded onto `master`. **No bump**: master was
> already 1.3.1 against tag `v1.3.0`, so CLAUDE.md's rule did not fire. Verified
> on Maple on the final tree: build 0 errors with `xUnit1031` the only code,
> 2248/2248 and 82/82 with name lists identical to `748765b`, and one
> `BuildInstaller.ps1` run producing `AstroCast_1.3.1.msi` -- ProductVersion
> 1.3.1, UpgradeCode unchanged, 471 files in the audited split (289 root / 7 ephe
> / 19 LatoFont / 13 cultures x 12), the runtime inside, the downgrade guard the
> only launch condition -- then deleted as a pre-release artifact. Report:
> `D:\Code\msi-landing-report.md`.
>
> **The `v1.3.0` packaging note.** The 1.3.0 Windows MSI installed on Maple
> (`dist\AstroCast_1.3.0.msi`, 73,827,000 bytes, SHA-256 `E79F2E66...1385DC2`,
> byte-identical to the Windows Installer cache copy) was built on 2026-08-10 from
> the working tree later committed as `ec85d12`; its binaries carry the `87f369a`
> source stamp because that commit changes no compiled source. Rebuilding from
> the `v1.3.0` tree produces a **different and defective** MSI: the flat harvest
> (296 files, no Lato fonts, no satellite assemblies), framework-dependent,
> written to the OneDrive Desktop. `v1.3.0` stays where it is -- tags are
> immutable here, as `v1.0.242` already established -- and `v1.3.1` will be the
> first tag whose tree reproduces the shipped packaging.

**Landing record (2026-10-01, later): the horary perfection-timing fix, closing
release 1.3.1 as `v1.3.1`**
> `fix/horary-perfection-timing-20261001`, five commits, cut from `c13a177`:
> `6738067` (the first-approach, exact-aspect and station-window finders),
> `400ecc0` (the perfection analysis moved into the engine and rendered from the
> result -- behaviour-neutral, all 19 categorised horary charts byte-identical),
> `2fad12f` (the new rules: perfection now requires completion, refranation is
> event-based, prohibition is time-based against either significator),
> `81ba330` (the CLAUDE.md count), and `87e2ea6` (fixup). `2fad12f` through
> `81ba330` carry a `CS8629` warning at `HoraryPerfectionAnalyzer.cs(647,72)` that
> Maple's warning gate caught on the first landing attempt; `87e2ea6` clears it
> with a `PerfectionInstant()` local function (`?? throw`), no null-forgiving
> operator. Fast-forwarded onto `master` at `87e2ea6`; this record follows it as
> the release head.
>
> Evidence from the Mac: the new rules agree with the independent harness on all
> 19 real charts and all 158,550 sweep cases (0 mismatches); no headline judgment
> changed on the real charts; 5.80% of formerly "Perfected" direct aspects now
> fail to complete; `AstroCast.Tests` **2248 -> 2294** (one name removed, 47
> added: `AspectPerfectionFinderTests` 27, `StationWindowFinderTests` 10,
> `HoraryPerfectionTimingTests` 8, `HoraryPerfectionAnalyzerTests` 2); no golden
> changed. Windows (Maple): build 0 errors with `xUnit1031` the only warning code
> at the branch head (`CS8629` absent); 2294/2294 and 82/82 with the eight timing
> tests and the Id 96 horary's stationing test passing against Windows time-zone data;
> each of the five commits green on its own (2284, 2284, 2294, 2294, 2294). Report:
> `D:\Code\horary-maple-report.md`.
>
> **Release 1.3.1** = the station-classification fix (`3bb5aff`, bump `748765b`) +
> the installer fix (`6ddb52a`, docs `c13a177`) + this horary fix, tagged
> **`v1.3.1`** on this commit -- the release head, per the tag-placement rule --
> and the first tag whose tree reproduces the shipped Windows packaging.

**Landing record (2026-10-02): branch A, the time-correctness fix, opening
release 1.3.2**
> `fix/time-correctness-20261002`, four commits, cut from `26b5d11` (`v1.3.1`):
> `7c24185` (the golden normaliser masks only the two Generated stamp lines --
> the `**Generated:**` header and the `*Generated by ... at ...*` footer -- so
> the 23 computed times it had hidden in five goldens are guarded to the
> second), `cb0c9c6` (the twelve JD-to-DateTime converters that truncated --
> `TransitEphemerisHelper.JulianDayToDateTime` and eleven private copies in the
> return, timeline, ingress, eclipse, lunation, great-conjunction, synodic and
> mundane services -- now round through `JulianDayUtility.ToDateTimeUtc`, and
> the six chart-casting services cast their charts at the rounded instant),
> `23fb053` (a stored chart time is read and written as the wall clock written:
> `ChartMapper.ParseStoredLocalDateTime` ignores the saving machine's offset
> stamp for `ToInput` and `ChartEntryViewModel.PopulateFrom` alike, and
> `ToEntity` writes no offset) and `3e7b51c` (CLAUDE.md). Fast-forwarded onto
> `master` at `3e7b51c`.
>
> Evidence from the Mac: the 129 regenerated real exports are byte-identical to
> the investigation's rounding simulation; 0 of the 95 saved charts shift in any
> of five machine zones (58 shifted before); the 20 categorised horary charts
> render byte-identically; seven goldens regenerated (Lunar, Planetary and Solar
> Return, Relocation, Transit, Transit Timeline, Return/Ingress Timeline);
> `AstroCast.Tests` **2294 -> 2304** (none removed, ten added:
> `EventTimeRoundingTests` 4, `ChartMapperTests` 4, `ChartEntryViewModelTests`
> 1, `GoldenFormatterTestBaseSelfTests` 1). Windows (Maple): build 0 errors with
> `xUnit1031` the only warning code; 2304/2304 and 82/82, with the four rounding
> tests, the five stored-time tests, the two mask self-tests and the seven
> regenerated goldens passing against Windows time-zone data; each of the four
> commits green on its own (2295, 2299, 2304, 2304). Report:
> `D:\Code\time-fix-maple-report.md`.
>
> **This landing opens release 1.3.2.** The version bump 1.3.1 -> 1.3.2 lands
> with it as `e36c573` (eight csprojs, 32 bytes, both heads'
> `AstroCast.Presentation.dll` reading 1.3.2.0 by reflection, 2304/2304 and
> 82/82 after it), and this record follows the bump. The tag does not: `v1.3.2`
> follows the remaining 1.3.2 branches (hygiene, Moon scan, horary rules).

**Landing record (2026-10-02, later): branch D, the horary hygiene, part of
release 1.3.2**
> `fix/horary-hygiene-20261002`, five commits, cut from `7da3953`: `aa8d71a`
> (`FormatLocalTime` moved unchanged from `ChartMarkdownFormatter` to a new
> internal value formatter, `ChartTimeFormatter`, so the horary analyzer no
> longer reaches into a document formatter -- the rule it settles, now in
> CLAUDE.md: services may call the value formatters in `Core/Formatting`
> (`PositionFormatter`, `ChartTimeFormatter`), never a document formatter;
> output byte-identical on 41 horary renders), `77c8f07` (a failed
> application's turnaround refined between the hourly samples to the second
> and printed to the minute -- within 16.4 s of an independent minimum across
> 653 sweep cases, mean 3.9 s, where the hourly grid was up to 29.7 min out;
> outcome, closest orb and the refranation window unchanged; the analyzer's two
> remaining `!` reads become non-nullable locals that throw
> `InvalidOperationException`, and that exception is documented on `Analyze`,
> `ChartEngine.Calculate` / `CalculateForElectional` and `IChartEngine`),
> `357fca1` (a chart neither perfected nor denied names the failed or
> unresolved application in its judgment note; an explicit-offset chart's
> Date/Time header carries the offset label, not the zone name -- four golden
> header lines, reviewed: Lunar, Planetary and Solar Return
> `(America/New_York)` -> `UTC+0 (explicit)`, Relocation `(Etc/UTC)` ->
> `UTC+0 (explicit)`), `1588a11` (the analyzer fixture's four inconsistent
> times made one instant; the 120-day application cap covered by an analyzer
> and a formatter test) and `1f249a3` (CLAUDE.md, and a `BuildInstaller.ps1`
> comment). Fast-forwarded onto `master` at `1f249a3`; this record follows it.
>
> `AstroCast.Tests` **2304 -> 2309** (none removed, five added:
> `HoraryPerfectionTimingTests` 1, `HoraryPerfectionAnalyzerTests` 3,
> `ChartMarkdownFormatterTests` 1); four goldens changed, one header line each.
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2309/2309 and 82/82, with the demo-chart turnaround test (`America/New_York`),
> the explicit-offset header test and the four changed goldens passing against
> Windows time-zone data; each of the five commits green on its own (2304,
> 2305, 2307, 2309, 2309). Report: `D:\Code\hygiene-maple-report.md`.
>
> **Part of release 1.3.2.** No bump: master was already 1.3.2 against tag
> `v1.3.1`, so CLAUDE.md's rule did not fire. The tag waits: `v1.3.2` follows the
> Moon-scan and horary-rules branches.

**Landing record (2026-10-02, evening): branch B, the Moon's targets move, part
of release 1.3.2**
> `fix/moon-scan-motion-20261002`, three commits, cut from `92c2087`: `a89d090`
> (three engine-level tests through the real ephemeris, pinning instants where
> held targets give the wrong Moon -- retrograde Jupiter's square, which the held
> positions put behind Mars's; a void the scan missed; a false void -- each
> skipped with a reason the fix removes), `47e5680` (the fix: the Moon's first
> applying aspect and void of course are computed with moving targets.
> `GetMoonCondition` takes the ephemeris and the chart's Julian Day by method
> injection, with no constructor change; the window ends at the Moon's sign exit,
> found by bisection on its longitude;
> `AspectPerfectionFinder.FindFirstExactMajorAspect` finds the first exact aspect
> to each target inside it, and the earliest across the targets is the answer.
> The geometric scan survives as
> `GetMoonConditionWithFixedTargets` for composite charts, whose midpoints have no
> motion, and the 36 position-only tests call it by that name, unchanged. Two
> synthetic sign-exit tests and a null-ephemeris guard are added) and `c9e5dd1`
> (CLAUDE.md, and the `MoonConditionInfo` doc comments). The definition, the body
> set and the output are unchanged: the first exact major Ptolemaic aspect before
> the sign exit, no orb limit, none meaning void of course; the same
> `MoonConditionInfo` fields, so no formatter changed. Fast-forwarded onto
> `master` at `c9e5dd1`; this record follows it.
>
> Evidence from the Mac, against the horary review's independent moving-body
> truth: 0 mismatches on all 95 saved charts and on the 2000-2030 sweep's 22,646
> instants, where the fixed-target scan had 501 first-applying and 125
> void-of-course mismatches (2.21% and 0.55%); the 20 categorised horary charts
> byte-identical; no golden touched; about 0.46 ms per chart, and the one-year
> electional search 426.7 -> 451.8 ms (+5.9%), inside the 10% budget, so no shared
> sampling pass was needed. `AstroCast.Tests` **2309 -> 2315** (none removed or
> renamed; six added: `MoonConditionMovingTargetsTests` 3,
> `MoonConditionServiceTests` 3). Windows (Maple): build 0 errors with `xUnit1031`
> the only warning code; 2315/2315 and 82/82, with the three moving-target tests
> passing on the real ephemeris and all 22 golden tests passing unchanged; each
> commit green on its own (2309 passed + 3 skipped of 2312, then 2315, 2315).
> Report: `D:\Code\moon-scan-maple-report.md`.
>
> **Part of release 1.3.2.** No bump: master was already 1.3.2 against tag
> `v1.3.1`. The tag waits: `v1.3.2` follows branch C (the horary rules).

**Landing record (2026-10-03): branch C, the horary rules, completing release
1.3.2**
> `fix/horary-rules-20261002`, six commits, cut from `f349821`: `3b38c49`
> (H1/H8/H5), `198ceaa` (H2/H4/H6), `20c46d6` (H7/H9), `bad5088` (A1),
> `537c3ca` (CLAUDE.md and the interpretation reference) and `5aaca52` (the
> direction of one reception clause). Fast-forwarded onto `master` at
> `5aaca52`; this record follows it. The rules:
> - only the traditional seven translate or collect light; Uranus, Neptune,
>   Pluto, Chiron and the True Node never do;
> - a translation or collection counts only if every application it relies on
>   completes on its first approach, and prohibition is judged against its
>   completion (the last application), not its start;
> - mutual reception perfects only by domicile, exaltation or mixed
>   domicile-exaltation -- never triplicity -- and the Reception line names
>   the type;
> - an antiscion or contra-antiscion perfects only while applying; a
>   separating one is testimony;
> - a direct aspect perfects only if exact before either significator leaves
>   its sign, unless L1 and Lx receive each other by domicile or exaltation;
> - reception is judged both ways: one direction favoured and the other
>   hostile is Mixed, each hostile direction worded from its own side (Lx in
>   L1's detriment or fall: "the quesited is averse to the querent");
> - a retrograde translator or collector is named in the Notes;
> - an unrecognised question category still takes the 7th house, and a note
>   says so;
> - a significator's station after the turnaround but before the linear
>   perfection estimate is a refranation.
>
> Evidence from the Mac: every rule agrees with the harness's independent
> simulation with 0 mismatches on the 20 real charts, the 674-chart synthetic
> set and the 158,550-case forced-pair sweep; exactly two judgments change --
> Id 3 (Uncertain -> No; Chiron cannot collect) and Id 8 (Yes -> Uncertain;
> the sextile is exact only after both change sign); no golden touched.
> `AstroCast.Tests` **2315 -> 2330** (none removed or renamed; fifteen added,
> all in `HoraryPerfectionAnalyzerTests` and `HoraryPerfectionTimingTests`,
> 5 + 5 + 3 + 2 by commit). Windows (Maple): build 0 errors with `xUnit1031`
> the only warning code; 2330/2330 and 82/82, with all sixteen
> `HoraryPerfectionTimingTests` (the new real-chart fixtures and the
> demonstration-chart refranation among them) and all 22 golden tests passing
> against Windows time-zone data; each of the six commits green on its own
> (2320, 2325, 2328, 2330, 2330, 2330). Report:
> `D:\Code\horary-rules-maple-report.md`.
>
> **Release 1.3.2** = branch A (time correctness, bump `e36c573`) + branch D
> (horary hygiene) + branch B (the Moon scan with moving targets) + this
> branch C (the horary rules), tagged **`v1.3.2`** on this commit -- the
> release head, per the tag-placement rule.

**Landing record (2026-10-04): branch F1, sect and conditions, opening release
1.3.3**
> `fix/sect-conditions-20261004`, three commits, cut from `371a8bf` (`v1.3.2`):
> `ef47564` (Mercury's sect is decided by zodiacal longitude, and the
> comparison is corrected: behind the Sun in the zodiac -- rising before it,
> a morning star -- is diurnal; ahead of it, an evening star, nocturnal;
> ruling D8, finding F4-1), `85519d5` ("Applying benefic aspect" only for an
> applying trine or sextile from Jupiter or Venus, "Applying malefic aspect"
> only for an applying square or opposition from Mars or Saturn, each flag
> scored once however many aspects qualify; ruling D12, finding F4-2) and
> `9e4dfc5` (the `PlanetCondition` model docs, the interpretation reference's
> Mercury line and CLAUDE.md's test counts). Fast-forwarded onto `master` at
> `9e4dfc5`.
>
> Evidence from the Mac: the golden changes equal the predicted table cell for
> cell -- 19/19 in 13 goldens for the sect rule, 5/12 in 6 for the flags; the
> 95 saved charts change only as predicted; the 20 Horary Perfection Analyses
> are byte-identical; Mercury's sect matches the longitude rule with 0
> mismatches on the 95 saved charts and the 9,979 sweep charts, whose 27
> residuals are F3-1 applying labels, fixed by branch F3. `AstroCast.Tests`
> **2330 -> 2349** (two renamed; 21 added: `SectServiceTests` 7, the two
> renames among them, `PlanetConditionServiceTests` 10, eight of them theory
> cases, `SectAndConditionsIntegrationTests` 4 on the real ephemeris).
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2349/2349 and 82/82, with the four integration tests (charts 55 and 96) and
> the 13 regenerated goldens passing against Windows data; each of the three
> commits green on its own (2337, 2349, 2349). Report:
> `D:\Code\f1-sect-maple-report.md`.
>
> **This landing opens release 1.3.3.** The version bump 1.3.2 -> 1.3.3 lands
> with it as `20ffcf9` (eight csprojs, 32 bytes, both heads'
> `AstroCast.Presentation.dll` reading 1.3.3.0 by reflection, 2349/2349 and
> 82/82 after it), and this record follows the bump. The tag does not:
> `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-04, later): the bundle branch F2/K1/L1 -- Difda,
relocation house system, composite dignities -- part of release 1.3.3**
> `fix/stars-reloc-composite-20261004`, four commits, cut from `1d68d59`:
> `a2395a9` (F2: Difda is computed at beta Ceti, not iota Ceti -- the catalog
> asks the library for "Difda" rather than "Deneb Kaitos", which resolved to
> `Deneb Kaitos,ioCet`; finding F7-1), `34f3a75` (K1: a relocated chart keeps
> the natal chart's house system instead of falling back to Placidus;
> findings Y3-1 and Y3-5), `6977f0f` (L1: composite dignities include
> triplicity and face; finding Y2-6) and `eefc92b` (CLAUDE.md's test total).
> Fast-forwarded onto `master` at `eefc92b`; this record follows it.
>
> Evidence from the Mac: goldens 1/0, 3/0 and 1/0 (Composite, Electional,
> Lunar Return), Difda rows only; 62 of the 95 saved charts changed, only in
> their Fixed Stars sections, exactly as F7-1 lists; every star on them
> matches independent truth (Tier B 498 rows, 0 differences; Tier C 492 rows,
> largest 0.64"); the 33 standard relocations of the Equal and Regiomontanus
> charts exact (33 wrong before); 7 of the 9 polar cases now return, the
> other 2 (chart 17 at 78 N and 78 S) failing on the planetary-hour sunrise
> (finding F9-3, branch F4); every Placidus relocation byte-identical;
> composite dignity labels 0 differences from Lilly's table on 160 pairs (215
> before), including two Venus rows in the real composites that become
> Triplicity by day. `AstroCast.Tests` **2349 -> 2355** (one theory case
> replaced -- Difda's expected Swiss Ephemeris name "Deneb Kaitos" -> "Difda"
> -- and six more added: `FixedStarCatalogResolutionTests` 2,
> `RelocationServiceTests` 3, `CompositeServiceTests` 1).
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2355/2355 and 82/82, with the seven added tests and the three regenerated
> goldens passing against Windows data -- the catalog resolution test on the
> Windows native library and Windows' CRLF copy of `ephe\sefstars.txt`; each
> of the four commits green on its own (2351, 2354, 2355, 2355). Report:
> `D:\Code\bundle-f2k1l1-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`, so CLAUDE.md's rule did not fire. The tag waits: `v1.3.3` follows
> the last 1.3.3 branch.

**Landing record (2026-10-04, evening): branch F3, applying or separating by
the orb's rate at the instant, part of release 1.3.3**
> `fix/applying-20261004`, five commits, cut from `5c456e2`: `7248bd9` (the
> rule and chart aspects: a new `AstroCast.Core.Utilities.AspectDirection`
> decides applying or separating for every caller -- an aspect is applying
> when its orb is shrinking at the instant, from the bodies' speeds in
> longitude, with the Ascendant and Midheaven held still (ruling D5,
> unchanged); it replaces `AspectService`'s 0.01-day look-ahead, and
> composites follow it by their own averaged speeds; finding F3-1),
> `187f826` (transits: the orb's direction at the report's start by the
> transiting body's speed, not 12:00 UT against 24 hours later; T1-3),
> `17bc8ee` (progressions and solar arcs by the rate at the instant, not a
> 0.01-day or 0.01-degree step; T3-3), `500ab60` (CLAUDE.md: the rule, and
> the test total) and `51e90ae` (the `IAspectService` docs and the
> reference's D5 sentence). Fast-forwarded onto `master` at `51e90ae`; this
> record follows it.
>
> Evidence from the Mac: the 95 saved charts change by exactly 5 labels
> (charts 13, 44, 59, 76, 94) and chart 13's profection line, which quotes
> one of them; Planet Conditions and all 20 Horary Perfection Analyses
> byte-identical; 0 mismatches against Tier B in 2,288 + 934 chart labels,
> 254,628 + 94,587 sweep labels (392 + 97 before), 37,936 timing labels (141
> before) and 5,717 composite labels judged by the composite's own motion
> (2,713 before); F1's 27 sweep residual flags resolved; the composite
> golden 18/18, Ap/Sp cells only. `AstroCast.Tests` **2355 -> 2583** (none
> removed; 228 added, 207 of them theory cases: 99 + 99 for the 2026
> Sun-Moon aspects, 2 for the reversal cases, 7 for the `AspectDirection`
> argument theory). The 2026 Sun-Moon theory holds 99 aspects; the audit's
> 58 was a truncated scan. `MoonConditionEvaluator` keeps its own look-ahead
> until branch P.
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2583/2583 and 82/82, with every added test -- both Sun-Moon theories on
> the Windows native library -- and the composite golden passing against
> Windows data; each of the five commits green on its own (2576, 2579,
> 2583, 2583, 2583). Report: `D:\Code\f3-applying-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-05): branch O-1, culture-independent text in Core,
part of release 1.3.3**
> `fix/culture-20261004`, three commits, cut from `acfc107`: `6e87c83`
> (Core's own strings: every number and date a Core service or model formats
> into a string it stores on a result -- the offset label, the paran,
> station, antiscia, midpoint and house-change labels, the horary notes and
> denial reasons, the profection, firdaria and releasing summaries, the
> return, lunation, conjunction and election chart names, the progressed and
> solar-arc positions -- and the two value formatters the services call),
> `7e3e1a7` (the formatters: every Markdown export, the chart PDF and the
> chart JSON, one invariant field per formatter class) and `da45681`
> (CLAUDE.md: the rule, and the test total). Core now formats every number,
> date and time it writes into an export or a result string with the
> invariant culture, integers included, and never sets a thread's culture,
> so the screen may still follow the user's settings. The electional weights
> keep "25%" through an invariant format whose percent pattern is "n%"
> (findings E2-1, E4-1; ruling O-D4, option a). `CultureScope` runs a test
> under a named culture for that test's own flow only. Fast-forwarded onto
> `master` at `da45681`; this record follows it.
>
> Evidence: 393 source lines in 48 Core files; 344 outputs per culture (the
> chart Markdown, JSON and PDF text of the 95 saved charts, 34 harness jobs,
> 25 real exports) byte-identical under de-DE, sv-SE and th-TH to en-US, and
> en-US byte-identical to before; no golden changed; the whole suite with the
> process set to each of the three cultures 0 failures, on the Mac and on
> Windows (38, 38 and 53 before, on both). `AstroCast.Tests` **2583 -> 2783**
> (none removed; 200 added: the 5 `CultureScopeTests` facts and 195 theory
> cases, 65 `..._UnderCulture` tests each under de-DE, sv-SE and th-TH).
> Left as screen and log text, not exports: 33 exception messages with a
> number in them, 11 ordinal-safe `StartsWith` calls, 2 `char.ToUpper` and 2
> default-comparer sorts. The offset box's parsing stays with branch F5.
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2783/2783 and 82/82, every added test passing by exact name --
> `OtherCultures_Should_EachChangeTheTextTheyAreChosenFor` proves "1,5", a
> U+2212 "-5" and "2569" against Windows' own ICU (`icu.dll` 72.1); the whole
> suite with the test host set to en-US, de-DE, sv-SE and th-TH 2783/2783
> each, and the same instrument failing 38, 38 and 53 at `acfc107`; each of
> the three commits green on its own (2636, 2783, 2783). Report:
> `D:\Code\o1-culture-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-05, later): branch F4, the planetary hour, part of
release 1.3.3**
> `fix/planetary-hour-20261005`, four commits, cut from `01c76b3`: `6d468f9`
> (the sunrise and sunset around the instant, found directly: the hour comes
> from the latest sunrise at or before the instant, the sunset after it and
> the next sunrise, each search started well away from the event it looks
> for and the three checked to bracket the instant, replacing "the first
> sunrise after one day earlier" and "the first sunset after the instant";
> finding F9-1), `1ad31c7` (an explicit-offset chart's Hour Period on its
> own clock, through `PlanetaryHourCalculator.CalculateAtFixedOffset`, and
> labelled exactly as its Date/Time line; a zone chart is unchanged; finding
> F9-2), `72fbf47` (where the Sun does not rise or set -- the library's -2 --
> the chart returns without a planetary hour: `CalculateSunriseSunset`
> throws the new `SunDoesNotRiseOrSetException`, which derives from
> `EphemerisCalculationException` (unsealed for it) and which the engine
> catches; every other ephemeris failure still propagates; `IChartEngine`
> documents what `Calculate` and `CalculateForElectional` throw, part of
> queue item 4; finding F9-3) and `fc019cc` (CLAUDE.md's test total).
> Fast-forwarded onto `master` at `fc019cc`; this record follows it.
>
> Evidence from the Mac: the every-second scan of 2026 at nine latitudes 0
> seconds wrong (466.5 minutes a year at Richmond and 1,736.9 at 64 N
> before), and the 64 N pre-sunset skip windows 161 -> 0; the 10,003-chart
> sweep 0 ruler and 0 number mismatches (17 and 13 before), its 24 polar
> charts now returning; the 23 explicit-offset saved charts' Hour Period on
> their own clock (17 did not contain the chart's time before), and no saved
> chart's hour ruler or number changed; goldens 4 files, 1/1 each, the Hour
> Period line (solar, lunar and planetary return, relocation); electional
> exports unchanged. `AstroCast.Tests` **2783 -> 2797** (none removed; 14
> facts added: 4 for F9-1, 4 for F9-2, 6 for F9-3). Still open: the
> electional scan fails at a latitude where the Sun does not rise (branch
> P), and zone charts in local mean time (F0-3, branch TZ).
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 2797/2797 and 82/82, every added test passing by exact name -- the 64 N
> skip on Windows' own New York clock, and the two `SunriseSunsetTests`
> getting -2 from the Windows native library -- and the four goldens with
> their twelve culture runs passing against Windows data; each of the four
> commits green on its own (2787, 2791, 2797, 2797). Report:
> `D:\Code\f4-hour-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-05, evening): branch TZ, the app's own IANA
time-zone data, part of release 1.3.3**
> `fix/tz-data-20261005`, eight commits, cut from `5a742cd`: `5d95f57` (the
> truth table: 4,478 vectors over 13 zones from Python `zoneinfo` with
> tzdata 2026.5, IANA 2026e), `89eb35e` (NodaTime 3.3.5 and the seam),
> `f1dabe6` (cast and save), `a82f19b` (tzdb in the engine version),
> `3eb8682` (read instants on the chart's clock), `4086afa` (the
> lunar-return range), `21c1248` (zone list, default zone, place lookups,
> "Set to now") and `551b56c` (CLAUDE.md). Every civil-time conversion now
> goes through one seam, `ITimeZoneRules`, implemented by
> `TzdbTimeZoneRules` over the tzdb 2026e that NodaTime 3.3.5 embeds, so
> the Mac and Windows compute the same instant; `TimeZoneInfo` is kept only
> for the machine's own zone. Fast-forwarded onto `master` at `551b56c`;
> this record follows it.
>
> Rulings and cards: D17's instant -- a repeated hour takes the later
> instant, a skipped hour the first instant after the gap (the visible
> Time Note is F5's); the data is updated by rebuilding with a newer
> NodaTime; "tzdb 2026e" beside the engine version; zone.tab's 418 zones on
> both platforms; IANA local mean time before standard time; unknown
> identifiers refused, Windows identifiers translated; offset labels from
> the zone record; the default zone from the machine's zone; "Set to now"
> on the chart's clock; electional candidates cast at the sampled instant.
> Fixed: F0-1, F0-2's instant and label, F0-3, T0-2, TZ-1, TZ-2, TZ-3.
>
> Evidence from the Mac: 8,558 vectors 0 mismatches (master on the Mac
> missed 5,414; Windows 533 of the grid's 1,612, per the audit); the five
> inputs right in instant and label; the 95 saved charts keep their Julian
> Day; goldens 0 lines; `NodaTime.dll` (534,232 bytes) the only new file.
> `AstroCast.Tests` **2797 -> 2977** (none removed, one renamed; 181
> added: 26 facts and 155 theory cases). Still open: the electional filter
> compares wall clocks in a repeated hour (branch P or O); an Hour Period
> straddling a fall-back (O-4); the third-party notices file (its own
> branch, before the installers).
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> `NodaTime.dll` 534,232 bytes, SHA-256 `1ed970bf...6c18c53c28`, in every
> head's output; 2977/2977 and 82/82, every named test passing by exact
> name -- the 4,478 vectors through the seam and through
> `ChartMapper.ToEntity`, the five inputs, local mean time in the Hour
> Period, the 418-zone list with Vancouver (131 from Windows before), the
> 99 golden runs; the zone list byte-identical to the Mac's (SHA-256
> `f0f11bb2...eadc56d`); the default zone America/New_York from "Eastern
> Standard Time"; the installed database's 93 charts (70 zone-based, 23
> explicit) re-cast to their stored Julian Day, the 70 matching `zoneinfo`
> 2026e, with two labels gaining the zone's name ("PDT", "IDT") and no
> instant moving. Control: master on Windows cast New York 1880 at 16:00
> UT "EDT (UTC-4)" (now 16:56:02 "LMT (UTC-4:56:02)") and labelled the
> skipped 02:30 "UTC-4.5" (now "EDT (UTC-4)"); the other three inputs were
> already right here. Each of the eight commits green on its own (2801,
> 2856, 2927, 2941, 2944, 2946, 2977, 2977). Report:
> `D:\Code\tz-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-05, after TZ): branch F5, the time inputs, part
of release 1.3.3**
> `fix/time-inputs-20261005`, seven commits, cut from `8e73033`: `c24efed`
> (the Time Note), `2449a05` (explicit offsets and readings to the whole
> second), `526246a` (the calendar), `c517375` (the years the ephemeris
> files cover), `35bbb28` (a chart's UTC instant held and stored as UTC),
> `51a9fe0` (the explicit-offset box) and `a4c098d` (CLAUDE.md). One
> conversion, `ChartEngine.ResolveInstant`, is what the engine casts with
> and what `ChartMapper.ToEntity` saves with. Fast-forwarded onto `master`
> at `a4c098d`; this record follows it.
>
> The Time Note (D17, Q5, TZ card 4): a zone reading the clocks repeated,
> skipped or read on local mean time is said so under its own label,
> `**Time Note:**`, directly beneath the UTC Offset line -- never in Notes,
> which holds the user's text and a horary chart's question -- in the
> chart Markdown and PDF and every export that embeds the chart's header,
> and in the chart JSON as `timeNote`, omitted when there is none. A
> repeated reading gives both instants from the zone's record and says the
> chart is cast for the later; a skipped one gives the gap and casts for
> the first instant after it; local mean time names the zone's reference
> city and the offset to the second. An explicit-offset chart and an
> ordinary reading carry none. Explicit offsets and readings are taken to
> the whole second (F0-4). The calendar is decided on the local date, and
> the ten days 1582-10-05 to 1582-10-14 that never occurred are refused
> (D21, F0-5). Only instants inside the files are cast, 1200-01-01
> 04:54:46 UT (Julian) to 2399-12-31 23:51:20 UT, and anything outside is
> refused before the ephemeris is called; the old "-5400 to 5400" is gone
> (F0-6). The library was found to be stateful -- a data file left open
> serves past its own years -- so the old year-2400 test passed only by
> the order it ran in; it is now 2399. A chart's UTC instant is held and
> stored as UTC, with a `Z` (F0-7, queue item 3; E1-9). The explicit-offset
> box is read invariantly, within +-14 hours, at all four places that read
> it (O-D4, E3-1).
>
> Evidence from the Mac: the Note 0 mismatches against `zoneinfo` on all
> 4,478 committed readings; the calendar 0 mismatches against Meeus on
> 3,845 cases; goldens 0 lines; the 95 saved charts keep every Julian Day,
> their Markdown and PDF byte-identical, and the JSON of the 23
> explicit-offset charts gains only the `Z`. `AstroCast.Tests` **2977 ->
> 3111** (one renamed, 2400 -> 2399; 135 added: 7 facts and 128 theory
> cases). Still open: before 1583, `JulianDayUtility.ToDateTimeUtc`
> prints proleptic Gregorian dates for event instants, and an instant on a
> Julian leap day of 1300, 1400 or 1500 is refused (a `DateTime` cannot
> hold it); both are on the 1.4 list.
> Windows (Maple): build 0 errors with `xUnit1031` the only warning code;
> 3111/3111 and 82/82, every named test passing by exact name -- the Time
> Note cases, the three new `TzdbTimeZoneRulesTests` methods, the time
> input cases, the offset box under en-US, de-DE and sv-SE, and the 99
> golden runs; the span tests each run alone in a fresh process, so the
> Windows library (`swedll64.dll` 2.10.03) has the Mac's stateless span;
> the five Notes character-identical to the Mac's (as `TimeNoteTests`
> holds them) in Markdown and JSON under en-US and de-DE, the ordinary and
> explicit charts carrying none (the PDFs were written but not
> text-checked: the audit venv has no extractor); 1582-10-15 00:30 at
> UTC+1 cast at JD 2299160.4791667 and 1582-10-10 refused. The installed
> database's 93 charts (70 zone-based, 23 explicit), read only, keep every
> Julian Day at `8e73033` and at `a4c098d`, their Markdown identical
> between the two but for the calculation-time stamp, and the JSON
> differing only by the `Z` on the 23 explicit rows; re-saved on a copy,
> all 23 now end in `Z` with the Julian Day unchanged (Ids 22, 41 and 62
> had carried a machine offset). Each of the seven commits green on its
> own (2999, 3005, 3018, 3028, 3033, 3111, 3111). Report:
> `D:\Code\f5-time-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-05, after F5): branch O-4, the clock and zone
labels, part of release 1.3.3**
> `fix/clock-labels-20261005`, six commits, cut from `090db67`: `ec98448`
> (the birth-line labels), `5d37b83` (the PDF header), `2c1f006` ("UTC" on
> screen), `9dc77d5` (the straddling hour), `9fa5f2e` (the engine footers)
> and `62e7989` (CLAUDE.md). Fast-forwarded onto `master` at `62e7989`;
> this record follows it.
>
> An explicit-offset chart's birth clock is labelled with its own offset,
> as the chart export's Date/Time line labels it, in the relocation,
> astrocartography, electional, transit, daily and composite exports
> (E2-5); a zone chart's lines are unchanged. The daily header now says
> its days and their void-of-course times are on the zone's clock, which
> is kept deliberately: they are the reader's present days, and an
> explicit offset fixes only the birth instant. The PDF header says what
> the Markdown header says: an explicit chart's Date line carries its
> label and there is no Timezone line (E1-3). The returns screen says
> "UTC" beside the UTC times it shows, in both heads (the lunar-return
> list's `MultiBinding StringFormat`), and so does the planetary-return
> status (E3-2); default file names are unchanged. An Hour Period that
> straddles a change of the zone's offset shows each bound's offset
> (New York 2026-11-01: 01:47:57 EDT to 01:55:57 EST), read through the
> new `PlanetaryHour.HourStartUtc` and `HourEndUtc`; this was TZ's
> observation, and the "still open" Hour Period item in TZ's record is
> now closed. The electional and astrocartography exports print the
> engine version in their footers (E2-9); no "Generated" stamp changed.
>
> Evidence from the Mac: goldens three files, +2 lines each (the engine
> footer: astrocartography and both electional goldens); the 95 saved
> charts' Markdown, JSON and Julian Days unchanged, and the PDF text of
> the 23 explicit-offset charts changed in the header only; the straddle
> bounds against Python `zoneinfo` and direct sunrise and sunset.
> `AstroCast.Tests` **3111 -> 3184** (none removed; 73 added: 5 facts and
> 68 theory cases); `AstroCast.UiTests` 82, one assertion changed. Still
> open: the chart JSON's `hourTimeZone` and straddle bounds; the
> electional export's unnamed clocks (E2-14, branch Q); the
> explicit-offset condition written inline at 15 sites (a small refactor
> for later).
> Windows (Maple): build 0 errors across all eight projects with
> `xUnit1031` the only warning code -- the WPF head built with its changed
> `StringFormat`, the compiled `MainWindow.baml` carrying "HH:mm} UTC";
> the two head lines identical and differing from the old only by the
> added ` UTC`; 3184/3184 and 82/82, every named test passing by exact
> name -- the 48 `ExplicitOffsetBirthLineTests` cases under en-US, de-DE,
> sv-SE and th-TH, the 8 PDF header cases, the 14
> `HourPeriodClockChangeTests` cases (the New York and Moscow straddles
> through the app's own tzdb on Windows), both `EngineVersionFooterTests`,
> the planetary-return status test, the UI test
> `LunarReturnPicker_FormatsItemsWithTheDateSpecifier` and the 88 golden
> cases (22 goldens, each at its base run and under de-DE, sv-SE and
> th-TH). Each of the six commits green on its own (3159, 3167, 3168,
> 3182, 3184, 3184; UiTests 82 at `2c1f006`). Report:
> `D:\Code\o4-clocks-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-07, after O-4): branch J, astrocartography in
mundo, part of release 1.3.3**
> `fix/acg-in-mundo-20261005`, four commits, cut from `549d7ba`: `06f9289`
> (each body's lines in mundo, to 85 degrees), `b7db79f` (a crossing in
> km, to the line's nearest point), `6b4e5be` (the map segments and
> legend) and `58bcec4` (CLAUDE.md). Fast-forwarded onto `master` at
> `58bcec4`; this record follows it.
>
> Each body's lines are drawn in mundo (S-D7): MC and IC at alpha - theta
> and alpha + 180 - theta, ASC and DSC at alpha -+ H0 - theta with
> cos H0 = -tan(phi) * tan(delta). They come from the body's geocentric
> apparent right ascension and declination on the true equator of date
> and GAST, in closed form, with no parallax and the geometric horizon.
> The ARMC scan and its bisection are gone, and with them Y4-1 (ASC and
> DSC drawn as one curve). The lines run from 85 S to 85 N (Q2); a
> horizon line ends where the body stops rising and setting, at
> +-(90 - |delta|). A crossing is the great-circle distance to the line's
> nearest point on a 6,371.0088 km sphere, listed within 334 km. It is
> built once in Core and used by the screen's report, which stays on
> screen (S-D8, Q9, O-D5). Both map windows draw the shared
> `AcgMapGeometry` segments, split at +-180 degrees, with a one-line
> legend; the meridian labels are kept at +-66 degrees (E3-8). The
> export: the footer, the ranges, the ASC/DSC Latitudes column,
> latitudes to two decimals and the crossing sentence (E2-10, Y4-4,
> Y4-5). Steve's ruling on the step test: neighbouring line points
> within 9 degrees on the ground. The 9-degree step in longitude held
> only for zodiacal lines within +-66 degrees.
>
> Evidence from the Mac: Tier B on the 95 saved charts, 356,296 points,
> at most 1.4e-13 degrees; Tier C (Skyfield, DE440) on 20 charts, at
> most 0.78 arcsec; crossings at 14 places, 0 wrongly listed, 0 missed,
> at most 0.09 m. Chart 55 at Boulder: Mars ASC 21.93 km and Pluto MC
> 190.72 km listed, Mars DSC (5,408.73 km) not. One golden changed,
> 3,472 -> 3,857 lines (astrocartography); of the harness's 350 outputs,
> 8 changed, all astrocartography. `AstroCast.Tests` **3184 -> 3228**
> (one removed, the bisection safety cap; 45 added: 17 facts and 28
> theory cases); `AstroCast.UiTests` **82 -> 84**. Still open: CARTO's
> base tiles now show an "API KEY REQUIRED" placeholder in both map
> windows (found by J, not caused by it; a separate item); the
> astrocartography export's Generated line reads the real clock.
> Windows (Maple): build 0 errors across all eight projects with
> `xUnit1031` the only warning code -- the WPF head compiled with its
> changed map window; 3228/3228 and 84/84, every named test passing by
> exact name -- the 45 added cases (among them the km crossing texts
> under en-US, de-DE, sv-SE and th-TH), the astrocartography golden at
> its base run and under de-DE, sv-SE and th-TH, the astrocartography
> footer case, the two new UI tests and the 88 golden cases (22 goldens,
> each at its base run and under de-DE, sv-SE and th-TH). Against
> `549d7ba`: one name removed, 45 added (17 facts, 28 theory cases).
> Each of the four commits green on its own (3201, 3222, 3228, 3228;
> UiTests 82, 82, 84, 84). The committed golden counts 3,474 -> 3,859
> newlines (+385, as on the Mac). Off-screen, in scratch processes, the
> WPF `AcgMapWindow` was built for chart 55 with the real Core services
> and never shown: 35 strokes (7 bodies, the lines split at +-180
> degrees), none wider than 180 degrees of longitude, and one
> `TextBoxWidget`, the legend. The Avalonia window, built the same way,
> drew the same 35 strokes point for point (2,160 points) in the same
> pens and colours, with the same 56 labels and legend. Through the
> window's own tap path both heads read Boulder as "Mars ASC: 22 km |
> Pluto MC: 191 km", the header at 40.01 N because the inverse Mercator
> returns 40.01499999999999 (a rounding tie; `UpdateCrossingReport`
> called directly gives 40.02 N); 60 N 100 E ends "No angular lines
> within 334 km." Report: `D:\Code\j-acg-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-07, after J): branch A, the year and window
selection, part of release 1.3.3**
> `fix/year-windows-20261007`, four commits, cut from `0b1e24f`: `931fc36`
> (the solar return's year), `f1095f1` (the Sun's ingress of a year),
> `ec3444c` (the whole of the last day) and `ff2fea9` (CLAUDE.md).
> Fast-forwarded onto `master` at `ff2fea9`; this record follows it.
>
> The solar return's year (T-D7, T2-1). "Solar Return Y" is the Sun's
> return nearest the birthday in Y: 12:00 UT on the birth's local month
> and day in Y, on the calendar in force, with 29 February read as
> 28 February. The window is +-200 days. The birth year gives the birth
> instant: the birth-year exports of charts 13 and 59 no longer fail in
> the profection, and the 2026 returns of event charts 63 and 66 (cast at
> 00:00 UT in 2026) no longer fail. Chart 59's "2026" is 2026-12-06
> 19:59:19 UT (it was 2025-12-06 14:11:06); chart 13's is 2026-12-23
> 18:01:29.
>
> The Sun's ingress of a year (T4-1). The Sun's crossing of a sign's
> first degree in calendar year Y (UT). The cardinal set of 2026 now has
> Libra at 2026-09-23 00:05:14 and Capricorn at 2026-12-21 20:50:15 UT
> (they were 2025's). Tier C agrees within 0.017 s. Other bodies are
> unchanged; no head or CLI offers a single ingress chart, so only the
> Sun's is reachable.
>
> The whole of the last day (T0-1). The lunation, ingress, synodic,
> great-conjunction, mundane-aspect and timeline series run to 1 January
> 00:00 UT of the year after the end year. Laid end to end, one-year
> requests now equal the multi-year request.
>
> Evidence from the Mac, 1900-2050: lunations 3,736 and Moon ingresses
> 24,223, 0 missed (the 11 and 68 on 31 December had been lost); all
> 1,812 Sun ingresses in their year (906 had been the year before's);
> 4,940 solar returns (2000-2050 and the birth years), all within 1 s of
> Tier B (102 wrong and 4 failures before). No golden changed. Of the
> harness's 472 outputs, 24 changed, all in these families.
> `AstroCast.Tests` **3228 -> 3258** (30 added); `AstroCast.UiTests` 84.
> Still open: a year-2399 series now takes its last sample at 2400-01-01
> 00:00 UT, 8 min 40 s past the supported span, and works only because
> the ephemeris file is already open (Chiron); a clamp to the span end is
> a separate item. Return and ingress charts before 1583 are cast about
> 7-10 days late: `JulianDayUtility` labels pre-1583 instants on the
> proleptic Gregorian calendar, and the engine reads that label as a
> Julian date (found by A, not caused by it; a separate item).
> `PlanetaryReturnService`'s window still ends at 30 June 23.99 h
> (branch F). Single ingress charts of the Moon, Mercury and Venus take
> the first crossing from 1 July of the year before; no screen reaches
> them.
> Windows (Maple): build 0 errors across all eight projects with
> `xUnit1031` the only warning code -- no head, CLI, golden or project
> file changed; 3258/3258 and 84/84, every named test passing by exact
> name -- the 30 added cases (6 `SolarReturnYearTests`, 14
> `SunIngressYearTests`, 10 `YearWindowLastDayTests`; 19 theory cases
> and 11 facts) and the 99 cases of the golden classes (the 22 goldens
> and the default-clock chart export, each at its base run and under
> de-DE, sv-SE and th-TH, and the 7 normaliser self-tests). Against
> `0b1e24f`: no name removed, 30 added; theory cases 1123 -> 1142; the
> UiTests names unchanged. Each of the four commits green on its own
> (3234, 3248, 3258, 3258; UiTests 84 at each). The goldens' tree is the
> same object at `0b1e24f` and `ff2fea9`. Report:
> `D:\Code\a-years-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-07, after A): branch Tiles, the base map with a
CARTO key, part of release 1.3.3**
> `fix/map-tiles-key-20261007`, three commits, cut from `330f313`:
> `549358e` (the settings and the key, in Presentation), `7b9a518` (both
> map windows) and `8e9ea39` (CLAUDE.md). Fast-forwarded onto `master` at
> `8e9ea39`; this record follows it.
>
> Why. Since 25 September 2026 CARTO serves raster basemaps only with a
> key, and every request without one gets an "API KEY REQUIRED" tile sent
> as HTTP 200. Both map windows, and the shipped v1.3.2, showed it.
> Steve's decisions: CARTO Voyager stays, for its English place names (no
> keyless provider has them); a free non-commercial key is built into the
> app, which will be given away free as a notarized app; and the key file
> lives outside the repo.
>
> The settings live once, in Presentation (`BaseMapSettings`): CARTO's
> documented keyed URL, the layer name (unchanged), the User-Agent
> `AstroCast/<version> (desktop app)` and the two attribution links. The
> key (`BaseMapKeyLoader`): first `carto-key.txt` in the data directory,
> then the key built in from `.astrocast\carto-key.txt` under the user's
> profile when that file exists at build time, then none. With no key a
> window adds no tile layer, sends no request and says "Base map
> unavailable: no CARTO key" under the legend. The attribution is two
> links, bottom right: OpenStreetMap's copyright page and CARTO's
> attribution page. The test seam is an optional `HttpClient` per window,
> so the UI suite no longer calls CARTO (42 attempts per run before, 0
> now).
>
> Evidence from the Mac: the live tile, 200 and not the placeholder; the
> capture with the key, 29 keyed requests, real tiles; the capture
> without, 0 requests and the notice; the key occurs 0 times in the diff,
> the commits and the scratch. `AstroCast.Tests` **3258 -> 3285** (27
> added); `AstroCast.UiTests` **84 -> 88** (4 added).
> Release checklist: the machine that builds the MSI or the DMG must have
> the key file, or that installer ships without a map.
> Still open: a refused or lapsed key shows the placeholder; the Nominatim
> User-Agent; a disk cache; a no-key window opens on the whole world
> rather than zoom 4.
> Windows (Maple): build 0 errors across all eight projects with
> `xUnit1031` the only warning code -- the WPF head compiled with its
> changed map window, and no build printed "no CARTO key file at"; each
> head's built `AstroCast.Presentation.dll` carries the resource
> `AstroCast.Presentation.CartoKey`, which the loader reads as "CARTO key
> (BuiltIn)". 3285/3285 and 88/88, every named test passing by exact
> name -- the 27 added cases (14 `BaseMapKeyLoaderTests` and 13
> `BaseMapSettingsTests`; 13 theory cases and 14 facts), the 4 new
> `AcgMapWindowTests` and the 2 existing ones. Against `330f313`: no name
> removed. The UI suite once more behind a local proxy that refuses and
> logs every request: 88/88 and 0 attempts (a positive control's did
> reach it). Each of the three commits green on its own (3285, 3285,
> 3285; UiTests 84, 88, 88). Off-screen, in scratch processes, with the
> test key `abc` and a stub `HttpClient`, the WPF `AcgMapWindow` built
> for chart 55 and never shown has one tile layer, "CartoDB Voyager",
> whose URL for tile 2/1/1 is
> `https://basemaps.cartocdn.com/rastertiles/voyager/2/1/1.png?key=abc`,
> whose request configurator sets `AstroCast/1.3.3 (desktop app)` and
> whose own attribution is off, and the map carries the two links; with
> no key there is no tile layer and no link, and the one notice; neither
> window sent a request. The Avalonia window, built the same way, matched
> it line for line. One live tile, 2/1/1 through the WPF head's built
> Presentation with its built-in key: 200, the ETag beginning `W/"2` (not
> `"wm-`) and the body's SHA-256 beginning `7da7df0d` (not `69be4aec`,
> the placeholder's). The key occurs 0 times in the branch's diff and
> commits and in the Maple scratch. Report:
> `D:\Code\tiles-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-08, after Tiles): branch B, the series labels
and the span clamp, part of release 1.3.3**
> `fix/series-labels-20261008`, eight commits, cut from `ca4f53f`:
> `0fb0881` (the sign entered), `1ab7763` (waxing and waning), `4590403`
> (the eclipse figure and the node flag), `fc0dfce` (the span clamp),
> `577c2fe` (the synodic cycle), `09e5f4e` (a clamped series scans to its
> window's end), `1affe28` (CLAUDE.md) and `674e5a7` (the solar phase
> rows, the lunation section, the synodic interface summary).
> Fast-forwarded onto `master` at `674e5a7`; this record follows it.
>
> The sign entered (T4-2, T2-6). A crossing made retrograde enters the
> sign that ends at the boundary: Pluto 2024-09-01 enters Capricorn,
> Chiron 2026-09-18 Aries (the timeline's Chiron row), and so do the True
> Node's crossings. Which crossings are found is unchanged.
>
> Waxing and waning (T5-1). The phase is faster - slower, the faster body
> by mean sidereal period (Moon ... Pluto, Chiron between Saturn and
> Uranus). The True Node keeps the old measure. Mars square Uranus
> 2026-12-04 is Waxing 90 degrees either way round.
>
> The synodic period (T5-2). Conjunctions are one cycle until the pair
> passes through an opposition. A triple counts once, from its first
> pass; later passes are marked "pass n of N" with no period. Steve ruled
> that pairs of the Sun, Mercury and Venus, never in opposition, have a
> cycle per conjunction. Saturn-Uranus 2000-2100: 17,044.39 days (46.66
> years), where it was 5,760.43.
>
> The eclipse labels (T-D13, Q7). The solar figure is "Duration of the
> central eclipse (whole Earth)", with one note under the timeline table.
> Its phase rows read "Central eclipse begins/ends (whole Earth)"
> (Steve's ruling). Lunar totality keeps its names. Values are unchanged.
>
> The node flag (T-D12 kept, Q8). The rule is unchanged. The text is
> "Near a node" and its limit in brackets (within 18 degrees for a New
> Moon, 12 for a Full Moon), the column "Near a node", and the summary
> and section "Lunations near a node" (Steve's ruling).
>
> The span clamp. The two constants live in `EphemerisSpan`. Every
> year-based series and the eclipse search stay inside the span,
> 1200-01-01 04:54:46 to 2399-12-31 23:51:20 UT. A clamped daily scan runs
> to its window's end and filters (Steve's ruling). The 1200 Chiron,
> timeline, synodic and mundane requests that failed now complete.
>
> Evidence from the Mac, 1900-2050: ingress labels 546 -> 0 wrong; phase
> words 4,319 -> 0; synodic averages 45 of 45; the same 661 lunations
> flagged; eclipses identical. The harness changed 20 of 485 outputs,
> then 7 more lines in the fix-up, all in these families; the span-edge
> requests all stay inside the span. Three goldens moved, only in the
> allowed lines: the timeline's Chiron row; the eclipse golden's note,
> solar figure name and two solar phase rows; the lunation golden's flag
> heading, two flag texts, summary line and section heading.
> `AstroCast.Tests` **3285 -> 3341** (56 added); `AstroCast.UiTests` 88.
> Still open: pre-1583 labels and casting (branch R); conjunction and
> opposition phase words are noise; a series window that cuts a
> multi-pass cycle counts only the passes inside it; the prompt-library
> lines `Eclipse_1_5:395-397`, `:689-691` and `Lunation_V1_3:259`.
> Windows (Maple): build 0 errors across all eight projects with
> `xUnit1031` the only warning code -- no head, CLI, project file or
> other golden changed, and the two span constants moved to
> `EphemerisSpan` with the refusal and its message unchanged. 3341/3341
> and 88/88, every named test passing by exact name -- the 56 added
> cases (4 `IngressSignEnteredTests`, 3 `MundanePhaseTests`, 16
> `EclipseAndNodeLabelTests`, 17 `SeriesSpanClampTests`, 4
> `SynodicPeriodTests`, 12 `EclipsePhaseAndLunationSectionTests`; 44
> theory cases and 12 facts), the two changed `LunationServiceTests`, the
> three moved goldens at their base run and under de-DE, sv-SE and th-TH
> (12), and the 8 `ChartEngineTimeInputTests` cases that pin the span
> message. Against `ca4f53f`: no name removed, 56 added; theory cases
> 1155 -> 1199; the UiTests names unchanged. `SeriesSpanClampTests` alone
> in a fresh test process: 17/17. Each of the eight commits green on its
> own (3289, 3292, 3308, 3323, 3327, 3329, 3329, 3341; UiTests 88 at
> each). Report: `D:\Code\b-labels-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-08, after B): branch R1, event charts cast at
their instant, part of release 1.3.3**
> `fix/event-chart-instants-20261008`, four commits, cut from `1495ac1`:
> `bd35794` (the engine's cast at an instant, and the electional
> instant), `8b5a796` (every event chart at its instant), `003f48b` (the
> cycle and transit longitudes at the Julian Day in hand) and `65efc0b`
> (CLAUDE.md). Fast-forwarded onto `master` at `65efc0b`; this record
> follows it.
>
> The rule (R, option (c); Steve's decisions D1-D7 of 2026-10-08). A
> `DateTime` always denotes an instant; a calendar applies only to typed
> input and to printing. R1 brings D4 (a chart's UTC time is the instant)
> to every event chart. R2, in 1.3.4, brings it to every chart and prints
> Julian dates before 15 October 1582.
>
> The cast at an instant. `IChartEngine.CalculateAtInstant(ChartInput,
> double julianDayUt, CancellationToken)` shares the full pipeline with
> `Calculate` through a private `Cast`. Its instant is the Julian Day
> rounded to the whole second, as `ToDateTimeUtc` rounds. It converts by
> arithmetic in `JulianDayUtility` (`JulianDayToInstant`,
> `InstantToJulianDay`), never through a calendar. It takes an
> explicit-offset input only, and refuses outside the span with
> `Calculate`'s message.
>
> Cast at the event's Julian Day: the solar, lunar and planetary returns;
> `CalculateIngress` and the cardinal set; the lunation charts (of a year,
> and near a date); the great-conjunction chart. The electional instant
> converts by arithmetic. Also read at the Julian Day in hand: the cycle
> timeline (each return chart's `JulianDay`; its private
> `DateTimeToJulianDay`, used only by the two replaced lines, was
> removed); the transit longitudes at each entry, exit and station (taken
> to the whole second); the exact pass's declination.
>
> Every printed label is unchanged. Event labels stay proleptic Gregorian
> before 1583 until R2; the chart under a label is now the chart of that
> instant.
>
> Evidence from the Mac. From 1582-10-15 on: B's full harness 485 of 485
> identical; `measure`'s 98 post-switch rows identical; 0 goldens moved.
> A probe of 400,000 Julian Days found the conversions bit-identical to
> `ToDateTimeUtc` and `swe_julday`. Before the switch, over 194 rows: the
> largest residual is 0.531 s and the largest body offset 0.30 arcsec;
> they were 7-10 days, and 1,847 to 526,966 arcsec. Now cast, formerly
> refused: N4's 1582 return, N1's lunar return labelled 1582-10-05, and
> the 24 lunation charts of 1582. The Julian leap days cast on the event:
> N3's return of Julian 1300-02-29, and the New Moon of Julian
> 1500-02-29. N1's Mars cycle #1: 122.57, 432.93 and 593.97 days (were
> 114.57, 424.93 and 585.97). Transit longitudes match Tier B to 0.000
> arcsec (were up to 7,679 arcsec off). `AstroCast.Tests` **3341 ->
> 3395** (54 added); `AstroCast.UiTests` 88.
> Left for R2 (1.3.4): progressions (RT5);
> `LunarReturnService.CalculateNext`, which nothing calls; relocation,
> which must cast at the natal Julian Day under D4; the time lords and
> the planetary-return age; the October 1582 `DateOnly` loops (RT7);
> `TransitEphemerisHelper.DateTimeToJulianDay`, now without a caller in
> `src/` (three `TransitTimePrecisionTests` still call it) and kept;
> `CalculateAtInstant`'s span message, which names a proleptic date;
> every printed event label.
> Windows (Maple): build 0 errors across all eight projects, the WPF head
> included, with `xUnit1031` the only warning code. The solution's two
> `IChartEngine` implementations, `ChartEngine` (Core) and
> `StubChartEngine` (TestDoubles), both carry `CalculateAtInstant`; the
> heads only construct `ChartEngine`. No golden, formatter, head, CLI,
> UiTests or project file changed, and the numstat equals the Mac's file
> for file. `Calculate`'s pipeline (steps 1-8 and `BuildResult`) moved
> into `Cast` byte for byte, its refusal unchanged; the two helpers' code
> names no calendar. 3395/3395 and 88/88, every named test passing by
> exact name -- the 54 added cases (30 `ChartEngineCastAtInstantTests`,
> 19 `EventChartInstantTests`, 5 `CycleAndTransitInstantTests`; 40
> theory cases and 14 facts), the 99 golden cases (69 of them the de-DE,
> sv-SE and th-TH runs) and the 8 `ChartEngineTimeInputTests` cases that
> pin the span message. Against `1495ac1`: no name removed, 54 added;
> theory cases 1199 -> 1239; the UiTests names unchanged. In fresh test
> processes: `ChartEngineCastAtInstantTests` 30/30, its span-edge and
> no-instant cases 7/7, `SeriesSpanClampTests` 17/17. Each of the four
> commits green on its own (3371, 3390, 3395, 3395; UiTests 88 at each).
> Report: `D:\Code\r1-instants-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch.

**Landing record (2026-10-09, after R1): branch C, void of course by the
1.3.2 definition, part of release 1.3.3**
> `fix/void-of-course-20261009`, three commits, cut from `ac41b94`:
> `af5dea8` (the definition, at event times), `baf06f0` (every period
> that touches the range, inside the span) and `62dbe84` (CLAUDE.md and
> comments). Fast-forwarded onto `master` at `62dbe84`; this record
> follows it.
>
> The definition (T7-1; 1.3.2's, H3). A void period runs from the Moon's
> last exact major aspect in its sign stay (to the Sun or Mercury through
> Pluto, every target in motion) until it leaves the sign. A stay with
> none is void from its sign entry. At every instant this equals
> `MoonConditionService`.
>
> Event times. The aspects come from `AspectPerfectionFinder`. The sign
> entry and exit are found to 1 s on an hourly grid fixed in time, so
> every scan computes a stay identically. A sign change is the first
> instant known to be in the next sign, 0 to 1.37 s after the crossing
> (Steve accepted this: it matches `MoonConditionService`).
>
> Every period that touches the range, with its true bounds (Steve's
> ruling of 2026-10-09). The `ElectionalService` look-back known issue is
> fixed. The daily horoscope's three-day pad is kept, but no longer
> needed (E may remove it).
>
> The span. The scanner never reads outside `EphemerisSpan`: a range is
> cut to it; a stay that begins before the span is searched from its
> start; a stay that ends after the span yields no period.
>
> Removed: the old sampling's eight private members,
> `FindLastMajorAspect`, `HasMajorAspect`, `FindSignBoundaries`,
> `BinarySearchSignBoundary`, `AspectScanStep`, `DetectionOrb`,
> `SignScanStep` and `SignBoundaryPrecision`. `MajorAspectAngles` is
> kept, though no longer read.
>
> Evidence from the Mac. Tier B 1900-2050: 24,222 stays, one period
> each, none missed; every aspect start within 1 s; total void time
> 8,561.3 days (was 6,323.6); at 20,000 instants the scanner agrees with
> `MoonConditionService` at every one (810 differed). Daily exports,
> 2026-03-09 to 10-30: 104 periods equal Tier B (all 104 were off, 81 by
> more than an hour); of 1,999 re-run days, 0 differ (1,094 did, and 115
> printed "None" while void). Electional: reported windows that are
> truly void went from 134 of 1,740 to 0 of 1,721; of the 26 real
> re-runs, 7 changed, 2 of them in their top window (R10, R22); R06
> moved through the sample pool (O's open issue). B's full harness: 4 of
> 485 outputs changed, only in VoC lines and void or disqualified rows.
> Cost: a one-year scan went from 87 ms to 733 ms; an electional search
> takes about 77 ms longer. A cheaper search with the same result is
> offered for later. Goldens: the two electional goldens, 10 lines
> changed and 2 added. `AstroCast.Tests` **3395 -> 3412**;
> `AstroCast.UiTests` 88.
> Still open: the electional sample pool and repeated-hour comparisons
> (O); the evaluator's look-ahead (P); the daily formatting and its pad
> (E).
> Windows (Maple): build 0 errors across all eight projects, the WPF head
> included, with `xUnit1031` the only warning code. The numstat equals
> the Mac's file for file; no head, Presentation, CLI, UiTests, `ephe/`
> or project file changed, no other golden, and no other Core file
> (`ElectionalService`, `MoonConditionEvaluator`, `MoonConditionService`,
> `AspectPerfectionFinder` and `VoidOfCoursePeriod` included). The
> formatter and floor-test edits are comment-only, their code lines
> identical, `VoidScanPadDays` still 3.0. The golden diffs are exactly
> the 12 allowed lines. The scanner's non-private surface is unchanged,
> by source and by reflection on both builds; the eight removed members
> were private, nothing at HEAD names them, and nothing at `ac41b94`
> reached them by reflection, `nameof` or string. 3412/3412 and 88/88,
> every named test passing by exact name -- the 17 added cases (7
> `VoidOfCourseDefinitionTests`, 10 `VoidOfCourseRangeTests`; 7 theory
> cases and 10 facts), the 4 `VoidOfCourseScannerTests`, the 2
> `DailyHoroscopeVocScanFloorTests`, `ElectionalScorerTests`' void-scan
> integration test and the 99 golden cases (69 of them the de-DE, sv-SE
> and th-TH runs, the two moved goldens' 8 included). Against `ac41b94`:
> no name removed, 17 added; theory cases 1239 -> 1246; the UiTests
> names unchanged. The suite ran in 1 m 32 s (R1: 1 m 19 s). In fresh
> test processes: `VoidOfCourseRangeTests` 10/10, its span cases 4/4,
> `SeriesSpanClampTests` 17/17. Each of the three commits green on its
> own (3402, 3412, 3412; UiTests 88 at each).
> Report: `D:\Code\c-voc-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-09, after C): branch P, the electional Moon,
part of release 1.3.3**
> `fix/electional-moon-20261009`, two commits, cut from `7197cc8`:
> `ff910f2` (read the Moon from `MoonConditionService`) and `91e910d`
> (CLAUDE.md). Fast-forwarded onto `master` at `91e910d`; this record
> follows it.
>
> The rule (Y6-4, Y6-5, Y6-10; H3; S-D11). The electional evaluator
> takes the Moon's next applying aspect and void of course from
> `MoonConditionService.GetMoonCondition` at the sample's instant, with
> positions from the ephemeris. Gone: the nearest applying aspect within
> 8 degrees, the 14.4-minute look-ahead (F3-1 again) and the half-day
> cache of Jupiter through Pluto. Unchanged: the description's form;
> `VoidStartLocal` and `VoidEndLocal` from the scanner's periods.
>
> Removed: the old rule's private members `FindNextApplyingAspect`,
> `GetTargetLongitude`, `RefreshOuterPlanetCache`, `ApplyingAspectInfo`,
> `CacheWindowJd`, `_outerPlanetCache`, `_outerCacheJd`, `AspectNames`,
> `AspectTypesByAngle`, `OuterPlanets` and `MajorAspectAngles`.
> `AspectTargets` is kept: it lists the positions passed.
>
> Evidence from the Mac (five sets). Y6-5: 0 of 30,088 instants differ
> from H3 (4,507 of 30,056 with C; 6,439 of 30,053 before C). Y6-4: 0 of
> 1,725 windows (282 of 1,721 with C; 379 of 1,740 before C); in the
> printed real exports 7 of 80 -> 0 of 80, with the orbs identical.
> Y6-10: 0 instants differ (52 in the aspect, 5,029 in the orb). Y6-3: 0
> of 1,725 reported windows truly void; Y6-7: the look-back changes no
> window. B's full harness: 485 of 485 identical. Real re-runs: 17 of 26
> change their windows, 7 of them the top window, each attributed to the
> samples whose Moon reading moved it. Golden: NYC's third candidate,
> Thu 23 Apr 2026 9:45 AM (82) -> 12:00 PM (78.5), through the pool; the
> 20 samples of 22 Apr 19:00 to 23:45 now see their sextile to Venus,
> which was more than 8 degrees away. Richmond is unchanged. Cost: the 26
> real re-runs went from 7.06 s to 34.4 s; the longest range from 0.48 s
> to 1.74 s; the slowest search (R21) from 0.34 s to 2.80 s; the Mac's
> test suite from 31 s to 51 s. Steve's ruling: land P now and revisit
> the speed after O (a per-sign-stay aspect list was proposed).
> `AstroCast.Tests` **3412 -> 3423**; `AstroCast.UiTests` 88.
> Still open: the grid, the pool and the search failing where the Sun
> does not rise (O); the scorer's tables, and the header's "78/100"
> beside the breakdown's "78.5" (Q, Y6-13).
> Windows (Maple): build 0 errors across all eight projects, the WPF head
> included, with `xUnit1031` the only warning code. The numstat equals
> the Mac's file for file; no head, Presentation, CLI, UiTests, `ephe/`
> or project file changed, no other golden (Richmond included), and no
> other Core file (`ElectionalService`, `ElectionalScorer`,
> `ElectionalMarkdownFormatter`, `MoonConditionService`,
> `VoidOfCourseScanner` and `MoonCondition` included). Every changed line
> of the NYC golden lies inside the third candidate's section; the
> preamble, candidates 1 and 2 and the two tables after it are
> byte-identical. The evaluator's non-private surface is unchanged, by
> source and by reflection on both builds; `Evaluate` calls
> `GetMoonCondition` on a `MoonConditionService`, and its code holds no
> 8-degree limit, look-ahead or cache. The eleven removed members were
> private; at HEAD none is named in the evaluator, and nothing at
> `7197cc8` reached them by reflection, `nameof` or string. 3423/3423
> and 88/88, every named test passing by exact name -- the 11 added
> cases (all `MoonConditionEvaluatorClosedRuleTests`; 6 theory cases and
> 5 facts), the 6 `MoonConditionEvaluatorTests`, the
> `ElectionalOrbConsistencyTests` case, the two `ElectionalScorerTests`
> that build an evaluator, C's
> `ElectionalSearch_StartingInsideAVoid_DisqualifiesItsSamples` and the
> 99 golden cases (69 of them the de-DE, sv-SE and th-TH runs, the two
> electional goldens' 8 included). Against `7197cc8`: no name removed,
> 11 added; theory cases 1246 -> 1252; the UiTests names unchanged. The
> suite ran in 2 m 21 s (C: 1 m 32 s). In fresh test processes:
> `MoonConditionEvaluatorClosedRuleTests` 11/11, `SeriesSpanClampTests`
> 17/17. Each of the two commits green on its own (3423, 3423; UiTests
> 88 at each).
> Report: `D:\Code\p-moon-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-09, after P): branch O1, the electional
bounds, instants and orbs, part of release 1.3.3**
> `fix/electional-bounds-20261009`, six commits, cut from `1ba5118`:
> `5dfde98` (no sample on the end bound; a retrograde keeps its own
> stations), `ef1cb3f` (compare instants, not local readings; a day
> begins at its first instant), `65ea507` (the natal harmony table takes
> each point's own orbs), `aacae9a` (CLAUDE.md), `3cdbd07` (Mercury's
> stations to the second, on a lattice fixed in time) and `0324e67` (the
> repeated-hour print tests read lines, not terminators).
> Fast-forwarded onto `master` at `0324e67`; this record follows it.
>
> Steve's rulings on O (2026-10-09), all nine. (1) O2 scores every
> sample from the scorer's inputs only, with full charts for the
> reported instants; the faster path goes on the 1.4 list with P's.
> (2) Polar (O3): drop the samples with no hour and count them; refuse
> up front, naming the system, where the natal house system has no
> houses. (3) Offsets only where a printed reading repeats. (4) The
> separation stays 6 hours and is stated in the export (O2). (5) Time
> limits stay inclusive. (6) The void "since ... ends ..." branch is
> left. (7) "Candidate instant N" in the heading and "Candidate
> instants: N" in the header (O2). (8) A retrograde prints its own
> stations. (9) `ElectionalOptions` is kept, noted for 1.4 hygiene. The
> order: O1, O2, O3.
>
> The end bound (Y6-6). No sample at or after the range's end. K100
> returns no candidate.
>
> The retrograde's own stations. Found outside the range as needed,
> never outside the span. Bisected to 1e-6 day on a lattice fixed in
> time and taken to the second, so a station gives the same second from
> any range.
>
> Instants (Y6-8's remainder, Y6-17). Void and disqualified periods carry
> `StartUtc` and `EndUtc`. Disqualification and the evaluator's period
> lookup compare instants; the separation is measured between instants.
> A day begins at its first instant (`LocalDayStartToJulianDay`).
> `LocalMidnightToJulianDay` keeps D17, and its other caller, the daily
> horoscope's void scan window, is left for a ruling. A repeated printed
> reading carries its offset.
>
> Natal harmony orbs (Y6-11). The orb lookup sees the bare names. No
> score changes.
>
> Line endings in the tests (`0324e67`). Maple's first run stopped at
> 3469/3471: two repeated-hour print tests assumed LF, and the formatter
> writes `Environment.NewLine` (CRLF on Windows). The tests now read
> lines with the terminator removed; no product change. **On Windows,
> `ef1cb3f`, `65ea507`, `aacae9a` and `3cdbd07` each fail those two
> tests** (Steve's ruling: fixed on top, not by rewriting the pushed
> branch). Expect them when bisecting on Windows.
>
> Evidence from the Mac. The end-bound sample admitted in 0 searches
> (was 68; reported in 6). Repeated-hour cases misjudged 12 -> 0;
> separation pairs under 6 h 8 -> 0; Havana 49 h -> 48 h. Natal harmony
> majors equal the category rule (44,476). Retrograde rows off Tier B's
> minute 346 -> 0. Over 1201-2398, 7,552 stations independent of the
> range, 7,390 on Tier B's second. No golden moved; B's harness
> identical; no real re-run window changed. The line-ending fix shown
> red, then green, by simulating CRLF.
> Still open: two stations print a minute off, both within 0.03 s of a
> minute boundary (2020-10-14 and 2028-05-21); the evaluator's unread
> `_timeZoneRules`, kept; O2 (every sample, "Candidate instant", the
> separation stated) and O3 (polar).
> `AstroCast.Tests` **3423 -> 3471**; `AstroCast.UiTests` 88.
> Windows (Maple), first run at `3cdbd07`: stopped at A1.2, 3469/3471,
> the two print tests failing only on their embedded terminators (the
> nine headings and both void rows were right); every A0 check had
> passed. Second run at `0324e67`: build 0 errors across all eight
> projects, the WPF head included, with `xUnit1031` the only warning
> code. The numstat equals the Mac's in all four parts; no golden, head,
> Presentation, CLI, UiTests, `ephe/` or project file changed, and no
> other Core file (`SynastryOrbs`, `ElectionalOptions`,
> `ElectionalScorer`, `MoonConditionService` and
> `DailyHoroscopeMarkdownFormatter` included). By source and by
> reflection the surface changes only by the two models' `required
> StartUtc`/`EndUtc`, `LocalDayStartToJulianDay` and
> `CalculateNatalHarmonyAspects` made internal; `LocalMidnightToJulianDay`
> is unchanged in code. The two models are built only in the scanner,
> the detector and `ElectionalService`. `0324e67` touches no file under
> `src/`, and the head's reflection is byte-identical to the first run's.
> 3471/3471 and 88/88, every named test passing by exact name -- the 48
> added cases (20 theory cases, 28 facts), all three
> `ElectionalRepeatedHourPrintTests`, the 5 `MercuryRetroDetectorTests`,
> `ElectionalRepeatedHourTests`, the 7 `ElectionalCandidateSeparationTests`
> cases and the 99 golden cases (69 of them the de-DE, sv-SE and th-TH
> runs, the two electional goldens' 8 included). Against `1ba5118`: no
> name removed, 48 added; theory cases 1252 -> 1272; the UiTests names
> unchanged. The suite ran in 2 m 22 s (P: 2 m 21 s). In fresh test
> processes: the Mercury span cases 4/4, `MercuryStationSecondTests`
> 9/9, `VoidOfCourseRangeTests` 10/10, `SeriesSpanClampTests` 17/17,
> `ElectionalRepeatedHourPrintTests` 3/3. Each commit on its own:
> `5dfde98` 3431/3431; `ef1cb3f` 3456/3458, `65ea507` 3460/3462,
> `aacae9a` 3460/3462 and `3cdbd07` 3469/3471, each failing only the two
> print tests; `0324e67` 3471/3471; UiTests 88 at each.
> Reports: `D:\Code\o1-bounds-maple-report.md` (first run) and
> `D:\Code\o1-bounds-maple-r2-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-10, after O1): branch O2, every grid sample
scored, part of release 1.3.3**
> Branch O2, four commits on `fccdbb5`: `a92f714` (the scorer's inputs
> without the full chart), `6e51d31` (score every grid sample),
> `9ca8c32` ("Candidate instant" and the separation in the export) and
> `14791ac` (CLAUDE.md). It landed on `master` from the Mac at Steve's
> request, before Maple's verification: a fast-forward of
> `fccdbb5..14791ac` and a push, 2026-10-09. This record follows that
> verification.
>
> Between O1 and O2, Steve's own session landed two commits: `0d7a679`
> (the `release-bump` project skill under `.claude/skills/`, with
> `.gitignore` sharing it) and `fccdbb5` (CLAUDE.md's tagging section
> pointing at the skill). Neither changes source or tests. The skill's
> first Windows run (Maple, at `594ba29`): the audit reads "Current
> 1.3.3 (all 8 agree)" under pwsh 7.6.0 and Windows PowerShell 5.1,
> exit 0 in each. `read-app-version.cs` reads 1.3.3 from both heads'
> Debug copies of `AstroCast.Presentation.dll`, exit 0 in each.
>
> After O2, F: `594ba29`. Maple's first run stopped at A0.9. Its `!`
> search found two null-forgiving operators in the skill's
> `read-app-version.cs` (from `0d7a679`) and none in O2. Steve ruled to
> fix them, and `594ba29` ("chore(skill): read-app-version without
> null-forgiving operators") replaced both, one line for one line. It
> was pushed to `master` from the Mac.
>
> Steve's rulings on O (2026-10-09), here 1, 4 and 7 (S-D10(b), Q14).
> (1) Every eligible sample is scored from the scorer's inputs only,
> with full charts for the reported instants. (7) "Candidate instant N"
> in each heading and "Candidate instants: N" in the header. (4) The
> 6-hour separation is stated in the header.
>
> The scorer's inputs. `IChartEngine.CalculateElectionalScoringInputs`
> returns the positions, houses, dignities and chart context at the
> instant `CalculateForElectional` casts at. It makes the same calls in
> the same order, through private helpers both paths share.
>
> Every sample scored. Each eligible grid sample is scored from those
> inputs, its Moon and its planetary hour. A stable sort follows (the
> earliest wins a tie), then the unchanged separation filter. A full
> chart is cast only for each reported instant. The pool is removed:
> `SelectFullScorePoolIndices`, its three constants, the quick score
> and `ElectionalPoolSelectionTests` (19 names).
>
> The wording. "Candidate instant N" in each heading, "Candidate
> instants: N" in the header, and a new header line, "Separation: at
> least 6 hours between candidate instants". CLAUDE.md's known issue
> on the pool is now a fixed entry.
>
> Evidence from the Mac:
> - E1: R01's top 76.5 -> 83.5, at 7 Sep 2026 00:00.
> - E2: the NYC golden {88.5, 85.5, 84.25}, printed {88, 86, 84}.
> - E3: Richmond's second candidate is Thu 11 Jun 9:00 PM (80.2).
> - E4: the top changes in R01, R21, R18, R20, R23 and R03 only. 23 of
>   26 real re-runs change a window, all 26 as O predicted.
> - E5: light equals full at 740,451 of 740,451 eligible samples.
> - E6: the 17 polar and Placidus-beyond-the-circle searches fail as
>   before (O3).
> - Goldens 522 in / 514 out: 516/510 equal to O's simulation line for
>   line, plus 9/7 of wording.
> - B's render: 483 of 485 identical, the two electional exports
>   explained.
> - Cost x1.08 on the real re-runs (median 1.35 -> 1.46 s), x1.09 on
>   120-day searches.
> - The WPF head compiled on the Mac. Mutations (a minute late, ties
>   latest-first) were caught.
>
> Still open:
> - O3 (polar).
> - A score of 88.5 prints "88" (Q's header/breakdown rounding item).
> - The June 2026 equality test cannot see a sect error (E5 covers
>   it).
> - P14 line 130 (the prompt library).
>
> `AstroCast.Tests` **3471 -> 3468**; `AstroCast.UiTests` 88.
>
> Windows (Maple), first run at `14791ac`. It stopped at A0.9 on the
> skill's two `!`. A0.1-A0.8 had all passed:
> - the history;
> - both numstats, equal to the Mac's;
> - every must-be-empty path, 0;
> - the base surface;
> - the removed members, named only in CLAUDE.md's fixed entry.
>
> Second run, at `594ba29`. F's diff is 2/2 in the script alone. The
> `!`/pragma search over `aec94ea..594ba29` finds nothing. The skill
> range's two `!` are the ones F removed, and O2 and F have none.
> - Build: 0 errors across all eight projects, the WPF head included.
>   This was the interface change's first Windows build. `xUnit1031`
>   is the only warning code.
> - Suites: 3468/3468 in 2 m 43 s (O1: 2 m 22 s), and 88/88.
> - Names against `aec94ea`: 19 removed, all
>   `ElectionalPoolSelectionTests` (11 facts, 8 theory cases). 16
>   added: `ElectionalScoringInputsTests` 5,
>   `ElectionalEverySampleTests` 5 and
>   `ElectionalCandidateInstantWordingTests` 6 (3, 3 and 4 theory
>   cases). Theory cases 1272 -> 1274. UiTests names unchanged.
> - Passing by exact name: the 16 added cases; the four updated
>   classes (22); `ElectionalCandidateSeparationTests`,
>   `ElectionalEndBoundTests` and `ElectionalInstantTests` (20); and
>   the 99 golden cases. Of those, 69 are culture runs, the two
>   electional goldens' 8 included.
> - Fresh processes: `ElectionalScoringInputsTests` 5/5,
>   `ElectionalEverySampleTests` 5/5, the Mercury span cases 4/4,
>   `VoidOfCourseRangeTests` 10/10, `SeriesSpanClampTests` 17/17.
> - Surface, by reflection: it gains only
>   `CalculateElectionalScoringInputs` (interface, engine, stub) and
>   loses only `SelectFullScorePoolIndices`.
> - Each commit on its own, all green: `0d7a679` 3471, `fccdbb5`
>   3471, `a92f714` 3476, `6e51d31` 3462, `9ca8c32` 3468, `14791ac`
>   3468. UiTests 88 at each.
>
> Reports: `D:\Code\o2-every-sample-maple-report.md` (first run) and
> `D:\Code\o2-every-sample-maple-r2-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-10, after O2): branch O3, the electional search
beyond the polar circles, part of release 1.3.3**
> `fix/electional-polar-20261010`, three commits, cut from `2dc2cf9`:
> - `ee9c327`: refuse a natal house system with no houses at the place;
> - `1c868b3`: drop and count the samples with no planetary hour;
> - `589d12c`: CLAUDE.md.
>
> Fast-forwarded onto `master` at `589d12c`; this record follows it.
>
> Steve's ruling 2 on O (2026-10-09, question 5, Y6-9):
> - drop the samples with no planetary hour, and say how many;
> - refuse up front, naming the system, where the natal house system has
>   no houses at the place.
>
> O3 completes the O series (O1, O2, O3).
>
> The refusal. Before its scans, the search casts the houses once, in the
> natal chart's house system at the event's place. The engine decides
> which systems fail; no latitude limit is restated. Where there are no
> houses (Placidus or Koch beyond the polar circles), the search throws an
> `ArgumentException` (parameter `input`). Its message names the system
> and the latitude. Until 1.3.3 the engine's house error ended the search
> at the first candidate.
>
> The dropped samples. The hour call alone is wrapped, catching the two
> exceptions the chart engine catches around the same call
> (`SunDoesNotRiseOrSetException`, `InvalidOperationException`). A sample
> with no hour is not scored. The hour is now computed before the scoring
> inputs. `ElectionalResult.SamplesWithoutPlanetaryHour` (`required`)
> counts these samples. Only when the count is above 0, the export adds a
> header line after Separation ("Samples without a planetary hour: n, not
> scored") and a sentence in "No Candidates Found". At 0 the export is
> byte for byte as before.
>
> Evidence from the Mac:
> - E1: 66.3 deg N in June drops 637 of 637 samples. E2: 66.3 deg S in
>   December drops 618 of 618. Both read "No Candidates Found".
> - E3: Tromso, 10-25 May, drops 416 of 1,044 samples and scores 628. On
>   O's chart 56 the top is 78 at 15 May 19:45, O's figure exactly.
> - E4: Placidus at Tromso and 66.6 deg N, and Koch at 66.6 deg S, are
>   refused. 66.5 deg N with Placidus is not.
> - E5: 750 of 750 searches that returned before return the same
>   windows, with 0 dropped.
> - The head's mirror equals the service in 772 of 772 searches and 23
>   refusals.
> - No golden moved; 485 of 485 renders identical; cost x1.00.
>
> Still open, all to Q:
> - a count of 1 prints "1 eligible samples";
> - in a wholly polar week the shortfall clause still names the 6-hour
>   separation;
> - `IElectionalService.Calculate`'s `<exception>` names only the
>   midnight refusal;
> - the refusal reaches the status bar with " (Parameter 'input')".
>
> For the prompt library: P14 assumes Placidus (`:91-92`, `:1265`,
> `:1286`) while the refusal suggests other systems, and P14 does not
> read the new header line.
>
> `AstroCast.Tests` **3468 -> 3477**; `AstroCast.UiTests` 88.
>
> Windows (Maple), at `589d12c`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (9 files, 429/6). No golden, head, Presentation, CLI,
>   UiTests, `ephe/`, `.claude/`, `.gitignore`, project file or other
>   Core file changed.
> - Surface. By source and by reflection, the only change is
>   `ElectionalResult.SamplesWithoutPlanetaryHour`, `public required int
>   { get; init; }`, with its summary and remarks.
> - The refusal sits after the midnight check and the bounds, before the
>   Mercury and void scans. It catches only `EphemerisCalculationException`
>   from its one `CalculateHouses` call.
> - The hour's catch wraps the hour call alone, with the engine's two
>   exceptions.
> - Every `ElectionalResult` built in `src/` and `tests/` sets the count:
>   the service, the three tests that gained a line, and the new export
>   tests.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 429 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3477/3477 in 2 m 51 s (O2: 2 m 43 s), and 88/88.
> - Names against `2dc2cf9`: none removed. 9 added:
>   `ElectionalPolarTests` 6 (4 theory cases) and
>   `ElectionalPolarExportTests` 3 facts. Theory cases 1274 -> 1278.
>   UiTests names unchanged.
> - Passing by exact name: the 9 added; the 17 of the three classes that
>   gained a line; and the 99 golden cases. Of those, 69 are culture
>   runs, the two electional goldens' 8 included.
> - Fresh processes: `ElectionalPolarTests` 6/6,
>   `ElectionalPolarExportTests` 3/3, `ElectionalEverySampleTests` 5/5,
>   the Mercury span cases 4/4, `VoidOfCourseRangeTests` 10/10,
>   `SeriesSpanClampTests` 17/17.
> - Each commit on its own, all green: `ee9c327` 3470/3470 (theory
>   1276), `1c868b3` 3477/3477 (theory 1278), `589d12c` 3477/3477.
>   UiTests 88 at each.
>
> Report: `D:\Code\o3-polar-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-10, after O3): branch Q, the electional score,
part of release 1.3.3**
> `fix/electional-score-20261010`, six commits, cut from `e8ec6c5`:
> - `587dc18`: print the score once, to its exact value;
> - `24cb652`: state the score's ceiling and the event's time zone;
> - `b5f6e09`: a count of one, nothing delivered, and the refusals
>   documented;
> - `b91669e`: the status bar's score and refusals;
> - `0395375`: the reference states the electional score;
> - `4048873`: CLAUDE.md.
>
> Fast-forwarded onto `master` at `4048873`; this record follows it.
>
> Rulings. Steve accepted all twelve of the Q investigation's
> recommendations (2026-10-10), under S-D12 and Q13, O's ruling 3 and
> O3's four follow-ups. The scorer's rules do not change. The figure is
> a "Score ceiling": in five of nine types no sky reaches it.
>
> The prints. The score is printed once, exactly (`0.##`, "/100"
> dropped): the candidate line, the breakdown's weighted cells and its
> Overall. Dimensions stay `F0` and weights `P0`. Every print agrees, and
> the cells add up to the Overall.
>
> Two header lines:
> - "Score ceiling: n (each of the seven dimensions at its highest)",
>   after Election Type. `n` comes from a new internal
>   `ElectionalScorer.ScoreCeiling`, 90.5 to 95.5 by type, over eight
>   private `Highest...` constants.
> - "Event Time Zone: zone (candidate instants and the period tables are
>   on this clock)", after Event Location.
>
> O3's follow-ups:
> - a count of one in the singular;
> - at zero delivered, "0 of N requested -- no moment in the date range
>   qualified";
> - `IElectionalService.Calculate`'s `<exception>` names both refusals.
>
> The status bar (`MainViewModel`, shared by both heads): "(score
> 82.25)" in the screen's culture, and an `ArgumentException`'s message
> without the framework's " (Parameter 'input')" suffix.
>
> The Techniques reference: a new section 11, "The Electional Score"
> (every table of the scorer), and two corrections (the Travel hour at
> `:271`; what the score reads, after the electional priorities).
>
> Evidence from the Mac:
> - No score moved: 758 of 758 searches, and 741,079 samples' breakdowns
>   identical.
> - 0 of 1,855 reported windows with disagreeing prints (529 before).
> - The ceilings equal Tier A for all nine types, and each is above the
>   highest seen.
> - Goldens 41 in / 37 out, all score prints and header lines.
> - 483 of 485 renders identical.
> - Public surface identical. The WPF head compiled.
>
> Still open:
> - For the prompt library, P14: `:130` (the header's count, since O2);
>   `:245` and `:360` (8deg30' -> 8deg); `:504-509` (triplicity and
>   cazimi, which the score does not read); `:733-750` and `:1351` (the
>   algorithm audit pairs the Ascendant and Natal Harmony with evidence
>   the score does not read).
> - `MainViewModel`'s electional summary was not expanded (a code comment
>   instead), under the branch's two-line scope.
>
> **The electional chain of 1.3.3 is complete:** C, P, O1, O2, O3 and Q.
>
> `AstroCast.Tests` **3477 -> 3507**; `AstroCast.UiTests` 88.
>
> Windows (Maple), at `4048873`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (12 files, 1181/61). No other golden, no head, CLI,
>   TestDoubles, UiTests, `ephe/`, `.claude/`, `.gitignore`, project
>   file or other Core or Presentation file changed.
> - In-file diffs. `ElectionalService` loses only six doc lines;
>   `IElectionalService` changes only doc lines; `MainViewModel` changes
>   only the status line and the electional catch, with its comment.
> - Surface. By source and by reflection over every type of Core and
>   Presentation, every public member is identical. The only change is
>   `internal static double ScoreCeiling(ElectionType)` on
>   `ElectionalScorer`. Its eight constants are private (100, 85, 50, 90,
>   70, 100, 100, 100).
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 1,181 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3507/3507 in 2 m 54 s (O3: 2 m 51 s), and 88/88.
> - Names against `e8ec6c5`: none removed. 30 added (24 theory cases, 6
>   facts): `ElectionalScoreCeilingTests` 17, `ElectionalScorePrintTests`
>   9 (3 theory) and `ElectionalStatusBarTests` 4. Theory cases 1278 ->
>   1302. UiTests names unchanged. The captures are UTF-8, so the `...`
>   truncations and degree signs are intact.
> - Passing by exact name: the 30 added; `ElectionalStatusBarTests`'
>   de-DE status line on Windows (the whole line, its date in Windows'
>   de-DE); and the 99 golden cases. Of those, 69 are culture runs, the
>   two electional goldens' 8 included.
> - Fresh processes: `ElectionalScoreCeilingTests` 17/17,
>   `ElectionalScorePrintTests` 9/9, `ElectionalStatusBarTests` 4/4,
>   `ElectionalPolarTests` 6/6, the Mercury span cases 4/4,
>   `VoidOfCourseRangeTests` 10/10, `SeriesSpanClampTests` 17/17.
> - Each commit on its own, all green: `587dc18` 3481 (theory 1281),
>   `24cb652` 3500 (1298), `b5f6e09` 3503 (1298), `b91669e` 3507 (1302),
>   `0395375` 3507, `4048873` 3507. UiTests 88 at each.
>
> Report: `D:\Code\q-score-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-10, after Q): branch F7, declination, part of
release 1.3.3**
> `fix/declination-20261010`, five commits, cut from `63dd38c`:
> - `4aa5d58`: out of bounds by the chart's own obliquity;
> - `acf35e4`: one aspect per pair, by side of the equator;
> - `8ee5974`: clusters span 2 deg or less;
> - `f855417`: CLAUDE.md;
> - `e1d1841`: the paran tests' copy keeps its chart's obliquity.
>
> Fast-forwarded onto `master` at `e1d1841`; this record follows it. It
> carries findings F5-1, F5-2 and F5-3 of the foundations audit and Y1-3
> of the synthesis (M1), under cards D13 and D14.
>
> Rulings (Steve, 2026-10-10):
> - D13, option (b): a parallel needs the same side, a contraparallel
>   opposite sides; a body at exactly 0 deg counts as north. Applied in
>   the cross-chart method too (Y1-3, M1).
> - D14, option (a): clusters span 2 deg or less. The window with the
>   most members wins; on a tie, the narrower span, then the lower start;
>   repeat until no window of three is left.
> - Out of bounds against each chart's true obliquity, carried on
>   `ChartResult` as a required value; the fixed constant goes.
> - The Sun is never out of bounds. With the true obliquity, its apparent
>   declination passes the bound by up to 0.0003 deg near about half of
>   all solstices.
> - The wording: "within a span of 2 deg or less".
> - `TransitService`'s copy of the two tests stays for branch D in 1.3.4.
>
> The change:
> - `ChartResult.Obliquity` (required), set by the engine's paths and the
>   composite from the obliquity they already fetch;
> - `IsOutOfBounds(double?, double)`, the new
>   `IsOutOfBounds(PlanetPosition, double)` (never the Sun), and
>   `ObliquityThreshold` deleted;
> - Excess against the chart's obliquity;
> - the side rule in `Calculate` and `FindCrossChartDeclinationAspects`;
> - the cluster choice, with `MergeOverlappingClusters` and the closing
>   sort deleted;
> - the paran tests' copy helper keeps its source's obliquity.
>
> Evidence from the Mac:
> - Goldens 21 in / 22 out over 11 fixtures: 3 Excess values, 1 `Par`
>   row, 18 cluster rows.
> - The 95 saved charts: exactly the foundations audit's lists (Excess on
>   17; 37 doubled rows in 29; clusters in 30), plus 6 charts whose
>   equal-sized clusters now come in the order chosen.
> - At head no cluster is wider than 2 deg, no pair is listed twice, and
>   no Sun is out of bounds.
> - 297 of 485 renders identical, every changed line classed and
>   explained.
> - The public surface changes in exactly three places; the WPF head
>   compiled.
>
> Still open:
> - For the prompt library: eleven current prompts read out of bounds
>   against a fixed 23 deg 26 min. The scans are Natal v3_4 `:745-746`,
>   Transit v3_6 `:668` and `:821`, Timeline V2_6 `:171` and `:660`,
>   Synastry V1_10 `:583` and Election V2_13 `:965`; the other lines are
>   glossary entries (the full list is in the Mac's
>   `f7-declination-report.md`). Horary V2_8 `:964-965` reads clusters as
>   "within +/-2 deg".
> - Rows of equal orb keep no fixed order (`List.Sort` is unstable); F7
>   did not change that.
>
> `AstroCast.Tests` **3507 -> 3542**; `AstroCast.UiTests` 88.
>
> Windows (Maple), at `e1d1841`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (60 files, 1020/189). No other golden, no head, CLI,
>   Presentation, `TransitService` or other Core file, no Techniques
>   reference, `ephe/`, `.claude/`, `.gitignore` or project file changed.
> - In-file checks, by script for the 32 construction-site files: all 37
>   added lines are `Obliquity = 23.4367,`, except the paran tests'
>   `Obliquity = source.Obliquity,`, and nothing is removed.
>   `CompositeService`, the electional OOB line, `PositionFormatterTests`
>   and `DeclinationClusterTests` change only as stated; the interface
>   and the two models change only doc lines.
> - Surface. By source and by reflection over every type of Core and
>   Presentation: public `IsOutOfBounds(double?)` removed,
>   `IsOutOfBounds(double?, double)` and
>   `IsOutOfBounds(PlanetPosition, double)` added, `ChartResult.Obliquity`
>   added; internal `ObliquityThreshold` gone; Presentation identical.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 1,020 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3542/3542 in 2 m 54 s (Q: 2 m 54 s), and 88/88.
> - Names against `63dd38c`: none removed. 35 added (24 theory cases, 11
>   facts): `ChartObliquityTests` 6, `OutOfBoundsObliquityTests` 12,
>   `DeclinationSideTests` 12 and `DeclinationClusterSpanTests` 5. Theory
>   cases 1302 -> 1326. UiTests names unchanged. Captures kept UTF-8.
> - Passing by exact name: the 35 added, the Sun of 1949-12-22 past the
>   chart's obliquity and not flagged on Windows, and the 99 golden runs
>   (30 base, 69 culture).
> - Fresh processes: `ChartObliquityTests` 6/6,
>   `OutOfBoundsObliquityTests` 12/12, `DeclinationSideTests` 12/12,
>   `DeclinationClusterSpanTests` 5/5, `DeclinationClusterTests` 11/11,
>   `DeclinationAspectServiceTests` 11/11,
>   `CrossChartDeclinationAspectTests` 9/9, `ChartMarkdownParanTests` 9/9.
> - Each commit on its own, all green: `4aa5d58` 3525 (theory 1315),
>   `acf35e4` 3537 (1326), `8ee5974` 3542 (1326), `f855417` 3542,
>   `e1d1841` 3542. UiTests 88 at each.
>
> Report: `D:\Code\f7-declination-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-10, after F7): branch E, the daily horoscope,
part of release 1.3.3**
> `fix/daily-horoscope-20261010`, seven commits, cut from `50c9897`:
> - `24b97e4`: the day logic moves to `DailyHoroscopeService`;
> - `8edeb55`: a day is its own instants; the void pad goes;
> - `10a7e29`: passes and void bounds on their local day, in local time;
> - `f348b3d`: the Moon's sign change at any hour;
> - `57b9c32`: orb, phase and listing at local noon;
> - `b9d77ca`: a golden for the daily horoscope;
> - `1960920`: CLAUDE.md.
>
> Fast-forwarded onto `master` at `1960920`; this record follows it. It
> carries findings T1-6, T1-7, T1-13 and E2-7.
>
> Rulings (Steve, 2026-10-10): all eleven recommendations of the E
> investigation.
> - Orb and phase are read at local noon; a transit is listed when in orb
>   at noon; part-day transits go to Entering/Exiting orb with their time.
> - Orbs stay truncated (O-D3).
> - A Core `DailyHoroscopeService`; `BuildDailyData` removed.
> - Passes, entries, exits and void bounds are filed by instant on the
>   local day; invented passes (residual over 0.01 deg) are dropped.
> - Times print as local `HH:mm`, with the zone's word only on a repeated
>   reading. The electional shares the helper, moved to
>   `ChartTimeFormatter` with its body unchanged.
> - The backdrop prints local dates.
> - The Moon's sign change is noted at any hour.
> - `LocalDayStartToJulianDay` for every day edge; the void pad removed.
> - A skipped date is left out.
> - A daily golden and tests.
> - The daily prompt's lines go on the library list.
>
> The change:
> - A new Core service: `DailyHoroscopeService.BuildDays(TransitResult,
>   string)` builds the days; `DailyHoroscopeMarkdownFormatter` keeps only
>   the Markdown; `MainViewModel` (both heads) calls the service at its one
>   call site.
> - A day is its own instants: its edges are `LocalDayStartToJulianDay`,
>   the three-day void pad is gone, and a date the zone skipped
>   (Pacific/Apia, 2011-12-30) is left out.
> - Every exact pass, void bound, entry, exit and Moon sign change is
>   filed on the local day its instant falls in, printed `HH:mm` on the
>   day's clock with the zone's word only where the clocks repeat the
>   reading. The rule is one helper, `ChartTimeFormatter`'s, moved out of
>   the electional unchanged. Invented passes are dropped; the backdrop's
>   "In Orb" dates are local; the Timezone line names every time.
> - The Moon's sign change is noted at any hour ("Moon enters Cancer at
>   15:54").
> - Orb, phase and listing at local noon: the true orb; "Exact" or F3's
>   `AspectDirection` rule; listed when in orb at noon; entering and
>   exiting from the day's own positions, with their times; an "Orbs and
>   Phases" header line says so.
> - A daily golden, `DailyHoroscopeMarkdownFormatter_J2000NYC_20261020_
>   20261105`, with its three culture runs.
>
> Evidence from the Mac:
> - Commit 1 changed no output (81 exports, 485 renders).
> - At head over the three sets: rows equal the in-orb-at-noon truth
>   (15,349 / 882 / 87,948); 0 rows out of orb or above their limit; orbs
>   and phases equal Tier B and F3's rule at noon; passes (360 / 22 /
>   4,110), entries (386 / 21 / 4,081), exits (418 / 20 / 4,072) and Moon
>   ingresses (880 / 70 / 6,080) on their local days at the right minute;
>   0 backdrop cells wrong; no void string changed.
> - On the 25 daily exports, 5,232 rows: 0 wrong, 128 of 128 passes, 306
>   of 306 sign changes.
> - The 81 exports equal the investigation's recommended outputs byte for
>   byte.
> - 483 of 485 renders identical (the two daily exports changed).
> - The golden reviewed against Tier B, 0 disagreements.
> - A 61-day report in 479 ms (488 before).
> - The WPF head compiled; the public surface changed exactly as listed.
>
> Still open:
> - For the prompt library, `AstroCast_DailyHoroscopeInterp_Prompt_V2_2`:
>   `:569-573` says exact times are in UTC, and every time is now local;
>   `:652-655` reads "Moon enters [Sign] today" and "mid-day ... evening",
>   and the note now gives the time.
> - T1-11, for branch D: a transit in orb only between the transit
>   service's 12:00 UT samples is not listed at all.
> - T1-1, for branch D: the cause of the invented passes.
> - 1.4 hygiene (Steve, 2026-10-10): `JulianDayUtility.
>   LocalMidnightToJulianDay` has no caller in `src/`, only in tests. It
>   is kept.
> - The concurrent-save race: a duplicate Id once in 157 Maple runs of
>   `SqliteChartRepositoryConcurrencyTests`. A read-only investigation
>   follows E (Steve, 2026-10-10).
>
> `AstroCast.Tests` **3542 -> 3564** (1329 theory cases);
> `AstroCast.UiTests` 88; golden runs 99 -> 103.
>
> Windows (Maple), at `1960920`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (16 files, 1812/562). No other golden, no head, CLI,
>   TestDoubles, UiTests, `TransitService` or other Core or Presentation
>   file, no Techniques reference, `ephe/`, `.claude/`, `.gitignore` or
>   project file changed.
> - In-file diffs as stated. `RepeatedReadingOffset` (11 lines) and
>   `ZoneWord` (4 lines) are in `ChartTimeFormatter` byte for byte as
>   removed from the electional, private -> internal only, checked by
>   script. Two test files also change one `<see cref>` doc line each,
>   from `BuildDailyData` to `BuildDays`.
> - Surface. By source and by reflection over every type of Core and
>   Presentation, exactly as listed: `BuildDailyData` removed;
>   `DailyHoroscopeService` with its constructor and `BuildDays`,
>   `TryGetAspect`, and the `MoonSignChange`, `EntryTime` and `ExitTime`
>   accessors added; internal `RepeatedReadingOffset`, `ZoneWord`,
>   `LongitudeToSign` and `FormatPlanetName`; Presentation identical.
> - Removed members: no call or reference to `BuildDailyData`,
>   `VoidScanPadDays`, `IsInOrbOnDay`, `GetLifecycleDate`,
>   `DeterminePhaseOnDay` or `ApproximateOrbOnDay` in `src/` or `tests/`
>   (only test names and one `<c>` doc mention).
>   `LocalMidnightToJulianDay` has no `src/` caller, six test callers.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 1,812 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3564/3564 in 3 m 18 s (F7: 2 m 54 s), and 88/88.
> - Names against `50c9897`: none removed. 22 added (3 theory cases, 19
>   facts): `DailyHoroscopeServiceTests` 11,
>   `DailyHoroscopeExportLineTests` 7, and the new golden's 4. Theory
>   cases 1326 -> 1329. UiTests names unchanged. Captures kept UTF-8.
> - Passing by exact name: the 22 added (London 1301, Havana and Apia on
>   the app's tzdb among them); the 8 daily `ExplicitOffsetBirthLineTests`
>   cases; and 103 golden runs (31 base, 72 culture). The new golden
>   passes on Windows at its base run and under de-DE, sv-SE and th-TH.
> - Fresh processes: `DailyHoroscopeServiceTests` 11/11,
>   `DailyHoroscopeExportLineTests` 7/7,
>   `DailyHoroscopeBuildDailyDataTests` 5/5,
>   `DailyHoroscopeIntegrationTests` 11/11,
>   `DailyHoroscopeVocScanFloorTests` 2/2, `DailyHoroscopeFormatterTests`
>   15/15, `ExplicitOffsetBirthLineTests` 48/48,
>   `UI.DailyHoroscopeWpfTests` 1/1.
> - Each commit on its own: `24b97e4` 3542, `8edeb55` 3545, `10a7e29`
>   3551, `57b9c32` 3560, `b9d77ca` 3564 (theory 1329, golden runs 103),
>   all green on the first run; UiTests 88 at each. `f348b3d` failed once
>   there, 3554/3555, on `SqliteChartRepositoryConcurrencyTests.
>   SaveAsync_TenConcurrentWrites_AllSucceedAndListAllSeesAllRows` (a
>   duplicate Id from ten parallel saves); no E commit touches the
>   database code. Steve ruled a re-run of that commit alone: green,
>   3555/3555 (theory 1326, golden runs 99/99, the concurrency tests
>   passing by name) in 3 m 13 s, and UiTests 88/88.
>
> Reports: `D:\Code\e-daily-maple-report.md` (first run) and
> `D:\Code\e-daily-maple-r2-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-11, after E): the unpooled chart repository,
part of release 1.3.3**
> `fix/sqlite-pooling-20261011`, two commits, cut from `8ead232`:
> - `62ba8bd`: the chart repository opens unpooled connections;
> - `b7d3fac`: docs (CLAUDE.md and the concurrency test's remarks).
>
> Fast-forwarded onto `master` at `b7d3fac`; this record follows it.
>
> The finding, from the read-only investigation that followed E:
> - Microsoft.Data.Sqlite's connection pool, 10.0.x through 10.0.12, can
>   lend one native connection to two owners when two opens coincide
>   (dotnet/efcore#39008, fixed by #39012 on `release/10.0`, not yet in a
>   published package).
> - On a shared connection, `SaveAsync`'s `SELECT last_insert_rowid()`
>   can read the other save's Id, and a busy reset can roll back the
>   other save's row: a wrong Id, or a silently lost save.
> - Only a concurrent caller can reach it. Neither head nor the CLI is
>   one today: every repository call runs alone. The planned MCP server
>   would be the first.
> - It is the duplicate Id that `SqliteChartRepositoryConcurrencyTests`
>   showed once in 157 Maple runs during E.
>
> Rulings (Steve, 2026-10-11): all six recommendations of the
> investigation.
> - Test hygiene for 1.3.3, with the latent finding recorded.
> - Pooling off in `SqliteChartRepository`'s constructor, in 1.3.3.
> - The concurrency test kept in the gating suite, its WAL remark
>   corrected.
> - In 1.4, Microsoft.Data.Sqlite 10.0.13, keeping the SQLitePCLRaw
>   2.1.12 pin, with pooling still off.
> - `INSERT ... RETURNING`, a transaction, WAL and a static lock are
>   rejected.
>
> The change:
> - The constructor rebuilds the caller's connection string through
>   `SqliteConnectionStringBuilder` with `Pooling = false`, keeping every
>   other setting.
> - One internal member, `EffectiveConnectionString`, and two tests in
>   `SqliteChartRepositoryPoolingTests` (a file database; a shared
>   in-memory database keeping its mode, cache and name).
> - The concurrency test's remarks: the file database uses SQLite's
>   default rollback journal, not WAL, and the tests caught #39008. No
>   method or assertion changed.
> - CLAUDE.md: the reason, the 1.4 plan and the test totals.
>
> Evidence from the Mac:
> - The investigation's harness: 0 failures in 2,000 natural runs and
>   200 amplified runs (189 of 200 before, at `8ead232`).
> - The real test passed 500 of 500, each run in its own process.
> - A save costs 0.44 ms against 0.25 ms.
> - 485 of 485 renders identical; 103 golden runs; the public surface
>   identical.
>
> Still open:
> - 1.4: Microsoft.Data.Sqlite to 10.0.13 when it is published, keeping
>   the `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 pin, with pooling still
>   off.
> - Docs-1: the concurrency test's `Dispose` doc still names "WAL/shm
>   sidecars"; the rollback journal's sidecar is `-journal`.
> - A side effect: a malformed connection string now fails when the
>   repository is constructed, not at first use.
> - 1.4, noted: a Windows save costs about 6 ms (the commit's flushes,
>   and likely the scanner). This branch does not change that; WAL is
>   its own performance decision.
>
> `AstroCast.Tests` **3564 -> 3566**; `AstroCast.UiTests` 88; golden runs
> 103.
>
> Windows (Maple), at `b7d3fac` (first run):
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (4 files, 125/6). No golden, head, CLI, Presentation,
>   TestDoubles, UiTests or other Core file, no `ephe/`, `.claude/`,
>   `.gitignore` or project file changed: the package versions and the
>   SQLitePCLRaw pin are as they were.
> - In-file diffs as stated, by script. `SqliteChartRepository.cs`
>   changes the constructor's assignment, its doc lines, the class
>   summary's "unpooled" and the internal member, and no SQL (its 19
>   SQL-bearing lines identical). The concurrency test changes 8 doc
>   lines (6 added, 2 removed) and nothing else (104 code lines and 7
>   assertions identical).
> - Surface. By source and by reflection over every type of Core and
>   Presentation: the public surface identical; one internal member
>   added, `SqliteChartRepository.EffectiveConnectionString`;
>   Presentation identical.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 125 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3566/3566 in 3 m 18 s (E: 3 m 18 s), and 88/88.
> - Names against `8ead232`: none removed. 2 facts added, both in
>   `SqliteChartRepositoryPoolingTests`. Theory cases 1329; nested-class
>   names 3. UiTests names unchanged. Captures kept UTF-8.
> - 103 golden runs (31 base, 72 culture), all passing.
> - Fresh processes: `SqliteChartRepositoryPoolingTests` 2/2,
>   `SqliteChartRepositoryConcurrencyTests` 2/2,
>   `SqliteChartRepositoryTests` 43/43.
> - The concurrency tests in 200 fresh processes, with `TMP` in scratch:
>   200 of 200 passed (400 results), about 4.3 s per run, no database
>   file left behind.
> - The cost on Windows. A Core-only harness built against `8ead232`'s
>   and `b7d3fac`'s Core made 2,000 sequential saves on a fresh file,
>   three runs each, alternating. Median of the three medians per save:
>   base 5.96 ms, head 6.59 ms, so +0.63 ms per save (p90 9.15-9.56
>   against 10.13-10.29 ms). `ListAllAsync` over 2,000 rows: 8.13
>   against 8.85 ms. Both builds report `journal_mode=delete`.
> - The gate. It was first set at 5 ms absolute, from the Mac's figures,
>   and the unchanged base also exceeds that on Maple (no base save in
>   6,000 took under 4.96 ms), so the first run stopped there. Steve
>   re-scoped it (2026-10-11) to "head minus base <= 1 ms" at the median
>   of medians: +0.63 ms passes.
> - Microsoft Defender's real-time protection is off ("Not running");
>   ESET Security is registered, and probably active.
>
> Windows (Maple), second run:
> - `62ba8bd` on its own, in a detached checkout: 0 errors across all
>   eight projects, `xUnit1031` the only warning code; 3566/3566 in
>   3 m 17 s (1329 theory cases, names identical to `b7d3fac`'s, 103
>   golden runs) and 88/88; both `SqliteChartRepositoryConcurrencyTests`
>   passing by name.
>
> Reports: `D:\Code\sqlite-pooling-maple-report.md` (first run, stopped
> at the gate) and `D:\Code\sqlite-pooling-maple-r2-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-11, after the unpooled repository): branch F6,
houses and sect, part of release 1.3.3**
> `fix/houses-sect-20261011`, five commits, cut from `ecb741e`:
> - `19b4a01`: one rule finds the house a longitude lies in;
> - `69b97f3`: hayz from the body's altitude;
> - `89d3aa0`: a body's house is the arc its longitude lies in;
> - `47b8bdc`: day or night from the Sun and the horizon;
> - `b155beb`: docs.
>
> Fast-forwarded onto `master` at `b155beb`; this record follows it. It
> carries findings F2-1, F2-2 and F4-6 of the foundations audit and Y1-1
> of the synthesis.
>
> Rulings: D2 with S-D1, and D3 (2026-10-03). Then the ten of 2026-10-11
> (Steve), the recommendations of the read-only investigation:
> - hayz's "above the horizon" is the body's true altitude, and
>   composites go by longitude;
> - `HouseExact` is the house plus the fraction of the arc;
> - one placement rule and one day-or-night rule replace the copies;
> - placement comes off `IEphemerisService`;
> - reversed polar cusps are followed in their own direction;
> - D3 inside the polar circles is S-prime;
> - the fixtures and the polar tests' values are rewritten, with no
>   assertion loosened;
> - the docs change in F6 and the prompts in Docs-1, where none becomes
>   wrong;
> - one branch in five commits, the altitude before the placement;
> - `DetermineHouseNumber` is removed.
>
> The change:
> - Houses by longitude (D2, S-D1). A body's house is the arc between
>   the cusps that its longitude lies on, taken in the cusps' direction;
>   the cusps run backwards for Regiomontanus and Campanus inside the
>   polar circles. One internal rule, `HousePlacement`, replaces
>   `swe_house_pos` (which used the body's latitude) and four copied
>   helpers. `HouseExact` is the house plus the fraction of the arc, so
>   its whole part is always `House`. Koch's "House 0" (a library failure
>   value) can no longer occur.
> - Placement leaves the ephemeris. `IEphemerisService.AssignHouses` and
>   `EphemerisService.AssignHouses` are deleted, with the four test
>   doubles' members, and so is the public
>   `TransitAspectFinder.DetermineHouseNumber`.
> - Hayz by altitude (F4-6). "Above the horizon" is the body's geometric
>   altitude, from its RA and Dec, the ARMC and the latitude, held on the
>   new `PlanetPosition.Altitude`, which nothing prints. Composites, which
>   have no sky, go by longitude. `ISectService.Analyze` gains an optional
>   `HouseData? houses`.
> - Day or night by the Sun's longitude against the horizon axis (D3,
>   S-prime). One internal rule, `ChartSect`, at all four sites that
>   decided it, flipped where the ecliptic's upper-meridian point is
>   below the horizon (only inside the polar circles). `HouseData` gains
>   `Obliquity` and `GeographicLatitude` for that test.
>
> Evidence from the Mac:
> - The grid: 6,051,024 placements equal Tier A; House 0 never occurs
>   (34,598 before).
> - Polar: 1,150,800 reversed placements equal floor(`swe_house_pos`) at
>   latitude 0.
> - The sweep: day or night right in all 10,003 charts (167 change, Whole
>   Sign and Morinus); hayz right everywhere (712 planets in 493 charts
>   change).
> - 59,883 polar charts cast: day or night right in every one.
> - The saved charts: Markdown 89/89 in 22 charts (the audit's 27
>   placements); JSON 974/974 in 93.
> - The goldens: 3 fixtures, 17/17.
> - The render: 183 of 487 outputs change, 1,487 lines in and 1,507 out,
>   identical to the investigation's simulation.
> - The electional searches: 31,901 samples change (the significator's
>   house term); windows change in 167 searches and the top in 76; R07,
>   R24 and R25 move their top to 2026-03-26 12:45 (78.3).
> - Composites: day or night unchanged in 160 of 160 pairs; 56
>   placements move; no hayz row.
> - Cost: the search x0.976, a chart x1.00.
> - The surface exactly as planned; the WPF head compiled.
>
> Still open:
> - Relocation's sect stays the relocated chart's until K2 (S-D6, 1.3.4).
> - F6 makes no prompt line wrong.
> - The investigation's measurement data (about 2.6 GB, on the Mac) may
>   be deleted.
>
> `AstroCast.Tests` **3566 -> 3585** (1335 theory cases);
> `AstroCast.UiTests` 88; golden runs 103.
>
> Windows (Maple), at `b155beb`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (36 files, 1005/296). No other golden, head, CLI,
>   Presentation, UiTests or other Core file (`SwissEphInterop.cs`
>   included; its `swe_house_pos` declaration stays), no `ephe/`,
>   `.claude/`, `.gitignore` or project file changed.
> - In-file checks, by script. The JSON golden changes exactly the 11
>   `houseExact` values in body order (the Moon's 10.6 unchanged; at head
>   every whole part equals the house). Each electional golden changes
>   exactly its three lines: Pluto's house 2 -> 1, the Air row (Richmond
>   1 -> 2, NYC 0 -> 1) and the Pluto Health row 2 -> 1.
>   `ElectionalPolarTests` changes the two values (75.45 -> 79.2;
>   Reykjavik 09:15 -> 09:00 at 68.25) and the two doc comments, and
>   nothing else.
> - The deleted members are named nowhere in `src/` or `tests/`: only
>   `#region AssignHouses` and three test names carry the word. The four
>   doubles lose only `AssignHouses`; the two fixtures change only the
>   Sun's longitude, and no assertion line.
> - Surface. By source and by reflection over every type of Core and
>   Presentation, exactly as listed: public `AssignHouses` (interface and
>   service) and `DetermineHouseNumber` removed; `PlanetPosition.Altitude`
>   and `HouseData.Obliquity` and `GeographicLatitude` added, each
>   documented; `Analyze` (interface and service) gains `houses`;
>   internal `HousePlacement` and `ChartSect` added,
>   `GetHouseForLongitude` and `FindHouse` removed; Presentation
>   identical.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 1,005 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3585/3585 in 3 m 19 s (the pooling landing: 3 m 18 s), and
>   88/88.
> - Names against `ecb741e`: 6 removed (4 theory cases, 2 facts), the
>   moved tests' old names; 25 added (10 theory cases, 15 facts):
>   `HousePlacementTests` 16 and `ChartSectTests` 9. Theory cases 1329 ->
>   1335; nested-class names 3. UiTests names unchanged. Captures kept
>   UTF-8, the theory doubles compared as printed.
> - Passing by exact name: the 25 added; all 6 `ElectionalPolarTests`
>   (the two changed values among them); the six tests on the rewritten
>   fixtures; and 103 golden runs (31 base; 24 each under de-DE, sv-SE
>   and th-TH).
> - Fresh processes: `HousePlacementTests` 16/16, `ChartSectTests` 9/9,
>   `ElectionalPolarTests` 6/6, `ChartContextServiceTests` 54/54 (one
>   nested), `ElectionalScorerTests` 20/20, `SectServiceTests` 27/27,
>   `HouseCalculationTests` 9/9, `SynastryServiceTests` 9/9,
>   `ArabicPartServiceTests` 20/20.
> - Each commit on its own, in detached checkouts, all green on the first
>   run: `19b4a01` 3573 (theory 1331), `69b97f3` 3576 (1331), `89d3aa0`
>   3579 (1331), `47b8bdc` 3585 (1335); UiTests 88 and 103 golden runs at
>   each; `xUnit1031` the only warning code.
>
> Report: `D:\Code\f6-houses-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-11, after F6): branch F8, the Lots, part of
release 1.3.3**
> `fix/lots-20261011`, three commits, cut from `232064d`:
> - `6580ce6`: a Lot keeps its full longitude (F8-2, F8-3);
> - `85f4b8c`: the Lots of Necessity and Courage follow Paulus (L1);
> - `2b79345`: docs.
>
> Fast-forwarded onto `master` at `2b79345`; this record follows it. It
> carries findings F8-1, F8-2 and F8-3 of the foundations audit.
>
> Rulings:
> - L1 (2026-10-03): Necessity and Courage follow Paulus Alexandrinus,
>   ch. 23, reversing by night (Q12).
> - Children and Exaltation by night are unchanged until checked against
>   a printed source.
> - Fortune by night stays reversed.
> - Steve went straight to implementation (2026-10-11).
>
> The change:
> - A Lot keeps its full longitude (F8-2, F8-3). `ArabicPartService` no
>   longer rounds a Lot to 4 decimals; its printed position, sign,
>   degrees, minutes and ruler all read one value. The chart JSON still
>   prints a Lot's longitude to 4 places, under its policy for angular
>   fields, so no JSON longitude changes.
> - The Lots of Necessity and Courage follow Paulus Alexandrinus, ch. 23
>   (L1, Q12). By day they are Asc + Fortune - Mercury and
>   Asc + Fortune - Mars; by night they reverse to Asc + Mercury - Fortune
>   and Asc + Mars - Fortune. In the catalog the two factors swap. Every
>   other Lot is unchanged, the Part of Children and the Part of
>   Exaltation included.
> - Docs: the reference's two formulas, and a note that all seven
>   Hermetic Lots reverse by night; CLAUDE.md's test totals.
>
> Evidence from the Mac:
> - Commit 1: the audit's 128 Lots rows in 66 saved charts move by one
>   arcsec (59 low, 69 high); no JSON value changes on the saved charts.
> - Commit 2: Necessity and Courage change on all 95 saved charts,
>   changing sign on 89 and 86.
> - The sweep: 150,045 Lots equal Tier A within 2.3e-13 deg; chart
>   8,124's Nemesis is now Aquarius, ruled by Saturn, printed 29 deg 59
>   min 59 sec.
> - The goldens: 16/16 in 5 fixtures, then 42/42 in 12, all Lots rows or
>   `arabicParts` fields. The audit's 25/25 included 15 JSON lines that
>   R1's 4-place JSON keeps.
> - The render: only Lots sections and `arabicParts` change; zodiacal
>   releasing is identical.
> - The surface identical.
>
> Still open:
> - The prompt library's lines quoting the old Necessity and Courage
>   formulas (the audit's card L1: N `:500-521`, L `:382-399`, E
>   `:764-766`).
>
> `AstroCast.Tests` **3585 -> 3591** (1339 theory cases);
> `AstroCast.UiTests` 88; golden runs 103.
>
> Windows (Maple), at `2b79345`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (19 files, 167/69; the Mac's report said 20 files, its
>   own list 19). No other golden, no other file under `src/` (heads,
>   CLI and Presentation included), no UiTests, TestDoubles or other
>   test file, no `ephe/`, `.claude/`, `.gitignore` or project file
>   changed.
> - In-file checks, by script. `ArabicPartService` changes the one
>   rounding line; `ChartJsonFormatter` rounds the Lots' JSON longitude
>   to 4 places, with a two-line comment; the catalog swaps Necessity's
>   and Courage's factors, `ReversesAtNight = true` kept, with a
>   two-line Paulus comment above each; `ArabicPart` changes doc lines
>   only.
> - The goldens, classified per commit. `6580ce6`: 5 fixtures, 16 lines
>   in and 16 out, each a Lots row moved by exactly one arcsec (9 up, 7
>   down), name, house, ruler, formula and domain unchanged. `85f4b8c`:
>   12 fixtures, 42 in and 42 out: 30 Lots rows of Necessity or Courage,
>   each new formula the old one with its factors swapped, and 12 fields
>   of those two Lots in the JSON golden's `arabicParts` (7 and 5);
>   everything else in that JSON identical. No other Lot, no other line.
> - The test file: three methods added (two facts, one theory). Of the
>   existing methods only
>   `CalculateAll_Should_ComputeLotOfNecessity_ReferencingFortune`
>   changes (expected 15.0 -> 345.0, its comment, a `<summary>`); one
>   `using` is added for the new fact's `PositionFormatter`.
> - Surface. By reflection over every type of Core and Presentation,
>   public and internal: identical.
> - Commit files as stated. 0 pragma or suppression, and 0 null-forgiving
>   `!`, in 167 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3591/3591 in 3 m 16 s (F6: 3 m 19 s), and 88/88.
> - Names against `232064d`: none removed; 6 added (4 theory cases, 2
>   facts), all in `ArabicPartServiceTests`. Theory cases 1335 -> 1339;
>   nested-class names 3. UiTests names unchanged. Captures kept UTF-8.
> - Passing by exact name: the 6 added and the rewritten Necessity test;
>   103 golden runs (31 base; 24 each under de-DE, sv-SE and th-TH).
> - Fresh process: `ArabicPartServiceTests` 26/26 (the Mac's 26).
> - Each commit on its own, in detached checkouts, all green on the first
>   run: `6580ce6` 3587 (theory 1335; its two facts added), `85f4b8c`
>   3591 (1339); UiTests 88 and 103 golden runs at each; `xUnit1031` the
>   only warning code.
>
> Report: `D:\Code\f8-lots-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-12, after F8): branch N, temperament, part of
release 1.3.3**
> `fix/temperament-20261011`, four commits, cut from `224dd01`:
> - `51b6b64`: a tie is printed as a tie (Y5-1);
> - `28851e6`: derived charts print no temperament (S-D9);
> - `92779cb`: the scheme is named as the app's own (S-D9; Y5-2, Y5-3);
> - `aeed92d`: no secondary when no other element scores (the fix-up).
>
> Fast-forwarded onto `master` at `aeed92d`; this record follows it. It
> carries findings Y5-1, Y5-2 and Y5-3 of the synthesis audit, under card
> S-D9 (2026-10-03).
>
> Rulings:
> - S-D9 (2026-10-03): the scheme is kept and named as the app's own; a
>   tie is printed as a tie.
> - Steve, 2026-10-11: no temperament on relocated, composite or
>   candidate charts; returns and event charts keep it (Q4); a tie is
>   marked at first and at second place.
> - Steve, 2026-10-12: a second place at 0 points prints "none";
>   `SecondaryTemperament` keeps today's value.
>
> The change:
> - A tie is printed as a tie, at first and at second place (Y5-1).
>   `TemperamentProfile` gains the public lists `DominantTemperaments`
>   and `SecondaryTemperaments`, computed from the scores, and the
>   internal texts `DominantText` and `SecondaryText`, for example
>   `Sanguine and Phlegmatic (tied, 5 each)`. Both exports print the
>   texts. A second place prints `none` when all four scores tie, or when
>   no other element scores. The old `DominantTemperament`,
>   `SecondaryTemperament` and `Description` keep their values.
> - Derived charts print no temperament (S-D9).
>   `ChartMarkdownFormatter.ToMarkdown` gains
>   `bool includeTemperament = true`. The relocated chart, the composite
>   and each electional candidate pass `false`, so they lose the
>   `### Temperament` block; the rest of their medical section stays.
>   Returns, event charts, natal, synastry, transit, timeline and
>   medical exports keep it.
> - The scheme is named as the app's own (S-D9; Y5-2, Y5-3). Both
>   scoring lines read "Scoring (AstroCast's own scheme): ... each body
>   in houses 1, 4, 7 or 10 ...". `TemperamentCalculator`'s docs drop
>   the claim that it is based on Lehman, Oliveira and the Galenic
>   framework.
>
> Evidence from the Mac:
> - 28 saved charts tie at first or second place. F6's houses by
>   longitude moved 16 charts' points since the audit, so the
>   first-place ties are charts 28, 51, 52, 55, 56, 68 and 84.
> - Chart 55 (2/3/5/5) prints "Sanguine and Phlegmatic (tied, 5 each)"
>   with "Melancholic" second; charts 21 and 22 (14/0/0/0, 13/0/0/0)
>   print "none" second.
> - The goldens: 1 line, then 91 out (seven whole 13-line blocks), then
>   10/10 scoring lines, then none.
> - The render: 11 blocks removed from the relocated, composite and
>   electional outputs, and 249 kept.
> - The surface exactly as planned; the WPF head compiled.
>
> Still open:
> - The prompt library (the Mac's report, Verification 9): the
>   relocation prompt P11 `:158`, `:631`, `:691-692` reads a temperament
>   its export no longer supplies; the medical prompt P23 `:58`, `:72`,
>   `:80` names sources the scheme does not follow; and the natal prompt
>   P01 `:46-47`, `:798`, `:1696`, `:1714`.
> - Docs-1: `SecondaryTemperament`'s doc sentence "Without a tie for
>   first, that is the first of `SecondaryTemperaments`" no longer holds
>   when no other element scores (chart 21: the property is Melancholic,
>   the list empty).
> - `MACOS_PORT_PLAN.md:1484-1486`'s byte-identical medical-export note
>   no longer reproduces.
>
> `AstroCast.Tests` **3591 -> 3605** (1345 theory cases);
> `AstroCast.UiTests` 88; golden runs 103.
>
> Windows (Maple), at `aeed92d`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (23 files, 445/127). No other golden, no other file
>   under `src/` (heads, CLI and Presentation included), no UiTests,
>   TestDoubles or other test file, and no reference, `ephe/`,
>   `.claude/`, `.gitignore` or project file changed.
> - In-file checks, by script. Each derived caller changes only its
>   `ToMarkdown(...)` call, which gains `includeTemperament: false`.
>   `MedicalMarkdownFormatter` prints `DominantText`, `SecondaryText`
>   and the new scoring line. "Lehman" and "Oliveira" are gone from
>   `TemperamentCalculator.cs`, and no tracked file names them.
> - The goldens, classified per commit. `51b6b64`: the one Richmond
>   line, whose Secondary becomes `Choleric and Melancholic (tied, 4
>   each)`. `28851e6`: 0 in and 91 out, seven whole 13-line
>   `### Temperament` blocks (composite 1, relocation 1, NYC electional
>   3, Richmond 2). `92779cb`: 10 in and 10 out in 8 fixtures, each a
>   scoring line. `aeed92d`: none.
> - The test files only add lines, and no existing test changes. The
>   medical chart tests' class joins the `Ephemeris` collection and takes
>   its fixture. Within the branch, `aeed92d` adds one theory case and
>   rewraps its summary.
> - Surface, by reflection of Core and Presentation. Core adds
>   `DominantTemperaments` and `SecondaryTemperaments` (public) and
>   `DominantText` and `SecondaryText` (internal), and `ToMarkdown`
>   gains its `Boolean`; Presentation is identical. The new public
>   members and the new parameter carry XML docs.
> - Commit files as stated. 0 pragma or suppression, and no `!` at all,
>   in 445 added lines.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3605/3605 in 3 m 20 s (F8: 3 m 16 s), and 88/88.
> - Names against `224dd01`: none removed; 14 added (6 theory cases, 8
>   facts), exactly the brief's. Theory cases 1339 -> 1345; nested-class
>   names 3. UiTests names unchanged. Captures kept UTF-8, xUnit's
>   truncation of the 3/3/3/3 case intact.
> - Passing by exact name: the 14 added; the 44 existing tests of the
>   six named classes; 103 golden runs (31 base; 24 each under de-DE,
>   sv-SE and th-TH).
> - Fresh process: `ChartMarkdownMedicalTests` 14/14,
>   `MedicalMarkdownFormatterTests` 13/13 and `TemperamentTests` 12/12.
> - Each commit on its own, in detached checkouts, all green on the first
>   run: `51b6b64` 3598, `28851e6` 3602 and `92779cb` 3604 (theory 1344
>   at each); UiTests 88 and 103 golden runs at each; `xUnit1031` the
>   only warning code.
>
> Report: `D:\Code\n-temperament-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-12, after N): branch O-2, transit and timeline
labels, part of release 1.3.3**
> `fix/transit-labels-20261012`, four commits, cut from `f0d4ac1`:
> - `7e63927`: degrees within the sign on the transit report (E2-2);
> - `7e2cfec`: an Entry or Exit row prints the orb limit (E2-4);
> - `f299ed1`: the timeline export carries the panel's context and
>   birth-time source (E2-6);
> - `9a4aa44`: transit, timeline and daily print the engine version
>   (E2-9).
>
> Fast-forwarded onto `master` at `9a4aa44`; this record follows it. It
> carries findings E2-2, E2-4, E2-6 and the version line of E2-9 of the
> outputs audit.
>
> Rulings, Steve, 2026-10-12:
> 1. E2-2: degrees within the sign on the transit report's planet and
>    target lines, in their present form.
> 2. E2-4: the timeline's Entry/Exit Orb is the orb limit, computed in
>    the formatter, with no model change.
> 3. E2-6: the timeline export gets the panel's context and birth-time
>    source, labels unchanged.
> 4. E2-9: O-4's engine line on transit, timeline and daily; Generated
>    and Software unchanged.
> 5. One branch, four commits.
>
> After the Mac's run, Steve accepted:
> - the transit export adds a rule before the engine line only when it
>   does not already end with one (it does unless progressions are on);
> - the scan-horizon exits go to branch D (below).
>
> The change:
> - E2-2, the transit report. The `**Transit planet:**` and
>   `**Natal target:**` lines print the degree within the sign:
>   `Pluto at 5 deg 22' Aquarius`, where they printed `305 deg 22'`. A
>   new private helper, `FormatDegMinInSign`, takes the sign index
>   exactly as `TransitEphemerisHelper.LongitudeToSign` does.
> - E2-4, the timeline. An Entry or Exit row's Orb cell is the transit's
>   orb limit: `TransitOrbs.GetOrb` for the aspect
>   `TransitAspectFinder.TryGetAspect` names, as the daily horoscope
>   reads it. It was the orb at report start, and empty when that was 0.
> - E2-6. `MainViewModel`'s timeline export passes the birth-time source
>   and the Transits panel's context, as the transit export does. The
>   formatter is unchanged.
> - E2-9. The transit, timeline and daily exports end with
>   `*Engine: AstroCast 1.3.3 (SwissEph 2.10.03, tzdb 2026e)*`, O-4's
>   form, with exactly one rule before it. The Generated and Software
>   lines are unchanged.
>
> Evidence from the Mac:
> - The goldens: 4 lines, then 26 Orb cells (14 at 1 deg 00', 12 at
>   1 deg 30'), then none, then the footers. At each of the 26 rows'
>   printed positions, the separation from the J2000 NYC natal point is
>   the limit within 0.000247 deg (Tier B).
> - The sweep: 95 saved charts, 190 outputs, 7,261 active transits.
>   Every changed line was classified, and all 14,522 planet and target
>   lines equal Tier B.
> - The real exports, counted: 938 lines in 35 of 36 transit exports. Of
>   the audit's 570 Entry/Exit rows in 11 of 22 timelines, 190 sit in
>   the cluster table, which has no Orb column; 378 of the other 380
>   change.
> - The surface identical; B's render fully accounted.
>
> Still open:
> - Scan-horizon exits, for branch D (with T1-4). When a slow transit is
>   still in orb 365 days after its last in-orb day
>   (`MaxExitForwardDays`), the finder returns that day as its "Exit",
>   at 12:00 UT. It is not a crossing. In the sweep there are 31 such
>   rows, all Pluto conjunct natal Pluto, at 2027-12-31. Before O-2 they
>   printed the orb at report start, and now they print the limit. The
>   separation there is 0.44 to 0.71 deg, so neither value is right.
> - The prompt library (the Mac's report, Verification 9): P04 `:185`
>   and `:435` read the degree within the sign; P04 `:222`, `:526` and
>   `:592` now hold for the value (`:526`'s D deg MM' SS" form is
>   O-5's); P04 `:573`: "Orb shows residual" is not true of Entry/Exit
>   rows; P04 `:74`, `:700` and `:701` hold except for the scan-horizon
>   exits; P04 `:57`, `:102-108`, `:287` and `:611`: the export now
>   carries `**Context:**` and `**Birth Time Source:**`; the colophons
>   P03 `:1115-1116`, P04 `:566-567` and P05 `:748-749` can now read the
>   engine line; and the export-spec lines P03 `:9`, P04 `:9` and P05
>   `:8`.
>
> `AstroCast.Tests` **3605 -> 3621** (1356 theory cases);
> `AstroCast.UiTests` 88; golden runs 103.
>
> Windows (Maple), at `9a4aa44`:
> - History, numstat and must-be-empty. The numstat equals the Mac's
>   line for line (12 files, 637/37; the view-model test file new). No
>   other golden, no other file under `src/` (heads, CLI and the rest of
>   Core and Presentation included), no UiTests, TestDoubles or other
>   test file, and no reference, `ephe/`, `.claude/`, this plan,
>   `.gitignore`, `.gitattributes` or project file changed.
> - In-file checks, by script. The transit formatter replaces exactly
>   the two `FormatDegMin` calls with `FormatDegMinInSign`, and adds it,
>   `AppendFooter` and the call in `ToMarkdown`. The timeline formatter
>   replaces exactly the three `OrbAtStartDegrees` lines with the
>   `TryGetAspect` and `TransitOrbs.GetOrb` lines, and adds the `using`,
>   `AppendEngineFooter` and its call after the data footer. The daily
>   formatter only adds `AppendFooter` and its call. `MainViewModel`
>   changes only `ExportTransitTimelineMarkdownAsync` (its body and its
>   own doc comment): `currentResult, path);` becomes
>   `currentResult, path, birthTimeSource, context);`, the two values
>   computed character for character as the transit export computes
>   them. Every new member is private.
> - The goldens, classified per commit. `7e63927`: the four transit
>   lines of the brief's table, each degree the old one less 30 times
>   the sign's index, prefix, minutes, sign and ending kept. `7e2cfec`:
>   26 in and 26 out, all 26 Entry/Exit rows of the main table, each
>   identical but for its Orb cell (14 1 deg 00', 12 1 deg 30'), and
>   each new cell the limit that `TransitOrbs.GetOrb`'s rule gives for
>   the row's planet and aspect; the three cluster tables, with no Orb
>   column, unchanged. `f299ed1`: none. `9a4aa44`: lines appended at
>   the end of each file only: the transit golden `---` and the masked
>   engine line, the timeline and daily goldens the engine line.
> - The test files only add lines, and no existing test changes. The new
>   view-model test builds `MainViewModel` from the TestDoubles stubs
>   with `StubChartRepository` (which stores nothing), writes under
>   `Path.GetTempPath()` and deletes the file in a `finally`.
> - Surface, by reflection of Core and Presentation: identical.
> - Commit files as stated. 0 pragma or suppression, and 0
>   null-forgiving `!`, in 637 added lines; the two `!` there are
>   logical negations.
> - Build: 0 errors across all eight projects, the WPF head included;
>   `xUnit1031` is the only warning code.
> - Suites: 3621/3621 in 3 m 24 s (N: 3 m 20 s), and 88/88.
> - Names against `f0d4ac1`: none removed; 16 added (11 theory cases, 5
>   facts), exactly the brief's, each theory case's values parsed from
>   its display name. Theory cases 1345 -> 1356; nested-class names 3.
>   UiTests names unchanged. Captures kept UTF-8.
> - Passing by exact name: the 16 added; the 172 existing tests of the
>   eight named classes; 103 golden runs (31 base; 24 each under de-DE,
>   sv-SE and th-TH).
> - Line endings: on Windows, where the formatters write CRLF,
>   `Transit_Footer_PrintsTheEngineVersion_AsTheChartExportDoes` passes:
>   one rule before the engine line with and without progressions.
> - Fresh process: `TransitTimelineExportTests` 2/2,
>   `EngineVersionFooterTests` 5/5, `TransitMarkdownFormatterTests`
>   47/47 and `TransitTimelineMarkdownFormatterTests` 52/52.
> - Each commit on its own, in detached checkouts, all green on the first
>   run: `7e63927` 3610 (theory 1350), `7e2cfec` 3616 and `f299ed1`
>   3618 (1356 at each); UiTests 88 and 103 golden runs at each;
>   `xUnit1031` the only warning code.
>
> Report: `D:\Code\o2-transit-labels-maple-report.md`.
>
> **Part of release 1.3.3.** No bump: master was already 1.3.3 against tag
> `v1.3.2`. The tag waits: `v1.3.3` follows the last 1.3.3 branch, and
> 1.3.3 stays open.

**Landing record (2026-10-12, after O-2): branch O-3, statements of
absence, part of release 1.3.3**
> `fix/absence-20261012`, cut from `dccce1f`: `5b17a5e` stations of
> Mercury, Venus and Mars (O-D6); `d691ad0` statements of absence (E2-3,
> E1-9). Rulings: O-D6 (b) of 2026-10-04; Steve's lean scoping of
> 2026-10-12.
> - Stations: the transit report lists Mercury's, Venus's and Mars's,
>   besides today's bodies; `TransitBody` gains `Mercury` and `Venus`,
>   for stations only.
> - Absence says what was searched: the progressed Moon "none within 90
>   days after ..."; "major" on the progression and solar-arc aspects;
>   the daily names the bodies it checks; relocation "No house
>   changes."; the return and ingress timeline "not searched".
> - Electional: a "Disqualifying rules" line, and each candidate's
>   Midpoint Activations table, computed. The chart JSON omits
>   `totalParans` when the paran calculation failed.
> - Mac: Tier B station times (Venus 2026-10-03 07:15:54, Mercury
>   2026-10-24 07:12:42, Mars 2027-01-10 12:59:17 UT); goldens: the
>   transit golden's 3 lines, and the two electional goldens' rules line
>   plus candidate midpoint rows (NYC 177, Richmond 100).
> - Deferred to 1.4: the house-overlay dashes; transit dignities without
>   triplicity or face; the medical export's empty region cells; the
>   cardinal ingress filter note.
> - Maple, lean check at `d691ad0`: numstat equal to the Mac's (29
>   files, 720/44); build 0 errors in all eight projects, the WPF head
>   included, `xUnit1031` only; 3632/3632 and 88/88, 103 golden runs;
>   11 names added, 0 removed. Report `D:\Code\o3-absence-maple-report.md`.
>
> `AstroCast.Tests` **3621 -> 3632**; `AstroCast.UiTests` 88. No bump,
> no tag; 1.3.3 stays open.

**Landing record (2026-10-12, after O-3): branch O-5, angular orb and
releasing row, part of release 1.3.3**
> `fix/orb-table-20261012`, cut from `7fa2225`: `fdc42c7` (E1-2) and
> `2d6350e` (E1-4).
> - E1-2: an angular planet's orb keeps its full precision, so no minute
>   is one off (J2000 NYC's Jupiter on the IC: 3 deg 12', not 3 deg 13');
>   the chart JSON prints those orbs to 4 decimals.
> - E1-4: zodiacal releasing prints no final Level 2 period under a day;
>   the period before it ends on the Level 1 end.
> - Goldens (Mac): 9 angular-orb lines over 8 fixtures (JSON 2.35 to
>   2.3527, 3.22 to 3.2159); 10 zero-length releasing rows removed, the
>   row before the synastry one ending a day later.
> - Card O-D3 (one orb format) moved to 1.3.4 (Steve, 2026-10-12).
> - Maple, lean, at `2d6350e`: numstat equal (18 files, 123/24); 0 errors
>   in all eight projects, `xUnit1031` only; 3634/3634, 88/88, 103 golden
>   runs; 2 names added, 0 removed. `D:\Code\o5-orb-table-maple-report.md`.
>
> `AstroCast.Tests` **3632 -> 3634**; `AstroCast.UiTests` 88. No bump,
> no tag; 1.3.3 stays open.

**Landing record (2026-10-12, after O-5): branch QI-2 + QI-1, horary
reception, part of release 1.3.3**
> `fix/horary-reception-20261012`, cut from `6b58083`: `3255777` (QI-2)
> and `2f33a97` (QI-1).
> - QI-2 (E1-1, E1-5): a mutual reception between the significators is
>   named whatever the perfection (`Mutual by domicile`, by exaltation,
>   or by both); the Detail's kinds follow it. Under a direct aspect it
>   printed `Neutral` (exaltation, mixed) or `Mutual` (domicile).
> - QI-1 (Q15): when the Moon rules both houses (Ascendant and quesited
>   in Cancer), the Moon is the querent and Jupiter, exalted in Cancer,
>   the quesited, with a note.
> - Changed test: `Analyze_MutualDomicileReception_ReceptionIsMutual`
>   now expects `Mutual by domicile`. No golden moves.
> - Prompts: P15 `:655-658` ("prints Neutral with no Detail") is now
>   false; review P15 `:169-170`, `:644-646` and the same-ruler lines
>   `:162-168`, `:179-181`, `:1046-1048`, `:1194-1195`, `:1488`.
> - Maple, lean, at `2f33a97`: numstat equal (3 files, 205/37); 0 errors
>   in all eight projects, `xUnit1031` only; 3639/3639, 88/88, 103 golden
>   runs; 5 names added, 0 removed. `D:\Code\qi-horary-maple-report.md`.
>
> `AstroCast.Tests` **3634 -> 3639**; `AstroCast.UiTests` 88. No bump,
> no tag; 1.3.3 stays open.

**Landing record (2026-10-12, after QI): branch O-7, medical Health
Context and export names, part of release 1.3.3**
> `fix/heads-20261012`, cut from `c74cd1e`: `c1db649` (medical, O-D8 (b),
> E3-3) and `fc93e42` (export names, E3-4, E3-5, E2-8).
> - The medical export prints the panel's Health Context in its header;
>   `MedicalMarkdownFormatter.Format` and `SaveAsync` gain an optional
>   `healthContext`. Both heads' Medical panel lists the export's sections.
> - Export Both names its Markdown after the chosen PDF and asks before
>   overwriting one; the electional default name comes from the natal
>   chart (a blank form name no longer throws); the medical default is
>   sanitised and ends `_Medical.md`; the cardinal ingress file takes the
>   results' year; the sanitiser removes the same characters everywhere.
> - E3-1 confirmed closed by F5 (offset parsed invariantly, -14 to +14).
>   E3-6 moved to the 1.4 list. (E3-6 fixed on screen 2026-10-16, branch
>   Panels, release 1.4.0: names for the five enum boxes and the saved-chart
>   row, Export Backup's count; the exports unchanged.)
> - Docs-1: the panel says "Health Houses (1st, 6th, 8th, 12th)" but the
>   export lists the 6th, 8th and 12th; the doc remark on
>   `MedicalPanelTests.HealthContextBox_CommitsTypedTextToTheViewModel`
>   is now false.
> - Maple, lean, at `fc93e42`: numstat equal (9 files, 491/32); 0 errors
>   in all eight projects, `xUnit1031` only; 3648/3648, 88/88, 103 golden
>   runs; 9 names added, 0 removed; the sanitiser test passes on Windows.
>
> `AstroCast.Tests` **3639 -> 3648**; `AstroCast.UiTests` 88. No bump,
> no tag; 1.3.3 stays open. `D:\Code\o7-heads-maple-report.md`.

**Landing record (2026-10-12, after O-7): branch Notices,
`THIRD-PARTY-NOTICES.md`, part of release 1.3.3**
> `docs/notices-20261012`, one commit `c568c33`, cut from `5a7cee4`.
> - A new `THIRD-PARTY-NOTICES.md` at the root lists every package both
>   heads ship, with licence and copyright, and the .NET runtimes; it
>   covers Swiss Ephemeris 2.10.03 (copyright, dual licence), the
>   time-zone and map data, licence texts and the packages' notices.
> - Core's csproj copies it into every build and publish output, as
>   `ephe/`; `make-bundle.sh` and `BuildInstaller.ps1` each fail without
>   it. Two tests: it reaches the output; it has a row for every direct
>   package reference of the shipped projects.
> - **Swiss Ephemeris: the licence AstroCast uses is Steve's decision,
>   before the installers**; the line naming it comes in Docs-1.
> - Docs-1, completing the file: the whole Swiss Ephemeris notice from
>   the `sweph.h` header (about 45 lines); the full Apache-2.0,
>   BSD-3-Clause and MS-PL texts, now only linked; the SIL OFL for
>   QuestPDF's Lato fonts; one copy of the four identical SkiaSharp and
>   HarfBuzzSharp notices; the runtime rows' version (10.0.9, from the
>   Mac) against Maple's (SDK 10.0.401, runtimes 10.0.12).
> - Maple, lean, at `c568c33`: numstat equal (6 files, 11991/1); 0 errors
>   in all eight projects, `xUnit1031` only; the file beside the WPF
>   head's `AstroCast.exe`; 3650/3650, 88/88, 103 golden runs; 2 names
>   added, 0 removed. `D:\Code\notices-maple-report.md`.
>
> `AstroCast.Tests` **3648 -> 3650**; `AstroCast.UiTests` 88. No bump,
> no tag; 1.3.3 stays open.

**Landing record (2026-10-12, after Notices): branch Docs-1, docs,
declination reference and notices, part of release 1.3.3**
> `docs/docs1-20261012`, cut from `f375286`: `fd0d911` corrected docs
> (Medical panel "Health Houses (6th, 8th, 12th)" in both heads; doc
> fixes in four test and source files and CLAUDE.md; slice 6's export
> figures marked stale here; reference section 12, Declination) and
> `85a1903` the notices completed (Swiss Ephemeris notice; Apache-2.0,
> BSD-3-Clause, MS-PL texts; Lato OFL; one shared package notice;
> runtime rows `10.0.x`; 604 KB to 214 KB).
> - **Claude's brief was wrong on composites.** Section 12 says what the
>   code does: a composite prints declinations, the OOB column and
>   clusters from the two charts' mean declinations, and no declination
>   aspects. S-D4's ruling (none on a composite) comes with L2 in 1.3.4.
> - The transit report's Transit-to-Natal table still runs its own two
>   declination tests, so a pair near the equator can be listed twice;
>   section 12 says so (branch D, 1.3.4).
> - **Still open before the tag: Licence** -- the Swiss Ephemeris line
>   (Steve deciding) and the MSI's licence page, which shows WiX's
>   placeholder text.
> - Maple, lean, at `85a1903`: numstat equal (10 files, 362/8188); 0
>   errors in all eight projects, `xUnit1031` only; 3650/3650, 88/88,
>   103 golden runs; names identical. `D:\Code\docs1-maple-report.md`.
>
> `AstroCast.Tests` 3650 and `AstroCast.UiTests` 88, unchanged. No bump,
> no tag; 1.3.3 stays open.

**Landing record (2026-10-13, after Docs-1 and the v1.3.3 tag): branch
D, transit lifecycle, the first of release 1.3.4**
> `d-transit-lifecycle`, cut from `361f4a2` (= `v1.3.3`): `e47c7a7` the
> bump to 1.3.4 (release-bump skill; 8 csprojs, 32 fields); `82f5432`
> one transit is one passage over one aspect point (T-D3: in-orb periods
> at a point less than 365 days apart are one transit, inside the report
> as well as outside it; the walk capped at ten years and the ephemeris
> span; measured over 1200-2399, the longest gap inside a passage is 287
> days, the shortest between two passages of a point 516, the longest
> passage 2047), passes to the transit's own point with a 90 deg guard
> and the half-day scan, stations over the transit's own dates in orb at
> their instant and flagged before or beyond, no North Node station rows
> (T-D4), a Mars transit or midpoint activation only where Mars is slow
> in orb on a report day (T-D5), declination rows by F7's side rule
> (D13); `2a143d6` ingresses dated by their instant
> (`TransitIngress.DateTimeUtc`), midpoint activations as passages over
> one of the eight dial longitudes with refined entry, exit and passes,
> timeline clusters measured from their first event.
> - Goldens: transit 36 out / 69 in, timeline 76 / 111, daily 2 / 2,
>   every line classed under a rule; the old golden's nine instants
>   reproduced to the second by Tier B.
> - Surface: `FindActiveTransits` without `preComputedStations`;
>   `TransitIngress.DateTimeUtc`; `FindDeclinationAspect` internal.
>   `FilterStationsToRange` and three constants removed; nothing names
>   them.
> - The passage rule joins a midpoint activation's retrograde returns
>   too: the April 2026 Neptune = Venus/True Node activation has three
>   passes, to 2027-02-21 (Steve kept it). Noted, not fixed:
>   transit-declination rows at passes outside the report; orb at start
>   and applying measured to the nearer side; two passages of one aspect
>   sort within it.
> - Release 1.3.3 shipped from `v1.3.3` (tag `ac663fa` on `361f4a2`):
>   MSI `AstroCast_1.3.3.msi`, 74,154,708 bytes, SHA-256
>   `aca099435b6ffbd728cba3e4c10d0206411385e5b824dd56758b4c31bee80cac`;
>   DMG `AstroCast-v1.3.3-macOS-arm64.dmg`, 68,761,034 bytes, SHA-256
>   `f48587a0551679218a32dfe0a6f3c388ddc4c47a86514a60d646b4b02fe513c9`,
>   notarized and stapled. Both installed and checked by eye.
> - Maple, lean, at `2a143d6`: numstat equal (25 files, 1681/630); 0
>   errors in all eight projects, `xUnit1031` only; 3662/3662,
>   88/88; +13 names, -1; both heads' `AstroCast.Presentation.dll`
>   read 1.3.4. `D:\Code\d-transit-maple-report.md`.
>
> `AstroCast.Tests` 3650 -> 3662; `AstroCast.UiTests` 88. Bumped
> to 1.3.4; no tag.

**Landing record (2026-10-13, after D): branch G, progressions, part of
release 1.3.4**
> `g-progressions`, cut from `42de87b`: `91d5ae4` the progressed
> Midheaven is the natal Midheaven plus the solar arc and the progressed
> Ascendant the Ascendant for it at the birth latitude with the natal
> true obliquity (T-D1 option b; `CalculateHousesFromArmc` with Equal
> houses, whose Ascendant is every system's); an angle's aspects have a
> direction from its speed per progressed day (the Midheaven's the
> progressed Sun's, the Ascendant's that times dASC/dMC); the angles'
> sign changes are found on MC(t) and ASC(t) by the bodies' method;
> `63f8482` every progressed event is dated by its instant (the midpoint
> date at 12:00 UT plus the offset times 365.25 real days, the UT date),
> and the Moon's next sign change and the stations are scanned to their
> bounds inclusive and bisected (until now 87.7 and 347 days, reported
> 0-4 and 0-19 days late). The key stays 365.25 days (T-D2). The Solar
> Arc Directions table is unchanged.
> - Golden: the transit golden's ASC and MC rows (02 50 Aquarius, 25 14
>   Scorpio) and its angle aspects (P ASC cnj Neptune 0 20 Applying in;
>   P ASC tri Mars and P MC sxt ASC out): 4 out, 3 in. J2000 NYC's
>   quotidian Ascendant was 27 27 Gemini.
> - Surface unchanged. Noted, not fixed: the Ascendant Swiss returns
>   beyond |latitude| 90 deg minus the obliquity; forward differences
>   for the Ascendant's rate; a sample exactly on a cusp or at speed 0.
> - Commit 2 was committed through the bridge by Claude after Claude
>   Code's session ended with the tree built, tested and uncommitted
>   (`g-progressions-report.md`).
> - Maple, lean, at `63f8482`: numstat equal (4 files, 413/103); 0
>   errors in all eight projects, `xUnit1031` only; 3667/3667, 88/88; +5
>   names. `D:\Code\g-progressions-maple-report.md`.
>
> `AstroCast.Tests` 3662 -> 3667; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-13, after G): branch F, planetary returns and
cycles, part of release 1.3.4**
> `f-returns`, cut from `6aa9b23`: `ed9f8cb` a planetary return is a
> passage of the planet over its natal longitude (a direct crossing,
> plus the retrograde and direct crossings that follow it; the birth's
> own passage is not a return), numbered by passages from birth, its
> instant the first pass (T-D6), the chart cast there, the passes
> carried (`PlanetaryReturnResult.Passes`) and printed on an "Other
> passes:" line; by year, the first passage with a pass in the calendar
> year, else an exception naming the neighbours; the age in whole years
> between the UTC instants; the cycle timeline's "for a date" by the
> passage count (one helper, `ReturnPassageFinder`); `511644a` the
> return and ingress timeline lists one entry per passage at its first
> pass, "Return #N" counted from birth (Mercury and Venus about once a
> year, where the old count divided by the sidereal period: J2000 NYC's
> 2026 Mercury return is #26, not ~#108), and the cycle timeline's rows
> carry a passage's passes, every phase point at its first pass.
> - Goldens: the Saturn1 return golden recast at 2028-07-21 08:08:04 UTC
>   (return #1's first pass; it printed the last pass, 2029-04-01), age
>   28, 232 out / 242 in; the timeline golden's six Label cells. The
>   Mars cycle golden unchanged.
> - Surface: `PlanetaryReturnResult.Passes` added;
>   `ReturnPassageFinder`, `ReturnPassage`, `ReturnCrossing` and
>   `ReturnChartFinder.RefineCrossing` internal; the two orbital-period
>   tables, the 120-day cluster and the number estimate removed.
> - Nine existing tests rewritten to the rule, among them the N1 Mars
>   cycle's opposition (its first pass, 273.5078 days) and the
>   exact-minute pin for return entries (a crossing at the probe chart's
>   own instant is the birth's passage).
> - Noted, not fixed: a crossing within the birth's first day; a speed
>   of exactly 0 at a crossing counts as retrograde;
>   `PassageCloseDegrees` 30 deg rests on the five planets' loops; a
>   passage straddling the span's end; the Summary table's Next Return
>   column; `PlanetaryCycleTimelineService.OrbitalPeriods` is dead code.
>   The R&I timeline golden test takes 401 ms (was 209): the passage
>   scan now runs from birth.
> - Maple, lean, at `511644a`: numstat equal (18 files, 1026/543); 0
>   errors in all eight projects, `xUnit1031` only; 3673/3673,
>   88/88; +12 names, -6. `D:\Code\f-returns-maple-report.md`.
>
> `AstroCast.Tests` 3667 -> 3673; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-13, after F): branch O-D3, one orb format,
part of release 1.3.4**
> `o-d3-orb-format`, cut from `cd72533`: `f9e3239` one function prints
> every orb printed in minutes (`PositionFormatter.FormatOrb`): degrees
> and whole minutes with the degree sign and the prime mark, the minutes
> floor(|orb| x 60), no rounding and no carry (card O-D3 option c; E1-7,
> E2-12). The chart Markdown's six legacy forms (the bare "129'" aspect
> and "003'" midpoint orbs; the apostrophe forms of the angular, star,
> declination and antiscia tables), the PDF's, the synastry, transit,
> timeline, daily and electional orbs and every cross-chart declination
> table go through it; `unifiedOrbFormat` and `FormatOrbLegacy` go, so
> the Moon's First Applying Aspect line prints the aspect table's orb
> (the canonical path, Markdown and PDF); the Fixed Stars heading and
> none line take the prime; the transit export's exact-pass note keeps
> its seconds with the prime and double-prime marks. `f4616de` the
> decimal-degree orbs too: the profection lords' aspects, the horary
> Perfection, Application-fails and analyzer lines, the medical table.
> `1210617` the floor takes a guard of 1e-9 minute, so a double a hair
> below a whole minute by arithmetic prints that minute.
> - Goldens: 2,071 lines in 13 goldens, every one an orb re-marked, a
>   bare orb given its degree sign, a decimal orb given degrees and
>   minutes, or a rounded orb (electional, cross-chart declination) one
>   minute lower; predicted from the goldens' own printed values by
>   `outputs/sim/od3_truth.py` (2,009, plus 41 timeline bullets its
>   table-cell scan missed; 21 of 44 boundary rows went down as the code
>   decides). Paran orbs (minutes of time), the JSON numbers, the
>   whole-degree orb limits and positions (the apostrophe form of the
>   transit, timeline, daily, progression and series exports; the
>   cluster zone) are unchanged: E1-7 item 9 is closed for orbs, open
>   for positions.
> - Surface: `ChartMarkdownFormatter.ToMarkdown` loses
>   `unifiedOrbFormat` (one caller); `ResolveCanonicalMoonOrb` internal;
>   `FormatOrbLegacy` and the daily formatter's `FormatDegMin` deleted.
> - Fifteen existing tests rewritten to the rule (the FormatOrb rounding
>   pair becomes a four-case theory; the timeline's Entry/Exit theory,
>   the residual note, the Moon evaluator's two pins, seven horary
>   pins).
> - Noted, not fixed: the timeline's Transit Windows print every pass's
>   residual as zero minutes; `FormatResidualOrb` truncates in three
>   steps; the antiscia, Moon-condition, progressed and solar-arc orbs
>   are rounded to 4 dp at their sources; the normaliser self-test's
>   literal keeps the old marks.
> - Maple, lean, at `1210617`: numstat equal (37 files, 2349/2302
>   with --minimal); 0 errors in all eight projects, `xUnit1031`
>   only; 3680/3680, 88/88; +15 names, -8 (by list-tests lines).
>   `D:\Code\o-d3-orb-format-maple-report.md`.
>
> `AstroCast.Tests` 3673 -> 3680; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-13, after O-D3): branch R2, pre-1583 labels
and every chart at its instant, part of release 1.3.4**
> `r2-labels`, cut from `c07ddd5` (rulings R-D1 to R-D6; R-D7 is branch
> R3): `8e908c7` one formatter, `CalendarDateFormatter`, prints every
> instant on the calendar in force: the Julian calendar's year, month
> and day before 1582-10-15, the clock's own after, the weekday the
> instant's; `97e6f5e` every chart's `UtcDateTime` is its instant (R-D4;
> the Julian Day by arithmetic, bit for bit the old one; the leap-day
> refusal kept, R-D5), relocation cast at the natal Julian Day,
> progressions counted from the natal Julian Day to the reading's noon
> (a 1300 native at 1600 was 8 days, 31 minutes of progressed time, too
> old), the JSON and database keep the Julian label and the JSON names
> the calendar for a pre-1583 chart (R-D3), the Time Note on the
> calendar in force; `236866c` the 63 instant print sites of the 194
> date sites go through the formatter (readings print as typed; the
> daily heading's weekday is the day's instant's), and a "Calendar:"
> line names the Julian calendar in an export whose earliest instant is
> before the switch (R-D2); `0d26cca` an explicit-offset chart's
> Date/Time line prints its instant, so an event chart's header agrees
> with its Return Date/Time and Calendar lines and a return on Julian 29
> February prints that date.
> - Goldens: none changed; no golden holds a date before 1583, and every
>   post-switch label is byte-identical.
> - Surface: `CalendarDateFormatter` public, new;
>   `ProgressedDateCalculator .Calculate` takes the natal Julian Day and
>   returns the progressed instant and Julian Day, `ToJulianDay`
>   removed; the JSON's `calendar` property for a pre-1583 chart.
> - Noted, not fixed: the time lords and the profection and return age
>   start from the birth reading and count by `.Year` (branch H, R-D6);
>   the electional status bar keeps the screen's culture; the transit
>   and daily `DateOnly` loops still step the proleptic calendar across
>   October 1582 (branch R3, R-D7); a series with no event prints no
>   Calendar line.
> - Maple, lean, at `0d26cca`: numstat equal (48 files, 1585/258,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3723/3723, 88/88; +44 names, -1 (by list-tests lines).
>   `D:\Code\r2-labels-maple-report.md`.
>
> `AstroCast.Tests` 3680 -> 3723; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-13, after R2): branch R3, the ranges step the
calendar in force, part of release 1.3.4**
> `r3-ranges`, cut from `a369e76` (ruling R-D7, the last of the R
> rulings): `8a505dd` `CalendarDateFormatter.ProlepticDay` and
> `ProlepticClock` read a typed date once to the proleptic day it names
> (D21), refusing 1582-10-05 to 1582-10-14 with the chart's own
> sentence; `d24f362` every range converts its typed bounds once at its
> entry (the transit report, the daily, the electional search, the
> lunar-return range) and steps proleptic days and instants from there:
> the transit scanners' one `DateOnly` conversion, the three
> `JulianDayUtility` local-clock conversions, the daily noon, the
> progressed age and the progressed offsets are arithmetic
> (`InstantToJulianDay`), never the ephemeris's calendar switch; the
> midpoint and a daily day are days derived from the scan and print
> through the formatter. Across October 1582 the daily report prints 21
> days for 30 (nine of them twice), the station detector finds Jupiter's
> station direct of 1582-09-28 and Neptune's retrograde of 1582-10-23
> once each for four; a Julian 29 February is a day of the report
> (1300-02-27 to 03-02 is 5 days, not 4), the midpoint of 1400-02-28 to
> 03-02 prints 1400-02-29; a bound in the ten days is refused at every
> entry.
> - Goldens: none changed; after the switch every Julian Day is
>   bit-identical.
> - Surface: `CalendarDateFormatter.ProlepticDay(DateOnly)` and
>   `ProlepticClock(DateTime)` public, new; `ProgressedDateCalculator
>   .Calculate(natalJulianDay, targetDate)`, the ephemeris parameter
>   gone.
> - Noted, not fixed: the time lords start from the birth reading and
>   count by `.Year` (branch H, R-D6); `ChartEngine.ResolveInstant`
>   keeps its own inline reading of a typed date, the same rule as
>   `ProlepticDay`.
> - Maple, lean, at `d24f362`: numstat equal (25 files, 509/142,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3743/3743, 88/88; +22 names, -2 (by list-tests lines).
>   `D:\Code\r3-ranges-maple-report.md`.
>
> `AstroCast.Tests` 3723 -> 3743; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-14, after R3): branch H, the time lords by the
sources, part of release 1.3.4**
> `h-timelords`, cut from `409ab0b` (cards T-D8, T-D9, T-D10; T6-4 to
> T6-6; ruling R-D6, Steve 2026-10-13: tropical anniversaries):
> `60f9705` firdaria in Abu Ma'shar's order (by day Sun, Venus, Mercury,
> Moon, Saturn, Jupiter, Mars, the nodes; by night Moon, Saturn,
> Jupiter, Mars, Sun, Venus, Mercury, the nodes), the nodes' periods
> undivided, every period between anniversaries of the birth day (the
> local birth reading's proleptic day plus N years, printed on the
> calendar in force) and a planet's period in sevenths of its span;
> `0364f29` the profected sign counted in whole signs from the
> Ascendant's (the cusps not read), a lunar or planetary return's
> profection the year running at the return; `da1166a` zodiacal
> releasing as Valens, 360-day years and 30-day months with the loosing
> of the bond, Level 2 durations in months, the 5th and 11th signs from
> Fortune succedent and the 6th and 12th cadent; `b2e84f3` a return's
> reference instant read on the natal chart's clock, the model docs and
> the status text; `e2d1694` the CLAUDE.md bullet. A reference before
> the birth day is refused.
> - Goldens: the nine J2000 NYC exports' Firdaria, Annual Profection and
>   Zodiacal Releasing sections, line for line as the truth (Tier A
>   arithmetic, `h-timelords/h_truth.py`); Saturn return #1's profection
>   age 29 -> 28. Nothing else.
> - Surface: none public; `FirdariaAnalyzer.BirthDayAndAge` internal,
>   new.
> - Noted, not fixed: a relocated chart's time lords read its own Lots
>   and Ascendant (K2, S-D6); the E1-4 residual fold in the releasing is
>   unreachable and kept; releasing from Fortune is a 1.4 item.
> - Maple, lean, at `e2d1694`: numstat equal (25 files, 1498/700,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3771/3771, 88/88; +39 names, -11 (by list-tests lines).
>   `D:\Code\h-timelords-maple-report.md`.
>
> `AstroCast.Tests` 3743 -> 3771; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-14, after H): branch K2, a relocation keeps
the birth's sect, time lords, hour and clock, part of release 1.3.4**
> `k2-relocation`, cut from `5f7fb4b` (card S-D6, Q3 and Q11; findings
> Y3-2, Y3-3, Y3-6; the horary category): `0093134` the engine casts the
> relocation (`IChartEngine.CalculateRelocated`): the birth input with
> only the place changed, run through `Calculate`'s own pipeline, so the
> Julian Day is the natal one bit for bit and the Date/Time line prints
> the birth clock, its offset label and any Time Note; the sect, the
> planets' sect table and the planetary hour are the birth chart's
> (`IChartContextService.Analyze` gains `sectFrom`; no hour is computed
> at the place), the dignities follow, the Lots take that sect at the
> new Ascendant; houses, placements, altitudes, aspects to angles,
> angular planets, Lots and parans follow the place; the relocation
> export prints the birth chart's Annual Profection, Firdaria and
> Zodiacal Releasing in place (`ToMarkdown` gains `timeLordsChart`); the
> horary category is kept and the perfection judged at the place.
> `fda3bc9` "Became angular" and "Left angle" by `AngularityService`'s 5
> deg rule, a row for every planet whose house or angularity changed.
> `86b060d` the polar-night relocation test pins the kept hour.
> - Goldens: `RelocationMarkdownFormatter_J2000NYC_to_LA` only: the
>   relocated body's Date/Time and UTC Offset lines, its Planetary Hour,
>   its three time-lord sections (the natal export's) and the House
>   Changes rows (Venus became angular; Jupiter and Mercury left angle;
>   nine unflagged).
> - Surface: `IChartEngine.CalculateRelocated` new; `Analyze` and
>   `ToMarkdown` each gain one optional parameter.
> - Noted, not fixed: `HouseChange.Description` is built and never read
>   (Y3-6, the deletion rule); the prompt's `RELO:616-632` lines are
>   Docs-2's.
> - Maple, lean, at `86b060d`: numstat equal (13 files, 691/196,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3780/3780, 88/88; +12 names, -3 (facts; by method the same).
>   `D:\Code\k2-relocation-maple-report.md`.
>
> `AstroCast.Tests` 3771 -> 3780; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-14, after K2): branch L2, a composite shows no
declinations, marks or Moon's next aspect, part of release 1.3.4**
> `l2-composite`, cut from `6844260` (card S-D4; Q10; findings Y2-1,
> Y2-5, Y2-8): `c395def` a composite body is a midpoint, not a point in
> the sky: `CompositeService` gives it no right ascension and no
> declination; `ChartMarkdownFormatter.ToMarkdown` takes `bool
> composite = false` and, when true, prints the Planetary Positions
> table without its Dec and OOB cells, no Equatorial Coordinates or
> Out-of-Bounds Planets section, the Moon Conditions without the Void
> of Course and First Applying Aspect lines, the Aspects table without
> its Ap/Sp cell, and no Declination Aspects section or Clusters table;
> `CompositeMarkdownFormatter` passes it and prints one "Not shown"
> line after its Method line, saying what is left out and why. The
> sect, the Planetary Sect table, the triplicity tier, the Moon's
> phase, sign and via combusta, the midpoints, fixed stars and medical
> correspondences stay; the applying flags and the fixed-target Moon
> scan are still computed, not printed. `cd6423a` CLAUDE.md's Composite
> bullet and three sentences; the reference's section 12 paragraph.
> - Goldens: `CompositeMarkdownFormatter_J2000_Secondary` only, 394 to
>   361 lines, byte for byte the truth derived from the old golden by
>   the ruling (`l2_truth_golden.py`).
> - Surface: `ToMarkdown` gains one optional parameter; nothing else.
> - Noted, not fixed: a composite's applying flags and Moon scan are
>   computed and withheld (the model keeps them; the fixed-target scan
>   has its own tests).
> - Maple, lean, at `cd6423a`: numstat equal (8 files, 320/150,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3785/3785, 88/88; +7 names, -2 (facts; by method the same).
>   `D:\Code\l2-composite-maple-report.md`.
>
> `AstroCast.Tests` 3780 -> 3785; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-14, after L2): branch L3, composite Mercury
and Venus nearer the Sun, the MC the midpoint of the Midheavens, part of
release 1.3.4**
> `l3-composite`, cut from `1e23df4` (cards S-D2, S-D3; Q1; findings
> Y2-2, Y2-3, Y2-4): `32bbdd6` composite Mercury and Venus take the
> midpoint nearer the composite Sun (the far-side midpoint when it is
> nearer; a tie keeps the short arc); `CompositeResult.SunSideBodies`
> names the bodies moved and the export prints one "Note" line per body
> after its Not shown line. `e0b36c1` the composite MC is the short-arc
> midpoint of the two Midheavens; the Ascendant and cusps are cast for
> it, through its right ascension at the midpoint obliquity, at the mean
> of the two birth latitudes, in the house system both charts share,
> else Placidus, so A & B and B & A give one chart;
> `ICompositeService.Calculate(chartA, chartB, token)`: the two
> reference-place parameters, which the app filled with Person A's
> birthplace, are gone; the Method line names the method. `7150ebd`
> CLAUDE.md's Composite bullet. Until 1.3.4 the houses came from the
> midpoint of the two ARMCs (neither published method; the MC up to 4.9
> deg from the Midheavens' midpoint) at Person A's place in Person A's
> system, and Venus landed opposite the composite Sun in 8 of 78 saved
> pairs, Mercury in 2.
> - Goldens: `CompositeMarkdownFormatter_J2000_Secondary` only: Venus to
>   0 deg 13 min Aries (was Libra), the Note and Method lines, ASC 12
>   deg 41 min Scorpio and MC 20 deg 11 min Leo (were 9 deg 20 min and
>   15 deg 57 min), the cusps, the Moon's and Uranus's houses, the
>   angular planets, every aspect and midpoint with Venus, the ASC or
>   the MC, Alphecca on the new Ascendant, the Moon's hayz mark.
> - Surface: three `Calculate` signatures lose two parameters;
>   `CompositeResult.SunSideBodies` new.
> - Noted, not fixed: `ProgressionService` keeps its own copy of the
>   RAMC formula; the reference longitude is a label.
> - Maple, lean, at `7150ebd`: numstat equal (10 files, 493/167,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3790/3790, 88/88; +8 names, -3 (facts; by method the same).
>   `D:\Code\l3-composite-maple-report.md`.
>
> `AstroCast.Tests` 3785 -> 3790; `AstroCast.UiTests` 88. No bump, no
> tag; 1.3.4 stays open.

**Landing record (2026-10-14, after L3): branch O-6, the chart PDF and
JSON, part of release 1.3.4**
> `o6-pdf-json`, cut from `38f16f8` (cards O-D1 option b, O-D2 option b;
> finding E1-6): `2429ff0` the chart PDF prints the Planetary Hour after
> Chart Context and, for a horary chart, the Horary Perfection Analysis,
> both from the Markdown's own section text
> (`ChartMarkdownFormatter.PlanetaryHourSection` and
> `HoraryPerfectionSection`, internal), so the two cannot disagree; the
> positions table gains Lat, Dec and OOB; Source, Description, Notes and
> Time Note leave the repeating page header and print once at the top of
> the content, so a Notes taller than a page lays out (it threw
> DocumentLayoutException). `766a66d` the chart JSON's planetaryHour
> gains hourStartUtc and hourEndUtc, one instant each across a clock
> change; an explicit-offset chart names its offset (hourOffsetLabel)
> and no zone. `7270ea3` the Chart Export group gains Export JSON
> (`ExportChartJsonCommand`) in both heads; the database pair become
> Export Backup and Import Backup. `cfaa0f4` CLAUDE.md's Chart PDF and
> JSON bullet; the reference's planetaryHour fields.
> - Goldens: `ChartJsonFormatter_ToJson_J2000NYC` only, two lines
>   (hourStartUtc 11:07:57Z, hourEndUtc 12:21:44Z on 2000-01-01).
> - Surface: two internal section methods; `ExportChartJsonCommand`.
>   Tests read the PDF's text with PdfPig 0.1.16, a test-only package.
> - Noted, not fixed: a heading can still strand at a page foot (E1-8);
>   the primes extract as U+02B9 and U+02BA (the font); the PDF carries
>   no other Markdown section (O-D1 option c).
> - Maple, lean, at `cfaa0f4`: numstat equal (15 files, 593/60,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3801/3801, 89/89; +11 names core, +1 UI (facts).
>   `D:\Code\o6-pdf-json-maple-report.md`.
>
> `AstroCast.Tests` 3790 -> 3801; `AstroCast.UiTests` 88 -> 89. No bump,
> no tag; 1.3.4 stays open.

**Landing record (2026-10-14, after O-6): branch O-8, the Avalonia head
catches up with the WPF head, part of release 1.3.4**
> `o8-avalonia-parity`, cut from `2ca34a8` (finding E3-7: H-01, H-03,
> H-04, H-05): `2477741` each Avalonia time-zone list opens with a click
> on its chevron, now a ToggleButton in the AutoCompleteBox's
> InnerRightContent, its IsChecked bound two-way to the box's
> IsDropDownOpen; drawn flat as before, no tab stop, no code-behind.
> `b829475` a new MessageWindow (internal, built in code as
> ConfirmationWindow is) shows a start-up failure, the head exiting with
> 1 when it closes, and an unhandled UI error, one at a time, with the
> WPF head's texts and titles (`App.StartupFailureMessage` and
> `UnexpectedErrorMessage`, internal; the UI tests reach them through
> InternalsVisibleTo). `7b477f2` both heads' Yes/No prompts default to
> No: the WPF MessageBox passes MessageBoxResult.No, the Avalonia window
> keeps No focused. `2d3e7a5` CLAUDE.md's Head Parity section. `5f6f908`
> the Chart panel's layout test, which pinned the old glyph, pins the
> toggle (TimezonePicker_CarriesAClickableChevron).
> - Goldens: none.
> - Surface: MessageWindow and its constructor; App's two message texts;
>   all internal to the head.
> - Noted, not fixed: the Mapsui split (WPF 5.0.2, Avalonia 5.1.0); H-06
>   (each head's own database folder); H-07 (an Avalonia save to a place
>   with no local path reads as cancel).
> - Maple, lean, at `5f6f908`: numstat equal (12 files, 622/47,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3801/3801, 96/96; core names identical, UI +8/-1 (facts).
>   `D:\Code\o8-avalonia-parity-maple-report.md`.
>
> `AstroCast.Tests` 3801 unchanged; `AstroCast.UiTests` 89 -> 96. No
> bump, no tag; 1.3.4 stays open.

**Landing record (2026-10-14, after O-8): branch Docs-2, the
repository's documentation pass, part of release 1.3.4**
> `docs2`, cut from `b32269b` (the outputs audit's Docs-2: E5-1 to
> E5-3, E1-10, E3-10, E3-11, the S4 comments; the kept cards T-D11,
> T-D15, S-D5 and L1): `0d8ae1d` the chart JSON's stationTimeUtc is
> the Julian label before 1583, as utcDateTime is (a site R2
> missed). `3ed4fa5` the code comments match the code (45 files;
> comment lines and two tests' reason strings only). `aa44ef3`
> CLAUDE.md matches 1.3.4: its status, test totals, schema, JSON
> example and the rules that had no line. `78a4941` the plan's
> status, Phase 6 and section 9 rechecked against the code; the
> prompt-workflow and H-1 documents marked historical. `62dacda` and
> `5259c2a` the reference: its lines match 1.3.4, version 1.3.4, and
> sections 13 to 24 for the 1.3.4 techniques, with the transit and
> synastry orb tables.
> - Goldens: none. Surface: no declaration line changed.
> - Left for 1.4 (Steve): the reference's 20 sections for 1.3.3
>   techniques; folding `ChartEngine.ResolveInstant`'s calendar copy.
> - The prompt library's pass follows the tag.
> - Maple, lean, at `5259c2a`: numstat equal (51 files, 900/266,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3802/3802, 96/96; +1 core name, UI identical.
>   `D:\Code\docs2-maple-report.md`.
>
> `AstroCast.Tests` 3801 -> 3802; `AstroCast.UiTests` 96. No bump; the
> tag `v1.3.4` is next.

**Landing record (2026-10-15, after the tag v1.3.4): branch Licence,
AstroCast under the GNU AGPL, version 3 or later, opening 1.3.5**
> `licence`, cut from `dece41a` = `v1.3.4` (Steve, 2026-10-15:
> "AGPL-3.0-or-later"; Swiss Ephemeris under its AGPL option): `3ce089c`
> the bump to 1.3.5 by the release-bump skill. `aa294d4` LICENSE,
> gnu.org's agpl-3.0.txt unchanged (661 lines, SHA-256 0d96a4ff...),
> shipped beside the app and checked by both packagers; the notices'
> licence line and Swiss Ephemeris's choice; License.rtf on the MSI's
> licence page (WixUILicenseRtf). `a737317` an About window in both
> heads (the AGPL's Appropriate Legal Notices, section 5(d)): version,
> copyright, notice, source, the licence and the notices, from
> AboutViewModel, opened by the status bar's About button. `34bb690`
> CLAUDE.md's Licence section.
> - Goldens: none. Surface: AboutViewModel, the two About windows,
>   MainViewModel's OpenAboutWindowCommand and OpenAboutWindowAction.
> - Open before sharing (Steve): Svg.Custom's MS-PL; the source
>   reachable (the repository is private); the CARTO key's terms; the
>   source-only tarball is not the complete source.
> - Maple, lean, at `34bb690`: numstat equal (27 files, 1726/51,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3813/3813, 101/101; +11 core names, +5 UI; both heads read 1.3.5;
>   the MSI's licence page holds the AGPL.
>   `D:\Code\licence-maple-report.md`.
>
> `AstroCast.Tests` 3802 -> 3813; `AstroCast.UiTests` 96 -> 101. 1.3.5
> is open; no tag.

**Landing record (2026-10-15, after the tag v1.3.5): branch MacMenu,
the macOS application menu names AstroCast, completing 1.3.6**
> `macmenu`, cut from `1527b3f` = `v1.3.5`. At 1.3.5's smoke gate the
> menu bar read "Avalonia Application" with About Avalonia (Steve: "the
> menu is not correct it should show Astrocast, not Avalonia"; then "Fix
> in 1.3.6"); no 1.3.5 DMG was made. `512bd22` the bump to 1.3.6 by the
> release-bump skill. `c8fd38f` App.axaml's Name is AstroCast; AppMenu
> builds the application menu, one item, About AstroCast, set in
> App.Initialize (Avalonia's macOS exporter reads it once, right after
> Initialize) and forwarded through the App's DataContext to the view
> model's OpenAboutWindowCommand; ShowAboutWindow reuses an open About
> window. `fa3d840` CLAUDE.md.
> - Goldens: none. Surface: AppMenu; Initialize's body (its signature
>   kept). The WPF head unchanged but for its csproj's bump.
> - Mac dev run (System Events): AstroCast, About AstroCast, Services,
>   Hide AstroCast, Hide Others, Show All, Quit; one About window.
> - Maple, lean, at `fa3d840`: numstat equal (14 files, 217/39,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3813/3813, 105/105; core names identical, +4 UI; both heads read
>   1.3.6. `D:\Code\macmenu-maple-report.md`.
>
> `AstroCast.Tests` 3813; `AstroCast.UiTests` 101 -> 105. 1.3.6 is
> complete; the tag `v1.3.6` goes on this commit.

**Landing record (2026-10-16, after the tag v1.3.6): branch Panels,
the Natal panel's order and six interface fixes, opening 1.4.0**
> `Panels`, cut from `ca94ea3` = `v1.3.6`. `c9d03c4` the bump to 1.4.0
> by the release-bump skill (Steve named the release 1.4.0).
> `671753c` the Natal Chart panel in entry order: Chart Subject,
> Location, Date, Time & Timezone, Houses (its own section, row
> "System"), Notes. `b9cbe0e` Use Natal Location enabled after a plain
> Calculate (Section 9, item 6). `4988e96` the Synastry, Electional
> and Relocation chart pickers bind AllSavedCharts and keep their
> charts by Id across a refresh (the scoping's probe found every
> refill emptied them; Steve's ruling; item 8 closed). `c617019` the
> Relocation hint and tooltip and the Electional tooltip say what is
> used until a chart is chosen (item 7). `8704c9e` one latitude and
> longitude check for every panel (a point, invariant, +-90 and +-180,
> a comma refused), Relocation validated as typed (item 9). `a11cc45`
> Relocation casts under its token, Use Natal Location casts nothing
> (item 10). `9db698c` Electional Calculate needs a time zone (item
> 16). `537d14e` names on screen for the enum boxes and the saved-chart
> rows, Export Backup counts what it wrote (E3-6); the exports
> unchanged. `a56bd3d` CLAUDE.md and the plan.
> - Goldens: none. Surface: DisplayNames (Core); DisplayText and the
>   coordinate check (Presentation); Relocation's error members; a
>   DisplayTextConverter in each head.
> - Accepted (Steve): three test-only `!`, each its file's idiom; a
>   dated note in O-7's record; in WPF the red border shows once the
>   box loses focus.
> - Maple, lean, at `a56bd3d`: numstat equal (40 files, 2532/375,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3852/3852, 122/122; names +41/-2 core, +17 UI; both heads read
>   1.4.0; Steve's WPF check by eye passed.
>   `D:\Code\panels-maple-report.md`.
>
> `AstroCast.Tests` 3813 -> 3852; `AstroCast.UiTests` 105 -> 122.
> 1.4.0 is open; no tag.

**Landing record (2026-10-16, after the tag v1.3.6): branch Relocate,
the Relocation panel as one birth chart, then a new place**
> `Relocate`, cut from `ca16592`, for Steve's item 3 ("All i want is to
> create a relocation chart for a single birth chart"). `ab5a4f5` Use
> Natal Location leaves the Relocation panel: its command and method
> removed, the RefreshRelocationCommandStates call kept, Electional's
> Use Natal untouched. `c9458d7` the panel reads Birth Chart, then New
> Location: a description line, the picker in row "Chart", Results
> labelled, one five-row New Location grid, the hint gone, tooltips
> that speak of one chart. `c8146d9` the picker follows each Load and
> Calculate & Save, and ChartInUseText names the chart in use.
> `cc47fc1` a result line under the buttons (ResultSummary). `efd923b`
> CLAUDE.md and the plan (Section 9 items 6 and 10).
> - Goldens: none; Core untouched; the export unchanged. Surface: the
>   command and its method removed; ChartInUseText and ResultSummary
>   added; DisplayText's parse shared through internal helpers.
> - Later (Steve): a result left stale after the chart or place
>   changes; the Electional picker's same two-source shape.
> - Maple, lean, at `efd923b`: numstat equal (14 files, 817/396,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3859/3859, 125/125; names -3/+10 core, -3/+6 UI; Steve's WPF check
>   by eye passed. `D:\Code\relocate-maple-report.md`.
>
> `AstroCast.Tests` 3852 -> 3859; `AstroCast.UiTests` 122 -> 125.
> 1.4.0 is open; no tag.

**Landing record (2026-10-17, after the tag v1.3.6): branches Arcs,
Modes and ModesPanel, the transit report's three techniques**
> Steve's item 2: Transits, Solar Arc and Secondary Progressions, each
> its own box, in any combination. `Arcs` (Maple, cut from `2839368`):
> `6386710` SolarArcHitFinder, the dated exact solar arc hits (arc
> inversion; majors, semisquare, sesquiquadrate; the 91 natal
> midpoints on the 45-degree dial); `7a864fa` ProgressedHitFinder (a
> daily grid, 1 s bisection; Sun to Mars, P MC and P ASC, a body to
> its own natal place); `62021a9` CLAUDE.md. `Modes` (Maple):
> `1e2db30` three technique flags replace IncludeProgressions;
> `4b5795f` any combination, the tables at the start, the dated hits
> in the period; `c817321` TransitReportNaming, titles and file names
> per combination; `a1ca2ed` CLAUDE.md. `ModesPanel` (Mac): `a31dbfa`
> the panel's three boxes and its gates; `776d489` Calculate and
> Export Report MD; `aaa062d` CLAUDE.md; `4cdb3fa` the Aspects line of
> a report without transits names each technique's set.
> - Goldens: the transit golden's header line and its two sections;
>   SolarArcMarkdownFormatter_J2000NYC_2026 new. Tier B: pyswisseph
>   2.10.03; Claude's own check agreed within 0.3 s.
> - Prompt library: P03 v3_9, P04 V2_9 and P28 V1_0 new, with their
>   Histories, User Guides and the 2026-10-17 manifest.
> - Maple, lean, at `4cdb3fa`: numstat equal (39 files, 3896/304,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3926/3926, 128/128; names -4/+71 core, +3 UI; Steve's WPF check
>   by eye passed. `D:\Code\transit-modes-maple-report.md`.
>
> `AstroCast.Tests` 3859 -> 3926; `AstroCast.UiTests` 125 -> 128.
> 1.4.0 is open; no tag.

**Landing record (2026-10-18, after the tag v1.4.0): branch Primary,
the 1.5.0 bump and the primary-directions engine**
> 1.5.0 opens with primary directions (plan v150-pd-plan.md, rules
> R1 to R10). `Primary` (Mac, cut from `bc9c8f6`): `eed4eab` the bump
> to 1.5.0 (release-bump skill); `4295e69` PrimaryDirectionService in
> AstroCast.Core: Placidus semi-arc, in zodiaco, direct and the
> traditional converse; Naibod, Ptolemy and the true solar arc in RA;
> the five classical significators (Mercury to Pluto by a setting);
> the planets, node, Chiron, antiscia and the 55 catalogue stars as
> promissors; months per minute of birth time and the range;
> CalculateFixedStarPosition on IEphemerisService; Bisection drawn
> out of SolarArcHitFinder. No panel and no export yet.
> - Tier B: Claude's pd_truth.py (pyswisseph 2.10.03), its seven lists
>   in the tests (TierB, .tsv), every hit equal to the digit printed.
>   Tier C: Moira 6.10.1 on DE440 equal on the direct directions it
>   computes, within 0.0015 deg.
> - Maple, lean, at `4295e69`: numstat equal (44 files, 6288/66,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   3957/3957, 128/128; names +31 core, UI unchanged.
>   `D:\Code\primary-maple-report.md`.
>
> `AstroCast.Tests` 3926 -> 3957; `AstroCast.UiTests` 128 -> 128.
> 1.5.0 is open; no tag.

**Landing record (2026-10-18, after the tag v1.4.0): branch
PrimaryExport, the primary-directions exports**
> `PrimaryExport` (Mac, cut from `7d0aa94`): `cbde1e1` the whole-life
> report (PrimaryDirectionMarkdownFormatter: the natal chart, then the
> section of method, key, significators, promissors, left out and
> birth-time lines and one row per hit: date, age, D or C,
> significator, aspect with sin or dex, promissor, arc D MM, months
> per minute, the range), FormatArc, .gitattributes keeping the TierB
> lists LF; `078f2e7` Primary Directions as the transit report's
> fourth technique (IncludePrimaryDirections, default false; the
> naming's fifteen forms); `3f56c5b` R5 amended: the Descendant and IC
> take only bodies' and stars' conjunctions (an antiscion, a
> contra-antiscion or the node there repeated a direction to the MC or
> Ascendant); the Tier B lists regenerated without the repeats.
> - Goldens: PrimaryDirectionMarkdownFormatter_J2000NYC and _Chart55,
>   TransitMarkdownFormatter_J2000NYC_2026_PrimaryDirections, new;
>   every table row equal to the Tier B lists but one half-rounding.
> - Maple, lean, at `3f56c5b`: numstat equal (32 files, 4043/319,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4049/4049, 128/128; names +123/-31 core, UI unchanged.
>   `D:\Code\primary-export-maple-report.md`.
>
> `AstroCast.Tests` 3957 -> 4049; `AstroCast.UiTests` 128 -> 128.
> 1.5.0 is open; no tag.

**Landing record (2026-10-18, after the tag v1.4.0): branch
PrimaryPanel, the Directions panel**
> `PrimaryPanel` (Mac, cut from `73f5497`): `57c0173` the Directions
> panel in both heads (NavigationPanel.Directions; a tenth rail button
> on Alt+0, all ten slimmed to fit; DirectionsViewModel with the key,
> planets, age limit and uncertainty for the session; the whole-life
> list on screen through FormatTable; its export and the Export
> panel's Primary Directions group; the engine an optional last
> MainViewModel argument); `54cb845` the Transits panel's fourth box,
> Include primary directions, checked by default and taking the
> Directions panel's settings, which gate Calculate; CLAUDE.md.
> - The Avalonia rail measured: ten fit at 950 x 600. Steve's looks
>   on the Mac and on Maple passed.
> - Maple, lean, at `54cb845`: numstat equal (29 files, 2177/119,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4080/4080, 138/138; names +31/-0 core, +11/-1 UI.
>   `D:\Code\primary-panel-maple-report.md`.
>
> `AstroCast.Tests` 4049 -> 4080; `AstroCast.UiTests` 128 -> 138.
> 1.5.0 is open; no tag.

**Landing record (2026-10-19, after the tag v1.5.0): branch
Asteroids, the 1.6.0 bump and the minor-points engine**
> 1.6.0 opens with the asteroids and Black Moon Lilith (plan
> v160-asteroids-plan.md, rules R1 to R9). `Asteroids` (Mac, cut
> from `46a34a8`): `b2371ac` the bump to 1.6.0 (release-bump skill);
> `94afbaf` Ceres, Pallas, Juno, Vesta and Lilith (the mean lunar
> apogee) in AstroCast.Core behind ChartInput.IncludeMinorPoints,
> off by default and never saved: ChartResult.MinorPoints and
> MinorPointAspects (the five Ptolemaic aspects at 2 deg to the Sun
> to Pluto, ASC and MC); none on a horary or electional chart; the
> relocation and the three returns copy the switch. No export,
> timing or panel yet; every golden unchanged.
> - Tier B: Claude's ast_truth.py (pyswisseph 2.10.03), its files in
>   the tests (MinorPoints/TierB), every value within 5e-10 deg.
>   Tier A: Lilith against Meeus ch. 47, within 4.7 arcsec (N1).
>   Tier C: JPL Horizons at J2000 NYC, within 2.0 arcsec (Pallas).
> - Maple, lean, at `94afbaf`: numstat equal (48 files, 1394/67,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4118/4118, 138/138; names +38/-0 core, UI unchanged.
>   `D:\Code\asteroids-maple-report.md`.
>
> `AstroCast.Tests` 4080 -> 4118; `AstroCast.UiTests` 138 -> 138.
> 1.6.0 is open; no tag.

**Landing record (2026-10-19, after the tag v1.5.0): branch
AsteroidsExport, the five in the exports**
> `AsteroidsExport` (Mac, cut from `aae88e4`): `c405156` prints
> Ceres, Pallas, Juno, Vesta and Lilith where R6 says, behind the
> switch: the section "Asteroids and Lilith" after Out-of-Bounds
> Planets in the natal and event chart Markdown and PDF (positions,
> then aspects at 2 deg), a minorPoints array in the chart JSON,
> synastry's own inter-chart section (each chart's five to the
> other chart's Sun to Pluto and angles), the composite five at the
> short-arc midpoints; relocation and the returns by their embedded
> charts. Primary directions, medical, electional and the mundane
> reports pass minorPoints: false (R8). With the switch off every
> output is as before: no existing golden changed.
> - Eight new _MinorPoints goldens; every row of the five in them
>   equal to Claude's Tier B (234 of 234), the returns to swe at
>   their printed instants.
> - Maple, lean, at `c405156`: numstat equal (37 files, 7821/159,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4178/4178, 138/138; names +60/-0 core, UI unchanged.
>   `D:\Code\asteroids-export-maple-report.md`.
>
> `AstroCast.Tests` 4118 -> 4178; `AstroCast.UiTests` 138 -> 138.
> 1.6.0 is open; no tag.

**Landing record (2026-10-19, after the tag v1.5.0): branch
AsteroidsTiming, the five as timing targets**
> `AsteroidsTiming` (Mac, cut from `5537b64`): `d7d5c01` makes the
> natal Ceres, Pallas, Juno, Vesta and Lilith targets for timing
> (R7), behind the switch, each in sections of their own: transits
> (the bodies that transit today, Mars by the existing gates; the
> five Ptolemaic aspects at each body's major orbs), the timeline's
> windows and events (not the Clusters), and the progressed and
> solar arc aspects to the five with their dated hits. The five
> never move: no transiting, progressed or directed asteroid or
> Lilith. With the switch off every report is as before: no
> existing golden changed.
> - Three new _MinorPoints goldens; every row of the five in them
>   equal to Claude's Tier B (141 of 141), the truth on the engine's
>   conventions (365.25-day year, 12:00 UT, in-orb stations only).
> - Maple, lean, at `d7d5c01`: numstat equal (25 files, 4298/154,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4212/4212, 138/138; names +34/-0 core, UI unchanged.
>   `D:\Code\asteroids-timing-maple-report.md`.
>
> `AstroCast.Tests` 4178 -> 4212; `AstroCast.UiTests` 138 -> 138.
> 1.6.0 is open; no tag.

**Landing record (2026-10-20, after the tag v1.5.0): branch
AsteroidsPanel, the Points row**
> `AsteroidsPanel` (Mac, cut from `90eca0d`): `7e8ee0f` puts the
> session switch on screen: a Points row under System in the Chart
> panel's Houses section in both heads ("Include asteroids and
> Lilith", off at launch, kept by Clear Form and Load, never saved);
> every chart input MainViewModel builds or maps carries its value
> (seven sites); ticking or unticking recasts the Chart panel's
> chart at once without saving it (Steve's choice, 2026-10-20),
> through ChartInput.WithIncludeMinorPoints, with a status line
> naming the chart. No golden changed.
> - Steve's looks on the Mac and on Maple passed.
> - Maple, lean, at `7e8ee0f`: numstat equal (11 files, 953/25,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4229/4229, 140/140; names +17/-0 core, +2/-0 UI.
>   `D:\Code\asteroids-panel-maple-report.md`.
>
> `AstroCast.Tests` 4212 -> 4229; `AstroCast.UiTests` 138 -> 140.
> 1.6.0 is open; no tag.

**Landing record (2026-10-21, after the tag v1.6.0): branch
StaleResult, the Relocation result and the export's labels**
> `StaleResult` (Mac, cut from `fe6e238`): `d22f90b` opens 1.6.1 (the
> bump, eight csprojs); `1b9df30` clears the Relocation panel's result
> when the picked chart's Id, the place or the Chart panel's chart
> changes, so Export Relocation MD never writes a result the panel no
> longer shows (RelocationViewModel.InvalidateResult; a saved-charts
> refresh re-pointing the picker at the same chart keeps it; a failed
> or cancelled Calculate leaves it); `e6a30d9` labels the relocation
> export's header by the chart's type (Birth as before; Question for
> a horary chart; Event for the rest); `24dde9a` the docs. No golden
> changed.
> - Steve's looks on the Mac and on Maple passed.
> - Maple, lean, at `24dde9a`: numstat equal (18 files, 490/61,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4240/4240, 141/141; names +11/-0 core, +1/-0 UI.
>   `D:\Code\stale-result-maple-report.md`.
>
> `AstroCast.Tests` 4229 -> 4240; `AstroCast.UiTests` 140 -> 141.
> 1.6.1 is open; no tag.

**Landing record (2026-10-21, after the tag v1.6.0): branch
ElectionalPicker, the Electional picker and the Points box**
> `ElectionalPicker` (Mac, cut from `7400142`): `c2dc723` folds the
> nested test double into the shared RecordingChartEngine (probe
> and hold); `b0ed45e` makes the Electional natal-chart picker
> follow each Load and Calculate & Save, with a line under it
> naming the chart in use, as the Relocation picker has since
> 1.4.0; `9621cb3` Use Natal Location takes the saved chart's own
> zone, the lookup only when it is blank; `2e35699` the electional
> form fallback recasts its natal chart when the Points box moved
> during the cast; `b21c286` a recast error puts the box back;
> `abce6db` Clear Form empties both pickers (Steve's WPF look,
> item 4); `9f28dea` and `d115e62` the docs. No golden changed.
> - Steve's looks on the Mac and on Maple passed.
> - Maple, lean, at `d115e62`: numstat equal (13 files, 616/153,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4251/4251, 142/142; names +12/-1 core, +2/-1 UI.
>   `D:\Code\electional-picker-maple-report2.md`.
>
> `AstroCast.Tests` 4240 -> 4251; `AstroCast.UiTests` 141 -> 142.
> 1.6.1 is open; no tag.

**Landing record (2026-10-21, after the tag v1.6.0): branch
TiesAndGuards, the last of 1.6.1**
> `TiesAndGuards` (Mac, cut from `019303e`): `1778ec5` makes the
> solar-arc key refuse a birth whose Sun reading would pass the
> ephemeris end (2399-09-02 at the default limit), with a plain
> message, and clamps the inverter's bracket; `03a9041` gives a
> defined order on ties, synastry aspects at one orb by their names
> and the angle, primary-direction hits within a second by
> significator (CompareByOrbThenNames, CompareHits); `6484d1d` the
> panel-width sweep's comments count ten panels; `4159e88` the docs
> marking 1.6.1 complete. No golden changed.
> - Steve's looks on the Mac and on Maple passed.
> - Maple, lean, at `4159e88`: numstat equal (7 files, 527/34,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4257/4257, 142/142; names +6/-0 core, 0/0 UI.
>   `D:\Code\ties-and-guards-maple-report.md`.
>
> `AstroCast.Tests` 4251 -> 4257; `AstroCast.UiTests` 142 -> 142.
> 1.6.1 is complete; the tag v1.6.1 follows in the release run.

**Landing record (2026-10-21, after the tag v1.6.1): branch
Davison, the Davison chart's engine and export**
> `Davison` (Mac, cut from `970af18`): `6383fea` opens 1.7.0 (the
> bump, a minor: a new capability); `6906a9c` DavisonService casts a
> real chart at the mean of the two birth Julian Days and the mean
> latitude with the short-arc mean longitude (an exact 180 deg takes
> the eastward half), type "Davison", Chart A's houses, the five by
> the Points switch, through CalculateAtInstant at offset 0;
> `25b24e9` DavisonMarkdownFormatter, the composite's shape with the
> chart printed complete, and its two goldens checked row by row
> against Claude's pyswisseph truth (davison_truth.py, five pairs);
> `b80a941` the docs; `77f4f21` one test matching the title by its
> first line (Maple's first run failed it on CRLF). Nothing visible
> yet: the panel follows.
> - No look: no head changes.
> - Maple, lean, at `77f4f21`: numstat equal (19 files, 2066/44,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4287/4287, 142/142; names +30/-0 core, 0/0 UI.
>   `D:\Code\davison-maple-report2.md`.
>
> `AstroCast.Tests` 4257 -> 4287; `AstroCast.UiTests` 142 -> 142.
> 1.7.0 is open; no tag.

**Landing record (2026-10-21, after the tag v1.6.1): branch
DavisonPanel, the Davison section on the Synastry panel**
> `DavisonPanel` (Mac, cut from `d85441f`): `4ecd686` puts the Davison
> chart on screen: a Davison Chart section under the Composite
> section in both heads (a Context box; Calculate Davison, Cancel
> with the key "davison", Export Davison MD), the Export panel's
> group "Synastry, Composite & Davison" with a third button;
> SynastryViewModel's DavisonResult and context; MainViewModel's
> commands, CalculateDavisonAsync (Person B cast with the Points
> box) and ExportDavisonMarkdownAsync ({A}_{B}_Davison.md); the
> service as the last optional constructor argument, both Apps
> passing one; `21e53e4` the docs. No golden changed.
> - Steve's looks on the Mac and on Maple passed.
> - Maple, lean, at `21e53e4`: numstat equal (16 files, 813/34,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4296/4296, 145/145; names +9/-0 core, +3/-0 UI.
>   `D:\Code\davison-panel-maple-report.md`.
>
> `AstroCast.Tests` 4287 -> 4296; `AstroCast.UiTests` 142 -> 145.
> 1.7.0 is open; no tag.

**Landing record (2026-10-24, after the tag v1.7.0): branch
Fixtures, the private-person fixtures replaced**
> `Fixtures` (Mac, cut from `9e227eb`): `f1760da` bumps to 1.7.1;
> `7a9c9ae` carries person A (invented) through the saved-row layer
> and both tooltips read "(e.g., Richmond, VA)"; `acf5b9a` persons
> A, B and C in the engine-level tests, the Tier B tables
> hartwell_naibod, hartwell_points, hartwell_aspects and the lilith
> row (Claude's truth, copied byte for byte), the chart55 tables
> removed; `043b28a` the formatter tests and the primary-direction
> golden regenerated as _Hartwell; `50153c4` six invented horaries
> at Columbus, Ohio, found by the file's harness; `1553fb8` the
> prose. Every other pinned value re-derived on pyswisseph
> (fixtures/truth/). No private person, birth date or question
> remains outside CLAUDE.md and this plan.
> - Steve's WPF look on Maple passed (the tooltip, the version).
> - Maple, lean, at `1553fb8`: numstat equal (68 files, 2363/2334,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4296/4296, 145/145; names 61 renamed core, 0/0 UI.
>   `D:\Code\fixtures-maple-report.md`.
>
> `AstroCast.Tests` 4296 -> 4296; `AstroCast.UiTests` 145 -> 145.
> 1.7.1 is open; no tag.

**Landing record (2026-10-24, after the tag v1.7.0): branch
SvgExclude, the Svg assemblies stop shipping**
> `SvgExclude` (Mac, cut from `99f9c0f`): `890c59f` gives both heads
> direct references to Svg.Custom, Svg.Model and Svg.Skia 3.2.1 with
> ExcludeAssets="runtime", so no Svg assembly ships (Mapsui's raster
> tiles never touch the SVG path: the probe of 2026-10-24); a test
> pins the six references; `31a164e` records the settlement in
> CLAUDE.md's Licence section and the notices' preamble. The notices
> rows stay; deps.json still names the packages without a runtime
> section. No golden changed.
> - The Avalonia head's Release publish holds no Svg DLL; its map
>   drew tiles (screenshot). Steve's WPF look on Maple passed.
> - Maple, lean, at `31a164e`: numstat equal (5 files, 68/5,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4297/4297, 145/145; names +1/-0 core, 0/0 UI; the WPF publish
>   output holds no Svg DLL. `D:\Code\svg-exclude-maple-report.md`.
>
> `AstroCast.Tests` 4296 -> 4297; `AstroCast.UiTests` 145 -> 145.
> 1.7.1 is open; no tag.

**Landing record (2026-10-28, after the tag v1.7.0): branch
Public, the front page and the swept history**
> `Public` (Mac, cut from `afd1930`): `960bf7c` adds README.md,
> CONTRIBUTING.md and docs/LICENSING.md (drafted by Claude, approved
> by Steve, copied in byte for byte) and CLAUDE.md's Documentation
> section with the rule that a user-visible change updates the user
> guide and the usage map in the same branch; `f1d266b` sweeps
> CLAUDE.md and this plan of the last private data (one wrapped
> source note, one horary's name) and rewrites the sharing items to
> the decisions: the public repository takes sthornhill008/AstroCast
> from the cleaned tree, the private history renamed; public builds
> ship no CARTO key. No code, no test, no golden changed.
> - Maple, lean, at `f1d266b`: numstat equal (5 files, 295/7,
>   histogram); 0 errors in all eight projects, `xUnit1031` only;
>   4297/4297, 145/145; names 0/0 core, 0/0 UI.
>   `D:\Code\public-maple-report.md`.
>
> `AstroCast.Tests` 4297 -> 4297; `AstroCast.UiTests` 145 -> 145.
> 1.7.1 is open; no tag.

**Phase 6 -- Parity verification and steady state (2-3 days, ~2-4 sessions)**
Export the full set of Markdown reports for reference charts on both OSes and
diff them (goldens make this nearly free), from 1.3.4 output: every 1.3.4 branch
moved goldens, and every byte count in Section 6 predates 1.3.3. Add, on top of
the equality check, the content assertion Section 6 adopted (does the output
contain what the panel claims it contains?). Decide WPF retirement vs. dual-UI
(Q4). Document the macOS build in CLAUDE.md (its build table names the bundle
and DMG scripts since Docs-2) and a verification script.
*Exit: documented, repeatable macOS build; parity report saved to
`D:\Code\`, beside the Maple branch reports.*

Total: **~5-8 weeks part-time**; the engine itself proves out on macOS within
Phase 1, de-risking everything after it.

---

## 7. Risks and Open Questions (decisions needed)

> **Note (2026-10-14, Docs-2).** Sections 7 and 8 are the planning text of
> 2026-07-05 and stand as written. Two of their statements no longer hold:
> `Mapsui.Avalonia12` 5.1.0 ships in the Avalonia head beside `Mapsui.Wpf` 5.0.2
> in the WPF head, and by ruling the split waits (CLAUDE.md, *Head Parity*); and
> the Serilog file sink writes to `<DataDirectory>/logs`,
> `~/Library/Application Support/AstroCast/logs` on macOS (`AppPaths`), not to
> `~/Library/Logs/AstroCast` (8.2).

**Q1. Repo sharing between the PC and the Mac.** The repo is local git inside a
OneDrive-synced folder. Letting OneDrive sync `.git` across two OSes risks
corruption (file locks, metadata races) -- and the existing OneDrive sync rule
(disable before heavy disk writes) is hard to honor from two machines. Options:
(a) bare repo on OneDrive, working clones outside OneDrive on each machine,
push/pull to the bare repo; (b) direct clone over LAN/USB and manual sync;
(c) a private remote. Recommendation: (a). **Needs your call.**

> **RESOLVED 2026-07-05 (variant of (a)).** Working clone created at
> `~/Developer/AstroCast` via `git clone --no-hardlinks` (objects are real
> copies, immune to OneDrive dehydration): clean tree at `dad3c56` on the fix
> branch, all seven branches available as `origin/*`, 44 MB (the 2.7 GB of
> OneDrive bulk was ignored build output; `ephe/` and `lib/swisseph` are
> tracked and came over). The existing OneDrive repo stays untouched as
> `origin` and as the Windows working copy. Ground rules: never push to the
> branch the OneDrive repo has checked out; pushing *new* branch names is safe
> (they reach the PC via OneDrive sync). `MACOS_PORT_PLAN.md` (untracked) and
> `CLAUDE.local.md` (ignored) were copied into the clone by hand. Long-term, a
> private GitHub remote (option (c), the ChessSage pattern) remains the likely
> end state.

**Q2. Line-ending policy.** Standards mandate CRLF; the repo has *no*
`.gitattributes` and relies on `core.autocrlf=true` locally. A Mac clone with
default settings would produce LF working files and noisy diffs. Recommendation:
commit a `.gitattributes` (`* text=auto` + `*.cs text eol=crlf` etc.) before the
first Mac checkout. **Needs your call (touches a workspace convention).**

> **RESOLVED 2026-07-05 -- measured, and better than feared.** `git ls-files
> --eol` shows every committed text blob is already LF (503 i/lf, 0 i/crlf):
> Windows `core.autocrlf=true` has been normalizing on commit all along, so no
> renormalization is needed and the Mac clone checks out **clean** -- the "273
> modified files" seen in the OneDrive folder were only the Windows CRLF
> working files synced across, never real diffs. A `.gitattributes` is in the
> clone: `* text=auto` plus binary pins (`.ico .se1 .dll .lib .dylib`, images,
> `.db`), deliberately **without** `eol=crlf` overrides -- Windows checkouts
> keep CRLF via autocrlf, and forcing `*.cs eol=crlf` would fight the existing
> LF blobs. Proven zero-churn (status stays clean with it in place). Still
> uncommitted; commit as its own small commit on a branch of Steve's choosing.

**Q3. IDE on the Mac.** Visual Studio 2026 has no macOS edition (VS for Mac was
retired in 2024). Realistic options: JetBrains Rider (free for non-commercial
use) or VS Code + C# Dev Kit. Related consequence: the Automatic Versions
extension is VS-only, so commits made from the Mac will not auto-increment
versions -- either keep version-bump commits on Windows or script the bump.
**Preference needed.**

> **RESOLVED 2026-07-05: both.** Rider 2026.1.4 (Toolbox-managed, free
> non-commercial license) as the primary IDE, plus VS Code 1.127.0 with the
> C# Dev Kit. The Automatic-Versions consequence stands: version bumps stay
> on Windows until/unless the bump is scripted.

**Q4. One UI or two, long-term.** Avalonia runs on Windows as well. After the
port stabilizes you can retire the WPF head and maintain a single UI -- or keep
WPF as the Windows daily driver indefinitely. The plan keeps both alive through
Phase 6 either way. **Decision can wait until Phase 6.**

**Q5. Keyboard shortcuts on macOS.** Alt+1..9 conflicts with macOS Option-key
character input; the natural mapping is Cmd+1..9. Confirm preference.

**Q6. Windows data location.** ~~Should Windows also move its database/logs out
of the EXE directory (to `%LOCALAPPDATA%\AstroCast`) when `IAppPaths` lands, or
keep current behavior?~~ **Resolved 2026-07-28 (Phase 4 slice 2).** The Avalonia
head uses `%LOCALAPPDATA%\Thornhill Computer Software\AstroCast` — the folder
the installer already creates — so an installed Avalonia build inherits an
installed WPF build's database with no migration. The WPF head keeps
`BaseDirectory` and is untouched. The premise that a migration would be needed
was based on the Section 9 claim that has since been struck as false.

**Q7. Signing tier** (Section 8.3): ad-hoc signing (free, fine for your own
Mac) vs. Apple Developer ID + notarization (USD 99/yr, needed only if the app
will ever be given to someone else). Recommendation: ad-hoc for now.

> **CONFIRMED WORKING 2026-07-05.** The ad-hoc tier was functionally verified
> on this Mac: a clang-built arm64 dylib signed with `codesign -s -` reports
> `Signature=adhoc` and passes `codesign --verify --strict`. Recommendation
> unchanged: ad-hoc until the app ever leaves this machine.
>
> **RESOLVED 2026-08-10 (Phase 5 slice 5c): Developer ID + notarization, not
> ad-hoc.** The condition the recommendation named -- "until the app ever leaves
> this machine" -- was reached, so the higher tier was taken. Both the `.app` and
> the DMG are signed with Developer ID, notarized and stapled, and accepted by
> Gatekeeper on a machine that has never seen the source. The bundle identifier
> `com.sthornhill.astrocast` must stay stable across releases. Procedure and
> script: `build/macos/SIGNING.md` and `build/macos/package-macos.sh`.

**Technical risks (no decision needed, tracked):**
- `Mapsui.Avalonia` version pairing with Mapsui 5.0.2 could not be verified
  offline; confirm the exact package/version at Phase 4 start. Fallback exists
  (render ACG lines with Avalonia primitives) but would cost extra sessions.
- QuestPDF/SkiaSharp text metrics could differ slightly on macOS; Phase 1
  explicitly opens the generated PDF to check.
- The Linux `libswe.so` naming mismatch (Section 3.2) suggests non-Windows
  native loading has never run in anger; Phase 1 is sequenced first precisely
  to burn this risk down.
- Estimates for Phase 4 carry the usual XAML-conversion uncertainty (108
  trigger lines translate to Avalonia selectors with some judgment calls).

**Could not be determined from the code (stated per the read-only constraint):**
whether the CLI has ever actually run on Linux Mint end-to-end; actual
availability/versions of Avalonia-side packages as of today; QuestPDF rendering
fidelity on macOS. All three are verified empirically in Phases 1 and 4.

---

## 8. macOS-Specific Concerns

### 8.1 App bundle and publishing

- Publish `osx-arm64`, self-contained (no .NET install needed on the Mac;
  simpler Gatekeeper story). The M5 Pro is Apple Silicon; building a universal
  (x64+arm64) binary is possible (two publishes + `lipo` for the dylib) but
  unnecessary for personal use -- recommend arm64-only.
- Bundle layout produced by the Phase 5 script:

  ```
  AstroCast.app/Contents/
    Info.plist            (CFBundleIdentifier e.g. com.thornhill.astrocast,
                           version fields fed from the csproj like WiX today)
    MacOS/                (published binaries + libswe.dylib)
    Resources/            (AstroCast.icns, ephe/ data files)
  ```

- `AstroCast.ico` converts to `.icns` via `sips`/`iconutil` (scriptable in the
  bundle script).

### 8.2 Runtime behavior differences to plan for

- Writable data must leave the bundle (Section 3.5) -- signed bundles are
  immutable in practice and app translocation can run the app from a
  randomized read-only path.
- Serilog file sink path moves to `~/Library/Logs/AstroCast`.
- Menu bar: macOS expects an application menu (About/Quit, Cmd+Q). Avalonia's
  `NativeMenu` provides this cheaply; the nine panels can also be listed in a
  View menu, which is where Cmd+1..9 shortcuts conventionally live.
- Window chrome, Retina scaling, and native file dialogs come free with
  Avalonia (`StorageProvider` uses NSSavePanel).

### 8.3 Code signing, notarization, distribution

| Tier | What it involves | When it is enough |
|------|------------------|-------------------|
| Unsigned | Nothing; right-click-Open once to bypass Gatekeeper | Local builds built on the same Mac |
| Ad-hoc signed (`codesign -s -`) | One script line; no Apple account | **Recommended**: personal use on your own machine |
| Developer ID + notarization | USD 99/yr Apple Developer Program; `codesign` with hardened runtime + `notarytool` submit + staple | Only if the app is ever distributed to another Mac |
| Mac App Store | Sandbox entitlements, review process | Not recommended for a personal tool (network access, file dialogs, and the SQLite location all need entitlement work for zero benefit) |

If notarization is ever pursued: .NET apps under the hardened runtime need the
`com.apple.security.cs.allow-jit` (and typically
`allow-unsigned-executable-memory` / `disable-library-validation`) entitlements,
and every dylib in the bundle -- including `libswe.dylib` -- must be signed.
Distribution format: a DMG (or plain zip) produced by the Phase 5 script; this
is the macOS analogue of `BuildInstaller.ps1`, including reading the version
from the published binary as the MSI script does today.

> **DONE 2026-08-10 (Phase 5 slice 5c) -- and two of the guesses above were
> wrong.** The tier taken is **Developer ID + notarization**, not the
> recommended ad-hoc (see Q7). Of the three anticipated entitlements **only
> `allow-jit` was needed**: library validation passes without
> `disable-library-validation` because every nested binary is signed with the
> same Team ID. And **"every dylib must be signed" understates it by an order of
> magnitude** -- the requirement is every object in `Contents/MacOS/` other than
> the main executable, which for a self-contained .NET publish is **278 objects,
> not the 21 Mach-O binaries**. The predicate is location, not content. Both
> the `.app` and the DMG are notarized and stapled. Procedure:
> `build/macos/SIGNING.md`; script: `build/macos/package-macos.sh`.

---

## 9. Out-of-Scope Observations (not part of this port; noted separately)

1. ~~**Windows writable-data location.** The app writes `astrocast.db` and logs
   to `BaseDirectory`, but the MSI installs under Program Files, where standard
   users cannot write. If the installed (rather than dev-tree) app is ever run
   day-to-day, saves would fail.~~ **Struck 2026-07-28 (Phase 4 slice 2): both
   claims were false.** The package is per-user (`Package.wxs:13`,
   `Scope="perUser"`) and rooted at `LocalAppDataFolder` (`Directories.wxs:9`),
   never Program Files, so `BaseDirectory` for an installed build is
   `%LOCALAPPDATA%\Thornhill Computer Software\AstroCast` — a writable
   location. The disproof is on disk: the installed application ran and wrote
   there successfully on five occasions between 2026-03-11 and 2026-04-09,
   leaving a database and five log files. Saves were never at risk.

   The real finding in this area is a different one: the installed build's
   database is **empty**, while the ~88 charts of actual work live in the WPF
   *development* build's `bin\Debug` directory, which no installed head reads.
   That gap predates the port and no choice of data directory closes it; it
   needs a deliberate one-time import. Slice 2 pointed the Avalonia head at the
   same publisher-scoped folder the installer uses, so the two installed heads
   now share one database.
2. ~~**`GeoTimeZone` floats (`Version="*"`).** Any fresh restore (e.g., first
   build on the Mac) may silently pick a newer version than the PC used. Worth
   pinning independently of the port.~~ **Struck 2026-08-08 (dependency
   hardening): this was already done, and had been for two weeks.** Commit
   `d293711` (2026-07-24, "chore(deps): pin GeoTimeZone to 6.1.0 in WPF
   project") replaced `Version="*"` with `Version="6.1.0"`, and the
   `AstroCast.Presentation` copy was created already pinned by the Phase 3
   extraction (`9bbb4c3`), so it never floated. A sweep of the whole tree for
   floating syntax -- `Version="*"`, wildcards, ranges, `VersionOverride`, and
   central package management -- found **nothing floating anywhere**;
   `Directory.Build.props` carries metadata only. The version question is moot
   in any case: **6.1.0 is the newest stable** (17 versions, no prereleases),
   so pinning to the proven-good version and pinning to the newest are the same
   act. This item survived as open work only because the document was never
   updated to match the commit.
3. **Linux native library naming** (Section 3.2): `libswe.so` as instructed by
   the README cannot be found by `DllImport("swedll64")`. The resolver planned
   in Phase 1 fixes this as a side effect.
4. **The medical health-context input is dead** (found 2026-08-08, Phase 4
   slice 6). `MedicalHealthContext` is bound by the Medical panel in both heads
   and read by nothing: neither `HealthProfileGenerator.Generate(ChartResult)`
   nor `MedicalMarkdownFormatter.Format(ChartResult, HealthProfile,
   TimeProvider?)` accepts a health context. The panel tells the user the text
   is "included verbatim in the exported report header for AI-assisted
   interpretation"; it never reaches the file. Confirmed by negative control --
   different text in each head, byte-identical exports, neither string present.
   Fixing it means a formatter parameter, a call-site change, and golden
   regeneration: a product change, deliberately not made during the port. The
   full record is in the slice 6 entry in Section 6.
   **Fixed 2026-10-12 (branch O-7, release 1.3.3):** the medical export prints
   the panel's Health Context in its header (`MedicalMarkdownFormatter.Format`'s
   optional `healthContext`).
5. **CLAUDE.md drift.** The documented DB schema and stage list lag the code
   (e.g., `Gender`/`SourceNotes` fields exist in `ChartInput`; many post-Stage-8
   features shipped). Worth a docs pass someday; the plan above trusts the code.
   **Closed by Docs-2 (2026-10-14, release 1.3.4):** CLAUDE.md lists the
   schema's 22 columns, and its status, project structure and build commands
   follow the code.
6. **`UseNatalLocationForRelocationCommand` is unreachable after a plain
   calculate** (found 2026-08-08, Phase 4 slice 7). `HasResult`'s setter calls
   `RefreshExportCommandStates()`, whose list omits the command; only
   `RefreshRelocationCommandStates()` raises it, and only the Relocation
   child's setters reach that. The button that fills the Location field can
   therefore be woken only by picking a dropdown chart or by typing into that
   same field. The Electional twin is in the refresh list, so this is an
   omission. One line to fix; it belongs with a review of who calls which
   refresh.
   **Status 2026-10-14 (Docs-2): open.** `RefreshExportCommandStates` still
   omits the command, and only `RefreshRelocationCommandStates` raises it.
   **Fixed 2026-10-16 (branch Panels, release 1.4.0):** `RefreshExportCommandStates`
   ends with `RefreshRelocationCommandStates()`, as it reaches the Electional twin.
   The scoping's review of every command's enablement found this the one gap,
   and item 16 the one missing input.
   **Status 2026-10-16 (branch Relocate, release 1.4.0): the command is removed.**
   The Relocation panel was reworked as one birth chart, then a new place
   (Steve's item 3), and `UseNatalLocationForRelocationCommand` left it with its
   method and button: relocating a chart to its own birthplace gives the birth
   chart back. The `RefreshRelocationCommandStates()` call in
   `RefreshExportCommandStates` stays, since Calculate Relocation reads
   `HasResult` too.
7. **The Relocation panel promises an empty-selection mode it does not have**
   (found 2026-08-08, Phase 4 slice 7). Hint and tooltip say to leave the chart
   box empty to use the currently calculated chart; the ComboBox is
   non-editable, there is no null sentinel in `SavedCharts`, and
   `RelocationViewModel.Clear()` is not called from `ClearForm`, so no UI path
   returns to empty. Either implement the mode or correct the text. Synastry's
   Person B does not share the defect.
   **Status 2026-10-14 (Docs-2): open.** Both heads' hint and tooltip still
   offer the empty selection, and `ClearForm` still does not call
   `Relocation.Clear()`.
   **Fixed 2026-10-16 (branch Panels, release 1.4.0), by correcting the text:**
   the hint reads "Select a saved chart. Until one is chosen, the chart
   calculated on the Chart panel is used." and the tooltip likewise; the
   Electional natal-chart tooltip, which made the same promise, now says the
   same, adding "or else one cast from the Chart panel's fields". No empty
   entry or Clear button was added (Steve's decision).
8. **`AllSavedCharts` is dead, and three comments claim otherwise** (found
   2026-08-08, Phase 4 slice 7). The collection is built and maintained but
   bound by nothing in either head; every picker binds `SavedCharts`. Delete
   the collection or bind it, and fix the two XML doc comments and the one
   axaml comment that describe it as in use.
   **Status 2026-10-14 (Docs-2): the comments are fixed, the collection is
   not.** Docs-2 corrected those three comments and the Synastry and Relocation
   view models' two; `AllSavedCharts` is still built and bound by nothing.
   **Closed 2026-10-16 (branch Panels, release 1.4.0), by binding it.** The
   scoping's probe found that every refill of `SavedCharts` (a save, a search,
   a sort, an import, a delete) emptied the three pickers bound to it: new
   instances, and each ComboBox wrote null back on the clear. Steve's ruling:
   Person B, the Electional natal chart and the Relocation natal chart bind
   `AllSavedCharts`, whose refresh keeps each selection by Id, so only deleting a
   chart empties its picker. The comments and XML docs say so.
9. **Coordinate entry fails silently and is unvalidated** (found 2026-08-08,
   Phase 4 slice 7). `LatitudeText`/`LongitudeText` raise no command refresh,
   parse only on button press, and report failure to the status bar; nothing
   range-checks them. Wants parse-on-change with inline validation.
   **Status 2026-10-14 (Docs-2): open.** `LatitudeText` and `LongitudeText` are
   still plain properties, parsed only by `CalculateRelocationAsync`, with no
   range check.
   **Fixed 2026-10-16 (branch Panels, release 1.4.0):** one check for every
   coordinate box, `ChartEntryViewModel.TryParseLatitude` and `TryParseLongitude`
   (a point, invariant, ±90 and ±180, the Natal panel's messages; a comma is
   refused). The Relocation boxes parse on every change, request a command
   refresh, draw the time-zone box's red border with the message as their
   tooltip, and disable Calculate while invalid or blank; Calculate & Save and
   the electional search read through the same check.
10. **Two UI-thread Swiss Ephemeris calls with no cancellation** (found
    2026-08-08, Phase 4 slice 7): in `CalculateRelocationAsync` before its
    `Task.Run`, and the whole of `UseNatalLocationForRelocation`. Both freeze
    the UI and neither is cancellable, unlike the Synastry and Composite paths
    beside them. Move both onto the existing background/token pattern.
    **Status 2026-10-14 (Docs-2): open.** `CalculateRelocationAsync` still casts
    the natal chart before it takes its token and starts its `Task.Run`, and
    `UseNatalLocationForRelocation` still casts on the UI thread.
    **Fixed 2026-10-16 (branch Panels, release 1.4.0):** `CalculateRelocationAsync`
    casts a dropdown chart inside its one `Task.Run` with the relocation token,
    Synastry's shape, so Cancel reaches it; `UseNatalLocationForRelocation` copies
    the saved chart's place as stored and casts nothing.
    **Status 2026-10-16 (branch Relocate, release 1.4.0):**
    `UseNatalLocationForRelocation` is removed with its command (see item 6), so
    `CalculateRelocationAsync` is the Relocation panel's one caster, and it casts
    under its token.
11. **A WPF label clip: `Relationship`** (found 2026-08-08, Phase 4 slice 7).
    It overflows the shipped WPF head's 72 px label slot by about 0.7 px,
    reporting 73 where every other label clamps to 72 -- the overflow
    signature that also identified the Transits `Progressions` clip. WPF only;
    the Avalonia head's 100 column clears it. Fixed by widening that head's
    label column, whenever WPF's retirement is decided (Q4).
    **Status 2026-10-14 (Docs-2): open in the markup.** The WPF Synastry grid's
    label column is still 80 wide (the 72 px slot); not re-measured, which needs
    Windows.
12. **The astrocartography map-drawing code now exists twice** (found 2026-08-08,
    Phase 4 Map slice). Roughly four hundred lines are identical between
    `src/AstroCast/AcgMapWindow.xaml.cs` and
    `src/AstroCast.Avalonia/Views/AcgMapWindow.cs`: layer building, projection,
    the CartoDB tile source, label placement, the twelve-body colour table and
    the crossing-report summariser. They are identical because they speak
    Mapsui's own API rather than either UI framework's, which is exactly why they
    could be shared. Two copies can drift, and the colour table is now covered by
    a test only in one direction.

    Extracting them into a shared Mapsui presentation type is genuine work that
    must **modify the WPF head**, which no port slice is allowed to do, so it was
    deliberately not attempted here. It also has a real design question attached:
    a shared type needs a home, and putting Mapsui into `AstroCast.Presentation`
    would drag it into the test projects and into `AstroCast.Cli`. Worth doing
    once WPF's future is decided (Q4), since if WPF retires the duplication
    disappears on its own.

    **Status 2026-10-14 (Docs-2): partly shared.** The tile settings
    (`BaseMapSettings`, `AstroCast.Presentation`, since the CARTO key branch)
    and the line geometry (`AcgMapGeometry`, Core, since branch J) are now one
    copy each; the layer building, the label placement, the colour table and
    the crossing-report summariser are still written twice (570 and 668 lines).
13. **RESOLVED 2026-08-08 -- the `NU1903` SQLite advisory, and the pin that
    closes it.** `AstroCast.Core` now carries an explicit
    `SQLitePCLRaw.bundle_e_sqlite3` **2.1.12** reference. It is a security pin,
    not a feature dependency, and **removing it silently reintroduces a high
    severity advisory**: CVE-2025-6965 (GHSA-2m69-gcr7-jv3q, CVSS v4 7.2,
    CWE-197), a memory-corruption flaw in SQLite before 3.50.2.

    **Bumping `Microsoft.Data.Sqlite` could not have fixed this.** Every
    10.0.x, checked through the newest stable 10.0.10, pins
    `bundle_e_sqlite3` 2.1.11; there is no version of the direct package that
    resolves a fixed SQLitePCLRaw, which is why an explicit transitive pin was
    the only route. `Microsoft.Data.Sqlite` deliberately stayed at 10.0.3 so a
    regression could not be ambiguous. Retire the pin only once the direct
    package's own floor reaches 2.1.12 or later.

    **A range check is not a fix check.** NuGet's vulnerability index declares
    the affected range as `(, 2.1.11]`, so *any* higher version clears the
    warning whether or not it carries the repair -- and GitHub's advisory page
    still reports "no patched version available". The fix was therefore
    confirmed at the artefact: the deployed native moved **SQLite 3.49.1 ->
    3.53.3** (`sqlite_version()` read at runtime, source id 2026-06-26), which
    is above the 3.50.2 fix line. Bundle 3.x was rejected as higher risk for no
    additional benefit -- it restructures into `config.e_sqlite3` plus a new
    `SQLite` package while `Microsoft.Data.Sqlite.Core` still asks for
    `SQLitePCLRaw.core` 2.1.11.

    **Two facts Phase 5 needs.** The `runtimes/` RID count is unchanged (26 WPF
    / 28 Avalonia) with every target platform present at 3.53.3, but
    `runtimes/win-arm/native/e_sqlite3.dll` (32-bit ARM Windows, which .NET 10
    does not target) is **no longer shipped** -- the only asset removed. And the
    bundle for the `.app` grows slightly: `osx-arm64/native/libe_sqlite3.dylib`
    goes 1,554,624 -> 1,696,624 bytes. Nothing here touches `libswe.dylib` or
    the ephemeris copy items, which are our own artefacts rather than
    NuGet-delivered.
14. **The Avalonia head's `CalendarDatePicker` button glyph is wrong on Windows,
    at nine sites** (found 2026-08-06, corrected 2026-08-07, count closed
    2026-08-10 at Phase 4 slice 9). The running app on Windows draws a stray
    digit -- a dot above a "7" -- where the calendar icon belongs; macOS renders
    it correctly. WPF is immune because its icon is theme vector art, while
    Avalonia's is a font glyph and can therefore fall back. `Segoe Fluent Icons`
    is installed and the app declares no global `FontFamily` override, so it is
    neither a missing font nor an app-level override; the unconfirmed hypothesis
    is that the Fluent theme's symbol font resource does not resolve to the icon
    font for this control, leaving the private-use codepoint to an arbitrary
    fallback. **Confirm the mechanism before choosing a fix** -- the fix differs
    depending on whether the fault is the resource or its resolution. The site
    count grew 5 -> 7 (slice 5) -> **9** (slice 9), and no slice introduced the
    defect; each only made it visible in more places. Its own task, cosmetic,
    Windows-only, and never a blocker for the macOS target.
    **Status 2026-10-14 (Docs-2): open in the code.** No style overrides the
    glyph at the nine `CalendarDatePicker` sites; the rendering was not
    re-checked, which needs Windows.
15. **Two `UiMeasure drive` instrument gaps, both specific to this machine and
    this panel** (found 2026-08-09, Phase 4 slice 9). Neither is an application
    defect; both cost real time when hit, so they are recorded with the
    findings. (a) **`savedlg` cannot drive the save dialog here**: the dialog
    exposes its filename box and Save button as `ControlType.Pane`, addressable
    only by `AutomationId` 1001 and 1, which the subcommand does not do.
    (b) **`invoke` uses `FindFirst` and so cannot address the Returns panel's
    WPF buttons unambiguously**, because their names repeat -- "Export MD" x4,
    "Find Conjunctions" x2, "Export Series MD" x2, "Cancel" x12 -- so a
    first-match lookup silently drives the wrong section. The workaround used
    throughout slice 9 was tree ordinal in the WPF head and unique automation
    name in the Avalonia head. Fixing the instrument is worth doing before the
    next panel-scale driving session.
    **Status 2026-10-14 (Docs-2): not checked.** The instrument lives outside
    the repository (`D:\Code\uimeasure`, Section 6), and nothing in the
    repository changed it.
16. **`CalculateElectionalCommand` can be invoked with no timezone, and throws**
    (found 2026-08-10, Phase 5 slice 5a). Its `CanExecute` checks Goal and
    LocationName but **not Timezone**, so an empty timezone leaves the button
    enabled and the operation fails with `TimeZoneNotFoundException`. This is
    **distinct from the slice 4 picker-commit fix**, which addressed a timezone
    the user had chosen failing to reach the model; here no timezone has been
    chosen at all and nothing stops the run. It reproduces under `dotnet run`,
    so it is not a packaging artefact. One predicate to fix, and it belongs with
    the same review of enablement predicates as item 6.
    **Status 2026-10-14 (Docs-2): open.** The `CanExecute` still checks the
    natal chart, Goal and LocationName, and not the time zone.
    **Fixed 2026-10-16 (branch Panels, release 1.4.0):** the `CanExecute` also
    requires a non-blank `Electional.TimeZone`, Natal's rule, and the zone's
    setter requests the command refresh (Use Natal Location sets the place, which
    raises, before the zone). An unknown zone still enables Calculate and keeps
    its red border, by decision.

---

*End of plan. No application code, project files, or tests were modified in
producing this document.*
