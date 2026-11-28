# AstroCast — Astrological Calculation & Chart Analysis Engine

> **For a reader new to the project.** This file is the project's working contract: every rule
> the engine follows, with the reasons, and the record of how each was decided. Commit hashes
> named here that the public repository does not hold, and tags before `v1.7.1`, belong to the
> private development history; the public repository's own history begins with snapshot
> commits. "Steve" is the maintainer, Steve Thornhill; "Maple" is his Windows machine.

Astrological calculation engine and desktop program using the Swiss Ephemeris as its
computational foundation. Implements tropical zodiac, houses in the chart's own
system (Placidus by default), full aspect and midpoint analysis per Reinhold
Ebertin's cosmobiology method.

## Tech Stack

- C# 14, .NET 10 (LTS), Visual Studio 2026
- Two heads on one set of view models (`AstroCast.Presentation`): WPF (Windows) and Avalonia
  (macOS); a console smoke test (`AstroCast.Cli`), which takes no arguments
- Swiss Ephemeris (native C library via P/Invoke)
- SQLite via Microsoft.Data.Sqlite
- Serilog for structured logging
- NodaTime for the IANA time-zone database the app carries (tzdb 2026e)
- System.Text.Json for JSON output
- QuestPDF for PDF generation (Community license)
- xUnit + FluentAssertions for testing
- Reference text: Reinhold Ebertin, "The Combination of Stellar Influences" (COSI)
- Adding or changing a shipped package means a row in `THIRD-PARTY-NOTICES.md`; `ThirdPartyNoticesTests` checks the direct references

## Licence

AstroCast is under the GNU Affero General Public License, version 3 or (at your option) any later
version: AGPL-3.0-or-later (Steve, 2026-10-15; branch Licence, 1.3.5).

- **`LICENSE`** at the root is the GNU's text verbatim (gnu.org's `agpl-3.0.txt`, 661 lines).
  `AstroCast.Core` copies it beside the application, as it copies `THIRD-PARTY-NOTICES.md`; both
  packagers check that it survived publish (`BuildInstaller.ps1`, `build/macos/make-bundle.sh`),
  and `LicenceTests` that it reached the output.
- **Swiss Ephemeris** is used under its AGPL option, which requires the whole project under the
  AGPL or a compatible licence; `THIRD-PARTY-NOTICES.md` records the choice.
- **The About window** in both heads, opened by the status bar's About button, gives the AGPL's
  "Appropriate Legal Notices" (section 5(d)): the version, the copyright, that there is no
  warranty, that the work may be conveyed under the licence, the source, and the licence and the
  third-party notices themselves (`AboutViewModel`). On the Mac the application menu, named
  AstroCast, opens it too: About AstroCast (`AppMenu`, set in `App.Initialize`, where Avalonia's
  macOS menu exporter reads it once; branch MacMenu, 1.3.6).
- **The MSI's licence page** shows `src/AstroCast.Installer/License.rtf`, made from `LICENSE` (one
  RTF paragraph per paragraph) and named by `WixUILicenseRtf` in `Package.wxs`. **The DMG** has no
  licence page; `LICENSE` is in the bundle beside the executable.

**Settled before AstroCast was shared** (the four items once open here):

1. **Svg.Custom 3.2.1 (MS-PL): settled 2026-10-24** (branch SvgExclude; the docs-p1 probe of
   2026-10-24). Mapsui's raster tile layer never touches the SVG path — the map drew tiles, lines
   and markers with every Svg DLL deleted — so both heads carry direct references to `Svg.Custom`,
   `Svg.Model` and `Svg.Skia` 3.2.1 with `ExcludeAssets="runtime"`, and no Svg assembly ships: the
   Avalonia head's Release publish output holds none (checked), and the WPF head's Release build
   output holds none (checked on the Mac with `EnableWindowsTargeting`; Maple's WPF look at the
   landing). The notices rows stay — `ThirdPartyNoticesTests` requires a row per direct reference,
   and `SvgPackages_ExcludeRuntimeAssetsInBothHeads` pins the three references in each head. The
   packages remain named in each head's `deps.json` as dependency-graph entries without a `runtime`
   section: the binary ships no MS-PL code, the metadata still names the package.
2. **The source is reachable at the About window's address.** The public repository takes
   `sthornhill008/AstroCast`, re-created from the cleaned tree at branch Public's landing, with the
   private history renamed `AstroCast-private`, so the About window's source line
   (`https://github.com/sthornhill008/AstroCast`) stays true.
3. **The CARTO basemap key: done (branch NoKey, 2026-10-29).** Public builds ship no CARTO key: the
   packagers never embed one, and a user supplies a free key through `carto-key.txt` (see
   *Base Map*).
4. **The source-only tarball script: removed (branch Review, 2026-11-14).**
   `Package-AstroCast-SourceOnly-TarGz.ps1` took files by extension and left out part of the
   source (52 of the 713 tracked files at Licence); the repository itself is the complete source.

## Documentation

- The guide of record for everything a public reader needs lives in the repository: `README.md`
  (the front page), `CONTRIBUTING.md` (the conventions a change must keep), `docs/LICENSING.md`
  (the licences in plain terms), `docs/USER_GUIDE.md` (the user guide, with its screenshots in
  `docs/images/`), `docs/PROMPT_LIBRARY_GUIDE.md` (the prompt library's guide), `prompts/` (the
  prompt library itself, under CC BY-SA 4.0, with a `README.md` of its own), and
  `docs/BUILDING.md` once it is written. Markdown is the source of record; PDFs are renders.
- A user-visible change updates `docs/USER_GUIDE.md` and the prompt usage map
  (`prompts/USAGE_MAP.md`) in the same branch, and CLAUDE.md where it states the rule.
- A release carries the prompt library on its own as `AstroCast_Prompt_Library_<date>.zip` (the
  `prompts/` folder, the guide and its PDF; `<date>` is the manifest's library release). The zip
  is built again and replaced on the current release whenever a prompt or the guide changes.
- The library's present release is that of 2026-11-28, for AstroCast 1.7.2 (branch Library172):
  thirty prompts, of which fifteen took a new version for 1.7.2's exports and after the review of
  the whole library of 2026-11-27 (P03 v3.13, P04 v2.13, P05 v2.5, P06 v1.15, P07 v1.14, P10 v1.6,
  P11 v1.11, P12 v1.14, P14 v2.16, P15 v2.11, P17 v1.10, P24 v1.7, P27 v1.7, P28 v1.5, P29 v1.1).
  `prompts/MANIFEST.md` is the list of record. What a library release keeps:
  - A prompt that changes is renamed, with its guide and its history, to the new version (`git mv`);
    its history takes a new entry at its head; the manifest's row, the usage map's row and every
    version the Prompt Library Guide names follow. An unchanged prompt is not touched by a byte.
  - A changed prompt's Compatible Export line (P05's Input line; P15's "What changed" block) names
    the AstroCast release whose exports it was written against and says what an older export lacks
    or prints differently; a reading of an older export must still work.
  - Every prompt is submitted whole. P12, P14 and P15 then take one template from their Chart
    Submission Templates section, with the export pasted where the template says.
  - A prompt asks only for what its export prints: nothing is computed outside the export, and a
    medical reading is written only on an operator's request.
  - No private person's name, birth data or question, no machine's name and no machine path under
    `prompts/`: examples use the invented charts (Eleanor Hartwell, Daniel Whitfield) or published
    data.
- The website for non-technical users is `website/`: `website/AstroCast/` is the folder served
  at `https://www.sthornhill.com/AstroCast/` (uploaded by hand), and `website/README.md` says
  how it is built, checked and uploaded. Every statement on it comes from the documents above.
  At each release, run `website/Update-WebsiteRelease.ps1` with the version and the folder
  holding that release's three files, commit, and upload what changed. When a guide changes,
  its PDF under `website/AstroCast/docs/` is replaced under a new versioned name. The sample
  pages are built by `website/tools/Build-Samples.ps1` from `website/samples-src/`; they use the
  invented charts, apart from one horary reading, a real question of the maintainer's own,
  there by his decision, and one pair for a public figure's published birth data (Harry, Duke
  of Sussex): an excerpt of his primary directions report and the rectification reading made
  from it. The site presents the prompt library as an aid to an astrologer, not a replacement
  for one (Steve, 2026-11-17).
- The private hand-over document and the continuity files stay outside the repository.

## Project Structure

```
AstroCast.slnx                      — every project (Windows)
AstroCast.Mac.slnx                  — every project but the WPF head (macOS)
├── src/
│   ├── AstroCast.Core/              — Class library: models, interfaces, calculations
│   │   ├── Configuration/           — Option classes (never bound) and the synastry orbs
│   │   ├── Data/                    — SQLite repository, schema, migrations
│   │   ├── Enums/                   — Zodiac signs, planets, aspects, house systems
│   │   ├── Exceptions/              — Specific exception types
│   │   ├── Formatting/              — Markdown, JSON and PDF formatters; value formatters
│   │   ├── Interfaces/              — IEphemerisService, IChartEngine, etc.
│   │   ├── Interop/                 — Swiss Ephemeris P/Invoke wrapper
│   │   ├── MedicalAstrology/        — Temperament and health correspondences
│   │   ├── Models/                  — ChartInput, ChartResult, PlanetPosition, etc.
│   │   ├── Services/                — Calculation logic, aspect engine, midpoint engine
│   │   └── Utilities/               — Julian Day and span helpers, aspect direction, file names
│   ├── AstroCast.Presentation/      — View models and head-neutral services, for both heads
│   ├── AstroCast/                   — WPF head (Windows): XAML views, converters, WPF services
│   ├── AstroCast.Avalonia/          — Avalonia head (macOS): views, converters, Avalonia services
│   ├── AstroCast.Cli/               — Console smoke test (J2000 NYC, no arguments)
│   └── AstroCast.Installer/         — WiX 6 sources for the Windows MSI
├── tests/
│   ├── AstroCast.Tests/             — xUnit tests of Core and the view models
│   ├── AstroCast.UiTests/           — Avalonia headless UI tests
│   └── AstroCast.TestDoubles/       — View-model factory and stubs for both test projects
├── lib/
│   └── swisseph/                    — Native Swiss Ephemeris binaries and C sources
│       ├── win-x64/                 — swedll64.dll
│       ├── osx-arm64/               — libswe.dylib (build/macos/build-libswe.sh)
│       ├── linux-x64/               — README.md only; no Linux library is built
│       └── src/                     — The library's C sources
├── ephe/                            — Swiss Ephemeris data files (.se1) and sefstars.txt
├── build/macos/                     — macOS bundle, signing, notarization and DMG scripts
├── docs/                            — The two guides, LICENSING.md and images/
│   ├── LICENSING.md                 — The project's licences in plain terms
│   ├── USER_GUIDE.md                — The user guide: install, first chart, every panel
│   ├── PROMPT_LIBRARY_GUIDE.md      — The prompt library's guide: export, choose, submit, read
│   └── images/                      — The user guide's screenshots
├── prompts/                         — The prompt library (CC BY-SA 4.0): thirty prompts by family,
│                                      guides/, history/, helpers/, the manifest and the usage map
├── website/                         — The website for sthornhill.com/AstroCast: the served folder, its sources and tools
├── README.md                        — The front page: what it does, install, first chart, building
├── CONTRIBUTING.md                  — The conventions a change must keep
├── BuildInstaller.ps1               — Windows MSI build
├── CLAUDE.md                        — This file (project contract)
└── CLAUDE.local.md                  — Personal preferences (gitignored)
```

## Summary Reports

Reports and scratch work are saved **outside the project tree** to keep the repo clean:

- **Mac:** a folder per branch under `/Users/stevenlthornhill/astrocast-mac-setup/`, the
  report in it and its logs under `logs/`.
- **Maple:** `D:\Code\<branch>-maple-report.md`, with scratch in `D:\Code\<branch>-scratch\`;
  never inside `D:\Code\AstroCast`, where an untracked file stops a landing.

Every branch prompt names its report's path.

## Build & Test Commands

The root holds two solution files, so a bare `dotnet build` stops with MSB1011: name one.

| Command                                           | Purpose                        |
|---------------------------------------------------|--------------------------------|
| `dotnet build AstroCast.slnx`                     | Build every project (Windows)  |
| `dotnet build AstroCast.Mac.slnx`                 | Build all but the WPF head (macOS) |
| `dotnet test tests/AstroCast.Tests`               | Core and view-model tests      |
| `dotnet test tests/AstroCast.UiTests`             | Avalonia headless UI tests     |
| `dotnet test tests/AstroCast.Tests --filter Category=Unit` | Unit tests only       |
| `dotnet test tests/AstroCast.Tests --filter Category=Integration` | Integration tests only |
| `dotnet run --project src/AstroCast.Cli`          | Smoke test: casts J2000 NYC (no arguments), saves it to a database beside the executable and writes its JSON, Markdown and PDF there |
| `dotnet run --project src/AstroCast`              | Run the WPF head (Windows)     |
| `dotnet run --project src/AstroCast.Avalonia`     | Run the Avalonia head          |
| `dotnet publish src/AstroCast -c Release -r win-x64 --self-contained true -p:Platform=x64` | Publish the WPF head for Windows, as `BuildInstaller.ps1` does |
| `BuildInstaller.ps1`                              | The Windows MSI (see *Versioning*) |
| `build/macos/make-bundle.sh`, then `build/macos/package-macos.sh` | The macOS app bundle, then the signed, notarized DMG (`build/macos/SIGNING.md`) |

## Code Standards

- XML documentation comments (`/// <summary>`) on ALL public types and members
- Include `<param>`, `<returns>`, `<exception>`, `<remarks>` tags where applicable
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- File-scoped namespaces
- Primary constructors where appropriate (C# 14)
- Field-backed properties using `field` keyword where appropriate (C# 14)
- Extension blocks for extension members where appropriate (C# 14)
- Explicit access modifiers on all members (no implicit `private`)
- `ILogger<T>` via Serilog for all logging — no `Console.Write` in library code
- `async/await` for all I/O operations (database, file system)
- Explicit error handling with specific exception types
- Cross-platform file paths using `Path.Combine()` — no hardcoded separators
- **Core writes culture-independent text.** Every number, date and time Core
  formats into an export or into a string it stores on a result goes through
  `CultureInfo.InvariantCulture`, integers included (sv-SE writes a negative with
  U+2212): one `Inv` field per formatter class, as `AstrocartographyFormatter` has
  (services, models and the electional formatter write
  `CultureInfo.InvariantCulture` inline), then `sb.AppendLine(Inv, $"…")`,
  `string.Create(Inv, $"…")` or `x.ToString(format, Inv)`, one idiom per file. So
  every export reads as it does under en-US whatever the machine's regional
  settings — a decimal point, English day and month names, the Gregorian calendar
  (th-TH would date 2026 as 2569) and "25%" (the invariant `P0` writes "25 %"; the
  electional weights use an invariant format whose percent pattern is "n%"). Never
  set the thread's culture, in Core, the heads or the CLI: the screen may follow
  the user's settings. Every golden, and every unit test whose text a culture once
  changed, also runs under de-DE, sv-SE and th-TH inside a `CultureScope`
  (`tests/AstroCast.Tests/Fixtures`), against the same golden file or en-US
  expectation.
- **An export carries the system's line ending throughout** (branch ExportText, 1.7.2;
  Steve's ruling): CR LF on Windows, LF on macOS. Every line is ended by the builder's
  own `AppendLine`: no line feed inside a string's own text, and an embedded chart's
  lines are joined with `AppendLine`, never `Append('\n')`. A `**Label:** value` line
  that another follows ends with two spaces, Markdown's hard break. The golden
  comparison normalises line endings, so `ExportLineEndingTests` holds both rules for
  every export the golden tests make (see *Export Text*).

## Architecture Rules

- **Core dependencies:** Swiss Ephemeris interop, Microsoft.Data.Sqlite, QuestPDF, Serilog, NodaTime
- AstroCast.Presentation, both heads (AstroCast, AstroCast.Avalonia) and AstroCast.Cli depend
  on AstroCast.Core, never the reverse; both heads also depend on AstroCast.Presentation, which
  depends on no head
- No static mutable state or singletons — use dependency injection
- All external services behind interfaces (IEphemerisService, IChartRepository, etc.)
- No `IOptions<T>`: the option classes in `Core/Configuration` are never bound, and a service
  takes its settings as constructor arguments (`EphemerisService` its ephemeris path)
- Planetary positions are always computed on demand from Julian Day + coordinates, never cached in the database
- All angular values stored internally as `double` (decimal degrees, 0–360)
- Display formatting (DD°MM′SS″ Sign) is a presentation concern, not a model concern
- Services may call the **value** formatters in `Core/Formatting` — `PositionFormatter`
  for positions and aspects, `ChartTimeFormatter.FormatLocalTime` for an instant on a
  chart's own clock — but never a **document** formatter such as `ChartMarkdownFormatter`.
  `FormatLocalTime` moved out of `ChartMarkdownFormatter` in 1.3.2 for exactly this:
  the horary analysis had been reaching into a document formatter for it.

## Swiss Ephemeris Conventions

- P/Invoke wrapper in `AstroCast.Core/Interop/SwissEphInterop.cs`
- Always call `swe_set_ephe_path()` before any calculation
- Always call `swe_close()` on disposal (implement `IDisposable`)
- Use `SE_FLG_SPEED` flag to get daily speed in longitude
- Use `SE_FLG_SWIEPH` for Swiss Ephemeris mode (requires .se1 files)
- Tropical zodiac is the default — do not set sidereal flags
- House system: pass the chart's system to `swe_houses()` (`'P'` for Placidus, the default; `HouseSystem`)
- True Node (not Mean Node): use `SE_TRUE_NODE`
- Body constants: SE_SUN=0, SE_MOON=1, SE_MERCURY=2, ..., SE_PLUTO=9, SE_TRUE_NODE=11, SE_CHIRON=15
- **The ephemeris path on Windows** (branch EphePath, 1.7.2; finding N4 of the 1.7.2 code audit, Steve's
  ruling 5: both parts in 1.7.2). The library takes its folder as a C string: the eleven imports of
  `SwissEphInterop.cs` are `CharSet.Ansi`, so on Windows the path is written in the process's ANSI code page,
  and the library (`swedll64.dll`, its C runtime linked in) reads it, and opens its files, in the same one.
  The folder is the program's own plus `ephe`, so it holds the Windows profile's name or the folder the user
  chose. Until 1.7.2 the process's code page was the system's: a letter it cannot hold was replaced on the
  way, the library looked in a folder that does not exist, and every chart failed at Chiron while the Sun to
  Pluto still answered from the library's built-in model with a log warning only. Measured on Maple (code
  page 1252) with `master`'s published CLI copied into five folders: `Jose Muller` and `José Müller` read
  Chiron; `Łukasz` reached the library as `Lukasz`, `Ελένη` as `?????` and `太郎` as `??`, and each run ended
  with an unhandled exception. macOS is not affected (the marshalling there is UTF-8).
  - *The method: UTF-8 as the process's code page, by manifest.* The WPF head and the CLI each carry an
    application manifest (`src/AstroCast/app.manifest`, `src/AstroCast.Cli/app.manifest`, named by
    `<ApplicationManifest>` in each csproj) whose `activeCodePage` is `UTF-8`. Windows 10 version 1903 and
    later then give the process UTF-8 as its ANSI code page: the marshaller writes the path in UTF-8, the
    library reads it back whole, and nothing in the interop changed, so a path that survived before reaches
    the same folder as before. With it the CLI reads Chiron (11°37′03″ Sagittarius) from all five folders.
    The folder's short (8.3) path was weighed and not taken: short names are a setting of each volume (on
    for `C:` and `D:` on Maple), a folder made while they are off has none, and it needs new interop code.
  - *What must not be undone.* Each manifest's `activeCodePage` entry: removing it brings the fault back.
    Each manifest's `trustInfo` block (`asInvoker`, no UI access): it is the whole of the default manifest
    the build embedded until 1.7.2, repeated because a manifest of the project's own replaces the default.
    `CharSet.Ansi` on the imports: it is what becomes UTF-8. And no display-scaling (DPI) setting is added
    to either manifest: WPF sets its own, and changing it is finding N25, for 1.8.0. The Avalonia head's own
    `app.manifest` holds its DPI settings and was not changed; that head is not shipped on Windows.
  - *What the manifest does not reach:* a process that is not one of the two programs (the test host,
    `dotnet AstroCast.Cli.dll`, PowerShell loading the Core DLL, the Avalonia head run on Windows, a later
    MCP server until it has the entry too) and a Windows before 10 version 1903. There the fault is as it
    was, and the start-up check below says so in plain words.
  - *Nothing else depends on the code page:* the eleven imports are the only `CharSet.Ansi` in `src`; the
    CLI sets `Console.OutputEncoding` to UTF-8 itself; SQLite, Serilog, QuestPDF with Skia, and NodaTime
    take paths and text as Unicode or UTF-8. Microsoft's documentation of `activeCodePage` notes that GDI
    does not honour it per process; the heads draw their text through WPF and Skia, which take Unicode.
- **The start-up check of the ephemeris files** (`EphemerisFilesCheck`, `Core/Services`; branch EphePath,
  1.7.2; finding N4, part 2). Directly after it makes its `EphemerisService`, each program calls
  `EphemerisFilesCheck.Run(ephemeris, folder, logger)`: it reads Chiron's position at J2000.0
  (JD 2451545.0) by `CalculatePosition` and discards it. Chiron, because it has no built-in model: a
  position of the Sun is answered without the files. It catches `EphemerisCalculationException` only,
  changes no chart and keeps nothing; in the sandbox's two starts of the Windows head it took 12 ms and
  11 ms.
  - *The log:* `Ephemeris files check passed for {folder} in {n} ms` (Information), or `Ephemeris files
    check failed for {folder}` with the exception (Error); the folder is its full path.
  - *The message,* returned when the read fails and built in the one place
    (`EphemerisFilesCheck.FailureMessage`): `AstroCast cannot read its ephemeris files in {folder}. Charts
    cannot be calculated until this is fixed. ` and then the cause with its remedy. When the folder's path
    does not survive the process's code page (`SurvivesTheCodePage`: the path to the bytes the library is
    handed and back, `Marshal.StringToHGlobalAnsi` and `PtrToStringAnsi`, compared ordinally): `The
    folder's name has letters outside your Windows language's character set: install AstroCast in a folder
    with a plain name.` Otherwise: `The files may be missing or damaged: install AstroCast again.` It names
    the folder and no file.
  - *How each program shows it,* as it shows its other start-up problems, and then stops: the WPF head a
    message box titled `AstroCast — Startup Error`, then `Shutdown(1)`; the Avalonia head standard error and
    its start-up `MessageWindow` (see *Head Parity*), exit code 1; the CLI `ERROR: {message}` and exit code
    1. When the check passes the CLI prints `Ephemeris files read from: {folder}`.
  - *Tests.* `WindowsManifestTests` (6 names: each of the two projects names its manifest, sets UTF-8 and
    keeps `asInvoker`, read from the source) and `EphemerisFilesCheckTests` (8 names, in the "Ephemeris"
    collection: the suite's folder passes and is logged; a folder with no files fails with the files
    message; on Windows a copy of the suite's files in a folder named with Polish, Greek and Japanese
    letters fails in the test host with the name message, or passes where the host's code page is UTF-8; the
    two messages; all three programs call the one check after they make the service). No test can show
    Chiron read from such a folder: the test host has no manifest. That is shown on the built programs: the
    CLI from the five folders, and the installed Windows head in Windows Sandbox from
    `C:\Łukasz Ελένη 太郎\AstroCast\` (system code page 1252), whose log reads `Ephemeris files check passed
    for C:\Łukasz Ελένη 太郎\AstroCast\ephe in 12 ms`. The released 1.7.1 starts from the same folder and
    logs nothing of the fault.
  - *A test that points the library elsewhere puts it back.* The library keeps one ephemeris path for the
    whole process and is closed once, by the fixture. A test that makes an `EphemerisService` over another
    folder is in the "Ephemeris" collection, disposes nothing, and ends by making one over the suite's own
    `ephe` folder, then reads Chiron through the shared fixture.

## Astrological Calculation Rules

- **Zodiac:** Tropical only
- **House system:** the chart's own, one of nine (`HouseSystem`): Placidus by default, Koch,
  Equal, Whole Sign, Regiomontanus, Campanus, Alcabitius, Porphyry, Morinus. Placidus and Koch
  have no houses inside the polar circles, and the engine refuses there.
- **Houses, day or night, hayz** (branch F6, 1.3.3; rulings D2 with S-D1, D3; Steve's decisions
  of 2026-10-11):
  - *House:* a body's house is the arc its longitude lies in, from cusp i to cusp i + 1 in the
    direction the cusps run, cusp i included and cusp i + 1 excluded (`HousePlacement`): natal
    charts, composites and overlays alike. The cusps run backward for Regiomontanus and Campanus
    inside the polar circles, where the ecliptic's upper-meridian point is below the horizon.
    `HouseExact` is the house plus the fraction of its arc. Until 1.3.3 a natal body was placed by
    `swe_house_pos` with its latitude: 5.70% of Placidus placements and 13.08% of Koch's differed
    from the longitude's house, and Koch's failure value printed as House 0.
  - *Day or night:* the Sun's longitude on the half of the ecliptic above the horizon, the arc
    from the Descendant to the Ascendant, or the other arc where the upper-meridian point is below
    the horizon (S′), in every house system (`ChartSect.IsDayChart`). Until 1.3.3 a day chart had
    the Sun in houses 7 to 12, wrong in 8.34% of Whole Sign and 10.86% of Morinus charts.
  - *Hayz:* "above the horizon" is the body's geometric altitude (`PlanetPosition.Altitude`, not
    printed); a composite, which has no sky, goes by its longitude. Lilly's rule otherwise (D9).
    Until 1.3.3 it was the house number: wrong for bodies with latitude in most systems, and in
    Whole Sign and Morinus for about one placement in ten of every body.
- **Mercury's sect** (D8, `SectService`): by longitude, a morning star (behind the Sun in
  zodiacal order, the forward arc from the Sun more than 180°) is diurnal and an evening star
  nocturnal; a forward arc of exactly 0° is nocturnal.
- **Bodies:** Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, True Node, Chiron
- **Angles:** Ascendant, Midheaven (MC), Descendant, IC, house cusps 1–12
- **Minor points (asteroids and Lilith)** (`CelestialBodyId`, `EphemerisService`, `ChartInput`, `ChartResult`,
  `IAspectService`, `AspectService`, `ChartEngine`, `ChartMapper`, the three return services; branch Asteroids,
  1.6.0; the plan's R1 to R5 and R9 with scoping decisions 1 and 2, all approved by Steve on 2026-10-19): Ceres,
  Pallas, Juno, Vesta and Black Moon Lilith, the mean lunar apogee, as minor modern points behind one switch,
  the engine only in this branch; the exports, the timing techniques and the Points row are branches
  AsteroidsExport, AsteroidsTiming and AsteroidsPanel. *Points* (1): `CelestialBodyId.MeanApogee` = 12
  (SE_MEAN_APOG), `Ceres` = 17, `Pallas` = 18, `Juno` = 19, `Vesta` = 20, each computed by the existing
  `IEphemerisService.CalculatePosition`, both of its calls (ecliptic and equatorial); named by the library for
  the four and "Lilith" for the mean apogee (the library says "mean Apogee"), normalised in `GetPlanetName`
  beside the True Node's line; the order everywhere Ceres, Pallas, Juno, Vesta, Lilith; `AllBodies` stays the
  twelve and the interface gains no member. *The switch* (2): `ChartInput.IncludeMinorPoints`, `init`, default
  false, a session value never saved: `ChartMapper.ToEntity` does not map it (the row has no column) and
  `ChartMapper.ToInput(entity, includeMinorPoints = false)` takes the caller's value, never the row's. *Where
  they live* (3): `ChartResult.MinorPoints` and `ChartResult.MinorPointAspects`, `init`, default empty, not
  required; `ChartEngine.Cast` fills them only when the switch is on and the EventType is not "Horary"
  (compared ignoring case, as the chart export reads it), directly after the altitudes: the five
  `CalculatePosition` calls in that order, `HousePlacement.Assign` on their list against the chart's houses,
  then the aspects, both handed to `BuildResult`. `Positions` stays twelve, the five take no altitude, and
  nothing else in `Cast` sees them (no midpoints, stars, declination aspects, dignities, conditions, Lots,
  antiscia, stations or parans); `CalculateForElectional` and `CalculateElectionalScoringInputs` never carry
  them. *Rows* (4): the house by `HousePlacement.Assign`, the direction by `IsRetrograde` (the mean Lilith is
  never retrograde), out of bounds by `PositionFormatter.IsOutOfBounds(position, result.Obliquity)`; no new
  member for any of the three. *Aspects* (5): `IAspectService.CalculateMinorPointAspects(minorPoints,
  positions, houses)`, from each point to the positions whose `Body` is the Sun to Pluto (values 0 to 9; never
  the True Node, Chiron, another of the five or the Part of Fortune, which is a Lot and never a position), then
  "ASC" and "MC" held still at speed 0; the definitions of `AspectDefinition.All` with `Category == Major`, in
  that order, each at an orb of 2.0 inclusive, the first match winning; applying by `AspectDirection.IsApplying`
  with the point's speed and the target's; `Body1Name` the point, `Body2Name` the target; sorted by orb.
  `FindAspect` has a private overload taking the definitions and an orb selector, the chart's own aspects
  passing every definition at its own orb, unchanged. *Recasts* (6): `CalculateRelocated` copies the switch
  into the relocated input, and `SolarReturnService`, `LunarReturnService` and `PlanetaryReturnService` set it
  from the natal input on their return inputs, so each recast chart carries its own five. With the switch off
  every output is as before (R9): no golden changed. Tier B (`ast_truth.py`, pyswisseph 2.10.03 over the
  shipped files, the rules from the plan): fifteen files under `tests/AstroCast.Tests/Services/MinorPoints/TierB/`,
  seven fixtures (J2000 NYC, J2000 NYC at Los Angeles as the relocation, Secondary, chart 55, M35, N1, Tromsø in
  Equal houses), the five's rows and their aspects (5, 4, 3, 4, 7, 6, 6), every longitude, latitude, right
  ascension, declination and speed within 5e-10 and every orb within 5e-10, the lists' printing precision, against
  tolerances of 1e-6; Tier A, Lilith against Meeus ch. 47 with the reduction to the ecliptic, within 10″ in
  longitude and 1″ in latitude (N1, in 1300, 4.66″ and 0.14″; the rest under 1″ and 0.1″). J2000 NYC holds
  Pallas retrograde and out of bounds (declination −29.707370°); chart 55's Vesta, N1's Ceres and Juno and
  Tromsø's Juno are retrograde; Tromsø's Lilith is out of bounds. `CelestialBodyIdTests` (1),
  `EphemerisServiceTests.CalculatePosition_MinorPoint_IsNamed` (5), `MinorPointTierBTests` (12),
  `MinorPointTierATests` (6), `ChartEngineTests` (5), `AspectServiceTests` (4), the three return services'
  `Calculate_SwitchOn_ReturnChartCarriesItsOwnFive` (3) and `ChartMapperTests` (2) hold it: 38 names.
  *The exports* (branch AsteroidsExport, 1.6.0; R6, R8 and R9 with scoping decisions 3, 4 and 8, approved by
  Steve on 2026-10-19): with the switch on the chart Markdown prints `## Asteroids and Lilith` directly after
  `### Out-of-Bounds Planets` and before `## Essential Dignities`, one writer,
  `ChartMarkdownFormatter.AppendMinorPoints`, behind `ToMarkdown`'s sixth parameter `minorPoints` (default
  true): the opening line `Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee):
  minor modern points, read as Chiron is. Their aspects are the five Ptolemaic aspects at a 2°00′ orb, to the
  Sun to Pluto, the Ascendant and the Midheaven only.`, then `### Positions` with
  `| Body | Position | Lat | RA | Dec | House | Speed | Dir | OOB |` (the positions table's cells and the
  right ascension, one row per point in R1's order) and `### Aspects (2°00′ orb)` with
  `| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |`, one row per aspect in the chart's order, the row's own orb
  through `FormatOrb` (never `Aspect.Orb`, the definition's 8, 7 or 5; the limit is `AspectService.MinorPointOrb`),
  or the line `No aspects within 2°00′.`; a composite prints the composite form, no RA, Dec, OOB or Ap/Sp
  cell. The chart JSON carries `minorPoints` directly after `positions`, omitted when the chart has none,
  else one element per point with a `positions` element's keys plus `isOutOfBounds` and `aspects`
  (`{ body, aspect, angle, orb, isApplying }`, in `MinorPointAspects` order); `metadata` is unchanged. The
  PDF draws the section after the positions table (`ComposeMinorPointsSection`: the title
  `ASTEROIDS AND LILITH`, the opening line in plain text, the two tables as the chart's own are drawn, the
  sub-headings bold 9 pt). *Synastry* (`SynastryResult.MinorPointAspects`, default empty, filled by
  `SynastryService` only when both charts carry the five): A's five to B's Sun to Pluto, "B ASC" and "B MC",
  then B's five to A's, names prefixed as the inter-chart rows, the majors of `AspectDefinition.All` in order
  at 2.0° inclusive, first match, never through `SynastryOrbs`, the note "exact" below 10′, sorted by orb
  (both inter-chart lists by `SynastryService.CompareByOrbThenNames`: a tied orb orders by `Body1Name`
  then `Body2Name`, ordinal, then the angle, branch TiesAndGuards, 1.6.1; until then a tie's order was
  the sort's own, the same on every run of one build but undefined by the code);
  printed as `## Inter-Chart Aspects: Asteroids and Lilith` directly after the Inter-Chart Aspects section
  and its rule, with the same spacing and its own rule after (the opening line, the inter-chart columns, or
  the none line); no overlays for the five (decision 8). *Composite* (`CompositeService.ComputeCompositeMinorPoints`):
  empty unless both charts carry the five, else per point the short-arc midpoint of the two longitudes (no
  Sun-side rule, which is Mercury's and Venus's), latitude, distance and the three speeds the means, RA and
  declination null, `HousePlacement.Assign` against the composite houses, the aspects by
  `CalculateMinorPointAspects` with the composite positions; both lists on the composite chart and
  `IncludeMinorPoints` on its input, so the export prints the composite form. Relocation, the three returns,
  synastry, composite, transits and the timeline carry the section through their embedded charts; the
  primary-direction, medical, electional, ingress, lunation and great-conjunction exports pass
  `minorPoints: false` (R8), and a horary chart never has the five. With the switch off every export is
  byte-identical (R9): none of the 27 existing goldens changed. Eight new goldens, each the existing one's
  name with `_MinorPoints` before `.golden` (the chart Markdown and JSON, relocation, the three returns,
  synastry, composite), every row of the five in them checked against Tier B (`ast_truth_export.py`:
  `synastry_j2000_secondary.tsv`, 10 rows, `composite_j2000_secondary_points.tsv`, 5, and
  `composite_j2000_secondary_aspects.tsv`, 4, under `TierB/`; the returns against pyswisseph at each golden's
  Return Date/Time instant). 60 names: the eight goldens with their three cultures (32); eight R8 facts
  holding the map, daily, two electional, medical, two primary-direction and return-ingress goldens
  byte-identical with the natal switched on; `ChartMarkdownFormatterTests` (6), `ChartJsonFormatterTests`
  (3), `ChartPdfFormatterTests` (2), `SynastryServiceTests` (3), `SynastryMarkdownFormatterTests` (1),
  `CompositeServiceTests` (4) and `CompositeMarkdownFormatterTests` (1).
  *The timing techniques* (branch AsteroidsTiming, 1.6.0; R7 with scoping decisions 5, 6 and 7, approved by Steve
  on 2026-10-19): the natal five are targets of the transits, the timeline, the progressions and the solar arcs,
  each in a section of its own, and never move: no transiting, progressed or directed asteroid or Lilith. *Transits:*
  only when the natal chart carries the five, `TransitService.BuildMinorPointTargets` makes one `NatalTarget` per
  `ChartResult.MinorPoints` entry, in R1's order, with the explicit symbols CER, PAL, JUN, VES, LIL
  (`MinorPointSymbolMap`; the planets' two-letter fallback would give Juno and Vesta JU and VE); the bodies are the
  report's active bodies, `StandardTransitBodies` and Mars only when `TransitMarsEvaluator.ShouldIncludeMars`,
  decided from the main natal targets alone, admits it; each body goes through `FindActiveTransits` over the five's
  targets with `includeMinorAspects: false`, the five Ptolemaic aspects at the body's own major orbs whatever the
  report's Aspects setting, a Mars row kept by `KeepMarsTransitsWhereSlowInOrb` as the chart's own are, the list
  sorted as the chart's own (`SortByBodyThenTarget`) into `TransitResult.MinorPointTransits` (default empty); no
  house overlay, midpoint activation, declination row or transit-to-transit row for the five (R8). J2000 NYC
  2026-04-12 to 18 lists five (Pluto trine Ceres and sextile Vesta, Saturn square Juno and trine Vesta, the node
  sextile Juno) and April 2026 seven (Neptune and Saturn opposite Ceres too); Mars, at about 0.7° a day over the
  five in April 2026, is not in either report, so its rows fall at `ShouldIncludeMars`. *The transit report*
  (`TransitMarkdownFormatter`): `## Transits to the Asteroids and Lilith` directly after the Active Transits section
  and before the house overlays, only when the natal chart carries the five: the opening line `Transits to the
  natal Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee): the five Ptolemaic aspects
  at each transiting body's major orbs.`, then one block per transit exactly as the Active Transits print (one
  writer, `AppendTransitBlock`) without the two Medical lines, or the line `No transit to the asteroids or Lilith
  is active during this period.`, and the section's own rule. *The timeline* (`TransitTimelineMarkdownFormatter`):
  `## Transit Windows: Asteroids and Lilith` directly after the Transit Windows section and `## Events: Asteroids
  and Lilith` directly after the Events section, each only when the natal chart carries the five, by the same
  writers and extractors over `MinorPointTransits` (the windows section prints nothing when there is none, as its
  twin does; the events section its twin's `No transit events during this period.`); the Clusters read the chart's
  own events only (decision 5). *Progressions and solar arcs:* `ProgressionResult.MinorPointAspects` and
  `SolarArcResult.MinorPointAspects` (default empty), the progressed points (the Sun to Mars, ASC, MC) and the
  directed points (the twelve bodies, ASC, MC) to the natal five with each technique's own aspects, orbs and
  direction rule (each service's `CalculateAspects` takes its target list); the dated hits by overloads of
  `ProgressedHitFinder.FindHits` and `SolarArcHitFinder.FindHits` taking the target list (the solar arc one seeks
  no midpoint contact), which `TransitService` calls a second time into `TransitResult.MinorPointProgressedHits`
  and `MinorPointSolarArcHits` (default empty). The report prints, each only when the natal chart carries the five
  and each as its twin prints (the same columns, order and empty text): `### Progressed-to-Natal Aspects: Asteroids
  and Lilith (major, 1° orb, at report start)` after the Progressed-to-Natal Aspects table, `### Progressed Hits to
  the Asteroids and Lilith` after the Progressed Hits table, `### Solar-Arc-to-Natal Aspects: Asteroids and Lilith
  (majors and the 45° series, 1° orb, at report start)` after the Solar-Arc-to-Natal Aspects table and `### Solar
  Arc Hits to the Asteroids and Lilith` after the Solar Arc Hits table; the Techniques and Aspects header lines are
  unchanged. With the switch off every output is byte-identical (R9): none of the 35 existing goldens changed. Three
  new goldens, each the existing one's name with `_MinorPoints` before `.golden.md` (the transit report of
  2026-04-12 to 18, the solar arc and progressions report of 2026, the timeline of April 2026), from the same
  fixtures with the switch on, every row of the five in them checked against Tier B (`timing_rows_check.py`).
  Tier B (`ast_truth_timing.py`, pyswisseph 2.10.03 over the shipped files; the report start the day's 12:00 UT as
  the scan samples it, the progressions at the day's noon with 365.25 days a year, a station listed only in orb
  at its instant, as the engine's rules read): `timing_transits.tsv` and `timing_prog.tsv` under `TierB/`, J2000
  NYC's natal five as targets over the transit golden's period and the timeline's (entry, exit and stations to the
  minute, passes to the second) and the progressions and solar arcs at 2026-01-01 and 2026-04-12 with the 2026
  hits (P Moon sextile Juno, P Venus square Ceres; SA Pluto conjunct Juno, SA Venus sesquiquadrate Pallas, the last
  0.002° inside the orb on 2026-01-01). 34 names: the three goldens with their three cultures (12),
  `TransitServiceTests` (7), `TransitMarkdownFormatterTests` (5), `TransitTimelineMarkdownFormatterTests` (2),
  `ProgressionServiceTests` (3), `SolarArcServiceTests` (3), `ProgressedHitFinderTests` and
  `SolarArcHitFinderTests` (1 each).
  *The Points row* (branch AsteroidsPanel, 1.6.0; R2 on screen, topic 8 and Steve's toggle choice of 2026-10-20):
  the session switch on screen in both heads. *The row* (1): in the Chart panel's Houses grid under System, a
  second row labelled "Points" with a CheckBox "Include asteroids and Lilith", `Margin="0,4"`, bound to
  `ChartEntry.IncludeMinorPoints`, its tooltip `Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the mean apogee)
  in every report that carries them this session; not saved with the chart.`, the Avalonia automation name
  `Include asteroids and Lilith` (WPF none, as its siblings); the label column keeps its width (100, WPF 80).
  *The view model* (2): `ChartEntryViewModel.IncludeMinorPoints`, false at every launch, never saved, `SetProperty`
  with no command refresh, left by `Clear` and `PopulateFrom` (Clear Form and Load keep it). *Core* (3):
  `ChartInput.WithIncludeMinorPoints(bool)`, a new input with every property copied and the switch set (the class
  is sealed with init-only properties, so no `with`); a property added to `ChartInput` must be added there, which
  `ChartInputTests.ChartInput_HasTheEighteenPropertiesTheCopyKnows` enforces. *Every input* (4) `MainViewModel`
  builds or maps carries the box's value: Calculate & Save, Load, synastry's and the composite's Person B, the
  electional natal from the list and from the form (which becomes the Chart panel's chart), and the relocation's
  picked chart (`ChartMapper.ToInput(entity, ChartEntry.IncludeMinorPoints)` at the five mapped sites). *The
  recast on toggle* (5, `RecastForMinorPoints`, from `OnChartEntryPropertyChanged`): every export prints a result
  as it was cast and Calculate re-saves the chart, so ticking or unticking the box recasts the Chart panel's chart
  at once, `LastResult = _chartEngine.Calculate(LastResult.Input.WithIncludeMinorPoints(value))`, synchronously as
  Calculate & Save casts and never saved (nothing reaches the repository, `HasResult` unchanged, the `LastResult`
  setter's relocation refresh as for any chart); the other panels take the new chart at their next Calculate.
  Nothing happens without a chart, or when the chart already agrees with the box. The status line, `{name}` the
  chart's: `Points on: {name} now carries the asteroids and Lilith; recalculate other panels to include them.` or
  `Points off: {name} no longer carries the asteroids and Lilith; recalculate other panels to leave them out.`; a
  horary chart is recast all the same, with `Points on: a horary chart never carries the asteroids or Lilith.` (or
  `Points off: ...`); an engine exception leaves `LastResult` unchanged and puts the box back to the chart's
  value, with `Points error: {message}; the box is back to {off|on}.` and a log line (the box's write re-enters
  `RecastForMinorPoints`, which returns at its early exit; 1.6.1, item E, branch ElectionalPicker). An operation
  already running keeps the chart it started with, except the electional search's form fallback, which runs only
  while `LastResult` is null: when its cast lands it sets `LastResult`, calls `RecastForMinorPoints` and scores
  `LastResult`, so a box moved during the cast recasts the landed chart at once and the chart and the box agree
  (1.6.1, item D). Until 1.6.1 the box kept its value after an error, and the fallback's chart kept the value the
  box had when the cast began, so each could disagree with the box until the next tick or Calculate.
  `RecordingChartEngine` (`tests/AstroCast.TestDoubles`) records every input given to `Calculate`, throws on
  demand, and since 1.6.1 probes the view model inside a cast (`Probe`, `ProbedDuringCalculate`), signals
  `Entered` and holds until cancelled (`HoldUntilCancelled`), the one engine double for every view-model test.
  19 names: `ChartInputTests` (2), `ChartEntryViewModelTests` (3),
  `MainViewModelTests` (12) and the UI `ChartPanelLayoutTests` (2; `FormLabels_ReadTopToBottomInTheEntryOrder`
  rewritten and `ExpectedChartLabelCount` 21, the scoping's table having counted that test green).
- **Lots of Necessity and Courage** (L1, Paulus Alexandrinus ch. 23; `ArabicPartCatalog`): by day
  Asc + Fortune − Mercury and Asc + Fortune − Mars, reversed by night (Asc + Mercury − Fortune,
  Asc + Mars − Fortune), as every Hermetic Lot reverses.
- **Retrograde:** Determined by negative daily speed in longitude
- **Years:** an instant from 1200-01-01 04:54:46 UT (Julian calendar) to
  2399-12-31 23:51:20 UT, the span the shipped ephemeris files cover; the engine
  refuses anything outside it (see *Time Inputs* below)
- **Calendar:** decided on the chart's local date — Gregorian from 1582-10-15,
  Julian to 1582-10-04; 1582-10-05 to 1582-10-14 never occurred and are refused;
  every printed instant before 1582-10-15 is on the Julian calendar
  (`CalendarDateFormatter`, branch R2, 1.3.4)
- **Event charts** (branch R1, 1.3.3): every chart cast from an event — the returns,
  the ingress chart and cardinal set, lunation and great-conjunction charts, electional
  candidates — is cast at the event's instant (`IChartEngine.CalculateAtInstant`; Julian
  Day and instant convert by arithmetic, `JulianDayUtility.JulianDayToInstant` and
  `InstantToJulianDay`), its `UtcDateTime` the instant (D4); the cycle timeline and the
  transit longitudes read the Julian Day in hand. Event labels before 1582-10-15 print
  on the Julian calendar since R2 (1.3.4, `CalendarDateFormatter`). Until 1.3.3 the label
  was read back as a Julian date, so such a chart landed 7 to 10 days late, or was
  refused in 1582-10-05…14.
- **Pre-1583 labels** (`CalendarDateFormatter`, branch R2, 1.3.4; rulings R-D1 to R-D6; R-D7,
  the ranges across October 1582, landed in branch R3, below): a `DateTime` always denotes an instant,
  proleptic ticks; a calendar applies only when a typed reading is read (D21) and when an
  instant is printed. One formatter prints every instant (R1): before 1582-10-15 the year,
  month and day are the Julian calendar's, the hours, minutes and seconds the clock's, the
  weekday the clock's `DayOfWeek` (the true weekday on either calendar), English names and
  invariant digits under every culture; `Date`, `DateTime`, `Minutes`, `Iso` (the round-trip
  text the JSON and the database hold), `IsJulian`, `Reading` (the clock on the calendar in
  force as the one label-valued `DateTime`, for `ChartInput.LocalDateTime`, refusing a Julian
  leap day) and `Format(DateOnly, …)` for a date derived from an instant. Every chart's
  `UtcDateTime` is its instant (R2, R-D4): the relocated chart is the natal input with only the
  place changed, so it resolves to the natal Julian Day (branch K2); the progressed instant is the natal
  instant plus the age in days, the age from the natal Julian Day to the midpoint day's noon
  (`ProgressedDateCalculator.Calculate(natalJd, day)`); the JSON's
  `utcDateTime`, `stationTimeUtc` (Docs-2) and the planetary hour's `hourStartUtc` and
  `hourEndUtc`, and the database column, keep the Julian label, and the JSON adds
  `"calendar": "Julian"` for a pre-1583 chart (R-D3). Every printed instant goes through it
  (R3): a chart's UTC time, every event's instant, a local clock read from an instant
  (`ChartTimeFormatter.FormatLocalTime`, the horary times), the progressed dates, a `DateOnly`
  derived from an instant, the strings the services build (refusals, chart names) and the
  CLI's and electional status texts; a typed reading prints as typed (`ChartInput.LocalDateTime`
  on a zone, picked bounds and periods, file names, status texts of now, log stamps); and since
  branch H the time-lord dates, proleptic days counted from the birth day, print through it as
  well (R-D6). The daily heading is the day's noon instant on the calendar in force, so its weekday
  is the instant's (Julian 1301-03-07 is a Tuesday). A chart at an explicit offset prints its
  Date/Time line (the PDF's Date) from its instant plus the offset in whole seconds, on the
  calendar in force (R4): a reading comes back as itself, byte for byte, and an event chart,
  handed the proleptic instant as its reading at offset 0, prints the Julian date its Return
  Date/Time and Calendar lines print, a return on Julian 29 February included. The scans step
  proleptic days (branch R3, 1.3.4; R-D7): a typed bound — `TransitInput.ReportStartDate`,
  `ElectionalInput.RangeStart`, a lunar-return range's `DateTime` — is read once at its entry point
  by `CalendarDateFormatter.ProlepticDay` or `ProlepticClock`, the inverse of `Format(DateOnly, …)`
  (on or before 1582-10-04 the Julian calendar's day as proleptic ticks; a bound in 1582-10-05 to
  1582-10-14 refused with `IsJulianDate`'s sentence), and everything after it is a proleptic day or
  an instant: every scan conversion is by `JulianDayUtility.InstantToJulianDay`
  (`TransitEphemerisHelper.DateToJulianDay(DateOnly)`, `LocalDateTimeToJulianDay`,
  `LocalDayStartToJulianDay`, the daily noon), never through `IEphemerisService.DateToJulianDay`'s
  calendar switch; the transit midpoint (`TransitInput.MidpointDate`) and a daily day
  (`DailyHoroscopeDay.Date`) are proleptic days printed through the formatter, and the progressed age
  runs from the natal Julian Day to the midpoint day's noon. Typed 1582-09-25 to 1582-10-25 is 21
  days (Julian 09-25 to 10-04, then 10-15 to 10-25), its midpoint 1582-10-15, with Jupiter's one
  station direct (Julian 1582-09-28) and Neptune's one retrograde (Julian 10-23); typed 1300-02-27
  to 1300-03-02 is 5 days, Julian 29 February among them; typed 1400-02-28 to 1400-03-02 has its
  midpoint on Julian 1400-02-29. Deliberately unchanged: a typed reading read on the calendar in
  force by the chart's rule (`ChartEngine.ResolveInstant`, the solar return's birthday, the ingress
  year bounds, a lunation's date, the mundane series), the Calendar-line conditions, and the Period,
  Date Range and file-name texts, which print the typed bounds. Until R3 the scans stepped the typed
  date on the proleptic Gregorian calendar and read each day back through the calendar switch: across
  October 1582 the ten typed days 1582-10-05 to 10-14 landed on the Julian Days of 10-15 to 10-24, so
  the daily report printed 31 days, ten of them twice, and the station detector scanned them twice; a
  Julian 29 February was skipped; the midpoint was typed arithmetic (1582-10-10, a date that never
  occurred); and a bound in the gap was read as Julian in silence. A chart export whose instant is before
  the switch prints `**Calendar:** dates before 1582-10-15 are on the Julian calendar`
  directly after its Date/Time line (the PDF after its Date line), and a report whose earliest
  instant is prints it after its Period or Return Date/Time line, or after its Software line
  where it has no Period line (R-D2): a series' first event, a return's return or natal
  instant, a transit, daily or timeline report's natal instant or period start, an electional
  run's natal instant or window start, either chart of a synastry or composite, the
  relocation's natal; never in the JSON. Until 1.3.4 every instant before the switch printed
  on the proleptic Gregorian calendar, 7 to 10 days from the date in force, beside a natal
  Date/Time line read on the Julian calendar; a natal chart's `UtcDateTime` was held as its
  Julian label; the relocated chart was recast from that label; the progressed age counted
  the label's ticks (N1 at 1600-03-15: 299.994524 years for 299.972621); and the daily
  heading's weekday was the proleptic components'.
- **Aspects and orbs:**
  - Conjunction (0°): 8° orb
  - Opposition (180°): 8° orb
  - Trine (120°): 7° orb
  - Square (90°): 7° orb
  - Sextile (60°): 5° orb
  - Semi-square (45°): 2° orb
  - Sesquiquadrate (135°): 2° orb
  - Semi-sextile (30°): 2° orb
  - Quincunx (150°): 2° orb
  - These are the chart's own orbs (`AspectDefinition`). Other comparisons have their own:
    synastry and the electional natal harmony by the wider of the two bodies' categories
    (`SynastryOrbs`: conjunction and opposition 8° for the luminaries, 7° personal, 5° outer
    and angles); transits by transiting body (`TransitOrbs`, 2° to 0.75°); progressions 1° (the
    five majors) and solar arcs 1° (the five majors with the semisquare and sesquiquadrate, branch
    Modes, 1.4.0); transit midpoint activations 10′; chart midpoints as in *Midpoints* below.
- **Orb format** (`PositionFormatter.FormatOrb`, branch O-D3, 1.3.4; card O-D3 option c, E1-7,
  E2-12): one function prints every orb printed in minutes, as truncated D°MM′ — the whole
  minutes `Math.Floor(|orb| × 60 + 1e-9)`, the degrees unpadded, the minutes two digits, the
  degree sign and the prime (U+2032), no rounding and no carry, as a position is truncated to
  the second: 1.9999° is 1°59′, 0.063724° is 0°03′, 0.75° is 0°45′ (R1). The guard, a billionth
  of a minute (6e-8 arc-second), lets a double a hair below a whole minute by arithmetic
  (2 − 0.01 × 120 = 0.7999999999999998, 0.8° by construction) print that minute, 0°48′, where
  the plain floor printed 0°47′, and leaves a real sub-minute difference untouched (R1a). Every site that prints an
  orb in minutes calls it (R2): the chart Markdown's angular, aspect, declination, midpoint,
  fixed-star and antiscia tables, its Fixed Stars heading and none line (1°00′) and its Moon
  line, always the canonical orb (`ResolveCanonicalMoonOrb`, internal: the aspect table's
  value, else the same quantity from the positions, else the evaluator's); the PDF's Moon line
  and tables; the synastry inter-chart table; the transit export's orb at report start and its
  declination, transit-to-transit, progressed and solar-arc orbs; the timeline's orb cells and
  pass notes; the daily Active rows; `FormatAspect`, `FormatActivation` and
  `AntisciaConnection.Notation`. The transit residual note keeps its seconds, truncated, with
  the prime and double prime (" (orb: 0°00′02″)"). The orbs once printed as decimal degrees
  call it too (R3): the profection's Lord's Natal Aspects ("(orb 1°12′, Ap)"), the horary
  Perfection and Application fails lines and `HoraryPerfectionAnalyzer`'s notes and denials
  ("closest 1°52′ on …", "turns around at 1°48′ on …"), and the medical Hard Aspects table.
  Untouched: paran orbs (minutes of time), the JSON's numbers, the orb limits stated in whole
  degrees ("±1° orb", "major, 1° orb", "within 18°"), the Near-a-node flag, and positions and
  arcs — the D°MM' with an apostrophe of the transit, timeline, daily, progression and series
  exports and the declination cluster zone — so E1-7 item 9 is closed for orbs and open for
  positions. Until 1.3.4 `FormatOrb` rounded half-up and only the electional export called it;
  the chart export's `unifiedOrbFormat` switch (gone, with `FormatOrbLegacy`) printed legacy
  forms everywhere else — a bare "129'" in the aspect table, "003'" in the midpoint table,
  "0°03'" with an apostrophe in the others, "(orb 1.49°)" on the Moon line, "(orb 1.2°, Ap)" in
  the profection, "| 2.35 |" in the medical table and "closest 1.88°" in the horary — so one
  value printed three ways (E1-7 cases 1, 2, 3, 8 and 9).
- **Applying/Separating** (`AspectDirection`): an aspect is applying when its orb is
  decreasing at the instant, separating otherwise; an orb of exactly 0, or a rate of
  exactly 0, is separating. The rate comes from the speeds in longitude at the instant —
  with Δ the signed longitude difference wrapped to (−180°, 180°], the orb's rate is
  sign(Δ)·(v₁ − v₂)·sign(|Δ| − aspect angle) — and a point that does not move has speed
  0: a natal point under a transit, progression or solar arc, and a chart's Ascendant
  and Midheaven (ruling D5). A solar-arc point moves at the arc's rate, a composite
  position at the average of the two natal speeds. Chart aspects, transits,
  progressions, solar arcs and composites all use it; a composite's marks are computed
  and not printed, a midpoint being motionless (S-D4, branch L2, 1.3.4). Until 1.3.3 each compared the orb
  now with the orb a step later (0.01 day; 24 hours; 0.01 progressed day; 0.01° of arc),
  so an aspect perfecting inside the step read separating — five labels on the saved
  charts, 392 of 254,628 in the 9,979-chart sweep — and a composite was judged against
  the real sky 14.4 minutes on.
- **Applying benefic and malefic aspects** (D12, `PlanetConditionService`): in a planet's
  condition an applying trine or sextile from Jupiter or Venus scores +1 and an applying square
  or opposition from Mars or Saturn −1, once per flag, beside +2 and −2 for a conjunction.
- **Moon's first applying aspect and void of course** (`MoonConditionService`): the
  first exact major Ptolemaic aspect the Moon makes to the Sun or Mercury through
  Pluto before it leaves its sign, with no orb limit; none means void of course. The
  targets move: `GetMoonCondition` takes the ephemeris and the chart's Julian Day and
  follows every body to the Moon's sign exit. Until 1.3.2 the targets were held at
  their chart longitudes, which gave the wrong first applying aspect on 2.21% of
  instants and the wrong void of course on 0.55% (2000–2030) — and void of course
  is a horary denial. Only positions with no motion of their own, composite
  midpoints, use the fixed-target scan, `GetMoonConditionWithFixedTargets`; a
  composite's first applying aspect and void of course are computed and not printed,
  its export reading the phase, sign and via combusta from the scan (S-D4, branch L2,
  1.3.4).
- **Void-of-course periods** (`VoidOfCourseScanner`, branch C, 1.3.3): the Moon is
  void from the last exact major aspect it makes in its stay in a sign (the five
  Ptolemaic angles either side, to the Sun or Mercury through Pluto, every target in
  motion) until it leaves the sign; a stay with none is void from its sign entry
  (four in 1900–2050), and an aspect exact at the exit does not count. At every
  instant this is `GetMoonCondition`'s void of course. The aspects come from
  `AspectPerfectionFinder.FindFirstExactMajorAspect`, the Moon followed from one
  first exact aspect to the next until none is left before the exit; entry and exit
  are found to 1 s in an hour of a grid fixed in time, so a stay's bounds do not
  depend on the scan, and printed to the second they lie within 1.4 s of Tier B
  (the 1 s bracket's end, rounded). `Scan` returns every period that touches its
  range, end after the start and start before the end, with its true bounds, and
  reads nothing outside `EphemerisSpan`: a stay begun before the span is searched
  from its first instant, one leaving after its last has no period. Until 1.3.3 a
  period started at the last hourly sample within 1° of any aspect (Tier B
  1900–2050: median 81 minutes late, 59 minutes early to 52 hours late), a stay
  with no sample in orb had none, a period straddling the range was dropped, and
  6,323.6 of 8,561.3 void days were reported.
- **Event dates** (T-D11, kept: UT throughout): the transit, timeline, return and series exports
  print every event's date and time in UT, most labelled UTC, after the time or in a "Date (UTC)"
  column; the great-conjunction series and the cycle timeline print UT dates with no label, and the
  daily horoscope and the electional export print local readings.
- **Transit report techniques** (`TransitInput`, `TransitService`, `TransitMarkdownFormatter`,
  `TransitReportNaming`, branch Modes, 1.4.0 item 2; scoping C1f, C2, C3, C5, C10; Steve's decisions
  of 2026-10-17): the report runs any combination of three techniques, `IncludeTransits`,
  `IncludeSolarArcs` and `IncludeSecondaryProgressions`, all default true (`IncludeProgressions` is
  gone); none at all is refused with an `ArgumentException` naming the three. With Transits off the
  whole transit scan is skipped and `TransitResult.IncludesTransits` is false, its transit lists empty,
  and the export prints no transit section. The progressions and solar arcs are taken at the report's
  **start** (its midpoint until 1.4.0; the house overlays keep the midpoint), the Moon's next sign
  change, the sign changes and the stations sought from it, and the dated hits of the two finders below
  are added over [start, end] (`TransitResult.SolarArcHits`, `ProgressedHits`). The export prints one
  form for every combination: the header's `**Techniques:**` line names the ones run, in the order
  Transits, Solar Arcs, Secondary Progressions (until 1.4.0 a `**Progressions:** Included` line); the
  headings `## Secondary Progressions` and `## Solar Arc Directions` keep their words and lose
  "(at date)"; each section gives the positions and the aspects in orb (1°) at the start, named in its
  sub-headings ("### Progressed Positions (at report start, 2026-04-12)"), then the hits in the period
  as a table headed `Date (UTC)`, dates on the calendar in force, a natal point in the export's notation
  (`SA Mercury sqr NA MC`, `P Moon cnj NA Moon`) and a midpoint as `SA Sun = NA Pluto/ASC` with its
  dial harmonic, or a sentence when there is none. The title and the default file name follow the
  techniques (`TransitReportNaming`): with the transits `# Transit Report` and `{name}_Transits_...`;
  solar arcs alone `# Solar Arc Report`, `_SolarArcs_`; progressions alone `# Secondary Progressions
  Report`, `_Progressions_`; both `# Solar Arc and Progressions Report`, `_SolarArcsProgressions_`; the
  save dialog's subject likewise. The Transits panel has a box for each since branch ModesPanel (see
  *Transits panel* below). The goldens
  `TransitMarkdownFormatter_J2000NYC_20260415` (regenerated: the header line and the two sections; the
  snapshot three days earlier gives solar arc 26°45′ and P Moon 0°51′ Scorpio, and the week holds one
  hit, SA Saturn sesquiquadrate Sun/True Node on 2026-04-14) and
  `SolarArcMarkdownFormatter_J2000NYC_2026` (new: solar arcs and progressions without transits over
  2026, 37 solar arc hits and 9 progressed hits) cover it; the Timeline and Daily goldens are unchanged.
  *A fourth technique* (branch PrimaryExport, 1.5.0; scoping decision 1): `TransitInput.IncludePrimaryDirections`,
  default **false** (so every caller, helper and golden of 1.4.0 is unchanged), with
  `TransitInput.PrimaryDirectionSettings` (`PrimaryDirectionSettings.Default`); the refusal names all four
  flags. `TransitService` builds `PrimaryDirectionService` inline over its raw ephemeris as it builds the
  two hit finders, calculates the whole life once into `TransitResult.PrimaryDirections` (null when off)
  and takes the period's hits, the UT date in [start, end] as `FindHits` reads its window, into
  `TransitResult.PrimaryDirectionHits`, with no second calculation. The export prints the section of
  *Primary directions export* (below) after Solar Arc Directions, sub-heading `Primary Direction Hits in
  the Report Period`, none sentence `No primary direction becomes exact in the report period.`; the
  Techniques line appends `Primary Directions`; the Aspects line without transits appends `primary
  directions: majors` to its join (`Primary directions: majors` when it stands first), and with transits is
  unchanged; `TransitReportNaming` takes a fourth bool, fifteen combinations in eight forms, the four new
  `Primary Directions Report` / `PrimaryDirections` / `Primary Directions`, `Solar Arc and Primary
  Directions Report` / `SolarArcsPrimaryDirections` / `Solar Arcs and Primary Directions`, `Progressions
  and Primary Directions Report` / `ProgressionsPrimaryDirections` / `Progressions and Primary
  Directions`, `Solar Arc, Progressions and Primary Directions Report` /
  `SolarArcsProgressionsPrimaryDirections` / `Solar Arcs, Progressions and Primary Directions` (title, file
  stem, dialog subject); `MainViewModel` names reports through the result, so no view model changed. The
  golden `TransitMarkdownFormatter_J2000NYC_2026_PrimaryDirections` (2026, the other three off, Naibod,
  uncertainty 4: four hits) covers it under the three cultures.
- **Transit lifecycle** (`TransitAspectFinder`, `TransitMidpointScanner`, `TransitIngressDetector`,
  `TransitService`, `TransitTimelineMarkdownFormatter`, branch D, 1.3.4; rulings T-D3 option a,
  T-D4, T-D5 option c and D13): a transit is one passage of the body over one point. For a natal
  longitude n and an aspect of angle a the points are n + a and n − a (one when a is 0° or 180°);
  the in-orb periods at one point (|signed separation to it| ≤ `TransitOrbs.GetOrb`) whose gaps
  are shorter than `PassageGapDays` (365) are one transit, inside the report as well as outside it,
  so a report may list two transits of one body, point and aspect (Mars's sextile over two years).
  Measured over 1200–2399, the longest gap inside a passage is 287 days (Pluto), the shortest
  between two passages of one point 516 (Mars) and the longest passage 2047 (Pluto). A transit is
  listed when the body is in orb at 12:00 UT on a report date; its entry and exit are the start of
  its first period and the end of its last, found by walks of at most `MaxWalkDays` (3653) from the
  passage's first (last) in-orb sample that stop after 365 consecutive samples out of orb and never
  sample outside `EphemerisSpan`, each bisected to the second; a walk that reaches the cap or the
  span's edge in orb keeps the noon fallback. The signed separation is to the transit's own point,
  ((lon − point + 180) mod 360) − 180, never the nearer side; a pass is a sign change between
  consecutive 12:00 UT samples both within 90° of the point, bisected to the second, scanned from
  the sample before the entry instant to the one after the exit and kept within [entry, exit],
  numbered 1..n within the transit. The lifecycle lists every station of the body between entry and
  exit at whose instant it is in orb (`TransitStationDetector.FindStations` over the transit's own
  dates). The North Node has no station rows: `TransitService.StationBodies` and the finder skip
  it. Mars: `TransitMarsEvaluator.ShouldIncludeMars` stays the gate for its presence in the report,
  and a Mars transit or Mars midpoint activation is listed only if on a report date between its
  entry and exit Mars is in orb at 12:00 UT with |daily motion| below
  `TransitOrbs.MarsSlowMotionThreshold` (0.3). A transit declination row is one per pair, by side
  of the equator (D13), through `DeclinationAspectService.FindDeclinationAspect`. An ingress is
  dated by its instant, the sign change bisected to the second between two 12:00 UT samples
  (`TransitIngress.DateTimeUtc`; the table prints the date): Neptune into Aries 2026-01-26 17:34:29
  UT. A midpoint activation follows the same rules: the dial position m stands for the eight
  longitudes m + 45k, one activation per passage over one of them, orb `TransitOrbs.MidpointOrb`,
  entry and exit bisected to the second, passes found on a half-day grid. A timeline cluster is a
  run of in-period events each within `ClusterWindowDays` (3) of the cluster's first event; the
  first event outside the window starts a new cluster, and a cluster of one is dropped. Until 1.3.4
  one transit covered every in-orb day of an aspect at either point and the walks stopped after 90
  days out of orb and 365 days from the report (T1-2: 740 of 1,706 entries and exits in the real
  exports cut, 381 of 947 pass numbers not the pass's place in its transit); a pass was a sign
  change of the separation to the nearer point, so a body passing the natal point or the point
  opposite was given a pass of 30° to 120° (T1-1: 1,079 of 5,412 passes on the saved charts over
  two years); a fast body's pass before its first in-orb noon was missed (T1-10); the stations were
  the report's (T1-9: 862 of 1,206 in-orb stations absent); every reversal of the true node was a
  station (198 of 229 station rows); a report that passed the Mars gate listed every Mars transit;
  a pair within a degree of the equator was listed as parallel and contraparallel both; an ingress
  was dated the first noon in the new sign (T1-5: 817 of 1,722 a day late); a midpoint activation's
  entry and exit were noons and its pass before the first in-orb noon missed (T1-8); and a cluster
  was a chain (T1-12: 45 of 79 longer than three days). The goldens
  `TransitMarkdownFormatter_J2000NYC_20260415`, `TransitTimelineMarkdownFormatter_J2000NYC_2026April`
  and `DailyHoroscopeMarkdownFormatter_J2000NYC_20261020_20261105` cover it.
- **Planetary returns** (`ReturnPassageFinder`, `PlanetaryReturnService`, `ReturnIngressTimelineService`,
  `PlanetaryCycleTimelineService`, `PlanetaryReturnMarkdownFormatter`, branch F, 1.3.4; ruling T-D6:
  the first pass, every planet, the other passes printed): a return is a passage of the planet over
  its natal longitude, and its instant is the passage's first pass. The crossings of the natal
  longitude after birth, each direct or retrograde by the sign of the planet's speed at the crossing
  (`ReturnChartFinder.RefineCrossing`), group as follows: a direct crossing begins a passage; a
  retrograde crossing, and the direct crossing after it, belong to the passage in progress. The
  birth's own passage is not a return: a planet retrograde at birth owns its first direct crossing
  after birth, and one direct at birth whose first crossing is retrograde owns that crossing and the
  direct one after it. Return #N is the Nth passage after birth, counted from the birth instant, for
  every planet (Mercury and Venus return about once a year as seen from the Earth); a passage has one
  pass or three. `ReturnPassageFinder.EnumeratePassages` finds them for every consumer: it steps a
  day from the first whole day after birth, never reads beyond `EphemerisSpan.LastJulianDay`, and
  closes a passage once the planet is more than `PassageCloseDegrees` (30°) past the degree after a
  direct crossing, beyond the longest retrograde arc of the five planets (Mars 19.5°). By number,
  `PlanetaryReturnService.Calculate` casts the chart at return #N's first pass and carries the
  passage's passes on `PlanetaryReturnResult.Passes`; no passage before the span's end throws, naming
  the last found. By year, `CalculateForYear` takes the first passage with a pass, any of its passes,
  whose UT date lies in the calendar year, and throws naming the previous and the next passage when
  none has. `AgeAtReturn` is whole years from the birth instant to the return's, both UTC. The return
  export prints `**Other passes:** none` or `**Other passes:** 2028-09-24 18:23:07 UTC (retrograde),
  2029-04-01 21:20:28 UTC (direct)` after Return Date/Time. The return and ingress timeline lists
  one entry per passage whose first pass lies in the window, at the first pass, "Return #N" or
  "Return #N (first of 3 passes)". The cycle timeline runs from return #N's first pass to #N+1's;
  its two return rows carry the passage's pass count and passes, every phase point takes its first
  pass with its passes listed, and the cycle for a date is return #N where N is the number of
  passages whose first pass is at or before the date (an exception naming the first return when
  there is none). `SolarReturnService`, `LunarReturnService`, the single ingress chart's pass
  choice (`IngressService`) and `ReturnFindMode` are unchanged. J2000 NYC: Saturn is retrograde at
  birth, so its crossing of 2000-01-22 is the birth passage's; Saturn #1 is 2028-07-21 08:08:04 UT
  (age 28) with passes 2028-09-24 18:23:07 and 2029-04-01 21:20:28, #2 2058-05-09 02:00:30;
  Mercury #1 to #6 are 2000-12-24, 2001-12-17, 2002-12-10, 2003-12-04 (then 2003-12-29 and
  2004-01-16), 2005-01-11 and 2006-01-05; Jupiter #2 2023-04-26 11:15:50 to #3 2035-04-10 00:53:17
  is the cycle that holds 2026-10-03; the 2026–2027 timeline lists Mercury #26, #27, #28, Venus
  #27, #28 and Mars #14. Until 1.3.4 a return by number was the last crossing (Jupiter, Saturn) or
  the first (the rest) in a window around birth + N × the sidereal period, so Mercury's and Venus's
  numbers counted periods (T2-2: Mercury #1 to #6 within two years of birth, the timeline's "Return
  ~#108"); by year the pass nearest 1 July in a window from the July before to the June after (T2-3:
  Jupiter threw for 574 of the audit's requests, Mars answered with another year's pass 314 times);
  the timeline grouped crossings less than 120 days apart (T2-5: one Jupiter loop listed as two
  returns 46 times in 149); the cycle timeline's return rows always read one pass (T5-3); and the age
  compared the local birth date with the UT return date (T2-7). The goldens
  `PlanetaryReturnMarkdownFormatter_J2000NYC_Saturn1`, `ReturnIngressTimelineFormatter_J2000NYC_2026_2027`
  and `PlanetaryCycleTimelineFormatter_J2000NYC_Mars1` cover it.
- **Progressions** (`ProgressionService`, branch G, 1.3.4; ruling T-D1 option b): the progressed
  Midheaven is the natal Midheaven plus the solar arc, MC(t) = natal MC + (progressed Sun(t) −
  natal Sun), and the progressed Ascendant is the Ascendant for that Midheaven at the birth
  latitude with the natal true obliquity (`ChartResult.Obliquity`): RAMC = atan2(sin MC · cos ε,
  cos MC), then `IEphemerisService.CalculateHousesFromArmc` with Equal houses, which no latitude
  refuses and whose Ascendant is every system's. The Solar Arc Directions table still directs every
  natal point, the Ascendant included, by the arc. An angle's aspects have a direction
  (`AspectDirection.IsApplying`): the Midheaven moves at the progressed Sun's speed per progressed
  day, the Ascendant at that times dASC/dMC, by a forward difference of 0.001° on the Midheaven.
  The angles' sign changes are found as the bodies' are, on the functions MC(t) and ASC(t) of the
  progressed Julian Day: the signs at the two ends of the ±182-real-day window, then 30 halvings
  (both are monotonic, so one change at most lies in the window). Every progressed event is dated
  by its instant: the real instant of a progressed offset d is the report's midpoint date at 12:00
  UT plus d × 365.25 real days, and the date printed is that instant's UT date (the column
  header keeps "Approximate Date", by decision). The progressed
  Moon's next sign change is scanned at 0.01 progressed day a step from the progressed instant to
  90 real days ahead inclusive (the last step is the bound, 90 / 365.25 = 0.24641), the first
  change found bisected; the stations of Mercury, Venus and Mars at 0.05 progressed day a step
  from −365 to +365 real days inclusive, each change of the speed's sign bisected on
  `SpeedInLongitude`, Retrograde when positive to negative. The key is unchanged (T-D2): 365.25
  days a year, `ProgressedDateCalculator` untouched. Since branch Modes (1.4.0) the reference day
  is the report's start, and the progressed-to-natal aspects include a body's aspects to its own
  natal place (the progressed Moon's return to the natal Moon), the angles keeping the skip; the
  typed range Julian 1400-02-28 to 1400-03-02 takes N1's progressions at the start, progressed JD
  2196056.956194, instant Julian 1300-06-23 10:56:55 UT (Tier B, n1_start_truth.py), where the
  midpoint gave 11:00:52. J2000 NYC at 2026-04-15: solar arc 26.769192°,
  MC 25°14′18″ Scorpio, ASC 02°50′40″ Aquarius, the Ascendant conjunct natal Neptune applying; its
  Midheaven enters Sagittarius on 2030-12-22 15:13:42 UT and its Ascendant Aquarius on 2024-01-26
  09:02:14 UT; from 2028-06-08 the Moon enters Sagittarius 88.93 days ahead, 2028-09-05 10:21:17
  UT; progressed Mercury stations retrograde at progressed 2000-02-21 12:46:24, real 2051-01-13
  00:26:47 UT. Until 1.3.4 the angles were the quotidian ones, the chart's for the progressed
  instant at the birthplace, which turn through the whole zodiac in a year (T3-1: on 2026-10-03
  the Midheaven was −153° to +162° from the solar-arc one across the saved charts, 986 of 2,023
  angle aspects were wrongly "applying", and 31 of 42,593 angle sign changes were found); an
  event was dated by its scan step rounded to whole days (T3-2: the Moon's sign change 0 to 4
  days late, a station 0 to 19), the Moon's scan stopped at 0.24 progressed day (87.7 real days)
  and the stations' at 0.95 (347 days). The golden
  `TransitMarkdownFormatter_J2000NYC_20260415` covers the angles and their aspects.
- **Dated solar arc and progression hits** (`SolarArcHitFinder`, `ProgressedHitFinder`, branch Arcs,
  1.4.0 item 2; Steve's decisions of 2026-10-17): two finders in Core list every instant in a period
  at which a directed or progressed point is exact to a natal point; `TransitService` runs them over
  the report period and the export prints their hits (branch Modes, *Transit report techniques*
  above). The period is every instant whose UT date lies in
  [start, end], its bounds proleptic days as the transit scans take them (start 00:00 UT to the day
  after the end's 00:00 UT, exclusive), and both return their hits sorted by instant, each bisected to
  within a second with its Julian Day and its UTC instant (`JulianDayUtility.JulianDayToInstant`).
  *Solar arcs* (`SolarArcHit`): the 12 bodies, Ascendant and Midheaven directed by the arc, the five
  majors with the semisquare and sesquiquadrate to the 13 other natal points (never a point's own
  place, since every point would hit at once whenever the arc is an aspect), and the chart's 91 natal
  midpoints (`ChartResult.Midpoints`) on the 45° dial, those holding the directed point included, each
  hit carrying the harmonic it stands at (0°, 45°, 90°, 135°, 180°). The arc is inverted, not scanned:
  the arc values at which each contact is exact lie between the arcs at the window's two ends, and the
  instant of each is bisected on the arc (the progressed Sun, by `ProgressedDateCalculator`'s key, less
  the natal Sun), one crossing per value. The arc is unwrapped, the whole revolutions counted from the
  Sun's mean motion (N1 in 1700 carries 394°; `ProgressionService.SolarArcValue` stays wrapped), and a
  window may not exceed 100 years. *Secondary progressions* (`ProgressedHit`): the Sun, Moon, Mercury,
  Venus and Mars at the progressed instant and the Midheaven and Ascendant by solar arc (T-D1;
  `ProgressionService.AscendantForMidheaven`, internal), the five majors to the 14 natal points; a body
  may aspect its own natal place (the progressed Moon's return to the natal Moon), the angles skip
  theirs (the progressed Midheaven is the solar arc Midheaven, whose aspect to itself is the arc).
  Mercury, Venus and Mars station in progressed time, so the points are sampled at 00:00 UT on every
  day from the day before the window to the day after, a hit is a change of sign of the signed
  separation between two samples both within 90° of the aspect point (the transit pass rule), and the
  Moon's 0.05° a real day keeps a daily grid from stepping over a crossing; a double crossing inside one
  day is not seen. `ProgressionService`'s snapshot follows the same self rule since branch Modes. Tier B
  (`arcs_truth.py`, pyswisseph 2.10.03 over the shipped files, its own daily scan and bisection, natal
  casts equal to the engine's to the bit): J2000 NYC 2026-01-01 to 2027-12-31 has 15 arc hits to points,
  45 to midpoints and 14 progressed hits (P Moon conjunct natal Moon 2027-04-17 21:00:52 UT; SA Mercury
  square MC 2026-02-06); M35 (1960-02-05 12:00 UT, Greenwich) has progressed Mercury square the natal
  Ascendant three times, 1974-09-01, 1997-02-25 and 2019-09-05, round its stations of 1985 and 2007;
  every P MC hit coincides with its SA MC twin to the second. `SolarArcHitFinderTests` (8) and
  `ProgressedHitFinderTests` (7) hold the lists.
- **Primary directions** (`PrimaryDirectionService`, `Speculum`, `PrimaryDirectionArcs`, `Bisection`, branch
  Primary, 1.5.0; the plan's rules R1 to R10, every scoping decision approved by Steve on 2026-10-18):
  Placidus semi-arcs in zodiaco, direct and traditional converse. *Speculum* (R1): for a point of
  declination δ at latitude φ, AD = asin(tan φ · tan δ), DSA = 90° + AD, NSA = 90° − AD, H = RAMC − α in
  (−180°, 180°], upper when |H| < DSA; in a hemisphere h is H from the upper meridian with the DSA, or
  H − 180° normalised from the lower with the NSA; |tan φ · tan δ| ≥ 1 leaves the point with no semi-arcs.
  The bodies' α and δ are the ephemeris's own (apparent, true equator of date); every other point's are
  its ecliptic coordinates rotated with the chart's true obliquity (`IEphemerisService.EclipticToEquatorial`).
  *Direct* (R2), promissor P to significator S, arc = (target − H_P) mod 360: to the Midheaven target 0,
  the IC 180, the Ascendant −DSA_P, the Descendant +DSA_P; to a body or the Part of Fortune, in S's
  hemisphere, h_S · SA_P / SA_S, plus 180 when that hemisphere is the lower. *Converse* (R3), S carried
  to P's place, arc = (target − H_S) mod 360, in P's hemisphere h_P · SA_S / SA_P, plus 180 when lower;
  an angle as significator is its ecliptic degree at latitude 0 (the Descendant Asc + 180, the IC
  MC + 180). *Left out* (R1): a direction whose semi-arc does not exist is left out and counted once
  per significator, promissor point and motion (`PrimaryDirectionResult.LeftOutCount`): direct uses
  SA_P for every significator but the Midheaven and IC, and SA_S for a body or the Lot; converse uses
  both always. J2000 NYC leaves out 60: Polaris, Eltanin (the catalog's Etamin), Canopus, Achernar and
  Toliman never rise or set at 40.7128° N (|δ| ≥ 49.3°), so each reaches only the meridian, direct;
  Tromsø (69.6492° N, the Sun circumpolar at the June solstice) 1,179. *Promissors* (R4): the Sun to
  Pluto and Chiron each as a conjunction at its real place (with latitude) and six aspect points at
  λ ± 60°, ± 90°, ± 120° and the opposition at λ + 180°, all at β 0; the true node as a conjunction
  and an opposition; the antiscion 180° − λ and contra-antiscion 360° − λ of all twelve bodies,
  unrounded (`AntisciaService.CalculateAntiscion`; the chart's antiscia table rounds to 0.0001°); the
  55 catalog stars at their own (λ, β), through `IEphemerisService.CalculateFixedStarPosition`, new,
  the same `swe_fixstar2_ut` call as the longitude. *Significators* (R5): the Ascendant, Midheaven,
  Descendant, IC, Sun, Moon and Part of Fortune (`ChartResult.ArabicParts`, β 0);
  `PrimaryDirectionSettings.PlanetsAsSignificators` adds Mercury to Pluto. The Descendant and IC take
  only the conjunction of a body other than the node (the Sun to Pluto and Chiron, at their real place
  with latitude) and the 55 stars (R5 amended, Steve, 2026-10-18, branch PrimaryExport): a point without
  latitude and its exact opposite give the same arc, so an antiscion to the Midheaven is the same
  direction as its contra-antiscion to the IC, an antiscion to the Ascendant as its contra-antiscion to
  the Descendant, and the node to the IC or Descendant as its opposition to the Midheaven or Ascendant;
  until then every such pair was listed twice (J2000 NYC 31 rows, chart 55 32). A body never takes its
  own points. *Keys and dates* (R6, `PrimaryDirectionKey`, named by `DisplayNames`): years =
  arc / k, Naibod k = 360 / 365.2422 (25° is 25.364042 years), Ptolemy k = 1; the true solar arc in
  right ascension is the t days, read as years, at which the Sun's right ascension has advanced by the
  arc from the birth, the Sun forward for converse too, bisected on [0, AgeLimitYears + 20] days
  (`Bisection.Solve`, extracted from `SolarArcHitFinder`, which calls it with no change), none beyond
  it (J2000 NYC: 25° is 23.1572 years). The bracket is read as days, so a birth within it of the
  span's end would read the Sun past the files: `Calculate` refuses such a birth under the solar-arc
  key at its entry, before the speculum reads anything, with an `ArgumentException` carrying no
  parameter name (the Directions catch prints the message as it is), "The solar-arc key reads the
  Sun's right ascension to {bracket} days after the birth, past the end of the ephemeris files
  (2399-12-31 23:51:20 UT); for a birth after {date} use the Naibod or Ptolemy key.", the date the
  last birth whose bracket stays inside the span (age limit 100: 2399-09-02), one message built by
  `SolarArcKeyBeyondSpanMessage`; and `SolarArcInverter` clamps its bracket to the span's end, so the
  internal `YearsFor` never reads past it either (branch TiesAndGuards, 1.6.1). Until 1.6.1 the key
  read the Sun past the span on the Moshier model, with a log line only. `JulianDayUt` is the natal Julian Day + years × 365.2422,
  `InstantUtc` by `JulianDayUtility.JulianDayToInstant`. *Window* (R7): `Calculate` keeps hits with
  0 ≤ years ≤ `AgeLimitYears` (100; 1 to 120); `FindHits` those whose instant's UT date lies in
  [start, end], as `SolarArcHitFinder.FindHits` reads its window. *Sensitivity* (R8): every hit is
  recomputed at the birth Julian Day a minute earlier and a minute later, the speculum rebuilt from
  the ephemeris (`CalculateAllPositions`, `CalculateHouses` in Equal houses, the stars, the Part of
  Fortune by `ChartSect.IsDayChart`); a hit is the same when significator, promissor, aspect, aspect
  angle and motion match; `MonthsPerMinute` = (JD at +1 − JD at −1) / 2 / (365.2422 / 12), null when
  the hit is missing at either end or it is a Part of Fortune hit and the sect at either end differs
  from the birth's (J2000 NYC's Moon conjunct the Midheaven, 2016-03-26 21:03:58 UT: −2.9539). With
  `BirthTimeUncertaintyMinutes` N (0 to 120), `RangeStartUtc` and `RangeEndUtc` are the earlier and
  later of the hit's instants at ∓N minutes, null by the same two conditions. Hits sort by
  `JulianDayUt` to the second (`CompareHits`: `Math.Round(JulianDayUt × 86400)`, a quantised key,
  never a tolerance comparer; `TieTolerance` = 1 / 86400 day), ties within a second by significator
  (Asc, MC, Dsc, IC, Sun, Moon, Mercury to Pluto, Fortune), promissor, aspect, angle, motion (branch
  TiesAndGuards, 1.6.1; until then the exact instant decided, so a direct conjunction and its converse
  twin, one Placidian distance landing ulps apart, were ordered by rounding). A natal chart without equatorial positions, houses, obliquity or
  Part of Fortune is refused with `ArgumentException`. Tier B (`pd_truth.py`, pyswisseph 2.10.03 over
  the shipped files, the rules from the plan and not from the code): seven lists under
  `tests/AstroCast.Tests/Services/PrimaryDirections/TierB/` (J2000 NYC under the three keys, 572, 582
  and 572 hits; chart 55, 534; M35 with the planets, 1,159; N1, 440; Tromsø in Equal houses, 185),
  every hit matched by key, the engine within 5e-7° of arc and 7e-7 day of instant, the lists'
  printing precision; Moira 6.10.1 (Tier C, the scoping's piece H) agrees on J2000 NYC's fifteen
  direct Midheaven arcs under 30° within 0.003°, its RAMC 0.0015° on for UT1.
  `PrimaryDirectionTierBTests` (7), `SpeculumTests` (6), `PrimaryDirectionKeyTests` (4),
  `PrimaryDirectionServiceTests` (10), `EphemerisServiceTests.CalculateFixedStarPosition_Regulus_CarriesItsLatitude`,
  `BisectionTests` (1) and `DisplayNameTests` (3) hold it. The export is branch PrimaryExport's (below); the
  panel is PrimaryPanel's.
  *The turning row and the missing figure* (branch DirectionNotes, 1.7.2; finding A5 of the 1.7.2 code audit,
  Steve's rulings: the values stay, the row is marked): three members on `PrimaryDirectionHit` say how a hit's
  two figures are to be read, and no date, age, arc, Months/min or Range value changed. *Turning*
  (`IsTurning`): a direction's proportion is taken in one point's hemisphere (the promissor's for a converse
  direction, the significator's for a direct one to a body or the Lot), and when that point crosses the
  horizon inside the tested birth times the direction's date, as a function of the birth time, turns there:
  its own date then lies outside its Range and its Months/min, a difference across two minutes, is not a
  rate. The service marks it from dates it already holds, with no new ephemeris work
  (`PrimaryDirectionService.IsBetween`): the hit has its Months/min and its own date does not lie between the
  dates a minute either side of the birth, or it has its Range and its own date does not lie between the dates
  at the window's two ends, both ends included. The test sees a turn only where it puts the row's own date
  outside the two dates compared: person A's converse Moon to Saturn's dexter square (1984-12-15), whose date
  turns about five minutes before her recorded time, is not turning at windows of 4 and 6 minutes and is at 8
  and more, and her chart cast at 09:24:50 turns within the minute with no window set. A fuller test, whether
  the governing point is on the same side of the horizon at the window's two ends, which the service could
  make from what it already holds, is for 1.8.0 (Steve's ruling of 2026-11-28): at a window of 10 minutes the
  three invented charts have one marked row and eight unmarked rows whose governing point crosses the horizon
  inside the window. *The missing figure*
  (`NoRangeReasons`, `NoMonthsPerMinuteReasons`, the flags enum `PrimaryDirectionGapReasons`): where the Range
  is null although a window is set, or the Months/min is null, the reason is recorded from what the service
  holds at that point. `SectChange`: the hit is to the Part of Fortune and the sect at one of the two instants
  differs from the birth's (the one case in which the figure is withheld though the direction may exist
  there). Otherwise, for each of the two instants at which the direction is absent (`GapReason`):
  `NoSemiArc` when it has no arc there; else `BeforeBirth` when the birth's arc plus the change to that
  instant's arc, the difference taken the short way round, is below zero (the arc has passed 0° and stands
  near 360°); else `BeyondAgeLimit`, the key and the age limit having dropped it. Each moved instant keeps the
  arcs of the directions it did not keep (`ShiftedHits.NotKept`), the arcs `HitsAt`'s own loop computes; only
  arcs are compared, so nothing says how an arc becomes a date (kept in view for 1.8.0's sidereal-day option).
  Counted on the three invented charts (J2000 NYC 572 rows, Eleanor Hartwell 538, Daniel Whitfield 578) at
  windows of 0, 4 and 10 minutes: turning rows 0, 0, 0; 0, 0, 1; 0, 0, 0; rows without a Range 7 and 15; 6
  and 15; 12 and 33; rows without Months/min 0, 1 and 6 at every window. `PrimaryDirectionServiceTests`
  (the `IsTurning_*`, `NoRangeReasons_*`, `NoMonthsPerMinuteReasons_*` and `SectFlip_IsTheReason*` tests) hold it.
- **Primary directions export** (`PrimaryDirectionMarkdownFormatter`, `PrimaryDirectionSectionFormatter`
  (internal), `PositionFormatter.FormatArc`, branch PrimaryExport, 1.5.0; Steve's choices of 2026-10-18):
  Markdown only, as the transit family is. *The section*, one writer for both outputs: `## Primary
  Directions`, a blank, then with no blank between them, each but the last ending with two spaces since
  1.7.2 (a hard break; see *Export Text* below), `**Method:** Placidus semi-arc, in zodiaco; direct
  and converse (traditional)`, `**Key:**` and the key's `DisplayName`, `**Significators:** Ascendant,
  Midheaven, Sun, Moon, Part of Fortune; the Descendant and IC by the conjunctions of bodies and stars`
  (", Mercury to Pluto" after the
  Lot when the setting is on), `**Promissors:** the planets and Chiron with the Ptolemaic aspects, the node by
  conjunction and opposition; their antiscia and the 55 fixed stars by conjunction`, `**Left out:** 60
  directions whose point never rises or sets at 40.7128° N (no semi-arc)` only above 0 ("1 direction" at 1,
  the latitude F4 with N or S as the electional refusal prints it), and `**Birth time:** unconfirmed (Birth
  Time Source not specified): every date below moves with the birth time; see Months/min` only when the
  chart's `SourceNotes` is null, blank or "Unknown" in any case ("Unknown" in place of "not specified" then);
  a blank, the `### ` sub-heading, a blank, then the table or the none sentence. *The table*, one row per hit
  in the result's order: `| Date (UTC) | Age | D/C | Significator | Aspect | Promissor | Arc | Months/min |`
  plus ` Range (±4 min) |` (N of the setting) only when the uncertainty is above 0; the date through
  `CalendarDateFormatter.Date` (Julian before 1582-10-15), the age `F2`, D or C, the hit's own names, the
  aspect as cnj, opp, sxt/sqr/tri sin (the positive angle) or dex (the negative), antiscion,
  contra-antiscion, the arc as D°MM′ truncated with the prime (`FormatArc`: 16.001096 is 16°00′), the months
  per minute as `+0.0;-0.0;0.0` (so a negative figure that rounds to zero prints 0.0, .NET's third section)
  or an em dash when null, the range as `2015-04-02 to 2017-03-21` or an em dash. *The whole-life report*
  (`PrimaryDirectionMarkdownFormatter.ToMarkdown(result, timeProvider)`, `SaveAsync`): `# Primary Directions
  Report`, then `Name`, `Natal Date`, `Birth Time Source` ("Not specified" when empty, as the transit
  report), `**Span:** birth to age 100` (the setting's limit), the Calendar line when the birth is before
  1582-10-15 (R-D2), `**Techniques:** Primary Directions`, `Generated`, `Software`; `---`; the natal chart
  embedded as the transit report embeds it (`TransitMarkdownFormatter.FormatNatalBody`, internal since this
  branch); the section with the sub-heading `Primary Directions, Birth to Age 100` and the none sentence
  `No primary direction falls before age 100.`; `---` and the engine footer. The goldens
  `PrimaryDirectionMarkdownFormatter_J2000NYC` (Naibod, uncertainty 4: 572 rows with a Range column, 60
  left out, the birth-time caution) and `PrimaryDirectionMarkdownFormatter_Hartwell` (person A, invented, since
  Fixtures; a source given, no uncertainty: 538 rows, 60 left out, no caution) cover it under the three cultures;
  `PrimaryDirectionSectionFormatterTests` (33) and `PrimaryDirectionMarkdownFormatterTests` (8) the rules.
  *The table's notes* (branch DirectionNotes, 1.7.2; finding A5): `PrimaryDirectionSectionFormatter.AppendTable`
  writes them after the rows, so the whole-life report, the transit report's section and the Directions
  panel's list (`FormatTable`) print the same; the section's header lines are unchanged, and nothing follows
  the none sentence. *Marks:* a turning row carries a dagger (U+2020) after each of its two figures, a space
  between, `-0.2 †` and `1984-10-27 to 1984-11-15 †`; a figure missing for a recorded reason prints the em
  dash and a double dagger (U+2021), `— ‡`, in the Range cell and, for the same causes, in the Months/min
  cell; a figure missing with no reason recorded prints the em dash alone. *Footnotes*, each after a blank
  line and only when a printed row carries its mark, the dagger's first: `† This direction's date turns
  within the birth-time window. Its Range does not bound its date and its Months/min is not a rate, so the
  row cannot be used to correct the birth time.`, and when no uncertainty is set, where the table has no
  Range, `† This direction's date turns within a minute of the recorded birth time. Its Months/min is not a
  rate, so the row cannot be used to correct the birth time.` (neither names a cause, Steve's rulings of
  2026-11-28: the point that crosses the horizon is the promissor of a converse direction and the
  significator of a direct one, and the footnote as first built named the promissor alone);
  and `‡ No Range: at one end of the birth-time window this direction falls before birth or beyond the age
  limit.`, the reasons those that occur among the printed rows and no other, joined with "or"
  (`ReasonWords`): "falls before birth", "falls beyond the age limit" (both: "falls before birth or beyond
  the age limit"), "is to a Part of Fortune whose formula changes between day and night", and for a
  direction with no arc there the general form "has no date". A row without Months/min is covered by the
  same footnote, its reason joining the others; where no printed row lacks a Range with a reason, as when no
  uncertainty is set, the footnote reads `‡ No Months/min: a minute from the recorded birth time this
  direction falls beyond the age limit.` *The natures of the promissor stars* (`AppendStarNatures`): after
  the footnotes, when a printed row has a fixed star as its promissor, a blank line, `### Natures of the
  Promissor Stars`, a blank line, `| Star | Nature |` with one row for each such star, once, in name order
  (ordinal), the nature exactly as `FixedStarCatalog` holds it and as the natal Fixed Stars table prints it,
  then a blank line and `Ptolemaic planetary nature from Robson/classical sources.`, the catalog's own words
  (`FixedStarCatalog.NatureSource`, from the comment on `FixedStarEntry.Nature`); no table when no printed
  row has a star. The goldens moved by these alone: J2000 NYC's seven rows without a Range, its footnote and
  54 stars; person A's one row without Months/min, its footnote and 53 stars; the transit report's
  directions golden, Capella and Sadalsuud. `PrimaryDirectionNotesTests` (10 names: the marked rows, the
  footnotes and the table in the report, the panel's list and the transit report's section, on the invented
  charts) and the three new regions of `PrimaryDirectionSectionFormatterTests` hold it.
- **Time lords** (`FirdariaAnalyzer`, `ProfectionAnalyzer`, `ZodiacalReleasingAnalyzer`,
  `ChartMarkdownFormatter`, branch H, 1.3.4; rulings T-D8 option a, T-D9 option b, T-D10 year (b) and
  second level (b), T6-4, R-D6): the three analyzers count years by one rule,
  `FirdariaAnalyzer.BirthDayAndAge` (internal). The birth day is the local birth reading's day as
  proleptic ticks (`CalendarDateFormatter.ProlepticDay`); anniversary N is the birth day plus N years
  (`DateOnly.AddYears`: 29 February falls on 28 February in a common year); the age at a reference date
  is the greatest N whose anniversary is on or before the reference day: the natal export's date of
  today as given, or a return's instant (`DateTimeKind.Utc`) read first on the natal chart's clock,
  its zone's reading or the explicit offset's; a reference day before the birth day is refused by all
  three with `ArgumentOutOfRangeException` ("The reference date 1979-12-31 is before the birth day
  1980-01-01.", T6-6). Every boundary is a day, or a day and a fraction for a firdaria sub-period, as
  proleptic ticks on the models, and prints through `CalendarDateFormatter`, so before 1582-10-15 it
  shows its Julian label: N1's anniversaries print 03-15 in the 1300s, 03-14 from 1401, 03-13 from
  1501 and 03-23 from 1583. The profection's "as of" date is the reference day the age was taken on
  and prints as typed.
  *Firdaria* (T-D8; Abu Ma'shar, al-Biruni, Bonatti): by day Sun 10, Venus 8, Mercury 13, Moon 9,
  Saturn 11, Jupiter 12, Mars 7, North Node 3, South Node 2; by night (Abu Ma'shar) Moon 9, Saturn 11,
  Jupiter 12, Mars 7, Sun 10, Venus 8, Mercury 13, North Node 3, South Node 2. Period k runs from
  anniversary S to anniversary S + years, and the timeline holds (age ÷ 75) + 1 cycles of the nine. A
  planet's period is divided into seven sub-periods of a seventh of its span in days, in Chaldean order
  from the lord (Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon), the seventh ending on the period's
  end; a node's period is not divided: `SubPeriods` empty, `CurrentSubRuler` empty, the sub dates the
  major's, the export's `**Current Sub-Period:** none (a node's period is not divided)` with no
  Sub-Periods table, and the summary "Age 71: North Node major period (2050–2053), undivided. …".
  *Profections* (T-D9): the profected house is age mod 12 + 1, counted in whole signs from the
  Ascendant's sign; its sign is the Ascendant's sign plus (house − 1) signs, its lord the traditional
  ruler, and the table's row h carries the sign h − 1 after the Ascendant's; the cusps are not read, and
  the summary reads "The profected house is the 9th (Cancer), making Moon the lord of the year".
  `AnalyzeForReturn` rounds from the birth instant (`chart.UtcDateTime`), and only
  `SolarReturnMarkdownFormatter` passes `isReturnContext: true`, since 1.7.2 to all three sections, which it
  reads for the year the return opens (see *Return Ages* below): a lunar or planetary return's
  profection is the year running at the return (Saturn return #1 of J2000 NYC, 2028-07-21: age 28, the
  5th (Taurus), Venus). *Zodiacal releasing* (T-D10; Valens, Anthology IV; Spirit only): Level 1 from
  the Lot of Spirit's sign, each sign for its years × 360 days from the birth day, the timeline closing
  with the first period after the current one; Level 2 within the current Level 1, from the Level 1
  sign, each sign for its years as 30-day months in zodiacal order, passing after one round of 211
  months to the sign opposite the Level 1 sign (the loosing of the bond), the last sub-period clamped
  to the Level 1 end; a Level 2 row's `Years` is its months over 12, and the Duration column prints the
  whole months ("27m", the clamped row its own, "20m", "14m"); from Fortune's sign 0, 3, 6 and 9 are
  angular, 1, 4, 7 and 10 succedent, 2, 5, 8 and 11 cadent (T6-4). J2000 NYC (night, Ascendant
  Capricorn, Spirit Scorpio, Fortune Pisces): firdaria Moon 2000-01-01–2009-01-01, Saturn –2020-01-01,
  Jupiter –2032-01-01 (sub-periods Jupiter –2021-09-18, Mars –2023-06-06, Sun –2025-02-21, Venus
  –2026-11-09, Mercury –2028-07-27, Moon –2030-04-14, Saturn –2032-01-01), Mars –2039, Sun –2049,
  Venus –2057, Mercury –2070, North Node –2073, South Node –2075; releasing Scorpio 2000-01-01–
  2014-10-14 (Cadent), Sagittarius –2026-08-12 (Angular), Capricorn –2053-03-23 (Succedent), Aquarius
  –2082-10-17 (Cadent); Capricorn's Level 2 is nineteen rows, Sagittarius 2042-12-16–2043-12-11 then
  Cancer 2043-12-11–2045-12-30 (the loosing of the bond) to Capricorn 14m 2052-01-28–2053-03-23. The
  goldens' natal clock is 2026-06-10 (`GoldenFixtures.GoldenClockDate`), where Sagittarius is current
  and its Level 2 eight rows, Cancer 20m 2024-12-20–2026-08-12 last. Until 1.3.4 the firdaria order was
  no source's (day: Sun, Moon, North Node, Jupiter, Mercury, Saturn, South Node, Mars, Venus; night:
  Moon, Sun, then as by day; T6-1: the major lord differed at 69% of the sampled dates), the nodes'
  periods were divided from Saturn, and every period ran years × 365.25 days from local midnight, a
  boundary up to a day off the birthday (T6-5); the profected sign was the sign on the profected
  house's cusp, so a quadrant chart skipped and repeated signs (T6-2: chart 55 read Leo and the Sun for
  Cancer and the Moon); releasing counted 365.25-day years and shared a Level 1 period out in
  proportion to the signs' years (T6-3); the 5th and 11th from Fortune were called cadent and the 6th
  and 12th succedent (T6-4: 17,951 of 58,058 labels); a reference date before the birth day threw from
  a cusp index (T6-6); and a boundary before 1582-10-15 printed as proleptic ticks. The nine goldens
  that hold the sections cover it.
- **Relocation** (`IChartEngine.CalculateRelocated`, `ChartEngine`, `ChartContextService`,
  `RelocationService`, `ChartMarkdownFormatter`, `RelocationMarkdownFormatter`, branch K2, 1.3.4; rulings
  S-D6, Q3, Q11; findings Y3-2, Y3-3, Y3-6): the engine casts the relocation from the natal chart's input
  with the place changed — Latitude, Longitude and LocationName from the target, the name "{natal} —
  Relocated to {place}", and every other member copied: the clock (LocalDateTime, TimeZone,
  UseExplicitOffset, UtcOffsetHours), HouseSystem, EventType, EventDescription, Gender, SourceNotes,
  Notes, Keywords, HoraryQuestion and HoraryCategory — through Calculate's own pipeline, so the reading
  resolves to the natal instant, the same Julian Day bit for bit, with the natal offset label, mapping
  and Time Note, and the Date/Time line prints the birth clock. The angles, cusps, houses, placements,
  altitudes, aspects to angles, angular planets, Lots and parans follow the place, and so do the chart
  ruler, the mutual receptions and the Ascendant's triplicity rulers; sect, the dignities
  (`CalculateDignities` reads the natal `ChartContext.IsDayChart`), the Planetary Sect table
  (`IChartContextService.Analyze`'s `sectFrom`, the natal context), the planetary hour (the natal
  chart's own object; none is computed at the place, and null stays null) and the printed clock stay
  the birth chart's; the Lots take the birth's sect at the place's Ascendant. The time-lord sections of
  the relocation export are the natal chart's (`ChartMarkdownFormatter.ToMarkdown`'s `timeLordsChart`,
  which `RelocationMarkdownFormatter` passes), printed where they print. A relocated horary chart keeps
  its category and its perfection is judged at the place. In the House Changes table a planet is angular
  when `AngularityService.FindAngularPlanets` lists it, within 5° of an angle (Q11): "became angular" is
  angular at the place and not natally, "left angle" the reverse, and every planet whose house or
  angularity changed is a row (an angularity change with the same house number is a row with equal
  houses); the sort is became angular, then left angle, then by name. Until 1.3.4 the relocated chart
  was a whole chart cast at the place at an explicit offset of zero: day or night flipped (74 of 187 standard
  relocations) and with it the triplicity dignities, the Planetary Sect table and the Lots' formula; the
  time-lord sections read the relocated Ascendant and Lots and counted from the UTC date; the planetary
  hour's day was the UTC date (the wrong ruler in 54 of 187, every chart at Tokyo, Sydney and Suva); the
  clock printed "UTC+0 (explicit)"; the horary category was not copied, so a relocated horary chart
  silently lost its Horary Perfection Analysis; and "became angular" and "left angle" were by house
  number 1, 4, 7, 10 (Y3-6). The golden `RelocationMarkdownFormatter_J2000NYC_to_LA` covers it: J2000
  NYC to Los Angeles stays a night chart, prints the birth clock, the Moon's night hour 12 and the natal
  time lords, and lists twelve House Changes rows, Venus become angular (ASC 1°32′) and Jupiter and
  Mercury left angle (natally IC 3°12′ and ASC 2°21′). The export's header labels the chart's own place and
  moment by its type (`RelocationMarkdownFormatter`, branch StaleResult, 1.6.1; item B of the 1.6.1 scoping,
  Steve's decision of 2026-10-21): `**Birth Location:**` and `**Birth Date/Time:**` for a Birth chart,
  `**Question Location:**` and `**Question Date/Time:**` for a Horary chart, `**Event Location:**` and
  `**Event Date/Time:**` for every other type, the type compared ignoring case; the title, Relocation
  Target, UTC Moment, Calendar and Julian Day lines and the tables' Natal columns are unchanged, and the
  two relocation goldens, both Birth charts, are byte-identical. Until 1.6.1 every relocated chart's header
  read Birth Location and Birth Date/Time. `RelocationMarkdownFormatterTests` holds the three forms.
- **Composite** (`CompositeService`, `ChartMarkdownFormatter`, `CompositeMarkdownFormatter`, branch L2,
  1.3.4; ruling S-D4, accepted 2026-10-03; Q10; findings Y2-1, Y2-5, Y2-8): a composite body is a
  midpoint, not a point in the sky. `CompositeService.ComputeCompositePositions` gives it no right
  ascension and no declination (both null); its aspects still carry the applying flag from the averaged
  speeds and its Moon condition still comes from `GetMoonConditionWithFixedTargets`, but neither is
  printed. `ChartMarkdownFormatter.ToMarkdown` takes `bool composite = false`: when true the Planetary
  Positions table is `| Body | Position | Lat | House | Speed | Dir |`, with no Dec and no OOB cell, the
  Equatorial Coordinates and Out-of-Bounds Planets sections are left out, the Moon Conditions print the
  phase, sign and via combusta only, the Aspects table is `| Body 1 | Aspect | Body 2 | Orb |`, with no
  Ap/Sp cell, and the Declination Aspects section and Declination Clusters table are left out; every
  other chart prints as before. `CompositeMarkdownFormatter` passes it and prints, directly after its
  Method line, `**Not shown:** declinations, right ascensions, out-of-bounds, declination aspects,
  stations, planet conditions, applying/separating marks, the Moon's next aspect and void of course — a
  composite body is a midpoint, not a point in the sky.` The composite keeps day or night (the sect, the
  Planetary Sect table, the triplicity tier), the Moon's phase, sign and via combusta, the midpoints,
  fixed stars and medical correspondences, and both source summaries. Until 1.3.4 the composite was run
  through the single-chart formatter as a moment in time: its bodies printed a declination and right
  ascension that were the means of the two charts' values, not the coordinates of any point (Y2-5: off
  by more than 1° for 1,156 of 1,920 bodies, with the OOB flags and clusters read from them); "No
  declination aspects found within ±1° orb." where none were sought (Y2-8); an Ap/Sp mark from the
  averaged speeds (Y2-1); and the Moon's first applying aspect and void of course from a scan over
  midpoints. The golden `CompositeMarkdownFormatter_J2000_Secondary` covers it (394 to 361 lines).
  *Mercury, Venus and the angles* (branch L3, 1.3.4; rulings S-D2 option (b) with a Note and S-D3
  option (b) with (d), accepted 2026-10-03; Q1 answered: the shared house system, else Placidus;
  findings Y2-2, Y2-3, Y2-4): every body takes its short-arc midpoint, then Mercury and Venus each
  compare its separation from the composite Sun with the far-side midpoint's (the short-arc one +
  180°) and take the far-side one when it is nearer, a tie keeping the short-arc one; latitude,
  distance and speeds stay the means, and `CompositeResult.SunSideBodies` names the bodies moved, in
  position order. The composite MC is the short-arc midpoint of the two Midheavens; its right
  ascension at the midpoint obliquity (RAMC = atan2(sin MC · cos ε, cos MC),
  `CompositeService.RightAscensionOfMidheaven`, the twin of `ProgressionService`'s private
  `AscendantForMidheaven`) is the sidereal time `IEphemerisService.CalculateHousesFromArmc` casts the
  Ascendant and cusps for, at the mean of the two birth latitudes, in the house system both charts
  share, else Placidus (the composite `ChartInput.HouseSystem` and `HouseSystemName` say which), so A
  & B and B & A are one composite. `ICompositeService.Calculate` takes the two charts and the token
  only: the two reference parameters are gone (the app had set them to Person A's birthplace; they
  were the order dependence), `ReferenceLatitude` is always the mean birth latitude and
  `ReferenceLongitude` the mean, a label. The export's Method line reads `**Method:** Tropical zodiac,
  short-arc midpoints (Mercury and Venus nearer the composite Sun); MC the midpoint of the two
  Midheavens, Placidus houses for it at the mean birth latitude`, and after the Not shown line one
  `**Note:** Venus takes the midpoint nearer the composite Sun, 00°13′17″ Aries; the short-arc
  midpoint, 00°13′17″ Libra, lies opposite.` line per body moved (positions through
  `PositionFormatter.FormatLongitude`). The golden pair (Tier B, l3_truth.out): the Midheavens
  28°28′08″ Libra and 11°54′29″ Gemini, their midpoint 140.188601° = 20°11′18″ Leo, RAMC 142.594671°,
  ASC 12°41′06″ Scorpio at 37.3825° N; the Moon moves to the 12th, Uranus to the 1st, Venus to the
  5th; Saturn DSC 1°16′ and Moon ASC 3°02′ are the angular planets; the Sun stays below the horizon,
  Night. Until 1.3.4 each body took its own short-arc midpoint, so the composite Venus landed opposite
  the composite Sun in 8 of 78 saved pairs (Mercury 2); the houses came from the midpoint of the two
  ARMCs, neither published method (the MC up to 4.9° from the midpoint of the two MCs, median 1.4°),
  at Person A's birthplace in Person A's house system, so A & B and B & A differed (the Ascendant by
  up to 26°; 117 bodies change house in 43 of 79 pairs) while the prompt called the composite
  "mathematically symmetric".
- **Davison chart** (`IDavisonService`, `DavisonService`, `DavisonResult`, `DavisonMarkdownFormatter`, branch
  Davison, 1.7.0; Steve's topics 1, 2 and 6 of 2026-10-21 and the scoping's decisions 1 to 4 and 7): a real
  chart cast for the moment halfway between two births at the place halfway between the two birthplaces, of
  any two charts the synastry pair can be (not Birth charts only). *The moment:* the mean of the two charts'
  Julian Days in UT, `DavisonService.MidpointJulianDay`, taken once (the composite takes its instant twice, from
  the Julian Days and from the `DateTime` ticks), then `JulianDayUtility.JulianDayToInstant`. *The place:* the
  mean latitude; the longitude by the shorter arc, `MidpointLongitude`: the difference from A to B normalised to
  (−180°, 180°], half of it added to A, normalised again; two places exactly 180° apart have no shorter arc, so
  the difference normalises to +180° and the midpoint is the eastward half from A (170° and −10° give −100°,
  −10° and 170° give 80°); New York and Tokyo give −147.15715°, in the Pacific, where the plain mean gives
  32.8429°. *The cast:* a `ChartInput` named `Davison: {A} & {B}`, `EventType` "Davison" (a string; the enum is
  untouched, as "Composite" is), `LocationName` `Midpoint of {placeA} & {placeB}`, `HouseSystem` chart A's (not
  the composite's agree-or-Placidus rule), `IncludeMinorPoints` the session's switch alone, whatever the two
  sources carry, no gender, no source notes, `UseExplicitOffset` true at offset 0 with `LocalDateTime` the
  instant, through `IChartEngine.CalculateAtInstant` at the mean Julian Day, the path every event chart takes
  (D4). A horary chart as either source gives a Davison chart: the type never fires the engine's horary branch
  or the five's horary gate. `DavisonResult` holds the two sources, the chart, `MidpointUtc`,
  `MidpointJulianDay`, the latitude, longitude and description, the chart's own `EngineVersion` and
  `CalculatedAt`. *The report* (`DavisonMarkdownFormatter.ToMarkdown(result, context)`, `SaveAsync`): the
  composite export's shape with the chart printed complete: `# Davison Chart: {A} & {B}`, Generated, Software,
  `**Method:** Tropical zodiac; the chart cast for the midpoint of the two birth instants (UT) at the midpoint
  of the two birthplaces (the mean latitude, the mean longitude by the shorter arc); {system} houses, Chart
  A's`, `**Midpoint Moment:** {yyyy-MM-dd HH:mm:ss} UTC (JD {jd:F6})` (`CalendarDateFormatter.DateTime`, Julian
  before 1582-10-15), the Calendar line when the midpoint or either source is Julian, `**Midpoint Place:**
  {description} ({lat:F4}, {lon:F4})`, `**Chart A:** {name}, {reading} ({zone or offset label}), {place}` and
  `**Chart B:**` likewise, the Interpretation Context when given, every header line ending in two spaces; then
  `## Davison Chart` holding `ChartMarkdownFormatter.ToMarkdown(chart)` with its defaults (temperament in,
  `composite: false`, the five in; the H1 and footer stripped), then `## Source Chart A: {name}` and `## Source
  Chart B: {name}` as the composite prints them. The chart report prints `**Type:** Davison`, its Date/Time
  line `{instant} UTC+0 (explicit)` (the label every explicit-offset chart prints) and no Time Note, no Gender, Source, Description or Notes line, and no Annual
  Profection, Firdaria or Zodiacal Releasing section, as for every chart that is not a Birth chart; everything
  else as for any cast chart (declinations, out-of-bounds, stations, dignities, Lots, fixed stars, parans, the
  planetary hour at the midpoint place, Temperament, and the Asteroids and Lilith section when the switch is
  on). The goldens `DavisonMarkdownFormatter_J2000_Secondary` and its `_MinorPoints` twin (J2000 NYC and the
  Secondary fixture: the midpoint 1985-03-24 15:30:00 UT, JD 2446149.145833, at (37.3825, −96.1249), the pair
  the composite golden uses) cover it under the three cultures. The truth is Claude's `davison_truth.py` on
  pyswisseph 2.10.03 over the shipped files (`astrocast-mac-setup/davison/truth/`): five pairs (the goldens',
  New York and Tokyo across the antimeridian, London and Sydney, N1 and the same place a century on in the
  Julian era, and an exact-180° pair in 2300), the midpoints to the second and 1e-6°, the Sun, Moon, Ascendant
  and Midheaven at each; `tests/AstroCast.Tests/Services/Davison/TierB/davison_pairs.tsv` holds its rows, and
  `DavisonServiceTests` (14) and `DavisonMarkdownFormatterTests` (8) with the 8 golden names pin them. The
  Synastry panel's Davison section (branch DavisonPanel, 1.7.0) casts it for the Chart panel's chart and the
  Person B picker, shown and exported only; see *Davison section* below.
- **Temperament** (S-D9, `TemperamentCalculator`): the scheme is the app's own, named so on the
  exports, not a published method; a tie prints as one ("Choleric and Sanguine (tied, 5 each)");
  the relocation, composite and electional candidate exports print none.
- **Chart PDF and JSON** (`ChartPdfFormatter`, `ChartMarkdownFormatter`, `ChartJsonFormatter`,
  `MainViewModel`, both heads, branch O-6, 1.3.4; cards O-D1 option b and O-D2 option b; finding E1-6):
  the PDF's text comes from the Markdown's. `ChartMarkdownFormatter.PlanetaryHourSection` and
  `HoraryPerfectionSection` (internal) return exactly what the chart Markdown prints for those sections,
  and `ChartPdfFormatter` draws them line for line with one helper: the `## ` heading becomes the PDF's
  own uppercase title (`PLANETARY HOUR`, `HORARY PERFECTION ANALYSIS`, bold 10 pt), a `### ` line a bold
  9 pt subheading, a `**Label:** rest` line its label bold and the rest plain, a wholly `**text**` line
  bold, `- ` a `• `, trailing spaces dropped, blank lines spacing. The Planetary Hour prints after Chart
  Context, then a horary chart's question and perfection analysis, then House Cusps. The positions table
  has the Markdown's eight columns: Body, Position, Lat, Dec, House, Speed, Dir, OOB. The page header
  keeps the name, Date, Calendar, Timezone and Location on the left and Type, Gender, UTC Offset and JD
  on the right; Source, Description, Notes and Time Note print once, as the content's first item, each
  `Label: value` with the label bold, only when present, so a note taller than a page lays out across
  pages. The chart JSON's `planetaryHour` carries `hourStartUtc` and `hourEndUtc` after `hourEnd`, the
  round-trip instants (`CalendarDateFormatter.Iso`), omitted when null, so a bound in a repeated or
  skipped wall hour is one instant; a chart cast at an explicit offset names no `hourTimeZone` and
  carries `hourOffsetLabel`, its `ResolvedOffsetLabel`. `MainViewModel.ExportChartJsonCommand` writes
  the chart JSON (`ChartJsonFormatter.SaveAsync`) under the chart's sanitised name with `.json`, and both
  heads' Chart Export group has an Export JSON button after Export Markdown; the Database Backup pair
  read Export Backup and Import Backup. Until 1.3.4 the PDF had no planetary hour, no horary analysis
  and no latitude, declination or out-of-bounds column; its source, description, notes and Time Note
  were in the page header, so they printed on every page and a note taller than a page could not be
  laid out (E1-6: QuestPDF's DocumentLayoutException); the JSON's hour bounds were wall clocks only and
  an explicit-offset chart's named the zone its offset overrode; no head could write a chart's JSON,
  and "Export JSON" meant the database backup. Not in this branch: the stranded-heading layout (E1-8),
  the primes' extraction as U+02B9 and U+02BA (the font), and every other Markdown section in the PDF
  (O-D1 option c). The tests read the PDF's text with PdfPig, a test package only; the golden
  `ChartJsonFormatter_ToJson_J2000NYC` carries the two UTC bounds.
- **Daily horoscope** (`DailyHoroscopeService`, `DailyHoroscopeMarkdownFormatter`, branch E,
  1.3.3; Steve accepted the E investigation's eleven recommendations on 2026-10-10):
  `DailyHoroscopeService.BuildDays` builds the days and the formatter only renders them
  (`BuildDailyData` is gone; `MainViewModel` builds the service inline, as it builds the
  scanner). A day is its own instants, `[LocalDayStartToJulianDay(D),
  LocalDayStartToJulianDay(D + 1))`, and the void scan reads the report's own instants with
  no pad; a date the zone skipped is left out, and the Report Period counts the days that
  occurred (Pacific/Apia 2011-12-28 to 2012-01-01: 4 days). Every exact pass, void bound,
  entry into or exit from orb and Moon sign change is filed on the day its instant lies in,
  instants compared, and printed as its local reading, `HH:mm`, with the zone's abbreviation
  only where the clocks repeat that reading (`ChartTimeFormatter.RepeatedReadingOffset`,
  shared with the electional): `| TR Saturn | sqr | NA Moon | 2 | 21:30 |`, `00:00 – 00:18
  CDT (Cancer)`. A lifecycle pass counts only when its residual is at most 0.01° (none
  remain since branch D). Each day is read at 12:00 local (D17 where
  noon was skipped): the Moon's sign and phase, and each transit's orb, true from the
  ephemeris and printed truncated, and phase, `AspectDirection` with the natal point still,
  "Exact" on a day with a pass. A transit is listed when it is in orb at noon
  (`TransitOrbs.GetOrb`, the angle from `TransitAspectFinder.TryGetAspect`); one that enters
  or leaves orb during the day, found between the day's start, noon and end, is in the
  Entering or Exiting line with its time (`DailyTransitSnapshot.EntryTime`, `ExitTime`). The
  Moon's sign change at any hour is the end of the void period that ends in the day, printed
  after the phase: `(Moon enters Cancer at 15:54)` (`DailyHoroscopeDay.MoonSignChange`). The
  backdrop's In Orb cells are the local dates of the envelope's entry and exit. The header
  names the zone as the clock of every time and states the noon reading. Until 1.3.3 the orb
  was the report's first orb scaled by days from the nearest pass, the phase came from the
  passes' UT dates, and a transit was listed through its envelope's UT dates (T1-6: in the
  real-export runs 1,040 rows out of orb all day, 3,575 above their limit, 2,468 of 15,333
  phases wrong); a pass was filed under its UT date and printed in UTC (T1-13: 58 of 360 a
  day late); a sign change after noon was never noted (T1-7: 451 of 880); and the backdrop
  printed UT dates (E2-7). The golden
  `DailyHoroscopeMarkdownFormatter_J2000NYC_20261020_20261105` covers it.
- **Electional Moon** (`MoonConditionEvaluator`, branch P, 1.3.3): the search reads each
  sample's next applying aspect and void of course from `MoonConditionService.GetMoonCondition`
  (H3) at the sample's instant, so a candidate's header and its Moon Conditions block are one
  reading, the answer does not depend on where the scan began, and the Moon score reads it
  (S-D11). Until 1.3.3 the nearest applying aspect within 8°, judged applying from the orb
  14.4 minutes later, with outer planets cached for up to half a day.
- **Electional search beyond the polar circles** (`ElectionalService`, branch O3, 1.3.3;
  Steve's ruling 2 of 2026-10-09, closing the O investigation's Y6-9): candidates are cast in
  the natal chart's house system, so before it scans the search casts the houses once, at the
  range's start, at the place, in that system, and where the engine has none (Placidus or Koch
  beyond the polar circles) refuses with an `ArgumentException` naming the system and the
  latitude ("… house system, Placidus, which has no houses at latitude 69.6492° N …"). The
  engine decides; nothing restates a latitude limit. A sample on a day when the Sun does not
  rise or set there has no planetary hour: the hour is taken before the scoring inputs, and
  `SunDoesNotRiseOrSetException` or `InvalidOperationException` (the two the chart engine
  catches around the same call) leaves the sample unscored.
  `ElectionalResult.SamplesWithoutPlanetaryHour` counts them, and the export says so only when
  there are any: a line after the Separation line, and a sentence in "No Candidates Found".
  Tromsø, 10–25 May 2026, drops 416 of 1,044 samples and scores the rest; 66.3° N in late June
  drops all 637. Every search where the Sun rises and sets drops none and is unchanged. Until
  1.3.3 either case ended the whole search with the engine's exception.
- **Electional score** (`ElectionalScorer`, `ElectionalMarkdownFormatter`, branch Q, 1.3.3;
  Steve accepted the Q investigation's twelve recommendations on 2026-10-10): the scorer's
  terms and weights are unchanged (S-D12), and no score moves; what changed is how a score is
  printed and stated. Every dimension score is a whole number and every weight a whole
  percent, so the candidate line (`**Score:** 82.25`, no "/100"), each weighted cell (12.75,
  22.5, 5) and the Overall row (`**82.25**`) print `0.##`, invariant: every print of one score
  agrees, and the cells add up to the Overall. Dimension scores stay `F0` and weights `P0`. The
  status bar reads "(score 82.25)" in the screen's culture, and its electional catch prints an
  `ArgumentException` that names its parameter without the framework's " (Parameter 'input')".
  Directly after `**Election Type:**` the header states `**Score ceiling:** 93.5 (each of the
  seven dimensions at its highest)`: `ElectionalScorer.ScoreCeiling` (internal), each
  dimension's highest score (Moon 100; Ascendant 85, 50 for Other; Significator 90; Natal
  Harmony 70; Malefic Avoidance 100; Mercury 100; Planetary Hour 100) weighted for the type,
  from 90.5 (Other) to 95.5 (Surgery). It is a ceiling, not an attainable maximum: for five
  types no sky reaches it. Directly after `**Event Location:**` it names the event's clock,
  `**Event Time Zone:** America/New_York (candidate instants and the period tables are on this
  clock)`, the zone as entered (E2-14). A count of one sample without a planetary hour reads in
  the singular, and a search that delivered nothing reads "0 of 3 requested — no moment in the
  date range qualified". `AstroCast_Techniques_Interpretation_Reference.md` §11 states the
  score in full. Until 1.3.3 the candidate line printed `F0` with "/100" and the cells and the
  Overall `F1`, rounding a midpoint to even (82.25 as "82/100", "**82.2**" and cells adding up
  to 82.3): three prints, three numbers, in 529 of the 1,855 windows the Q sets report.
- **Horary perfection** (`HoraryPerfectionAnalyzer`): the first mechanism that holds
  wins — direct aspect, mutual reception, translation, collection, antiscia — while
  every denial is collected. As of 1.3.3:
  - A **direct aspect** (natal orbs) perfects only if its first approach reaches exact
    before either significator leaves its sign, unless L1 and Lx receive each other
    by domicile or exaltation in either direction. One exact only out of sign is
    noted, naming the sign changes, and the next mechanism is tried.
  - **Mutual reception** perfects by domicile, exaltation, or mixed
    domicile–exaltation only. Whenever the significators are in such a reception the
    Reception line names its type, "Mutual by domicile", "Mutual by exaltation" or "Mutual
    by domicile and exaltation", whatever perfects (QI-2); until 1.3.3 only a reception that
    was the mechanism was named.
  - Only the **traditional seven translate or collect**. A retrograde intermediary
    counts and is named. Every application the mechanism relies on must reach exact on
    its first approach, or it does not perfect and a note names the one that failed;
    prohibition (Mars or Saturn, neither significator nor intermediary) is judged up
    to the last application, as for a direct aspect up to its perfection.
  - An **antiscion or contra-antiscion** perfects only while applying; a separating
    one is testimony, and noted.
  - **Refranation** is a station of L1 or Lx between the chart and a failing
    application's turnaround, or a significator's first station after it that falls
    before the linear perfection estimate (the chart's separation closed at the
    chart's relative speed).
  - **Reception** is judged both ways: favoured one way and hostile the other is
    "Mixed", and hostility is worded from the side it comes from.
  - An **unrecognised category** takes the 7th house, and the notes say so.
  - **One planet rules both houses** (QI-1, Q15): the Moon is the querent's significator.
    When that planet is the Moon, Jupiter, exalted in Cancer, signifies the quesited and the
    notes say so; otherwise the shared ruler stays the quesited's.

  Until 1.3.2 any of the twelve bodies could translate or collect, unfollowed and
  unprohibited; triplicity receptions and separating antiscia perfected; an aspect
  exact only out of sign counted; a station just after a failing application's
  turnaround went unseen; and only L1's side of a reception was tested for
  hostility. On the saved horary charts two judgments moved: Id 3 Uncertain → No
  (Chiron no longer collects) and Id 8 Yes → Uncertain (its sextile is exact only
  after both significators change sign).
- **Midpoints (Ebertin method):**
  - All planetary pair midpoints including ASC and MC
  - Activations at 0°, 45°, 90°, 135°, 180° to midpoint axis
  - Orbs: ±1.5° for conjunction/opposition, ±1° for 45°/90°/135°
  - Notation: A/B = C (per COSI)
- **Declinations** (`DeclinationAspectService`, `PositionFormatter`, branch F7, 1.3.3;
  Steve's decisions of 2026-10-10, cards D13 and D14):
  - *Out of bounds:* a body is out of bounds when the absolute value of its
    declination exceeds its own chart's true obliquity, `ChartResult.Obliquity`
    (nutation included, at the chart's Julian Day, from the `GetObliquity` call that
    assigns its houses; a composite body carries no declination and is never flagged,
    S-D4, branch L2, 1.3.4). Equal is in bounds. The Sun never is: the bounds are its own greatest
    declination, which near a solstice can pass the true obliquity by up to 0.0003°,
    through its ecliptic latitude (`PositionFormatter.IsOutOfBounds(position,
    obliquity)`). The Planetary Positions OOB cell, the Out-of-Bounds Planets list and
    its Excess (|declination| − obliquity) and the electional Natal Chart Summary read
    it. Until 1.3.3 every chart was read against 23.4367°, the obliquity of about
    2020: 454 wrong flags in 447 of the 9,979 sweep charts, the Sun 161 of them
    (foundations F5-1); on the saved charts no flag moves, and the Excess column by
    0.01° to 0.03° on 17.
  - *Parallels and contraparallels:* one aspect per pair, by side of the equator,
    north being 0° or more: a parallel needs both on the same side and |a − b| ≤ 1°,
    a contraparallel opposite sides and |a + b| ≤ 1°, within a chart and across two
    (`FindCrossChartDeclinationAspects`). Until 1.3.3 the two tests were independent,
    so a pair within a degree of the equator was listed twice: 37 pairs in 29 saved
    charts (F5-3), and M1 in the cross-chart table (synthesis Y1-3).
    Since branch D (1.3.4) the transit rows call `DeclinationAspectService.FindDeclinationAspect`
    too.
  - *Clusters:* 3+ points within a span of 2° or less of absolute declination. A
    window runs from the absolute declination s of a point not yet in a cluster to
    s + 2°, both ends included; the window with the most members is chosen, on a
    tie the narrower span, then the lower start; one of 3 or more is a cluster, its
    points leave the pool, and the choice repeats. Clusters print in the order
    chosen, largest first. Until 1.3.3 overlapping windows were merged, so a chain
    grew without limit: 31 of 135 clusters on the saved charts, in 30 charts, were
    wider than 2°, the widest 4.96° (F5-2); none is now.
- **Astrocartography** (`AstrocartographyService`, branch J, 1.3.3): lines in mundo,
  where the body itself is on the meridian or the geometric horizon, from its
  geocentric apparent RA and declination (true equator of date) and GAST at the
  chart's instant, in closed form. MC at RA − GAST and IC 180° from it, from 85° S
  to 85° N; ASC and DSC where cos H = −tan φ · tan δ, rising at RA − H − GAST and
  setting at RA + H − GAST, at every whole degree from 85° S to 85° N where the body
  rises and sets, plus the two ends at ±(90° − |δ|), where the two lines meet. No
  parallax (the Moon included), no refraction; the twelve chart bodies. A crossing
  is the great-circle distance to the line's nearest point, in km on a sphere of
  6,371.0088 km, listed within 334 km (`GetCrossings`'s default, which
  `MainViewModel.UpdateCrossingReport` uses); it is shown on screen only. Both map
  windows draw `AcgMapGeometry.Segments`, split where a line crosses 180°. Until
  1.3.3 the lines were the zodiac degree's, from an ARMC search whose DSC line was
  a copy of its ASC line, cut at ±66°, and a crossing distance was a difference of
  longitude within 3°.
- **Years and windows** (branch A, 1.3.3): solar return Y is the Sun's return nearest
  the birthday in Y (12:00 UT on the birth's local month and day, on the calendar in
  force; 29 February is 28 February in a common year), the birth year's being the
  birth instant; the Sun's ingress into a sign for Y is its one crossing in the UT
  year Y; and every year-based series (lunations, synodic cycles, great conjunctions,
  mundane aspects, the return and ingress timeline, the ingress series) runs from
  1 January 00:00 UT of its first year to 1 January 00:00 UT after its last,
  exclusive. Until 1.3.3 the return and the Sun's ingress were the first found from
  1 December and 1 July of the year before, and a series lost 31 December after 00:00.
- **Series labels** (branch B, 1.3.3), one line per rule:
  - *Sign entered:* a boundary crossed moving forward enters the sign beginning there;
    crossed retrograde (negative speed in longitude), the sign ending there, so backward
    over 0° Aries is Pisces. Every ingress list, the timeline's entries and the
    single-ingress chart; until 1.3.3 every crossing named the sign beginning there.
  - *Waxing and waning:* a mundane cycle's phase is faster minus slower (by mean sidereal
    period: Moon, Mercury, Venus, Sun, Mars, Jupiter, Saturn, Chiron, Uranus, Neptune,
    Pluto), 0–360°, waxing to 180°, whichever body is named first; the True Node keeps
    second minus first.
  - *Synodic period:* conjunctions are one cycle until the pair passes through an
    opposition; a period runs from one cycle's first pass to the next's, a later pass is
    "pass n of N", and the statistics take first passes only. Sun–Mercury, Sun–Venus and
    Mercury–Venus, never opposed, keep a cycle per conjunction.
  - *Solar eclipse figure:* the central phase's span over the whole Earth, printed as
    "Duration of the central eclipse (whole Earth)"; a lunar eclipse's is totality.
  - *Node flag:* a New Moon within 18° of the true node or a Full Moon within 12° is
    "Near a node (within 18°)" or "(within 12°)"; it measures distance, not eclipses.
  - *Rows and sections:* a solar eclipse's phase rows read "Central eclipse begins/ends
    (whole Earth)", and the lunation export's count and section "Lunations near a node".
  - *Span clamp:* `EphemerisSpan` holds the files' two instants; `ChartEngine` refuses
    outside them, and every year-based series clamps its window to them. A clamped
    window that is not whole days is scanned on to its next whole step inside the
    span (`EphemerisSpan.WholeDayScanEnd`), and what falls after its end is dropped.

## MCP Server Architecture

**Planned, not built.** There is no `AstroCast.Mcp` project (Stage 7 below); this section is the
design.

AstroCast.Mcp exposes the Core calculation engine as MCP (Model Context Protocol)
tools for AI-powered interpretation programs. Transport: stdio (local) or SSE (remote).

### Tool Catalog (initial)

| Tool                    | Description                                          |
|-------------------------|------------------------------------------------------|
| `calculate_chart`       | Full natal/event chart from input data               |
| `save_chart`            | Persist a chart to the database                      |
| `get_chart`             | Retrieve a saved chart by name or ID                 |
| `list_charts`           | Search/list saved charts                             |
| `calculate_synastry`    | Two-chart comparison with inter-aspects              |
| `calculate_transits`    | Transit positions and aspects to a natal chart       |

### Planned Extensions

| Tool                        | Description                                      |
|-----------------------------|--------------------------------------------------|
| `calculate_solar_return`    | Solar return chart for a given year              |
| `calculate_lunar_return`    | Lunar return chart for a date range              |
| `calculate_horary`          | Horary chart from question time and location     |
| `calculate_progressions`    | Secondary progressions to a target date          |

The MCP layer is thin — it maps tool calls to existing Core service interfaces.
All computation logic remains in AstroCast.Core.

## Database Schema (SQLite)

Single `Charts` table storing input data only:

| Column            | Type     | Description                              |
|-------------------|----------|------------------------------------------|
| Id                | INTEGER  | Primary key, autoincrement               |
| Name              | TEXT     | Person or event name                     |
| EventType         | TEXT     | Birth, Event, Horary, etc.               |
| EventDescription  | TEXT     | Optional description                     |
| LocalDateTime     | TEXT     | ISO 8601 local date/time                 |
| TimeZone          | TEXT     | IANA timezone identifier                 |
| UtcDateTime       | TEXT     | Computed UTC date/time, with `Z`          |
| JulianDay         | REAL     | Computed Julian Day (UT)                  |
| Latitude          | REAL     | Geographic latitude (decimal degrees)     |
| Longitude         | REAL     | Geographic longitude (decimal degrees)    |
| LocationName      | TEXT     | Human-readable location                   |
| Gender            | TEXT     | Unspecified, Male, Female or Other        |
| SourceNotes       | TEXT     | Optional source of the birth data         |
| UtcOffsetHours    | REAL     | Explicit UTC offset in hours, if any      |
| UseExplicitOffset | INTEGER  | 1 when that offset overrides the zone (default 0) |
| Notes             | TEXT     | Optional notes                           |
| CreatedAt         | TEXT     | ISO 8601 timestamp                        |
| UpdatedAt         | TEXT     | ISO 8601 timestamp                        |
| HoraryQuestion    | TEXT     | A horary chart's question                 |
| Keywords          | TEXT     | Optional keywords                         |
| HoraryCategory    | TEXT     | A horary chart's category                 |
| HouseSystem       | TEXT     | The house system's letter; null is Placidus |

Positions, aspects, and midpoints are computed on demand — never stored.

**Connections are unpooled** (1.3.3). `SqliteChartRepository`'s constructor keeps the caller's
connection string with pooling turned off (`new SqliteConnectionStringBuilder(connectionString)
{ Pooling = false }`), every other setting kept, so each call opens its own native connection,
about 0.2 ms more per call. Microsoft.Data.Sqlite's pool, 10.0.x through 10.0.12, can lend one
native connection to two `SqliteConnection` objects when two opens coincide (dotnet/efcore#39008).
On a shared connection `SaveAsync`'s `SELECT last_insert_rowid()` can read the other save's Id, and
a busy reset can roll back the other save's row: a wrong Id or a silently lost save.
`SqliteChartRepositoryConcurrencyTests` caught it, once in 157 Maple runs. No head and not the CLI
can reach it (every repository call completes synchronously, on the UI thread or in sequence), but
any concurrent caller would, the MCP server first. In 1.4 Microsoft.Data.Sqlite moves to 10.0.13,
which carries the fix (#39012), keeping the `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 pin; pooling
stays off (Steve's decision of 2026-10-11). The file database uses SQLite's default rollback journal
(`delete`); nothing sets WAL.

## Output Formats

### Human-Readable Text (the CLI smoke test)
```
Body          Position              House  Dir   Speed
------------  --------------------  -----  ---   ----------
Sun           10°22′08″ Capricorn     1    D        1.019/day
Moon          13°19′25″ Scorpio      10    D       12.021/day
```

### Structured JSON (AI-Ready)

Abridged from the golden `ChartJsonFormatter_ToJson_J2000NYC.golden.json`: every key as written
there, one element of each array, `...` for the rest. Non-ASCII characters are escaped, null
members are left out (`calendar`, `timeNote`, the explicit-offset fields), and a pre-1583 chart adds
`"calendar": "Julian"` after `utcDateTime`.
```json
{
  "chart": {
    "name": "Golden Test Chart",
    "dateTime": "2000-01-01T07:00:00.0000000",
    "timeZone": "America/New_York",
    "utcDateTime": "2000-01-01T12:00:00.0000000Z",
    "julianDay": 2451545,
    "location": { "name": "New York City", "latitude": 40.7128, "longitude": -74.006 },
    "gender": "Unspecified",
    "eventType": "Birth",
    "resolvedUtcOffsetHours": -5,
    "resolvedOffsetLabel": "EST (UTC\u22125)"
  },
  "positions": [
    { "body": "Sun", "longitude": 280.3689, "latitude": 0.0002, "sign": "Capricorn",
      "formattedPosition": "10\u00B022\u203208\u2033 Capricorn", "house": 1, "houseExact": 1.15,
      "speed": 1.0194, "direction": "D", "isRetrograde": false,
      "rightAscension": 281.2784, "declination": -23.0324 }
  ],
  "houses": {
    "system": "Placidus",
    "cusps": [ { "house": 1, "longitude": 274.242, "formatted": "04\u00B014\u203231\u2033 Capricorn" } ],
    "angles": { "ascendant": 274.242, "midheaven": 208.469, "descendant": 94.242, "ic": 28.469,
      "vertex": 134.2081, "armc": 206.4511 }
  },
  "aspects": [ { "body1": "Sun", "body2": "Saturn", "aspect": "Trine", "angle": 120, "orb": 0.0267,
      "isApplying": true, "category": "Major" } ],
  "dignities": [...],
  "moonCondition": {...},
  "chartContext": {...},
  "midpointActivations": [ { "midpoint": "Mars/Saturn", "midpointLongitude": 4.1795, "activator": "ASC",
      "activatorLongitude": 274.242, "harmonicAngle": 90, "orb": 0.0625,
      "notation": "Mars/Saturn = ASC (90\u00B0)" } ],
  "fixedStarConjunctions": [...],
  "planetConditions": [...],
  "moonMansion": {...},
  "planetaryHour": { "rulingPlanet": "Moon", "hourNumber": 12, "isDaytime": false,
    "hourStart": "06:07:57", "hourEnd": "07:21:44",
    "hourStartUtc": "2000-01-01T11:07:57.0000000Z", "hourEndUtc": "2000-01-01T12:21:44.0000000Z",
    "hourTimeZone": "America/New_York" },
  "arabicParts": [...],
  "antisciaPoints": [...],
  "antisciaConnections": [...],
  "stations": [],
  "parans": {...},
  "metadata": { "engineVersion": "AstroCast 1.3.4 (SwissEph 2.10.03, tzdb 2026e)", ... }
}
```

## Implementation Stages

1. **Planetary Positions** — Swiss Ephemeris P/Invoke, all body positions, full output ✓
2. **Houses & Angles** — cusps in the chart's house system, planet-to-house assignment by longitude ✓
3. **Aspects** — All aspect types, planet-to-planet and planet-to-angle, applying/separating ✓
4. **Midpoints** — All pair midpoints, activations per Ebertin method ✓
5. **Database** — SQLite storage, save/retrieve/search charts ✓
6. **Chart Engine & JSON** — Orchestration service (single call → full chart), JSON serialization ✓
7. **MCP Server** — Model Context Protocol server exposing calculation tools for AI integration
8. **WPF GUI** — Chart data entry, display, and management interface ✓

## Git Conventions

Use Conventional Commits:

| Prefix     | Usage                                    |
|------------|------------------------------------------|
| `feat:`    | New feature                              |
| `fix:`     | Bug fix                                  |
| `refactor:`| Code restructuring, no behavior change   |
| `test:`    | Adding or fixing tests                   |
| `docs:`    | Documentation only                       |
| `chore:`   | Build, config, dependency changes        |

## Versioning

- **Extension:** Automatic Versions 2/3 (Visual Studio) reads and increments
  `Version`, `AssemblyVersion`, and `FileVersion` directly in each `.csproj`.
- **Shared metadata:** `Directory.Build.props` at solution root provides
  Company, Product, Copyright, Authors, NeutralLanguage.
- **Per-project versions:** Each `.csproj` owns its own version block;
  Automatic Versions increments them on build in Visual Studio.
  `AstroCast.Avalonia` and `AstroCast.UiTests` are not registered with it. If
  this clone is opened in Visual Studio, register both, or a build there bumps
  the other six and leaves those two behind, and the release-bump audit then
  refuses to bump until they agree.
- **Installer sync:** `BuildInstaller.ps1` reads the assembly version from the
  published EXE and passes it to WiX via `$(var.ProductVersion)`.
- **Windows MSI (WiX 6 CLI, no `.wixproj`):** `BuildInstaller.ps1` publishes
  **self-contained** win-x64, generates `Components.wxs` by walking the publish
  tree **recursively**, and writes to a gitignored `dist\`. Three rules it
  encodes, each of which cost something to learn:
  1. **The harvest names no directory.** An earlier flat harvest of the publish
     root plus a hard-coded `ephe\` silently omitted **175 files (19.6 MB)** —
     the QuestPDF Lato fonts and 156 satellite resource assemblies. Adding a
     folder by name fixes one hole and leaves the next one open.
  2. **Component ids derive from the relative path, never the file name.** The
     payload ships 12 resource-assembly names 13 times over, once per culture
     folder; filename-derived ids collide and MSI drops files silently.
  3. **Self-contained, so no .NET launch condition is needed.** Its absence from
     `Package.wxs` is a consequence of that choice, not an oversight.

  The MSI is **unsigned** — Authenticode is a separate certificate from the
  macOS Developer ID and none exists. `UpgradeCode` must stay
  `890DAE32-060A-4B6A-99D4-D1C0F8229232` or upgrades install side by side.

  **The installer remembers its folder** (branch WindowsFit, 1.7.2; findings A6, N3
  and N19 of the 1.7.2 code audit, Steve's ruling 4: remember the folder from 1.7.2
  on, find 1.7.1's folder for this one upgrade, keep the folder page), all in
  `Package.wxs`; `BuildInstaller.ps1` and its three rules are unchanged. The Windows
  head keeps its database, logs and key file beside the program, and the folder page
  lets the user choose the folder. Until 1.7.2 nothing recorded the choice: the next
  installer offered the default folder again, and accepting it removed the old
  version from the chosen folder, left `astrocast.db` behind there, and the new
  version opened with no charts.
  1. **Record.** The component `InstallFolderRecord` writes the folder to
     `HKCU\Software\Thornhill Computer Software\AstroCast`, value `InstallFolder`,
     and `SetARPINSTALLLOCATION` gives Windows the same folder for its Apps list.
  2. **Read back.** A registry search puts that value in `RECORDEDINSTALLFOLDER`.
  3. **The one upgrade from 1.7.1, which recorded nothing.** A component search puts
     the folder of the installed component that holds `AstroCast.exe` in
     `FOUNDINSTALLFOLDER`. That component's GUID is generated by WiX from its install
     path, `2DB7EFBE-419D-5180-B965-3571232245F2` in the released 1.7.1 MSI and in
     every build since (read from both MSIs' Component tables); a component search
     with no Signature row yields the directory of the key file. If the install path
     in `Directories.wxs` ever changes, the GUID changes with it and this search no
     longer finds a later version; it must keep finding 1.7.1.
  4. **Order.** `SetInstallFolderFromRecord`, then `SetInstallFolderFromSearch`, both
     directly after `AppSearch`, in both sequences and run in the first; each sets
     `INSTALLFOLDER` only while it is unset. So the recorded value wins over the
     folder found, the default folder stands when there is neither, a folder passed
     on the command line wins over both, and both run before the folder page, where
     a folder the user types wins in its turn. A silent install lands in the same
     folder.
  5. **The Apps entry.** `ARPPRODUCTICON` is the program's icon (the `Icon` row
     `AstroCastIcon.ico`, from `AstroCast.ico` at the repository root),
     `ARPURLINFOABOUT` `https://www.sthornhill.com/AstroCast/` and `ARPHELPLINK`
     `https://github.com/sthornhill008/AstroCast/issues`.
  The `UpgradeCode`, the per-user scope, the folder page, the licence page and the
  shortcuts are unchanged. The recorded value is the installer's own and is removed
  on uninstall; the installer owns no database, log or key file (no row of the File
  or RemoveFile table names one), so an uninstall leaves them in place.
  **An installer is never run on Maple itself**, where Steve's own AstroCast is
  installed: the upgrade is tested in Windows Sandbox (`upgrade-test.ps1` and its
  `.wsb` in the branch's scratch folder; the script refuses any account but the
  sandbox's). Six cases, all passing on 2026-11-28: 1.7.1 in a custom folder, then
  the new MSI with no folder given (the program stays in the custom folder with a
  marker file named `astrocast.db` beside it, found by the component search); the
  same from the default folder; the new MSI over an earlier build of 1.7.2 in a
  custom folder (the recorded value read back, the search skipped) and once more
  over itself; the new MSI alone on a clean start (the default folder); an
  uninstall (the marker stays, the recorded value goes); and a folder given on the
  command line (it wins).
- **UI display:** Both heads' window titles and status bars show the version,
  through `MainViewModel.WindowTitle` and `MainViewModel.AppVersion`, and so does
  the About window (`AboutViewModel.VersionLine`, from `AppVersion`), which the
  status bar's About button opens.
- **Engine version:** `ChartEngine` reads `AssemblyInformationalVersionAttribute`
  and includes it in `EngineVersion` (e.g., "AstroCast 1.3.4 (SwissEph 2.10.03, tzdb 2026e)").

## Git Commit and Tagging Process

The procedure lives in the project skill `.claude/skills/release-bump/`: the bump
script, the read-back from the built `AstroCast.Presentation.dll`, the bump
commit's message, and the tag. Use it for every bump and every tag. This section
keeps only the rule that triggers it and the policy it carries out.

1. **Before committing,** compare the `<Version>` in `src/AstroCast/AstroCast.csproj`
   with the latest `v*` tag. If they are equal, this commit opens the next
   release: bump the patch first with the skill. Minor and major bumps are the
   user's call. If the version is ahead of the tag, a release is open: commit
   normally. If it is behind, stop and ask.
2. **Every versioned `.csproj` carries the same version** in four fields:
   `Version` and `InformationalVersion` as X.Y.Z, `AssemblyVersion` and
   `FileVersion` as X.Y.Z.0. There are eight such projects today. The skill's
   script counts them from the tree and refuses to write if any disagree.
3. **A bump is verified at the artifact, not in the `.csproj` text.**
   `MainViewModel.GetAppVersion()` reads the `InformationalVersion` of
   `AstroCast.Presentation.dll` for both heads' window titles and status bars,
   and each head deploys its own copy.
4. **The tag goes on the last commit of the release:** annotated,
   `v{Major}.{Minor}.{Patch}`, message `Release v{version}`, added once
   everything belonging to the release has landed. Then `git checkout v{version}`
   reproduces exactly what was built, tested and shipped. Anything landing after
   the tag belongs to the next version. This was settled at `v1.0.243`
   (2026-08-05). A pushed tag is never moved, so tags at or before `v1.0.242` stay
   on their bump commits.

## Verification Methodology (hard-won; read before verifying)

Eleven procedures below look like implementation detail and are not. Each was
established after a verification produced a confident, wrong answer.

### Test-name baselines: capture by header offset, never by regex

The suite's name list is compared byte-for-byte against a stored baseline. To
capture it, take **every non-empty line after the
`The following Tests are available:` header** in `dotnet test --list-tests`
output, sort with `[Array]::Sort($arr, [StringComparer]::Ordinal)`, and write
UTF-8 **without BOM** and LF-terminated.

Do **not** filter with an identifier pattern such as
`^\s+(AstroCast\.[A-Za-z0-9_.]+)\s*$`. It silently drops every xUnit theory
case, whose display name carries an argument suffix —

```
AstroCast.Tests.Formatting.ElectionalNatalHarmonyFilterTests.IsMajor_Should_ClassifyCorrectly(type: Conjunction, expected: True)
```

— and it also drops the three nested-class names, whose `+` separator falls
outside the character class —

```
AstroCast.Tests.Services.ChartContextServiceTests+IntegrationTests.Analyze_Should_ProduceValidContext_When_RealChartCalculated
```

— leaving a baseline that silently stops guarding the theory-driven tests.

**Counts, and the baseline each describes.** The left column is `master` at
`e3fec32`, tagged `v1.0.242`, immediately before the electional
candidate-starvation branch landed; the right is the tree at `v1.0.243`, which
added 24 names (8 of them theory cases) and removed none. Re-derive either with
`dotnet test tests/AstroCast.Tests --list-tests`, taking every non-empty line
after the header: a name is a theory case if it contains `(`, and a nested-class
name if it contains `+` before any `(`. The qualifier matters from branch TZ on:
17 theory cases carry a `+` in their arguments (offsets such as "UTC+3", zone
names such as "+06"), so a bare search for `+` counts 20 where there are 3.

| Figure                          | `e3fec32` (`v1.0.242`) | `v1.0.243` | after `fix/ut-local-date-boundary-20260805` |
|---------------------------------|------------------------|------------|---------------------------------------------|
| Total names                     | 2175                   | 2199       | 2218                                        |
| Theory cases (argument suffix)  | **296**                | **304**    | **316**                                     |
| Nested-class names (`+`)        | 3                      | 3          | 3                                           |
| Identifier regex keeps          | 1876                   | 1892       | 1899                                        |
| Identifier regex drops          | 299                    | 307        | 319                                         |

This entry previously recorded **299** as the theory-case count. It is not — 299
is the *total* the regex drops, being 296 theory cases plus the 3 nested-class
names. That is why the wrong figure still reconciled against "1876 of 2175" and
survived unnoticed. Count the two categories separately; a single drop total
cannot distinguish a regex that lost the theory cases from one that lost a
nested class.

Related: `-Encoding ascii` mangles the ten names containing `°`/`···` — all ten
are theory cases, so a capture that has already dropped the theory suffix will
not surface this — and `Sort-Object` is culture-aware where the baseline is
ordinal. Any of these produces a false diff or a false pass.

### Path verification on Maple must run out-of-container

Claude Desktop is an MSIX-packaged application. Any process launched from a
Claude Code session inherits its package identity, and Windows redirects that
process's `%LOCALAPPDATA%` and `%APPDATA%` into
`...\Packages\Claude_pzs8sxrjxfjjc\LocalCache\`. The redirection is
transparent in both directions — querying the real path and the redirected
path returns the **same NTFS file id** — so a path check made from inside a
session cannot distinguish the user's real profile from the container's
private copy.

This is not hypothetical: Phase 4 slice 1 recorded an "orphaned Roaming
database" in the user profile that does not exist there and never did.

To verify a path for real:

1. Launch the application via `explorer.exe "<path-to-exe>"`, which starts it
   under the shell without package identity.
2. Inspect the filesystem from a probe started with
   `Invoke-CimMethod -ClassName Win32_Process -MethodName Create`, which runs
   under `WmiPrvSE` outside the container.
3. Have that probe write its output to a location that is never redirected —
   anywhere on `D:` — and read the result back from the session.

Relative conclusions ("it resolved to the roaming branch, not the local one")
survive the container; absolute paths do not.

### Synthetic clicks: verify which control acted, not merely that one did

A synthetic click can be displaced by concurrent physical mouse use. During the
Phase 4 slice 6 macOS verification a click computed for row 1 of the saved-charts
list landed roughly 171 px lower and loaded row 4. The click "succeeded" — a
chart loaded, the UI responded, and nothing in the output said the wrong row had
been used.

This is the **fourth** recorded instance of synthetic input misbehaving in this
project, after the slice 3 `SendKeys` failure, the Mapsui probe's viewport, and
the container rule below. It is the first where the failure could have destroyed
data: each row's Delete button sits about 34 px from its Load button, so the same
displacement that loaded the wrong chart could have deleted one.

Two rules follow:

1. **Assert which control acted**, by reading back a property that identifies it
   (the loaded chart's name, the field the text landed in), never by observing
   that *something* happened.
2. **Keep synthetic clicks away from destructive neighbours.** Prefer a UI
   Automation `InvokePattern` on a named element, which addresses the control
   directly and cannot be displaced by the pointer. Where a click is
   unavoidable, drive it when the machine is idle and verify before proceeding.

### On macOS, rule 2 is unavailable — Avalonia exposes no `kAXPressAction`

The preferred mitigation above assumes a press action exists to invoke. In the
Avalonia head on macOS it does not. Measured during Phase 5 slice 5a: of **390**
elements in the window subtree, exactly **4** advertise any accessibility action
at all — the window's `AXRaise` and the three OS traffic-light buttons. Every
application control advertises none, so there is nothing to press through the
accessibility API and no guarded equivalent of `InvokePattern`.

**The working split is CGEvent to act, AX to verify.** Synthesize the click,
then read the resulting state back through the accessibility tree — the value a
field now holds, the chart a status line names. That still satisfies rule 1,
because the verification is a semantic property read from the control rather
than a screenshot, and it keeps the failure mode visible: if the click lands
somewhere else, the read-back does not show the expected change.

Do not conclude from a missing press action that the element is not a control,
and do not fall back to comparing screenshots — that is the observation rule 1
exists to forbid.

### A build reporting errors may be file locks, not code

During slice 6 a `dotnet build` reported **8 errors** that were entirely
`MSB3027`/`MSB3021` file-lock failures from three application instances left
running by an earlier measurement pass. The output is indistinguishable from a
real compilation failure at a glance, and the natural response — reading the
code for a mistake that is not there — costs real time.

Close every running head before believing a build failure, and re-run. Only a
failure that survives with no `AstroCast` or `AstroCast.Avalonia` process
running is a code failure.

### A mutation that misses its target reports the assertion as weak

A mutation proves an assertion is load-bearing by breaking the thing it guards
and watching the test fail. When the mutation lands somewhere inert the test
stays green — and the conclusion drawn is about the wrong thing entirely: it
reads as "the assertion is weak" when the truth is "the mutation missed".

Phase 4 slice 7 hit this exactly. The `HorizontalContentAlignment="Center"`
setter is textually identical on `Button.nav` and `Button.action`, so a
first-occurrence replacement mutated the nav style — which is inert, because
the nav template hardcodes `Center` on its own presenter — and the
caption-centring tests were right to stay green.

**Anchor every mutation to the specific style block, class, or element it is
meant to hit, and confirm from the mutated file that it landed there** before
believing anything about the test. A green suite after a mutation is evidence
only once the mutation is known to have changed the code under test. This is
the same "verify which driver acted" shape as the synthetic-click rule above,
and the shape the tests in this port are themselves written to enforce.

### Scrolling is synthetic input too: a `DatePicker` eats the wheel

A wheel event delivered over a `DatePicker` is consumed by the control and
**mutates its value** — the page does not scroll, and a date silently changes.
Found during the Phase 4 slice 9 macOS measurement, this is the **eighth**
recorded instance of synthetic input misbehaving here (the section above records
the fourth, which was the count when it was written), and it is a new *form* of
the failure: not a displaced click and not an autocompleting field, but
scrolling changing state.

**Choose scroll points over inert regions.** The label-column strip contains
only `TextBlock`s and worked reliably for the whole panel. Before scrolling a
panel that contains date pickers, combo boxes or numeric spinners, pick an x
that cannot land on one.

**The ninth member: a synthetic input channel can stop working mid-session.**
During Phase 5 slice 5b, CGEvent unicode typing silently stopped delivering.
Clicks still registered and fields still took focus, but no characters arrived
and the event log showed no activity; `System Events ... keystroke` worked
immediately in the same state. Nothing announced the failure. **This is
invisible unless the field is read back after every write** — which is the
cheapest possible guard and the reason to do it unconditionally rather than only
when a write looks suspicious.

### Read a control's semantic value, not its display text

Setting a `CalendarDatePicker`'s inner `PART_TextBox` through the accessibility
API **does not commit the binding**: the text updates while `SelectedDate` stays
null. The value must be typed and committed with Return.

This sharpens the read-back rule from the synthetic-click section above. Reading
the text back would have confirmed the write and still been wrong about the
state, because the two had diverged — the display said one thing and the bound
property said nothing at all. **Verify against the property the application
actually reads**, not against what the control is showing.

### Verify the effect, not the report — a property is only a claim

The rule above says to trust the semantic value over the display. Phase 5 slice
5b found the **inverse**, which is what fixes the rule's real shape.

Finder's AppleScript `icon size` property accepted a write of 512 and **reported
512 back on read** — and changed nothing. Captures at 64 and 256 were
byte-identical, while the slider's true value read **24 of 112** through the
accessibility API. There the display lied and the value was truthful; here the
property lied and the display was truthful.

So neither is privileged. **A property and a display are both claims about
state; only the observable outcome is authoritative.** After any write, verify
by the thing the write was supposed to change — the rendered pixels, the exported
file, the recomputed result — not by reading back the channel you wrote through.
A successful read-back of your own write is the weakest evidence available,
because it is exactly what a no-op also produces.

### AX value-writes do not run handlers; AX actions do

Setting `AXValue` on Finder's icon-size slider did nothing, while `AXIncrement`
worked. This is the same shape as the `CalendarDatePicker` `PART_TextBox`
finding above, where setting the text left `SelectedDate` null and only typing
with a committing Return took effect.

**Two instances now, so treat it as an idiom rather than a coincidence:** an
accessibility *value* write often reaches the control's storage without running
the change handling that gives the write its meaning, while an accessibility
*action* goes through the same path a user does. **Prefer the action** —
`AXIncrement`, `AXPress`, a committing keystroke — and where only a value write
is available, verify the effect per the rule above. (On macOS this head
advertises almost no actions at all — see the `kAXPressAction` section earlier
in this chapter, which is why CGEvent does the acting there and AX only the
verifying.)

### `ToggleButton` derives from `Button` in Avalonia and `ButtonBase` in WPF

So `OfType<Button>()` returns check boxes in the Avalonia head and does not in
WPF. A control census carried over from WPF thinking counts too many here — in
the Phase 4 slice 9 Returns panel, two too many. Check the base class before
writing any `OfType<>` sweep in a test or a driver; the two heads do not agree
on what a `Button` is.

## Current Status

- **Release:** 1.3.4 is tagged `v1.3.4` (2026-10-15, on `dece41a`), its DMG notarized. 1.3.5 is
  tagged `v1.3.5` (2026-10-15, on `1527b3f`), its MSI built, its DMG not shipped: the app menu
  said Avalonia. 1.3.6 is tagged `v1.3.6` (2026-10-15, on `ca94ea3`, branch MacMenu), its DMG
  notarized. 1.4.0 is tagged `v1.4.0` (2026-10-17, on `bc9c8f6`, after branches Panels, Relocate,
  Arcs, Modes and ModesPanel), its DMG notarized; a minor bump, the first release that reshapes the
  panels. 1.5.0 is tagged `v1.5.0` (2026-10-19, on `46a34a8`, after branches Primary, PrimaryExport and
  PrimaryPanel), its DMG notarized; a minor bump, the release that adds primary directions (the engine, its
  exports, and the Directions panel with the Transits panel's fourth box; see *Primary directions* above and
  *Directions panel* below). 1.6.0 is tagged `v1.6.0` (2026-10-20, on `fe6e238`, after branches Asteroids,
  AsteroidsExport, AsteroidsTiming and AsteroidsPanel), its DMG notarized; a minor bump, the release that adds
  Ceres, Pallas, Juno, Vesta and Black Moon Lilith as minor points behind a session switch (Asteroids, the
  engine, landed on `aae88e4`, AsteroidsExport, the five in the exports, on `5537b64`, AsteroidsTiming, the
  five as timing targets, on `90eca0d`, and AsteroidsPanel, the Points row and the recast on toggle, on
  `fe6e238`; see *Minor points (asteroids and Lilith)* above). 1.6.1 is tagged `v1.6.1` (2026-10-21, on
  `970af18`, after branches StaleResult, ElectionalPicker and TiesAndGuards), its DMG notarized; a patch, the
  small fixes of 1.5.0 and 1.6.0, the eight items A to H of the 2026-10-21 scoping (A and B landed on
  `7400142`, C, D, E and H's test-double fold with Clear Form emptying both pickers on `019303e`, F, G and H's
  comments on `970af18`). 1.7.0 is open: `<Version>` 1.7.0 in all eight csprojs since branch Davison's first
  commit (`6383fea`); a minor, the Davison chart (Steve's eight topics of 2026-10-21): branch Davison, the
  engine and the export (see *Davison chart* above), landed on `d85441f`; then DavisonPanel, the Synastry
  panel's section in both heads (see *Davison section* below), landed on the Mac, awaiting Maple; then the
  library run (P30 from P13, the manifest and the usage map); then the tag `v1.7.0`, the MSI and the DMG.
  1.7.0 is tagged `v1.7.0`. 1.7.1 is tagged `v1.7.1`, after branches Fixtures, SvgExclude, Public,
  UserGuide, GuideWin and NoKey: the first public release, the tree of the public repository's `99245d3`.
  1.7.2 is open: branch Prompts (2026-11-14), the prompt library in `prompts/` with
  `docs/PROMPT_LIBRARY_GUIDE.md`, then branch Review, the read-through before the repository turned public;
  then branch InputChecks (2026-11-27), the first branch of the 1.7.2 plan: seven input and message fixes
  from the 1.7.2 code audit (see *Input Checks* below), made on Maple, landed on `e7fd2be` and pushed the
  same day; then branch StaleResults (2026-11-27), finding N2 of that audit: every result made from the Chart
  panel's chart clears when that chart changes (see *Stale Results* below), then branch ReturnAges
  (2026-11-27), finding A1 of that audit: a solar return export reads its three time-lord sections for the
  year the return opens (see *Return Ages* below); both made on Maple, landed together on `655bff5` and
  pushed the same day; then branch ExportText (2026-11-27), fourteen text faults in the exports from the
  code audit and the prompt audit (see *Export Text* below), made on Maple, landed on `a328c28` and pushed on
  2026-11-28; then branch DirectionNotes (2026-11-28), finding A5 of the code audit: a primary direction
  whose date turns is marked, a missing Range says why, and the natures of the promissor stars are listed
  (see *Primary directions* and *Primary directions export* above), with the medical export's "Not medical
  advice" line moved into its header, and the two footnote wordings of Steve's rulings of 2026-11-28 on
  `469f6ef`; then branch WindowsFit
  (2026-11-28), four things a Windows user meets: the Saved Charts buttons kept in view beside a long row
  (A9; see *Head Parity*), the no-key message naming the key file's path (A7; see *Base Map*), and the
  installer remembering its folder and filling in the Apps entry (A6, N3, N19; see *Versioning*); both made
  on Maple, landed together on `ff85374` and pushed on 2026-11-28; then branch EphePath (2026-11-28), finding
  N4 of the code audit, the last code branch of the 1.7.2 plan: the ephemeris folder's path reaches the
  library whole on Windows, and each program checks its ephemeris files at start-up and says plainly when
  they cannot be read (see *Swiss Ephemeris Conventions*); made on Maple, landed on `fa67061` and pushed on
  2026-11-28; then branch Library172 (2026-11-28), the prompt library run of the 1.7.2 plan: fifteen prompts
  re-versioned with their guides and histories, the guides of P23 and P24, the manifest, the usage map, the
  library's README and the Prompt Library Guide (see *Documentation*), with no change to the program, its
  tests or its goldens (the totals stand at 4626 and 149); made on Maple, awaiting its landing. The guide's
  PDF, the library zip and the website's prompt links are for branch Docs172 and the release.
- **Landed for 1.3.4:** every code branch, D, G, F, O-D3, R2, R3, H, K2, L2, L3, O-6 and O-8,
  then Docs-2; the prompt library's pass followed the tag.
- **In 1.3.5:** branch Licence, AstroCast under the AGPL with an About window in both heads; the
  four items under *Licence* are open before AstroCast is shared. Stage 7, the MCP server, is not
  built.
- **In 1.4.0:** branch Panels (see *Panels* below): the Natal panel's new order, the chart pickers
  kept across a refresh, and five fixes from `MACOS_PORT_PLAN.md` §9 (items 6, 7, 9, 10 and 16, with
  item 8 closed by binding `AllSavedCharts`), and finding E3-6, raw values on screen, fixed on screen
  (the exports unchanged); then branch Relocate (see *Relocation panel* below), Steve's item 3: the
  Relocation panel reads as one birth chart, then a new place, and Use Natal Location leaves it.
  Item 2, the Transits panel's three checkboxes, has its own branch, after these.
- **Known issues:** every defect recorded here is **fixed**, and 1.3.4 leaves two groups open on
  purpose, the last two items below. Fixed: the two UT-vs-local date-boundary defects
  (`fix/ut-local-date-boundary-20260805`); in 1.3.2, the truncating JD→DateTime converters and
  the machine-zone reload of saved charts — see *Date and time boundaries* below — and the
  return-type chart header, which now labels an explicit-offset chart's wall clock with that
  offset (`UTC+0 (explicit)`) instead of the natal zone; in 1.3.3, an explicit-offset chart's
  `UtcDateTime` written from the input's own kind (no `Z`, or the saving machine's offset), by
  branch F5 — see *Time Inputs* below — the electional search's own boundary defects (a sample
  on the range's end bound, which the index arithmetic below reduced and did not remove, a
  retrograde cut at the range's ends, and every comparison made on the local clock), by branch
  O1 — see *Date and Time Boundaries* below — and the two that follow, each left for its own
  branch so it stayed independently verifiable, both surfaced by the UT-vs-local boundary fix.
  - `ElectionalService.SelectFullScorePoolIndices` — **fixed** in 1.3.3 by
    branch O2, and removed with its tests. The search scores every eligible
    sample of its grid from the scorer's inputs alone
    (`IChartEngine.CalculateElectionalScoringInputs`: the positions, houses,
    dignities and chart context, from the full chart's own calls in its order)
    and casts the full chart only for the instants it reports, so the first
    candidate instant is always the best eligible sample, the earliest on a tie.
    It costs about 8% more than the pool did (×1.08 on the 26 real re-runs,
    ×1.09 on six 120-day searches). Until 1.3.3 only the pool was fully scored:
    the top `CandidateCount × 15` samples by a Moon-only quick score, plus the
    best of each separation-width bin measured from the earliest surviving
    sample, so shifting the window re-phased every bin, and a moment outside the
    pool was invisible however well it would score. Correcting the scan bounds
    cost the NYC golden a moment scoring 86 ({88, 86, 82} became {88, 82, 82}),
    and the O investigation found a better sample never scored in 6 of its 26
    real re-runs (R01: 76.5 where 83.5 was there) and 187 of 324 standard
    searches. The NYC golden now returns {88, 86, 84}, and Richmond's second
    candidate is Thu 11 Jun 9:00 PM (80). Introduced with the anti-starvation
    pool in `25d9a16`.
  - `ElectionalService` void-of-course look-back — **fixed** in 1.3.3 by branch
    C. The electional scan passes its bounds straight to `VoidOfCourseScanner`
    with no look-back, and until 1.3.3 a void already in progress at
    `RangeStart` was never emitted and could not disqualify candidates: the
    scanner dropped, rather than truncated, a period whose opening aspect lay
    outside the scanned interval. The scanner now returns every period that
    touches its range with its true bounds, so the search from 2026-04-25 in
    New York disqualifies every sample from local midnight to 21:04, the Moon
    void in Leo since 18:20 the evening before.
  - *Left open in 1.3.4, between the heads* (see *Head Parity*): the map package (WPF
    `Mapsui.Wpf` 5.0.2, Avalonia `Mapsui.Avalonia12` 5.1.0); H-06, each head's own database
    folder; and H-07, an Avalonia save to a place with no local path reading as a cancel.
  - *Left open in 1.3.4, from branch D (noted, not fixed):* the transit-declination rows are
    sampled at every listed pass, before and beyond the report too; the orb at report start
    and applying/separating are measured to the nearer of an aspect's two points; a body's
    transits sort by natal target, then aspect, so a second passage of one aspect prints after
    other aspects' first passages; a pass exactly at a 12:00 UT sample, a double crossing
    between samples and an ingress and return within one day are not seen; a station's
    before/beyond flag uses the detector's slowest-day date; the timeline's Events table sorts
    an ingress at 12:00 of its date; and the span-edge noon fallback.
- **Total tests:** 4626 in `tests/AstroCast.Tests` and 149 in
  `tests/AstroCast.UiTests` at EphePath, all passing on Windows (4612 and 149 at WindowsFit, 4604 and 146 at
  DirectionNotes, 4563 and
  146 at ExportText, 4504 and 146 at ReturnAges, 4476 and
  146 at StaleResults and 4411 and 145 at InputChecks, on Windows too; 4297 and 145 at SvgExclude,
  all passing on macOS; 4296 and 145 at Fixtures
  and at DavisonPanel; 4287 and 142 at Davison;
  4257 and 142 at TiesAndGuards;
  4251 and 142 at ElectionalPicker;
  4240 and 141 at StaleResult;
  4229 and 140 at AsteroidsPanel;
  4212 and 138 at AsteroidsTiming;
  4178 and 138 at AsteroidsExport;
  4118 and 138 at Asteroids;
  4080 and 138 at PrimaryPanel;
  4049 and 128 at PrimaryExport;
  3957 and 128 at Primary; 3926 and 128 at ModesPanel;
  3859 and 125 at Relocate; 3852 and 122 at Panels;
  3813 and 105 at MacMenu;
  3813 and 101 at Licence; 3802 and 96 at Docs-2;
  3801 and 96 on both Windows and macOS at O-8). Track the
  two figures separately and never sum them: they are separate projects invoked
  as separate `dotnet test` runs, and a combined number hides which suite moved.
  (This line read "422+" until `v1.0.243` — stale by roughly 1,800 tests,
  because it was never updated after the suite grew.)

## Date and Time Boundaries (hard-won; read before touching a scan window)

Three defects of the same family were fixed together on
`fix/ut-local-date-boundary-20260805`. They are recorded because the pattern
recurs and because two of them were invisible until the first was fixed. The
fourth item records branch O1's rule for the electional search: compare instants.

1. **A local calendar date is not a UT hour.**
   `IEphemerisService.DateToJulianDay` reads its hour argument as **UT**, so
   passing a user-picked calendar date with hour `0.0` denotes midnight in
   Greenwich, not local midnight — 20:00 the previous evening in
   `America/New_York`, 09:00 the same morning in `Asia/Tokyo`. Use
   `JulianDayUtility.LocalMidnightToJulianDay`, which converts through the
   target zone on the app's own tzdb by the same rule as
   `ChartEngine.ConvertInputToJulianDay` (D17: a repeated midnight is the later
   instant, a skipped one the first instant after the gap; see *Time-Zone Data*
   below). **Convert each bound on its own date**: a range
   spanning a DST transition has a different offset at each end, and resolving
   one offset for both displaces *and* resizes the window. The lunar-return range
   was a further instance until branch TZ: its picked dates were read as UT
   (T0-2), and `LunarReturnService` now reads them on the return chart's clock.

   **A day's edge is its first instant** (branch O1, 1.3.3). The electional
   search takes each bound from `JulianDayUtility.LocalDayStartToJulianDay`: the
   first instant the date's midnight reads, the **earlier** of a repeated
   midnight and the first instant after the gap of a skipped one, so the hour
   between two midnights belongs to the date it is dated. Until 1.3.3 it used
   `LocalMidnightToJulianDay`, whose D17 later instant is right for a chart's
   reading and wrong for a day's edge: Havana's range ending 31 October 2026
   scanned 49 hours, into 1 November, and a range starting 1 November lost its
   first hour. `LocalMidnightToJulianDay` keeps D17. The daily horoscope takes its
   day edges from `LocalDayStartToJulianDay` too since branch E (1.3.3), which
   left `LocalMidnightToJulianDay` with no caller in `src`.

2. **`VoidOfCourseScanner` returns every period that touches its range, with
   its true start and end** (branch C, 1.3.3). A stay under way at the range's
   start is searched from its own sign entry, and one under way at its end to
   its own sign exit, so no caller pads or looks back. Until 1.3.3 it dropped
   them: it emitted a period only when both the opening aspect and the closing
   sign ingress fell inside the scanned interval, so a period overlapping either
   end vanished entirely and the day rendered "VoC: None" while the Moon was
   void.
   `MercuryRetroDetector.FindRetrogradePeriods` keeps the same rule since branch
   O1 (1.3.3): a retrograde under way at either end runs from its own station to
   its own station, never read outside `EphemerisSpan`. Until then it was cut to
   the range, and search K100 (2026-10-28 to 11-03) printed the range's two
   midnights where the stations are 24 October and 13 November. Each station is
   bisected to a cell of 1e-6 day on a lattice fixed in time (whole multiples of
   it from Julian Day 0) and its instant taken to the second, so a station prints
   on its own minute and the same station gives the same second from any range
   that reaches it. Until the O1 fix-up the scan's own bracket was halved to
   0.0007 day, about ±30 s by where the scan began: K100's station retrograde
   printed 3:13 AM for Tier B's 3:12 AM EDT. A plain bisection to 1e-6 day is not
   enough: from the bracket a scan hands over it still moves 374 of the 7,552
   stations of 1201–2398 by a second with where the scan began (simulated on
   Tier B). On the lattice, swept over the span from three starts, no station
   moves; 7,390 are Tier B's second and 162 a second off. Two lie within 0.03 s
   of a minute's rounding edge and print a minute off Tier B:
   2020-10-14 01:04:59.5 UT and 2028-05-21 08:42:59.5 UT.

3. **Sub-second error becomes a wrong day at a date boundary.** Two instances,
   both of which became wrong *dates* once bounds landed on local midnight:
   - `JulianDayUtility.ToDateTimeUtc` truncated a fractional hour carrying
     floating-point error (04:00 UT returns as `3.9999999962747097`), promoting
     ~13 microseconds to a full second. It now rounds.

     **Which values bite — measured, not reasoned about.** All 96 quarter-hour
     slots of 2026-04-20 swept through the round trip: 32 decompose bit-exactly
     (the multiples of 45 minutes — 45 minutes is 1/32 of a day, a dyadic
     fraction the double holds); the other 64 carry an identical
     13.411045074462891 microseconds and differ only in **sign**. The 32 early
     ones are exactly the 32 truncation mislabels; the 32 late ones it survives.
     Rounding is correct at all 96. Swept on Windows x64 and on macOS arm64 with
     identical results — same 64 inexact slots, same 32 early ones, same probe
     value — so this is IEEE 754 arithmetic, not a platform trait.

     This entry previously recorded that `00:00, 04:15, 08:15, 13:30, 17:45`
     and `23:45` "decompose exactly". Only `00:00`, `08:15` and `13:30` do.
     `04:15`, `17:45` and `23:45` carry the same error as `04:00` but late
     rather than early, so truncation happened to survive them — which is what
     made them look exact.

     **Severity, corrected.** `caf09c4`'s commit message called this "cosmetic
     almost everywhere, decisive at a midnight instant", and this entry said
     every export time was "one second early". Both understate it. Grid times
     carry zero seconds, so a second rolled backwards crosses the **minute**
     boundary too: roughly a third of grid-derived times printed a *minute*
     early at the precision the exports display — `:14` for `:15`, `:59` for
     `:00`. Midnight is where it additionally became a wrong *day*.

     **Twelve more converters truncated the same way, until 1.3.2.**
     `TransitEphemerisHelper.JulianDayToDateTime` and eleven private
     `JulianDayToDateTime` copies in the return, timeline, ingress, eclipse,
     lunation, great-conjunction, synodic and mundane services printed every
     transit, return, ingress, lunation, eclipse and cycle time up to a second
     early, and an event at an exact minute a whole minute early (01:00:00 UT as
     00:59:59). The copies are gone and every call site now goes through
     `JulianDayUtility.ToDateTimeUtc`; the helper stays public and delegates to
     it. **Do not add another converter.** Six services cast a whole chart at the
     converted instant — solar, lunar and planetary returns, ingress, lunation
     and great-conjunction charts — so those charts were cast up to a second
     early, angles off by up to 17″, and are now cast at the rounded instant.
     The golden normaliser had masked every `YYYY-MM-DD HH:MM:SS UTC`, hiding the
     second-precision event times along with the run-time stamp; it now masks
     only the `**Generated:**` header and `*Generated by … at …*` footer, so
     event times are guarded. `EventTimeRoundingTests` pins one event per family
     at an exact-minute instant.
   - `ElectionalService`'s scan loop accumulated `jd += scanStep`, drifting far
     enough over several hundred steps to admit one extra sample sitting on the
     exclusive end bound — a moment dated the day after `RangeEnd`. Sample
     Julian Days are now computed from the index. That reduced it and did not
     remove it: the last index's Julian Day can still land a few ulps below the
     end bound and round, as an instant, to that midnight — 71 of 716 searches
     in the O investigation, reported in 6, among them K100's only window,
     "Wed 04 Nov 2026 12:00 AM", inside a Mercury retrograde to be avoided. Since
     branch O1 (1.3.3) a sample is admitted only while its instant, taken to the
     second, is before the end bound's.

   **Corollary for verification.** A fix that moves scan bounds onto local
   midnight will expose latent rounding bugs that nothing previously caught,
   because midnight is the only place where a rounding error changes a date.
   Sweep a range of request counts, not one: until 1.3.3 the stray sample appeared
   only once the full-score pool reached far enough to select it. Since branch O2
   every eligible sample is scored.

4. **Compare instants, not local readings** (branch O1, 1.3.3). In an hour the
   clocks repeat a local reading names two instants; across a change of the
   clocks a difference of readings is off by the change. So
   `VoidOfCoursePeriod` and `DisqualifiedPeriod` carry their UTC instants
   (`StartUtc`, `EndUtc`) beside their readings; the electional search
   disqualifies a sample when its instant lies in `[StartUtc, EndUtc)`, the
   evaluator finds the void that holds an instant by the instant, and the
   separation filter measures the candidates' instants (`Chart.UtcDateTime`).
   Until 1.3.3 each compared readings: void samples were admitted at St. John's
   and Havana on 5 November 2028, samples an hour clear of a void excluded at
   St. John's, Sydney and Cairo, and 8 of 80 spring-forward searches kept two
   windows less than six hours apart. Where a printed reading repeats, the export
   names its offset ("Sun, 01 Nov 2026 1:30 AM EDT": the candidate heading and
   the two period tables). Time limits and allowed days stay on the wall clock,
   inclusive at both ends: a limit covering a repeated hour admits both passes.

**Golden diffs: separate artifacts from corrections.** Moving a scan floor
re-phases the grids anchored to it. Until 1.3.3 void-of-course *start* times
shifted by up to an hour (a one-hour aspect grid) and *end* times by up to a
minute (the binary-search bracket for a sign ingress); since branch C both are
event times that do not depend on where a scan starts, and a moved floor does
not move them.
Such a shift is neither a fix nor a regression — the underlying instant is
unchanged — and should not be read as one. Genuine
corrections look different: a period newly present, or one no longer wrongly
absent.

**A stored local time is a wall-clock reading, not an instant.** The clock part
of `ChartEntity.LocalDateTime` is the chart's own date and time in its own zone
(`ChartEntity.TimeZone`). Rows saved before 1.3.2 may also end in an offset such
as `-04:00`: that is the offset of the machine that saved the row, stamped
because the value was a local-kind `DateTime`, and never the chart's own. Read
the clock as written and ignore any suffix — `ChartMapper.ParseStoredLocalDateTime`
does, for `ChartMapper.ToInput` and `ChartEntryViewModel.PopulateFrom` alike —
and write it with `DateTimeKind.Unspecified` so no suffix is stamped.
`DateTime.Parse` converts a suffixed string into the machine's zone: on any
machine outside the zone that saved it, the chart loaded hours off, and a
re-save persisted the shift.

## Time-Zone Data (the app's own tzdb)

Every chart time the app converts goes through one seam, `ITimeZoneRules`, whose
only implementation, `TzdbTimeZoneRules`, reads the IANA time-zone database
embedded in NodaTime: a chart's wall clock to its instant, and any instant back to
a zone's clock (stations, horary times, the Hour Period, void-of-course and
retrograde periods, electional samples, the daily horoscope). The operating
system's zone data is never consulted. It had been: macOS's zoneinfo lagged IANA
(2026c on the Mac when branch TZ landed, against IANA's 2026e), and the Windows
registry keeps no history at all, so the same chart cast to different instants on
different machines. `TimeZoneInfo` remains only as `TimeZoneInfo.Local`, to find a
new chart's default zone.

- **Version.** NodaTime 3.3.5, embedding tzdb 2026e (`ITimeZoneRules.DataVersion`).
  Every export's engine version names it: "AstroCast 1.3.4 (SwissEph 2.10.03, tzdb
  2026e)". The goldens' `\(SwissEph [^)]+\)` mask covers the whole parenthesis.
- **How it reaches the code.** The converting services, and those that print an
  engine version, take the rules as an optional last constructor parameter;
  omitted, each builds `new TzdbTimeZoneRules()` over the same embedded data, so
  neither head passes anything. `MainViewModel.TimeZoneRules` is the form's single
  instance. `ChartResult.TimeZoneRules` records the rules a chart was cast with,
  and its exports and horary analysis convert its other instants on them. Static
  helpers (`JulianDayUtility`, `ChartMapper.ToEntity`) take them as a parameter. There is no static instance: the no-singletons rule.
- **D17: a reading the clocks repeat or skip.** A wall clock that occurs twice
  (clocks going back) is the **later** instant; one that never occurs (clocks going
  forward) is the **first instant after the gap**. The chart engine, the save path,
  `JulianDayUtility` and the form all apply it. `ChartResult.LocalTimeMapping`
  (`Unique`, `Repeated`, `Skipped`), `ResolvedLocalDateTime` (03:00 for a skipped
  02:30 in New York) and `EarlierUtcDateTime` (a repeated reading's other instant,
  from the zone's own record) record what happened, and the export says it in the
  Time Note (see *Time Inputs* below); the offset and label are those in force at
  the instant ("EDT (UTC−4)", where it used to print "UTC−4.5").
  An electional candidate is cast at the instant it was sampled at
  (`IChartEngine.CalculateForElectional(input, utcDateTime)`), never converted back
  from its wall clock, which in a repeated hour names two instants.
- **Before a zone's first transition** the data gives IANA's local mean time of the
  zone's reference city, to the second: New York before 1883 is −4:56:02, labelled
  "LMT (UTC−4:56:02)". .NET had moved it to a whole minute.
- **The offset label** is `OffsetLabelFormatter`'s, shared by the engine and the
  entry form: the zone record's name when it is a word ("EST", "BST", "LMT"; not
  "+06"), then the offset as hours on the half-hour grid ("UTC−5", "UTC+5.5") and as
  h:mm or h:mm:ss otherwise ("UTC+5:45").
- **Identifiers.** A tzdb identifier or alias resolves in any letter case; a Windows
  identifier converts through NodaTime's CLDR mapping, with one log line; anything
  else raises `TimeZoneNotFoundException` naming the identifier and the data
  version. There is no fallback to UTC or to the machine's zone: it would give a
  silently wrong instant.
- **The zones offered** are zone.tab's, 418 in 2026e, identical on every platform
  (`TimezoneProvider.GetTimezoneIds`). Windows had offered 131, without
  America/Vancouver. A place lookup's answer is kept if offered, else its canonical
  zone if offered, else left in the field for the user
  (`TimezoneProvider.FindOfferedZone`). A new chart's default zone is the machine's,
  mapped into the list (`TimezoneProvider.FindMachineZone`): first the zone
  `/etc/localtime` links to on macOS and Linux, then `TimeZoneInfo.Local`, a Windows
  identifier through CLDR; blank if not offered. On this Mac `TimeZoneInfo.Local.Id`
  reads "EST5EDT", which tzdb 2026d made a zone of its own on US federal rules, not
  New York's before 1966. "Set to now" reads the current instant on the chart's own
  clock, its explicit offset's or its zone's, never the machine's.

**Updating the data.** Rebuild with a newer NodaTime; no data file ships beside the
ephemeris.

1. Bump `NodaTime` in `src/AstroCast.Core/AstroCast.Core.csproj`, its only
   reference, to a release that embeds the new tzdb.
2. Regenerate the truth table at that release. In a virtual environment holding only
   the matching `tzdata` package, set `EXPECTED_IANA` and `EXPECTED_TZDATA` in
   `tests/AstroCast.Tests/Fixtures/TimeZoneVectors/make_tz_vectors.py` and run it.
   It refuses any other release, and asserts the 2026e set sizes (1,612, 1,822,
   1,044): a release that adds or removes a transition in the 13 zones changes the
   edge count, so update that assertion and the counts in
   `TimeZoneVectorTableTests` with it. Replace the old `tz-vectors-<release>.csv`
   and update `TimeZoneVectorTable.DataVersion` and `FileName`.
3. Run the suite. `TzdbTimeZoneRulesTests` holds the data's version to the table's,
   and the vector theories (39 zone-and-set cases through the seam, 39 through
   `ChartMapper.ToEntity`) hold every conversion to it.

**The truth table.** `tz-vectors-2026e.csv` holds 4,478 readings over 13 zones, each
with the instant it denotes under D17, the offset in force and the zone record's
name: the grid (local noon on the 15th of four months, every fifth year 1890–2040,
1,612), every repeated and skipped interval 1890–2040 at its midpoint (1,822: 913
repeated, 909 skipped) and local mean time every fifth year from 1800 (1,044). It
comes from Python's `zoneinfo` over the pip `tzdata` package with the system
zoneinfo directory cleared, never from NodaTime or from AstroCast. The tests read it
from beside `TimeZoneVectorTable.cs`, as the goldens are read.

## Time Inputs (branch F5, 1.3.3)

How a chart's time input becomes its instant. One conversion,
`ChartEngine.ResolveInstant`, is what the engine casts with and what
`ChartMapper.ToEntity` saves with, so a saved chart stores the instant it is
calculated at.

- **The Time Note (D17, Q5, TZ card 4).** When a zone reading was repeated, skipped
  or read on local mean time, the chart export says so under its own label,
  `**Time Note:**`, directly beneath the UTC Offset line — never inside "Notes",
  which seven prompts read as biography and the horary prompt as the question — and
  the chart JSON carries the same text as `timeNote`, omitted when there is none.
  `TimeNoteFormatter` builds it once, culture-independently, and the engine records
  it on `ChartResult.TimeNote`. A repeated reading names both instants in UT with
  their zone names, says the chart is cast for the later, and gives the explicit
  offset that selects the earlier; a skipped one gives the gap and the first instant
  after it; local mean time (the zone record named "LMT") names the zone's reference
  city from its canonical identifier, the offset to the second, and says an explicit
  offset gives the birthplace's own mean time. An explicit-offset chart and an
  ordinary reading have none. The chart Markdown and PDF print it, and every export
  that embeds a chart's header (transit, timeline, synastry, composite, returns,
  relocation, medical, electional, ingress, lunation, great conjunction) carries it
  through `ChartMarkdownFormatter`. An electional candidate cast at the earlier
  instant of a repeated hour says so. Checked against `zoneinfo` on all 4,478 rows
  of `tz-vectors-2026e.csv`: 0 mismatches.
- **Whole seconds (F0-4).** The reading and an explicit offset are taken to the
  nearest second, so `UtcDateTime` and `JulianDay` are one instant: −5:50:36 entered
  as decimal hours was cast a second early, and a reading's fraction was kept in the
  instant but dropped from the Julian Day.
- **The calendar is the local date's (D21).** On or after 1582-10-15 Gregorian, on or
  before 1582-10-04 Julian; the reading is read on its own calendar and the offset
  applied after, so 1582-10-15 00:30 at UTC+1 is JD 2299160.479, not 2299170.479.
  1582-10-05 to 1582-10-14 are refused. A zone's offset before the switch is its
  local mean time, from the seam for the same instant. `UtcDateTime` is the instant,
  proleptic ticks, on every chart (R-D4, branch R2, 1.3.4), its Julian Day by arithmetic
  (`InstantToJulianDay`, bit for bit the calendar read's), and a relocated chart is cast
  at that Julian Day; until 1.3.4 it was held as the Julian label before the switch, so
  that a chart recast from it as a reading at offset zero landed on the same Julian Day.
  A typed reading whose instant falls on 29 February UT of 1300, 1400 or 1500 — a
  Julian leap day `DateTime` cannot hold — is refused (R-D5). Checked against Meeus
  (ch. 7) on 3,845 cases: 0 mismatches.
- **Only the years the files cover (F0-6).** 1200-01-01 04:54:46 UT (Julian) to
  2399-12-31 23:51:20 UT, from the files' headers and direct `swe_calc_ut` calls from
  a freshly opened library: Pluto's light time sets the first edge, the missing
  `seas_24.se1` (Chiron) the last. Outside it the engine refuses with a message
  naming 1200 to 2399. It had claimed "−5400 to 5400" and then failed on Chiron, or
  took the planets silently from the Moshier model — or succeeded or failed by what
  had been cast before, because a file left open serves past its own year (a test
  at 2400-06-15 passed in the full suite and failed run alone).
- **Stored UTC is UTC (F0-7).** `ChartResult.UtcDateTime` is always
  `DateTimeKind.Utc`; `ToEntity` writes a `Z`, and the JSON's `utcDateTime` ends in
  `Z`, for an explicit-offset chart as for a zone one. Rows stored before keep their
  text until saved again (nothing reads the column back); rows 23, 29 and 72 carried
  a machine offset.
- **The offset box (O-D4).** Every place that reads it — Calculate & Save, the
  electional search's natal fields, "Set to now" and the form's label — reads it
  through `ChartEntryViewModel.TryParseUtcOffsetHours`: decimal hours with a point in
  any culture, as latitude and longitude are read, within −14 to +14, refusing
  anything else with a message like a bad latitude's. "-5,5" is refused, never −55.

## Input Checks (branch InputChecks, 1.7.2)

What the forms accept, and what the status line says when they refuse: seven findings of the
1.7.2 code audit (N1, N5, N6, N7, N16, N17, N18), with the 1.7.2 plan's rulings.

- **The Time box is a time of day (N1).** `ChartEntryViewModel.TryParseTimeOfDay`, beside
  `TryParseUtcOffsetHours`: hours and minutes with a colon, seconds optional (`H:mm`, `HH:mm`,
  `H:mm:ss`, `HH:mm:ss`), 00:00:00 to 23:59:59, trimmed, a colon in every culture. Everything
  else is refused with "Invalid time format. Use HH:mm:ss." ("Invalid natal time format. Use
  HH:mm:ss." when the electional search casts from the form). Calculate & Save, the electional
  cast from the form, the electional time window and the form's offset label (which falls back
  to noon) read through it. Until 1.7.2 the box was read by `TimeSpan.TryParse`: "9" was nine
  days, so the chart was cast and saved for midnight nine days on, and "-5:00" was five hours
  before midnight.
- **One builder for the form's chart (N5).** `MainViewModel.BuildChartInputFromForm` is the one
  place the form becomes a `ChartInput`; Calculate & Save and the electional search's cast from
  the form both call it, each after its own checks with its own messages. Until 1.7.2 the
  electional cast built an input of its own, always "Birth" in Placidus houses with no horary
  fields, and that chart became the Chart panel's chart.
- **Refusals in plain words (N6).** `DisplayText.ErrorMessage(exception)` is the text every
  status line prints for an exception (58 sites in `MainViewModel`, 1 in
  `LocationSearchViewModel`): the message without the framework's " (Parameter '...')" suffix
  and without its "Actual value was ..." line, taken off exactly as the framework put them on;
  the logs keep the exception. Each panel checks its own range before it starts, without
  calling the engine: "The end date is before the start date." (Transits, Electional, lunar
  returns) and "The end year is before the start year." (great conjunctions, synodic cycles,
  mundane aspects, eclipses, the return and ingress timeline).
  - *A one-day transit report is allowed* (Steve's ruling): every scan already read the end
    date as a whole day, and `TransitAspectFinder.ValidateScanRange` now refuses only an end
    before the start. The lunar return range is two midnights and still needs its end after its
    start; the panel says "The end date must be after the start date."
  - *The electional time window:* both boxes empty is no window; both holding a time of day
    (N1's forms) is the window; anything else stops before the search and names the box ("The
    Latest time is empty. Fill in both Earliest and Latest, or leave both empty.", "The Earliest
    time is not a time of day. Use HH:mm."). Until 1.7.2 a half-filled or mistyped window was
    dropped in silence and the whole day searched. Both heads' two tooltips say so.
- **Invariant times and file names (N7).** Set to now writes `HH:mm:ss` in the invariant
  culture (the dot fi-FI writes is refused by the box's own check). A default file name's date
  goes through `MainViewModel.FileNameDate` (`yyyyMMdd`, invariant: the backup, the transit
  report, the daily horoscope, the transit timeline, the electional export) or the invariant
  `yyyy-MM-dd` (the lunar return), where th-TH wrote 2569 for 2026. Nothing inside a report
  changed.
- **The place lookup (N16).** A failure that carries an HTTP status code reads "The
  place-search service answered {code}; try again in a minute. You can enter coordinates
  manually."; with none, the sentence about the connection stays. The warning logs the code and
  the exception. The four panels share `LocationSearchViewModel`.
- **Import Backup (N17).** `MainViewModel.DescribeFileThatIsNotABackup`: "This file is one
  chart's JSON export, not a chart backup." (an object holding a `chart` object and a
  `positions` array), "This file is not an AstroCast chart backup." (any other JSON), "This file
  is not valid JSON." (text that does not parse, an empty file included). Nothing is imported:
  the repository reads the whole file before its first save.
- **Saved charts by name (N18).** Sorted by name, the list is ordered after the read by
  `MainViewModel.OrderedByName`: the comparison of `CultureInfo.CurrentCulture` with
  `IgnoreCase | IgnoreNonSpace`, stable, reversed by the A-Z button; the three pickers always
  read A to Z; other sort keys stay as the database reads them. Until 1.7.2 the order was
  SQLite's, by bytes: "Zoe Carter" before "anna lind", and a name with an accented capital after
  both. **Left for 1.8.0:** the Search box (`LIKE ... COLLATE NOCASE`) and the same-name check on
  save (`Name = @Name COLLATE NOCASE`), which ignore case for the letters a to z only.

`TimeOfDayEntryTests`, `MainViewModelTests` (`Electional_NatalFromTheForm_*`),
`StatusBarErrorTextTests`, `RangeCheckTests`, `TransitServiceTests` (`Calculate_OneDay_*`),
`PanelExportTests.DefaultNames_UnderTheThaiCalendar_CarryGregorianDates`,
`LocationSearchStatusTests`, `ImportBackupTests` and `SavedChartsNameOrderTests` hold it.

## Base Map (CARTO key, 1.3.3)

Both astrocartography map windows draw CARTO Voyager raster tiles, for their English
place names. Since 25 September 2026 CARTO serves them only with a key; without one,
every tile is an "API KEY REQUIRED" placeholder sent as an ordinary HTTP 200.

- **One place for the settings.** `BaseMapSettings` (Presentation) holds the tile URL
  `https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key={k}`, the layer
  name, the User-Agent `AstroCast/<version> (desktop app)` and the attribution's two
  links. Both `AcgMapWindow` files build their tile layer only from it.
- **The key, in this order** (`BaseMapKeyLoader`): the override file `carto-key.txt` in
  the app's data directory (Avalonia `AppPaths.DataDirectory`, WPF beside the
  executable), then the key built into Presentation, then none. Content is trimmed, and
  empty counts as absent.
- **The built-in key** is read at build time from the file the `CartoKeyFile` MSBuild
  property names, by default `.astrocast/carto-key.txt` under the user's profile folder
  (`%USERPROFILE%\.astrocast\carto-key.txt` on Windows). It is embedded only when that
  file exists. A build without it succeeds, says so in one message and has no key.
- **No key:** no tile layer, no tile request, and one line under the legend that says so and
  where the key file goes (branch WindowsFit, 1.7.2; finding A7): "Base map unavailable: no
  CARTO key. Put your key in a text file named carto-key.txt at: {full path}". It is built
  once, `BaseMapSettings.NoKeyMessageFor(dataDirectory)`, from the path the loader itself
  reads, `BaseMapKeyLoader.OverrideFilePath(dataDirectory)` (the data directory's full path
  and `carto-key.txt`), and both heads' map windows call it with the data directory they were
  given, so both print the same words. Of the loader's two sources only the override file is
  a user's to fill, so that is the one named; no key file is opened to build the line and no
  key is ever part of it. A window given no data directory keeps the short line,
  `BaseMapSettings.NoKeyMessage`. Until 1.7.2 the line was the short one alone. The line is
  one Mapsui text widget and does not wrap: measured headless, 185 characters are about
  1,044 px in the 1,200 px map window. `BaseMapNoKeyMessageTests` (8 names) and the UI
  `AcgMapWindowTests.MapWindow_WithNoKeyAndADataFolder_SaysWhereTheKeyFileGoes` hold it.
- **Never commit, copy or print the key.** It lives outside the repository. A tile URL
  carries it, so write one as `?key=REDACTED`.
- **The packagers never embed the key** (branch NoKey, 2026-10-29): `make-bundle.sh` and
  `BuildInstaller.ps1` publish with `CartoKeyFile` naming a path that does not exist and stop if the
  published `AstroCast.Presentation.dll` carries the `AstroCast.Presentation.CartoKey` resource (its
  name is ASCII in the metadata; the count is 1 with a key, 0 without). Every installed copy reads
  its key from `carto-key.txt`. A build from the IDE or the command line still embeds the profile's
  key file when it exists.
- **A public build ships no built-in key** (the decided policy; branch Public): it is made with the
  key file absent, or with `-p:CartoKeyFile=<a path that does not exist>`, which the csproj honours
  (`AstroCast.Presentation.csproj`, the property's `Condition` at its line 47); a user supplies a free
  key through `carto-key.txt` as above. The packagers do this always (the bullet above).
- No test reaches a tile server: `AcgMapWindowTests` fetch through
  `RecordingTileHandler` and give the window its key sources.

## Head Parity (branch O-8, 1.3.4)

The Avalonia head caught up with the WPF head on four counts (finding E3-7: H-01, H-03, H-04, H-05; Steve's
rulings of 2026-10-14).

- **The time-zone lists open with a click on their chevron (H-01).** The Chart and Electional panels'
  time-zone AutoCompleteBoxes carry their chevron as a `ToggleButton` in `InnerRightContent`, holding the
  ComboBox template's `PathIcon`, its `IsChecked` bound two-way to the box's own `IsDropDownOpen`
  (`$parent[AutoCompleteBox]`): a click opens the list, a second click closes it, and the list closing any
  other way releases the toggle. No code-behind. Automation names `Show time zones` and `Show electional
  time zones`, tooltip `Show the time-zone list`; the `ToggleButton.timezoneChevron` style draws the glyph
  alone (Fluent's ToggleButton template would paint a background and an accent while checked), with a
  transparent hit area, an arrow cursor and no tab stop. A ToggleButton, not a Button: the panels' button
  counts leave toggles out. Until 1.3.4 the glyph ignored the pointer and the list opened only on typing.
- **A start-up failure and an unhandled UI error show a message (H-04, H-05).** `MessageWindow`
  (`AstroCast.Avalonia/Services`, internal, built in code beside `ConfirmationWindow`) shows a message and a
  title with one `OK` button (IsDefault and IsCancel, focused on open with `NavigationMethod.Tab`).
  `App.StartupFailureMessage` and `App.UnexpectedErrorMessage` (internal) return the WPF head's texts word
  for word. A start-up failure is logged and written to standard error, then the lifetime's `ShutdownMode`
  becomes `OnExplicitShutdown`, a MessageWindow titled `AstroCast — Startup Error` becomes the main window,
  and closing it shuts down with exit code 1 (at once if the window cannot be made). An unhandled UI-thread
  exception is logged, then shown in a MessageWindow titled `AstroCast — Unexpected Error`, over the main
  window when it is visible, else on its own, at most one at a time, any failure to show it swallowed; the
  exception stays handled. Until 1.3.4 the head shut down with nothing on screen and logged UI errors only.
  `AstroCast.Avalonia` makes its internals visible to `AstroCast.UiTests`.
- **Both heads default a Yes/No prompt to No (H-03).** `WpfDialogService` passes `MessageBoxResult.No` as
  the MessageBox's default result (the six-argument overload); the Avalonia `ConfirmationWindow` keeps No
  focused. Every prompt asks before something that cannot be taken back: the import, the overwrite of a
  chart that exists, the delete, and Export Both's Markdown replacing a file. Until 1.3.4 the WPF box
  defaulted to Yes, so Enter confirmed.

**A row with buttons never scrolls sideways** (branch WindowsFit, 1.7.2; finding A9). The Saved Charts list
is the one list in either head whose rows hold buttons (Load and Delete, after a name, a detail line and a
keywords line of free length). The WPF `ListView` carries `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`,
so each row is measured at the viewport's width: the Auto column keeps the buttons in view and the star
column takes the rest. In both heads each of the row's three lines has `TextTrimming="CharacterEllipsis"` and
its full text as its tooltip. Until 1.7.2 the WPF list scrolled sideways, so each row was measured with
unlimited width and `Stretch` made every row as wide as the widest: one saved chart with a long place put
Load and Delete of every row past the pane's edge, behind a scroll bar (the Avalonia `ListBox` never scrolled
sideways and simply cut a long line). No window's size changed. Checked off screen from the markup file
itself, no window and no picture (`a9-wpf-layout.ps1` in the branch's scratch folder): short rows, one long
row and twenty long rows at pane widths of 486 (the window as it opens), 436 (its minimum width) and 292 (the
column's own minimum); the UI theory `SavedChartsPaneTests.ChartRow_WithLongLines_KeepsItsButtonsInsideTheList_AndTrimsItsText`
holds the Avalonia row at 950 by 600 and 900 by 500.

**Still different between the heads**, by ruling and not in this branch: the map package (WPF
`Mapsui.Wpf` 5.0.2, Avalonia `Mapsui.Avalonia12` 5.1.0; a bump moves six Windows packages and the notices);
H-06, each head's own database folder; and H-07, an Avalonia save to a place with no local path reading as a
cancel.

## Panels (branch Panels, 1.4.0)

1.4.0's item 1 and six interface fixes, in both heads (scoping of 2026-10-16; Steve took every
recommendation, and ruled on the refresh defect the scoping's probe found).

- **The Natal Chart panel reads in the order a chart is entered:** Chart Subject (Name, Type,
  Gender), Location (Lookup, Results, Latitude, Longitude, Location), Date, Time & Timezone (Date and
  Now, Time, Timezone, Offset, Override, UTC Offset), Houses (two rows, "System" and "Points", the second
  since 1.6.0, branch AsteroidsPanel; the ComboBox keeps the automation name "Houses", and the CheckBox's
  Avalonia automation name is `Include asteroids and Lilith`), Notes (Source, Notes, Keywords), then the
  Horary Question and the
  Actions. The place comes before the date because a lookup fills the time zone the Offset line
  reads. The panel has six form grids; no tab index is set, so Tab follows this order (Gender, then
  the location lookup). `ChartPanelLayoutTests` pins the section order, the labels by rendered
  height and the Tab stop.
- **The chart pickers keep their charts.** Person B, the Electional natal chart and the Relocation
  natal chart bind the unfiltered `AllSavedCharts`, which only `RefreshSavedChartsAsync` refills (a
  save, an import, a delete, start-up); it reads the three selected Ids before the clear and points
  each selection at the refilled chart with the same Id, or at nothing when the chart is gone. So a
  search, a sort or the saved-charts filter does not reach them, and only deleting a chart empties
  its picker. Until 1.4.0 they bound the filtered `SavedCharts`: every refill (new instances,
  reference equality) made each ComboBox write null back, so Calculate & Save emptied all three and
  Relocation and Electional fell back silently to the chart just calculated. Since branch Relocate
  the Relocation picker also follows each Load and Calculate & Save (see *Relocation panel*), and
  since branch ElectionalPicker (1.6.1, item C) the Electional picker does too, with its own line
  under the picker, Avalonia automation name `Electional chart in use`
  (`ElectionalViewModel.ChartInUseText`, raised by the picker's setter and by `LastResult`'s setter
  through `OnChartPanelChartChanged`): the picked chart, `{Name} — {type}, {reading}, {place}`; else
  the Chart panel's chart the same way with ` (the Chart panel's chart, not saved)`; else `No chart
  yet: pick a saved chart, or load or calculate one on the Chart panel; otherwise Calculate casts one
  from the Chart panel's fields.` The picker's field cell is a StackPanel holding the ComboBox and
  the line, no row added, and its tooltip reads "The natal chart the search scores against. Loading
  or calculating a chart on the Chart panel picks it here; with none picked, the Chart panel's chart
  is used, or else one cast from its fields." Use Natal Location takes a picked chart's own zone,
  the GeoTimeZone lookup only where it is blank. Clear Form empties both pickers, the Electional
  and the Relocation one, through their setters, since each follows the Chart panel's chart and
  Clear Form empties the Chart panel (Steve's decision of 2026-10-21); the Relocation panel's place
  fields keep their values. Until 1.6.1 the Electional picker showed only a hand pick, Use Natal
  Location always looked the zone up from the coordinates, and both pickers kept their pick through
  Clear Form (the Electional one since 1.4.0, the Relocation one since branch Relocate).
- **Picker texts** (§9 item 7): the Electional tooltip "… Until one is chosen, the chart calculated on
  the Chart panel is used, or else one cast from the Chart panel's fields.", the wording until 1.6.1
  (the pickers bullet above carries the current one). No empty entry was added.
  Panels gave the Relocation panel a hint and tooltip of the same rule; branch Relocate replaced them
  with its Birth Chart section and the chart-in-use line.
- **Coordinates, one check** (§9 item 9): `ChartEntryViewModel.TryParseLatitude` and
  `TryParseLongitude`, beside `TryParseUtcOffsetHours`: decimal degrees with a point, invariant,
  within ±90 and ±180, the Natal panel's messages; a comma is refused ("-7,5" was −75 in the Natal
  and Electional panels). Calculate & Save and the electional search (its place, and the Chart
  panel's fields when it casts the natal chart from them) read through them. The Relocation boxes
  parse on every change (`LatitudeError`, `IsLatitudeValid`, `LatitudeToolTip`,
  `HasValidCoordinates`, and the longitude's); an invalid box draws the time-zone idiom's red border
  with the message as its tooltip (Avalonia `TextBox.invalidCoordinate`, styled on
  `PART_BorderElement` too so it stays red while focused; WPF a DataTrigger, which the Aero2 template
  recolours while focused), a blank box is not red, and Calculate Relocation needs both valid.
- **Enablement** (§9 items 6 and 16): `RefreshExportCommandStates` ends with
  `RefreshRelocationCommandStates`, so the Relocation panel's commands follow `HasResult` (it was
  added for Use Natal Location, which branch Relocate removed from the panel; Calculate Relocation
  reads `HasResult` too); Electional Calculate needs a non-blank zone, and
  `ElectionalViewModel.TimeZone`'s setter requests the refresh.
- **Relocation off the UI thread** (§9 item 10): Calculate Relocation casts a dropdown chart inside
  its one `Task.Run`, under its token, as Synastry casts Person B. Use Natal Location, the panel's
  other caster, copied the saved chart's place without a cast; branch Relocate removed it.
- **Names on screen** (E3-6): `Core/Enums/DisplayNames.cs` gives `HouseSystem`, `RelationshipType`
  and `ElectionType` a `DisplayName` (C# 14 extension blocks; the first two are the exports' former
  private tables, moved unchanged), `DisplayText` (Presentation) names `ReturnFindMode` and
  `ChartSortField` and builds the saved-chart row, and one `DisplayTextConverter` per head shows them:
  "Whole Sign", "Parent & Child", "Business Launch", "By Year", "Event Type", and "Birth — Harrisburg,
  Pennsylvania, United States (1947-10-05 09:30:00)", the stored reading as typed through
  `ChartMapper.ParseStoredLocalDateTime`, never `CalendarDateFormatter`. The exports were unchanged in
  1.4.0: the electional export printed "BusinessLaunch" until 1.7.2, and prints the display name since
  (see *Export Text* below). Export Backup counts the charts in the file it wrote, not the filtered list.

## Relocation panel (branch Relocate, 1.4.0)

Steve's item 3 (scoping of 2026-10-16; every recommendation taken): the panel had read as a comparison
of two charts ("All i want is to create a relocation chart for a single birth chart"). It now reads
as one birth chart, then a new place, in both heads, in the Chart panel's section style.

- **The rows:** the title; the line "Recast a birth chart for a new place: the same moment, with new
  angles and houses."; **Birth Chart**, its row "Chart" (the picker over `AllSavedCharts`, automation
  name "Relocation birth chart") and under it, in the field column, the chart-in-use line; **New
  Location**, one grid of Search with Find, Results (labelled since Relocate), Latitude, Longitude and
  Location; Calculate Relocation, Cancel and Export Relocation MD; then the result line. Two form
  grids, on the shared label column (100, WPF 80); the hint and its margin copied from the label
  column are gone.
- **The picker follows the Chart panel:** Load sets it to `FindSavedChart(entity.Id)` after its
  `LastResult` write, and Calculate & Save to `FindSavedChart(id)` after `RefreshSavedChartsAsync`
  (which would otherwise restore the earlier pick): always `AllSavedCharts`' instance, never Load's
  parameter from the filtered list. A pick by hand holds through a search, a sort, the filter and a
  reload until the next Load or Calculate & Save, or until its chart is deleted. The list is every
  saved chart: an event or horary chart relocates as K2 casts it, and the line names its type.
- **The chart-in-use line** (`RelocationViewModel.ChartInUseText`): the picked chart, "{Name} —
  {type}, {reading}, {place}" ("Eleanor Hartwell — Birth, 1947-10-05 09:30:00, Harrisburg, Pennsylvania,
  United States"); else the Chart panel's `LastResult` the same way with " (the Chart panel's chart,
  not saved)" (the Electional search's chart cast from the form, or a picked chart since deleted);
  else "No chart yet: load or calculate one on the Chart panel, or pick a saved chart." The type and
  reading come from `DisplayText` (`EventTypeName`, `StoredReading`, `Reading`), shared with the
  saved-chart row: the reading as typed, never through `CalendarDateFormatter`. Raised by
  `SelectedNatalChart`'s setter and `Clear`, and by `MainViewModel.LastResult`'s setter through
  `OnChartPanelChartChanged()`.
- **The result line** (`RelocationViewModel.ResultSummary`, shown while `HasResult`): "{chart} at
  {place}: Ascendant {X} → {Y}, {n} house changes", or "Ascendant unchanged ({X})", "1 house change"
  in the singular. It names the chart and the place, which say what Export Relocation MD writes.
- **The result clears** (`RelocationViewModel.InvalidateResult`, branch StaleResult, 1.6.1; the 1.4.0
  scoping's Finding 3, item A of the 1.6.1 scoping, Steve's decision of 2026-10-21: clear it rather
  than mark it stale): `CurrentResult` is nulled, the line empties and Export Relocation MD greys when
  the picked chart's Id, the place (location name, latitude or longitude) or the Chart panel's chart
  (`MainViewModel.LastResult`, through `OnChartPanelChartChanged`, which now also requests a command
  refresh) changes, until the next Calculate. The key is the chart's Id, not the instance: a
  saved-charts refresh re-points the picker at a new instance of the same chart on every save, import
  and delete (`ChartEntity` compares by reference), and that keeps the result. `MainViewModel` is
  unchanged: a failed or cancelled Calculate leaves the result for the inputs as they stand, and a
  change made while a run is in flight clears the old result and the run lands its result for the
  inputs it read, which the line names. Until 1.6.1 the result stayed through every change and Export
  wrote it. `RelocationViewModelTests` (the Id key, the three place boxes, one refresh),
  `RelocationChartInUseTests` (the Chart panel change disables Export), `RelocationCommandTests` (a
  cancelled run leaves the result) and the UI `RelocationPanelTests.ResultLine_HidesWhenThePlaceChanges`
  pin it. Since 1.7.2 every other result made from the Chart panel's chart clears by the same rule: see
  *Stale Results* below.
- **Use Natal Location left the panel** with `UseNatalLocationForRelocationCommand` and its method:
  relocating a chart to its own birthplace gives the birth chart back, and Search finds a nearby
  place. The Electional panel's own Use Natal Location stays.
- **Tooltips** speak of one chart: the picker "The birth chart to relocate. Loading or calculating a
  chart on the Chart panel picks it here."; Results "Pick a place to fill in its latitude, longitude
  and name"; Location "Name of the new place (filled in from Results, or typed)"; Calculate "Recast
  the birth chart for the new place"; Export "Export the relocated chart, with its angle and house
  changes, as Markdown". The export is unchanged: its Natal and Relocated columns compare one chart at
  two places.
- `RelocationPanelTests` pins the sections, the labels by rendered height, the tooltips, the line
  after a Load and the result line after a run; `RelocationChartInUseTests` the preselect and the
  line; `SavedChartPickersTests` the Relocation picker following Calculate & Save.

## Transits panel (branch ModesPanel, 1.4.0)

Item 2's panel (scoping C1a to C1f; Steve's decisions of 2026-10-17): the one "Include progressions" box,
which drove the solar arcs and the progressions together, became three, so the report runs any of the
techniques of *Transit report techniques* above, in both heads. Branch PrimaryPanel (1.5.0) added a fourth,
Directions; its row and its gate are below.

- **The rows:** Start Date, End Date, Transits "Include transits", Minor "Include minor aspects", Solar Arc
  "Include solar arc directions", Progressions "Include secondary progressions", Context: seven rows
  where there were five. "Progressions" is still the widest label, so the label column (100, WPF 80) is
  unchanged. Each box's tooltip names what it adds.
- **The view model:** `TransitsViewModel.IncludeTransits`, `IncludeSolarArcs` and
  `IncludeSecondaryProgressions`, true by default and after `Clear`, replace `IncludeProgressions`, and
  Calculate passes them as `TransitInput`'s three flags. Each setter raises `HasTechnique` (any of the
  three) and requests a command refresh; `RefreshTransitCommandStates` raises Calculate with the three
  exports. `ResultIncludesTransits` reads `TransitResult.IncludesTransits`.
- **The gates:** Calculate needs a chart on the Chart panel (`HasResult`) and `HasTechnique`. Its tooltip,
  shown while it is disabled, says so. Minor is enabled only while Transits is checked: its `IsEnabled`
  binds `IncludeTransits`, and its tooltip, also shown while it is disabled, reads "Transits only: …".
  Calculate passes the minor aspects only with the transits (`IncludeTransits && IncludeMinorAspects`).
  Export Report MD needs a result; Export Timeline MD and Export Daily Horoscope MD need
  `ResultIncludesTransits`, since both are made of transits.
- **The buttons** (C1e): "Calculate Transits" became "Calculate" and "Export Transit MD" became "Export
  Report MD", with their automation names, in the Transits panel and the Export panel. The Returns
  panel's own Export Timeline MD is another command. The panel title "Transits & Progressions" and the
  nav tooltips stay (C1d). The titles and file names per combination are `TransitReportNaming`'s (C1f).
- **The status line:** a result with transits reads as before ("Transits calculated: … active transits,
  … midpoint activations."). One without names its techniques and counts its hits: "Solar Arcs
  calculated: 37 solar arc hits.", "Solar Arcs and Progressions calculated: 37 solar arc hits, 9
  progressed hits." The singular is "1 solar arc hit".
- **The button row** wraps 2+2 in both heads at the default window:
  - Avalonia, measured on macOS: 100, 100, 138.5 and 148.5 px against 334.
  - WPF, computed from its markup and Segoe UI at 13 DIP: 100, 100, 129.8 and 139.4 against 342; the
    third button misses the first line by 5.8 px.
  - The notes beside both rows give the figures.
- `TransitsViewModelTests` and `MainViewModelTests` (the `CalculateTransit_*`, `ExportDaily_*` and
  `ExportTimeline_*` tests) pin the techniques, the gates, the input and the status line.
  `TransitsPanelTests` pins the six boxes by keyboard, the Minor and Calculate gates with their tooltips,
  and the two exports' gate.
- **The fourth box** (branch PrimaryPanel, 1.5.0; scoping decision 1): an eighth row, Directions "Include
  primary directions" after Progressions, its tooltip "Include the primary directions exact in the period,
  found with the Directions panel's settings"; Context moves down a row. `TransitsViewModel.IncludePrimaryDirections`
  is true by default and after `Clear`, set through `SetTechnique`, and `HasTechnique` reads all four;
  `TransitInput.IncludePrimaryDirections` keeps its model default, false. Calculate needs `HasResult`,
  `HasTechnique` and, while the box is checked, `Directions.HasValidSettings`; the Directions child's refresh
  raises `RefreshTransitCommandStates` too. Its tooltip reads "Calculate the report for the checked techniques
  over the dates above. Needs a chart on the Chart panel and at least one of Transits, Solar Arc, Progressions
  and Directions checked; Directions takes the Directions panel's settings." `CalculateTransitAsync` passes
  `PrimaryDirectionSettings = Directions.Settings` when the box is checked (else the default), so the period
  and the life list agree, and `DirectionHitCounts` counts the period's hits after the progressions ("4
  primary direction hits", "1 primary direction hit"): "Primary Directions calculated: 1 primary direction
  hit." for the directions alone. "Directions" is narrower than "Progressions", so the label column stays.
  `TransitsPanelTests` pins seven boxes by keyboard and the new tooltip; `MainViewModelTests`
  (`CalculateTransit_PassesThePrimaryDirectionsAndTheirSettings`,
  `CalculateTransit_InvalidDirectionSettings_GateCalculateOnlyWithTheBox`) the input and the gate.

## Davison section (branch DavisonPanel, 1.7.0)

Steve's topic 3 of 2026-10-21 and the scoping's decisions 5 and 6: the Davison chart on screen, in both heads,
as a section of the Synastry panel directly under the Composite Chart section, the Composite's twin for the same
two charts (the Chart panel's chart and the Person B picker); shown and exported only, never saved.

- **The section:** the `sectionHeader` "Davison Chart"; the grey line "A real chart cast for the midpoint of the
  two births, in time and place; the same two charts as the synastry above."; a Context grid on the shared label
  column bound to `Synastry.DavisonInterpretationContext` (Avalonia automation name "Davison context"; WPF with
  `UpdateSourceTrigger=PropertyChanged`); a WrapPanel with "Calculate Davison" (`CalculateDavisonCommand`,
  tooltip "Cast the Davison chart of the Chart panel's chart and Person B: the midpoint moment at the midpoint
  place."), Cancel (`CommandParameter="davison"`, automation name "Cancel Davison", tooltip "Cancel the running
  Davison calculation") and "Export Davison MD" (`ExportDavisonMarkdownCommand`, tooltip "Export the Davison
  chart, complete, as Markdown."). The Synastry panel has nine buttons, three of them Cancel, and three form
  grids. The Export panel's group reads "Synastry, Composite & Davison" with a third button, Export Davison MD
  (automation name "Export Davison MD"): 21 buttons in 8 groups.
- **`SynastryViewModel`:** `DavisonResult` (raises `HasDavisonResult` and a refresh), `HasDavisonResult`,
  `DavisonInterpretationContext`; `Clear` nulls and empties them by its field-bypass pattern, the one refresh
  kept, so Clear Form covers them.
- **`MainViewModel`:** `OperationKeys.Davison` = "davison", display name "Davison"; `CalculateDavisonCommand`
  (`HasResult && Synastry.PersonB is not null`) and `ExportDavisonMarkdownCommand` (`Synastry.HasDavisonResult`),
  refreshed wherever the Composite's are (`RefreshCommandStates`, `RefreshExportCommandStates`,
  `RefreshSynastryCommandStates`, `RefreshCompositeCommandStates`, which covers both pairs). `CalculateDavisonAsync`
  as `CalculateCompositeAsync`: Person B cast from `ChartMapper.ToInput(entity, ChartEntry.IncludeMinorPoints)`
  inside the one `Task.Run` with `IDavisonService.Calculate(A, B, ChartEntry.IncludeMinorPoints, token)`, the
  `PersonBChart` cache kept; status lines "Calculating Davison chart...", "Davison chart calculated for {A} & {B}:
  {yyyy-MM-dd HH:mm:ss} UTC at {Midpoint of placeA & placeB}." (the result's `MidpointUtc` through
  `CalendarDateFormatter.DateTime`), "Davison calculation cancelled.", "Davison error: {message}".
  `ExportDavisonMarkdownAsync` offers `{A}_{B}_Davison.md` (through `SanitiseFileName`) under "Export Davison as
  Markdown", writes `DavisonMarkdownFormatter.SaveAsync` with the trimmed context, "Davison exported: {path}" or
  "Davison export error: {message}".
- **Registration:** `IDavisonService? davisonService = null`, the constructor's last optional argument after
  `IPrimaryDirectionService`; null builds `new DavisonService(chartEngine, logger)`; both Apps build one beside
  the primary-direction service and pass it by name. `StubDavisonService` (`tests/AstroCast.TestDoubles`)
  records every call, answers with chart A as the Davison chart and the midpoints computed from the two charts,
  throws on demand and holds until cancelled; `MainViewModelFactory.Create` passes it by name, and
  `MainViewModelTests.CreateViewModel`, `MainViewModelCancellationTests.CreateViewModel` take an optional one.
- `SynastryViewModelTests` (`DavisonResult_SetToNewValue_…`, `Clear_…` rewritten), `MainViewModelTests` (the six
  `CalculateDavison_*`, `ExportDavison_*`, `ClearForm_ClearsTheDavisonResultAndContext`),
  `MainViewModelCancellationTests.CancelDavison_CancelsThroughItsOwnKey`,
  `PanelExportTests.ExportDavisonMarkdown_UsesTheDefaultName` and the UI `SynastryPanelTests` (9 buttons, 3
  cancels, `DavisonSection_HasItsThreeButtonsAndContextBox`, `DavisonExplanation_SitsUnderItsHeader`),
  `LabelColumnAlignmentTests` (3 grids) and `ExportPanelTests` (21, `ExportDavisonButton_IsBoundToItsCommand`)
  pin it.

## Directions panel (branch PrimaryPanel, 1.5.0)

Primary directions on screen, in both heads (decisions of topic 7a, Steve's of 2026-10-18 for the
engine's argument): a tenth panel with the settings, the whole-life list and its export.

- **Navigation:** `NavigationPanel.Directions`, after `Relocation`; `MainViewModel.IsDirectionsPanelVisible`,
  raised by the `ActivePanel` setter beside the nine others.
- **The rail:** a tenth button directly after Relocate, glyph U+2192, caption "Primary", tooltip "Primary
  Directions (Alt+0)"; Avalonia's automation name "Directions" (the enum's, which the accessibility test
  requires), WPF a `DataTrigger` on `ActivePanel` = Directions. Alt+0, the last free digit: Avalonia
  `Gesture="Alt+D0"`, WPF `Alt+0`. The buttons are slimmer so ten fit (scoping decision 2a): padding 4,5
  (4,8 until 1.5.0) in Avalonia's `Button.nav` and WPF's `SidebarButton`, and the caption's top margin 1 (2
  until then); the window sizes stay, 950 × 600 and at least 900 × 500. Measured headless on macOS: at 950 ×
  600 the lowest button ends at 479.0 px against the rail border's inner bottom at 543.0 (549.0 with ten
  buttons at the old padding, which `ShellTests.NavigationRail_TenButtons_FitTheDefaultWindowHeight` was run
  red against); at 900 × 500, 473.0 against 443.0, the last button clipped. WPF, computed from its markup and
  Segoe UI's line spacing 1.3301: 51.57 px a button, 531.7 with the border's padding, 11.5 px less than the
  nine buttons of 1.4.0 needed (543.2), so if the 1.4.0 rail showed its Export button whole at 600 the ten fit.
- **The panel**, after Relocation, root `DirectionsPanelRoot` (Avalonia `x:Name`), in the Chart panel's
  section style: the title "Primary Directions"; the line "A dated list of Placidus semi-arc directions,
  zodiacal, direct and converse, for the chart on the Chart panel."; the section Settings, one form grid on
  the shared label column (100, WPF 80): Key (a ComboBox over `DirectionsViewModel.AvailableKeys` by
  `x:Static`, the names through `DisplayTextConverter`, tooltip "The key that turns an arc into years"),
  Planets (CheckBox "Add Mercury to Pluto as significators"; "Significators" would not fit the column),
  Age limit (a 60 px box and "years, 1 to 120"), Uncertainty (a 60 px box and "minutes either way, 0 to 120;
  0 for none"); Calculate Directions (its tooltip, shown while it is disabled, "List the primary directions
  of the chart on the Chart panel. Needs a chart there, an age limit of 1 to 120 years and an uncertainty of
  0 to 120."), Cancel (`CommandParameter` "directions") and Export Directions MD; the result line
  (`Directions.ResultSummary`, shown while `Directions.HasResult`); and the list, a read-only TextBox of
  `Directions.ListText`, Consolas 11, 320 high, unwrapped, both scroll bars Auto. Avalonia automation names:
  "Primary directions key", "Add Mercury to Pluto as significators", "Primary directions age limit",
  "Birth-time uncertainty", "Calculate Directions", "Cancel Directions", "Export Directions MD", "Primary
  directions result", "Primary directions list".
- **`DirectionsViewModel`** (`ChildViewModelBase`): `AvailableKeys` (Naibod, Ptolemy,
  SolarArcRightAscension); `Key` (Naibod), `PlanetsAsSignificators` (false), `AgeLimitYears` (100),
  `BirthTimeUncertaintyMinutes` (0), each notifying on a change and requesting one refresh, the two numbers
  raising `HasValidSettings` (the age within 1 to 120, the uncertainty within 0 to 120; a value outside is
  held, so the box shows what was typed); `Settings`, a new `PrimaryDirectionSettings`, which throws as its
  init accessors do while the settings are invalid; `CurrentResult` raising `HasResult`, `ResultSummary` and
  `ListText`; `ResultSummary` "J2000 NYC Fixture: 572 directions to age 100, Naibod (0°59′08″ a year); 60
  left out" (the left-out part only above 0, "1 direction" in the singular, the limit and key the result's);
  `ListText` = `PrimaryDirectionMarkdownFormatter.FormatTable`; `Clear()` by the field-bypass pattern, one
  refresh. Session only (decision 7): nothing persists.
- **`MainViewModel`:** the `Directions` child beside Relocation, its refresh to `RefreshDirectionsCommandStates`
  (and the Transits panel's), which `RefreshExportCommandStates` calls last; `CalculateDirectionsCommand`
  (`HasResult && Directions.HasValidSettings`) and `ExportDirectionsMarkdownCommand` (`Directions.HasResult`);
  `OperationKeys.Directions` = "directions", display name "Primary Directions". Calculate takes `LastResult`
  and `Directions.Settings` once, runs the engine in one `Task.Run` under the key's token and sets
  `Directions.CurrentResult`; status lines "Calculating primary directions...", "Primary directions
  calculated: 572 directions to age 100 (60 left out)." (the bracket only above 0), "Primary directions
  calculation cancelled.", "Primary directions error: {message}". Export offers
  `{SanitiseFileName(name)}_PrimaryDirections_0-{limit}.md` under "Export Primary Directions as Markdown" and
  writes `PrimaryDirectionMarkdownFormatter.SaveAsync`: "Primary directions exported: {path}" or "Primary
  directions export error: {message}". `ClearForm` calls `Directions.Clear()`.
- **Core, one public member:** `PrimaryDirectionMarkdownFormatter.FormatTable(result)`, the whole-life
  section's table exactly as `ToMarkdown` prints it (header, separator, one row per hit; or "No primary
  direction falls before age 100."), each line ended by `AppendLine`. One writer:
  `PrimaryDirectionSectionFormatter.AppendTable` (internal), which the section calls; every golden is
  byte-identical. Since branch DirectionNotes (1.7.2) the list carries the table's notes as the report prints
  them, so the panel shows the same marks on its rows and, under the rows, the same footnotes and the natures
  of the promissor stars (see *Primary directions export*, the table's notes); the panel's markup and
  `DirectionsViewModel` are unchanged.
- **The engine reaches the view model** as a last optional constructor argument (Steve: the scoping's
  eleven construction sites are 26), `IPrimaryDirectionService? primaryDirectionService = null`; null builds
  `new PrimaryDirectionService(ephemeris)`. Both Apps build one beside `TransitService` and pass it by name;
  `MainViewModelFactory.Create` passes a `StubPrimaryDirectionService`, which throws; the other test helpers
  are untouched, `MainViewModelTests.CreateViewModel` alone taking an optional recording service.
- **The Export panel:** a group "Primary Directions" with "Export Directions MD" after Transits: 20 buttons
  in 8 groups.
- `DirectionsViewModelTests` (19), `MainViewModelTests` (`CalculateDirections_*`, `ClearForm_ClearsTheDirections`),
  `PanelExportTests.ExportDirections_WritesTheReportUnderItsDefaultName`, the `FormatTable_*` tests and the UI
  `DirectionsPanelTests` (9), `ShellTests` (ten buttons, ten bindings, the fit) pin it.

## Stale Results (branch StaleResults, 1.7.2)

Finding N2 of the 1.7.2 code audit, Steve's ruling 3: a panel's result does not outlive the chart it was made
from. The Relocation panel has cleared its result since 1.6.1 (see *Relocation panel*); every other panel now
does, in both heads, with no change outside the view models.

- **The rule.** `MainViewModel.LastResult`'s setter calls each panel's `OnChartPanelChartChanged`, so every
  change of the Chart panel's chart (Load, Calculate & Save, the Points recast, the electional search's cast
  from the form, Clear Form) clears every result made from that chart: the result is null, what the panel
  shows of it is emptied, and its exports, wherever their buttons sit, are disabled until the next Calculate.
  Until 1.7.2 only the relocation result was cleared: a user who listed directions for one chart, loaded
  another and pressed Export Directions MD got the first chart's report.
- **Made from the Chart panel's chart, cleared:** `Directions.CurrentResult`, with its line and list;
  `Transits.TransitResult`, and with it the report, the timeline and the daily horoscope exports; the Synastry
  panel's `SynastryResult`, `CompositeResult` and `DavisonResult`; the Returns panel's
  `CurrentSolarReturnResult`, `LunarReturnResults` with `SelectedLunarReturn`, and
  `CurrentPlanetaryReturnResult`; `Cycles.CurrentCycleTimeline`; `ReturnIngressTimeline.CurrentResult` with
  `HasResult`; and the map, `Astrocartography.CurrentResult` with `HasResult`, `VisibleLines` and
  `CrossingReportText` (a map window left open draws no lines). The chart exports and the medical export hold
  no result: each reads the Chart panel's chart when it runs.
- **Made from a chart picked on its own panel, cleared as the Relocation panel's is:**
  `Relocation.CurrentResult` (1.6.1) and `Electional.ElectionalResult`. Both pickers follow the Chart panel
  (Load and Calculate & Save re-point them), so each result clears on every change of the Chart panel's chart,
  and on a pick of another chart by hand, keyed on the chart's Id.
- **Made from no chart, never cleared by this rule:** the ingress series and the cardinal ingresses, the
  lunation series, and the eclipse, great-conjunction, mundane-aspect and synodic-cycle series. Clear Form
  still empties the great-conjunction and synodic series with the rest of `Cycles.Clear()`, as before.
- **Person B.** The Synastry panel's three results also clear when Person B changes, keyed on the chart's Id
  (`SynastryViewModel.PersonB`'s setter, `InvalidateResults`): a refresh of the saved-charts lists re-points
  the picker at a new object for the same chart and clears nothing, and deleting Person B's chart clears
  them. In the window each picker's ComboBox loses its item while `AllSavedCharts` is refilled, and writes
  null to its view model before the refresh picks the same chart again: `MainViewModel.IsRefillingSavedCharts`
  is set for the refill, the Person B, Electional and Relocation setters clear nothing while it is, and
  `OnSavedChartsRefilled(idBefore)` then compares the Id held before the refill with the one held after. The
  Relocation picker has had its Id key since 1.6.1 and takes the guard here (Steve's ruling of 2026-11-27):
  until then, in the window, its result was cleared by any refresh while a chart was picked there, an import
  or the deletion of another chart, though 1.6.1 meant a refresh to keep it.
- **Clear Form** clears every chart-made result: through the six panels' `Clear()` as before, through the
  `LastResult` setter, and by name for the two it left until 1.7.2, the return and ingress timeline and the
  map, whether or not there was a chart to change. Those two panels' own settings (the timeline's years and
  boxes, the map's sixteen line boxes) stay; neither panel's `Clear()` is called.
- **A failed run and a run in flight** are as on the Relocation panel. A run that fails or is cancelled
  leaves the earlier result, which is still a result for the chart on the Chart panel; the electional search
  alone clears its result when it fails, as it did before. No panel guards a run in flight: a chart changed
  while a run is under way clears the old result, and the run then lands its result for the chart it started
  with.

`StaleResultsTests` (65 names: each of the twelve results against each of the four changes, the Synastry
panel's three against Person B, the refresh with and without the picker's null, the Relocation picker across
a refresh and a deletion, the mundane series, Clear Form, a failed run) and the UI
`SavedChartPickersTests.ARefresh_KeepsTheResultsOfThePickersItRepoints`, which sees all three pickers' nulls
arrive, hold it.

## Return Ages (branch ReturnAges, 1.7.2)

Finding A1 of the 1.7.2 code audit, Steve's ruling: a solar return export reads its three time-lord sections
for one year, the year the return opens.

- **The rule.** In a solar return export the annual profection, the Firdaria and the Zodiacal Releasing are
  all read for the year the return opens: the age `ProfectionAnalyzer.AnalyzeForReturn` gives, and the
  Firdaria and the releasing as they stand on the birthday that year begins on, the anniversary of the birth
  day at that age on the chart's clock. A Firdaria or releasing period that begins on that birthday is the
  one printed, not the one that ended the day before. Until 1.7.2 only the profection's age was that year's:
  the Firdaria and the releasing counted completed birthdays on the return's own day, so a return on the
  local day before the birthday printed two ages.
- **One method decides the year:** `ProfectionAnalyzer.YearOpenedBySolarReturn(chart, returnIngressUtc)`
  (internal) returns the birth day, the age, the birthday the year begins on and the day the year is read
  on. The age is the years from the birth instant to the return's, rounded to the nearest whole at 365.2425
  days a year; the birthday is the birth day plus that many years (`DateOnly.AddYears`); the reading day is
  the later of that birthday and the return's day on the natal chart's clock, both read by
  `FirdariaAnalyzer.BirthDayAndAge`, which still refuses a return before the birth day. So a return before
  its birthday is read on the birthday, and a return on its birthday or after it on its own day, as before.
  `AnalyzeForReturn` takes its age and its reference day from the method.
  `ChartMarkdownFormatter.AppendFirdaria` and `AppendZodiacalReleasing` have an `isReturnContext` flag, as
  `AppendProfectionYear` has, and with it read on that day (`SolarReturnReadingDay`, a plain date the
  analyzers take as given); `SolarReturnMarkdownFormatter` passes `isReturnContext: true` to all three with
  the return's instant. The rounding is in the one method only.
- **The worked example.** Person A (born 1947-10-05 09:30 at UTC−5), solar return 2026: the return is
  2026-10-04 17:18:50 UT, the day before the birthday, and opens the year of age 79. The export prints
  `**Age:** 79 (as of 2026-10-05)` and "At age 79, …" in the profection, "Age 79: Sun major period
  (2022–2032), Mercury sub-period (2025-08-13–2027-01-17)" in the Firdaria, and the releasing as it stands
  on 2026-10-05, Leo at both levels; until 1.7.2 `(as of 2026-10-04)` and "Age 78: …". Her 2022 return,
  2022-10-04 18:09:22 UT, is the day before the birthday on which the Sun's ten years begin: the export
  prints `**Current Major Period:** Sun (2022–2032, 10y)` and "Age 75: Sun major period", where it printed
  the South Node's period, "Age 74". Her 2032 return, 2032-10-04 04:20:08 UT, is 3 October on her clock, two
  days before: Venus (2032–2040), as of 2032-10-05. In her hundred years the return falls before the birthday
  84 times and on it 16; the Firdaria differs between the two days in 13 of them and the releasing in none,
  its periods running on 360-day years and 30-day months from the birth day.
- **What the sections print.** The profection's "(as of …)" date is the reading day: the birthday when the
  return falls before it, else the return's day on the natal chart's clock. The Firdaria and the releasing
  print no date of their own, only their periods' bounds and the "← current" marks, which follow the
  reading day; the releasing prints no age. The three headings keep "(at Return Ingress)", and no line was
  added.
- **Unchanged.** A lunar or planetary return's three sections keep the year running at the return (branch H,
  1.3.4): person A's lunar return of 2026-10-01 23:05:25 UT reads age 78 as of 2026-10-01, and her second
  Saturn return, 2006-09-14 16:17:30 UT, age 58. A solar return on its birthday (her 1995 return, 00:15 on
  5 October on her clock) or after it (person B's 1966 return, 6 December for a birthday of 5 December)
  prints what it printed. The Returns panel's three lines are the natal chart's at today's date, the solar
  return's status line names no age, and no PDF or JSON of a return exists. A planetary return's own
  "(age N)" line counts whole years between the two instants in UT (`PlanetaryReturnService`), not completed
  birthdays on the chart's clock, so on the birthday itself, before the birth's time of day, it reads one
  less than the sections; left as it is.
- **Goldens.** The two J2000 NYC 2026 solar-return goldens moved by one line each, "Age 25:" to "Age 26:" in
  the Firdaria summary: that return is 2025-12-31 19:17:48 UT for a birthday of 1 January. The golden
  comparison masks the "(as of …)" date, which moved from 2025-12-31 to 2026-01-01. Four goldens were added,
  written from `master` before the rule changed: `SolarReturnMarkdownFormatter_Hartwell_1995`,
  `SolarReturnMarkdownFormatter_Whitfield_1966`, `LunarReturnMarkdownFormatter_Hartwell_20261001` and
  `PlanetaryReturnMarkdownFormatter_Hartwell_Saturn2`.

`ReturnTimeLordYearTests` (22 names: person A's 2026, 2022 and 2032 returns, the four goldens with their
as-of lines, and the three cultures) and `ProfectionAnalyzerTests` (6 names for the method and the
profection's date) hold it.

## Export Text (branch ExportText, 1.7.2)

Text faults in the exports: findings A2, A3, A4, N8 to N15 and N21 of the 1.7.2 code audit and Q12 and Q23
of the prompt audit, one commit for each. Wording, labels, rules and line endings; no calculation changed,
and one printed position (Q12). No product code reads any of the strings changed.

- **Line endings (A3; Steve's ruling).** An export carries the system's own line ending throughout, CR LF
  on Windows and LF on macOS. The ten formatters that embed a chart (composite, Davison, electional, lunar
  return, medical, planetary return, solar return, synastry, transit report, transit timeline; the primary
  directions report through the transit report's method) joined its lines with `Append('\n')` and now join
  with `AppendLine`; a horary chart's `**Status:** Perfected` line had a line feed inside its own text.
  Until 1.7.2 a solar return held 166 CR LF endings and 359 bare line feeds on Windows.
- **Hard breaks (A3, N8).** A `**Label:** value` line that another follows ends with two spaces; the last
  of a block needs none; no blank line was added or removed. The places: the headers of the transit report,
  the transit timeline, the daily horoscope, the electional report and the primary directions report; the
  transit report's Secondary Progressions lines; each electional candidate's Score, Moon and Ascendant
  lines; the primary directions section's Method, Key, Significators, Promissors and Left out lines; the
  synodic series' Conjunctions Found line; the synastry Method and Relationship lines; the composite's
  Reference Location line; the map export's Name and Born lines; the cycle timeline's Cycle Period line
  before a Calendar line; and the Calendar line in each of those headers.
- **One rule before a footer (A2, N9).** `PositionFormatter.EndsWithRule` and `AppendRuleUnlessPresent`,
  the check the transit report's footer has always made: the solar, lunar and planetary return exports and
  the synastry export print one rule after their cross-chart declination table, and the transit timeline
  one after its Clusters section (its footer adds its rule and blank line only when none stands there).
- **Articles and counts (A2, N12).** The profection summary reads "an 8th" and "an 11th house profection
  year", the releasing summary "an Aries" and "an Aquarius Level 1 period". A count's noun follows it:
  "(1 day)" in the daily horoscope's Report Period, "1 New Moon, 1 Full Moon" in the lunation series, "1
  return, 1 ingress" in the return and ingress timeline, "Duration: 1 day" in the transit timeline, "at
  least 1 hour apart" in the electional header.
- **Display names (N11).** `DisplayNames` names the Moon's phases (`MoonPhaseType.DisplayName`, which
  `MoonConditionInfo.PhaseLabel` reads) and the aspects (`AspectType.DisplayName`: "Semi-Sextile" and
  "Semi-Square", the rest the enum's one word). The electional export prints a candidate's phase in words
  in its Moon line and its strengths ("Waning Crescent"), the election type's display name in the header
  and in "Significator well-placed for Business Launch", and the Ascendant ruler's house as "in the 7th
  house"; the Annual Profection's Lord's Natal Aspects read "Venus Semi-Sextile Mercury"; the paran export
  prints "Anti-Culm." as the chart's Parans table does (`ChartMarkdownFormatter.FormatAngleType`). The
  JSON keeps the enum names. Until 1.7.2: "WaningCrescent", "BusinessLaunch", "in House 7", "SemiSextile",
  "AntiCulminating".
- **Ingress rows (N13, Q12).** In the return and ingress timeline an ingress row keeps its columns and its
  position, the boundary crossed, and its Label is `Enters {sign}`, or `Enters {sign} (retrograde)` when
  the body's speed was negative at the crossing: Chiron on 2026-09-18 reads `00°00′00″ Taurus | Enters
  Aries (retrograde)`, where it read "Sign Entry". The Sun's four cardinal ingresses keep their season
  names. In the transit timeline a retrograde ingress (the sign entered is the one before the sign left)
  prints `Ingress Virgo → Leo (retrograde)` and the boundary crossed, `0°00' Virgo`, where it printed 0° of
  the sign entered whatever the direction; a direct ingress is unchanged.
- **Releasing summary (Q23).** The summary closes with one of three sentences, by the Level 1 period's
  angularity: Angular " This is a peak (angular) period — heightened activity and visibility.", Succedent
  " This is a moderately active (succedent) period — steady development between peak and background.",
  Cadent " This is a background period — quieter, preparatory themes." Until 1.7.2 a Succedent period took
  the Cadent sentence.
- **Medical export (N14; Steve's ruling on the line).** `ChartMarkdownFormatter.ToMarkdown` has a seventh
  parameter, `medicalCorrespondences` (default true); the medical export passes false, so the chart it
  embeds prints no `## Medical Astrology Correspondences` section, and the export's own fuller sections are
  the only copy. `ChartMarkdownFormatter.MedicalBasisLine`, "*Traditional correspondences for educational
  and historical study. Not medical advice.*", prints once in the medical export: in its header, as a
  paragraph of its own after the header's labelled lines (under the Tradition line, or under the Health
  Context line when one is given) and before `## Natal Chart`, a blank line either side (branch
  DirectionNotes, 1.7.2; Steve's ruling of 2026-11-27). ExportText had printed it directly before
  `## Temperament Assessment`. A chart export of its own is unchanged.
- **Daily horoscope with an unknown birth time (N15).** The backdrop table has no House column and the
  Entering orb and Exiting orb lines no "(H7)", as the natal summary and the Exact and Active tables
  already printed no house.
- **Relocation export (N10).** The relocated chart sits under `## Relocated Chart` without the chart
  export's own title and footer, as the composite and Davison exports embed theirs, and the one footer
  carries the relocated chart's `EngineVersion` ("AstroCast 1.7.2 (SwissEph 2.10.03, tzdb 2026e)").
- **Chart PDF (A4, N21).** The document information holds a title, the chart's name and kind as the page
  heading states them ("Eleanor Hartwell (Birth)"), and the creator "AstroCast 1.7.2"; no author and no
  subject. The house table labels cusp 1 "(ASC)" and cusp 10 "(MC)" only when the cusp's longitude is the
  angle's (`CuspAngleLabel`): both in Placidus, cusp 1 alone in Equal houses, neither in Whole Sign. The
  chart ruler's house prints as an ordinal, "Ruler House: 4th".
- **Goldens.** 34 of the 44 golden files moved, each change put to its item's commit: 128 lines gained two
  trailing spaces (19 files); 14 files lost one rule (the two timeline goldens its blank line too); 26
  lines changed a name (8 files); 8 ingress labels (1); 5 Succedent sentences (5); 5 article or plural
  lines (5); the medical golden lost 47 lines and the two relocation goldens 5 each.
- **Tests.** `ExportLineEndingTests` (4 names) runs every golden fact inside
  `GoldenFormatterTestBase.CollectCandidates`, which hands back each export as its formatter returned it,
  and holds the line endings, the hard breaks and the single rule for every export the golden tests make,
  and the line endings of a horary chart that perfects. Each item also has tests that name its corrected
  text outright, beside the tests of its formatter or analyzer (`ExportWordingTests` for the lunation
  series and the return and ingress timeline, which had no test file of their own).
