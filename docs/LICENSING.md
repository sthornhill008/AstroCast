# Licensing

This page sets out, in plain terms, what the licences of AstroCast and of the components it
ships mean for a user, a builder or a contributor. It is a description, not legal advice: the
licence texts themselves govern. Each one is in this repository.

## AstroCast

AstroCast is licensed under the **GNU Affero General Public License, version 3 or (at your
option) any later version** (AGPL-3.0-or-later). The full text is [LICENSE](../LICENSE), the
GNU's `agpl-3.0.txt` unchanged, and the same file ships beside the program.

In outline: you may use, study, copy, change and redistribute AstroCast, in source or binary
form, and anything you distribute that contains or is derived from it must carry the same
licence and make its complete source available. The AGPL adds one clause to the GPL: if you
run a modified version as a network service, the people who use that service must be offered
its source too. There is no warranty.

The program's About window (the status bar's About button; on macOS also *About AstroCast* in
the application menu) gives the notices the licence asks for: the version, the copyright, the
absence of warranty, where the source is, and the licence and third-party texts themselves.

## Why the AGPL

AstroCast calculates on the **Swiss Ephemeris** (Astrodienst AG), which is offered under a dual
licence: the AGPL, or a paid Professional licence. Under the AGPL option, which AstroCast has
chosen, the whole project must be under the AGPL or a compatible licence. The Swiss Ephemeris
library ships as `libswe.dylib` on macOS and `swedll64.dll` on Windows, with its data files in
`ephe/`; its own notice and licence conditions are reproduced in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). The names of its authors and of
Astrodienst may not be used to promote software that uses it.

## Third-party packages

Every package that ships with either head is listed in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) with its version, licence, copyright
holder, the head it ships in, and its project page, followed by the licence texts. The
packages are under the MIT licence, the Apache License 2.0, the BSD 3-Clause licence, or a
licence file of their own (BruTile; OpenTK; QuestPDF under its Community licence, whose Lato
fonts are under the SIL Open Font License). The .NET runtime is MIT.

Three packages that the map library depends on, `Svg.Custom` (Microsoft Public License),
`Svg.Model` and `Svg.Skia` (MIT), are build-time dependencies only: both heads reference them
with `ExcludeAssets="runtime"`, so none of their assemblies is included in the shipped
application. They remain listed because they remain in the dependency graph.

## Data

- **Time zones:** the IANA Time Zone Database, public domain, as built into NodaTime.
- **Time-zone boundaries:** GeoTimeZone's data, from timezone-boundary-builder, derived from
  OpenStreetMap, © OpenStreetMap contributors, under the Open Database License 1.0.
- **Map tiles:** the Map panel's base layer is served by CARTO from OpenStreetMap data under
  CARTO's own Basemap Terms, with a key the user supplies (see the README); the map credits
  both. Public builds of AstroCast ship no key.

## The prompt library

The interpretation prompts in `prompts/` are prose, not software, and are licensed separately
under the **Creative Commons Attribution-ShareAlike 4.0 International** licence (CC BY-SA 4.0):
you may share and adapt them, with attribution, under the same licence. The notice is
[prompts/LICENSE.md](../prompts/LICENSE.md), with the licence's full text beside it.

## Contributions

By contributing to AstroCast you agree that your contribution is licensed under the same
AGPL-3.0-or-later terms as the rest of the project, or, for a contribution to the prompt
library in `prompts/`, under CC BY-SA 4.0. There is no contributor licence agreement and no
copyright assignment.
