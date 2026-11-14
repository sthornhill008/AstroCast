# P16 Revision History and Extracted Records

Current release: 3.8 (2026-10-20). Previous release: 3.7 (2026-10-15).

Source: AstroCast_EventChartInterp_Prompt_V3_7.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`): an event chart is a chart export, and with the Points switch on it carries a `## Asteroids and Lilith` section (branches Asteroids and AsteroidsExport). This is run 4 of 4 of the 1.6.0 library work, which it completes (Steve's decision L1: P12, P13, P16 and P17 here; runs 1 to 3 accepted). The decision it rests on: topic 6d's families (Steve, 2026-10-20), P16 and P17 get one line setting the section aside and no shared block. The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `ChartMarkdownFormatter_ToMarkdown_J2000NYC_MinorPoints.golden.md` (CMG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry and its exports part (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v3.8's; the replaced v3.7 passages follow as extracted records with their v3.7 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v3.8, the documentation line links the V3_8 guide and history, the Version line reads 3.8 and the Date line 2026-10-20 (Extract 1).
- **:10, Compatible Export.** AstroCast 1.6.0, in runs 1 to 3's words: older event chart exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:274, Instructions for Use.** P16's family line as Steve wrote it, one paragraph directly after the paragraph that begins "Paste the complete AstroCast event chart export after this": the section `## Asteroids and Lilith` (CMG :49), printed by the chart export with the Points switch on (CL :262-263) and never for a horary chart (CL :231), with Ceres, Pallas, Juno, Vesta and Black Moon Lilith, the mean lunar apogee (CMG :51), set aside, the prompt reading the event's own techniques. Nothing was replaced.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: an event chart exported with Points ticked on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356) carries the section (CMG :49), which this prompt sets aside.

Unchanged, by instruction: every other line of v3.7. No shared block is added.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 3.7 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. R8: the event significators are the engine's (Sun: Political; Moon: RealEstate, Relocation; Mercury: Contract, TechnologyLaunch, Education; Venus: Wedding, CreativeProject; Mars: Surgery; Jupiter: BusinessLaunch, Travel, LegalFiling, Investment; Saturn: JobStart, Organization; none for Birth, Event, Horary, Electional, Other). Older lines made true: the **Type:** line prints the engine's event types, the military and other named kinds arriving as Event or Other (the routing tree and the Combat/Military trigger); the Markdown has no joy column; the export scores no terms and prints Exalted and Face; the paran angle prints Anti-Culm.; an aspect to the ASC or MC is applying with the angle held still.

Its changes, and the earlier releases', are listed in the 3.7 revision history (in Prompts.zip).

## Extract 1 — Header (v3.7 lines 1-10)

Source lines 1-10 of v3.7; lines 1, 3, 5-6 and 10 are replaced by v3.8's lines 1, 3, 5-6 and 10.

~~~~text
# AstroCast Event Chart Interpretation Prompt (Type A) — v3.7

**Operator documentation:** P16 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 3.7
**Date:** 2026-10-15
**Derived From:** AstroCast Event Chart Interpretation Prompt v3.1
**Method:** Eight-layer Standalone Event Chart (Brennan / Lilly / Forrest / Ebertin / Robson / Brady / Classical Electional & Sect)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Event Chart Export, AstroCast 1.3.4 (earlier exports: Phases 1–6 + Fixed Stars + Parans with declination data v1.0.42+, parans v1.0.61+, techniques data v1.0.101+)
~~~~
