# Contributing to AstroCast

Thank you for looking. This page gives the conventions a change must keep. The full contract,
including every astrological rule the engine follows and the reasons behind them, is
[CLAUDE.md](CLAUDE.md); where the two differ, CLAUDE.md wins.

## Before you start

- Build and run the tests on your system first (see the README's *Building from source*).
  Both suites must be green before and after your change: `tests/AstroCast.Tests` and
  `tests/AstroCast.UiTests` are two figures, reported separately, never summed.
- Open an issue for anything larger than a fix, and say what rule or behaviour you mean to
  change. Astrological rules are decided deliberately and recorded in CLAUDE.md; a change to
  one is a design decision, not a bug fix, and needs the reasoning written down.

## Code

- C# 14 on .NET 10. XML documentation comments on every public type and member, with
  `<param>`, `<returns>`, `<exception>` and `<remarks>` where they apply.
- Nullable reference types on; file-scoped namespaces; explicit access modifiers; primary
  constructors, `field`-backed properties and extension blocks where they read well.
- Logging through `ILogger<T>` (Serilog); no `Console.Write` in library code. `async`/`await`
  for all I/O. Specific exception types. `Path.Combine` for every path.
- **Core writes culture-independent text.** Every number, date and time that `AstroCast.Core`
  puts into an export or stores on a result goes through `CultureInfo.InvariantCulture`,
  integers included, one idiom per file. Never set the thread's culture anywhere. Every golden,
  and every unit test whose text a culture once changed, also runs under de-DE, sv-SE and th-TH.
- No null-forgiving operator (`!`) and no `#pragma` on new lines under `src/`; make the type
  system right instead.

## Architecture

- `AstroCast.Core` depends only on the Swiss Ephemeris interop, Microsoft.Data.Sqlite, QuestPDF,
  Serilog and NodaTime. `AstroCast.Presentation` and the two heads (`AstroCast` for WPF,
  `AstroCast.Avalonia` for macOS) depend on Core, never the reverse; both heads depend on
  Presentation, which depends on no head. The CLI is a smoke test.
- No static mutable state or singletons; dependency injection throughout. Every external
  service sits behind an interface. No `IOptions<T>`: a service takes its settings as
  constructor arguments.
- Planetary positions are computed on demand from the Julian Day and the coordinates, never
  cached in the database. Angles are stored as `double` degrees, 0 to 360; the DD°MM′SS″ form
  is a presentation concern.
- A service may call the value formatters in `Core/Formatting`, never a document formatter.
- Both heads share one set of view models. A user-visible change is made in both heads, and
  the Avalonia head's headless UI tests cover what the WPF head cannot be tested for on macOS.

## Tests and truth

- A rule change comes with a test that was red before the change and green after; say so in
  the pull request.
- **Golden files** under `tests/AstroCast.Tests/Formatting/Golden/Goldens/` pin whole exports.
  They are regenerated only on purpose, with the environment variable
  `ASTROCAST_REGENERATE_GOLDENS` set, and every changed line is explained in the pull request.
  A golden that moves for a reason you cannot state is a defect, in the code or in the change.
- **Tier B tables** under the tests' `TierB/` folders are produced outside the engine, on the
  Swiss Ephemeris through an independent script, and the tests check the engine against them.
  Never regenerate a Tier B table from the engine's own output: that would be the engine
  grading itself. The scripts will live under `tools/truth/`; until then, ask.
- Test names are stable identifiers: the test-name list of each suite is compared before and
  after a change, so rename only when the test's meaning changed.
- Fixtures are invented persons and public figures. Never put a private person's birth data,
  name or question into a test, a golden or a table.

## Commits and versions

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, with the
  branch name in parentheses at the end of the subject when the work is on a branch.
- One concern per commit; each commit builds and passes both suites on its own.
- Every versioned `.csproj` (eight today) carries the same version in `Version`,
  `InformationalVersion`, `AssemblyVersion` and `FileVersion`. The first commit after a release
  tag bumps the patch; minor and major bumps are the maintainer's call. The bump and the tag go
  through the project skill `.claude/skills/release-bump/`, which also reads the version back
  from the built `AstroCast.Presentation.dll`.
- A release is tagged on its last commit, annotated, `vX.Y.Z`, message `Release vX.Y.Z`; a
  pushed tag is never moved.

## Dependencies and notices

- Adding, removing or changing a shipped package means a row in `THIRD-PARTY-NOTICES.md`:
  package, version, licence, copyright, which head it ships in, and its project page.
  `ThirdPartyNoticesTests` checks that every direct reference has its row.
- A package whose licence is not compatible with the AGPL cannot ship. The three Svg packages
  Mapsui depends on are referenced with `ExcludeAssets="runtime"` so that no Svg assembly ships;
  keep those lines.

## Documentation

- A user-visible change updates the user guide (`docs/USER_GUIDE.md`) and the
  prompt usage map in the same branch, and CLAUDE.md where it states the rule.
- Markdown is the source of record; PDFs are renders.

## Secrets

The CARTO base-map key is a per-builder secret, read at build time from a file outside the
repository (`.astrocast/carto-key.txt` under the user's profile folder). Never commit, copy or
print it; write a tile URL as `?key=REDACTED`. Public builds are made without it.
