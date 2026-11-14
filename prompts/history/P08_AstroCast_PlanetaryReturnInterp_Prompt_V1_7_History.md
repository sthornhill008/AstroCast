# P08 Revision History and Extracted Records

Current release: 1.7 (2026-10-20). Previous release: 1.6 (2026-10-15).

Source: AstroCast_PlanetaryReturnInterp_Prompt_V1_6.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose planetary return, when the natal input carries Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the Points switch on), is cast with the return chart's own five (branches Asteroids and AsteroidsExport). This is run 3 of 4 of the 1.6.0 library work (Steve's decision L1: P06, P07, P08 and P11 here, with one correction to P04; runs 1 and 2 accepted). The decisions it rests on: topic 6d and L2, the shared block, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and topic 6d's families (Steve, 2026-10-20): a return chart's own five are read as the natal ones, briefly, for the return's period. The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `PlanetaryReturnMarkdownFormatter_J2000NYC_Saturn1_MinorPoints.golden.md` (PRG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry, its exports and Points row parts (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.7's; the replaced v1.6 passages follow as extracted records with their v1.6 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.7, the documentation line links the V1_7 guide and history, the Version line reads 1.7 and the Date line 2026-10-20 (Extract 1).
- **:9, Compatible Export.** AstroCast 1.6.0, in runs 1 and 2's words: older planetary return exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:440-478, the shared block and the family line.** At the end of Your Five Analytical Layers and directly before Timing Technique Integration, inside the preamble's code fence as those headings are: the block's 37 lines verbatim under its own heading (L2), then P08's family line as Steve wrote it. It rests on the return chart's `## Asteroids and Lilith` (PRG :188), directly after its Planetary Positions (PRG :148, under `## Return Chart`, PRG :140) and before `## Essential Dignities` (PRG :212); the limits of the opening line (PRG :190); each recast return carrying its own five (CL :246-248; CL :289-290); and the export's single section of that name, the return's (PRG :188 is the only line naming it). Nothing was replaced.
- **:529-533, Output PDF Structure.** After the section list, as P07 v1.13's: a contact to the return's Ascendant or Midheaven read in Section 1 (The Cycle Ahead) and to its Moon in Section 2 (The Cycle's Emotional and Instinctive Dimension), where those are read; anything else, the Sun's included (no section of P08 reads the return Sun), in a sentence or two in Section 5, Hidden Patterns and the Cycle's Deeper Story, the section that reads the chart's lesser factors (midpoints, declination, fixed stars, parans); no section of their own; Section 7, the Key Factors Reference Table, may list one, marked as a minor point, as P06 v1.14's Section 8 (topic 6d). The list and the Per-Planet Word Targets are unchanged. Nothing was replaced.
- **:631-633, Compliance Gate.** Under Methodology, one item, as P06 v1.14's. Nothing was replaced.
- **:755 and :758, Default Glossary.** Group 9, Lots and Hellenistic Points, becomes Lots, Hellenistic Points and Minor Points (Extract 2) and gains the entry Asteroids and Black Moon Lilith, as P06 v1.14's (B :8-10; PRG :188 and :190).

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: tick Points on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356), off at every launch and kept by Clear Form and Load (CL :360-361), ticking recasting the chart on screen without saving it (CL :368-372); then, in the Returns panel's Planetary Return section (:1234), **Calculate Return** and **Export Return MD** (:1304 and :1314), the other panels taking the new chart only at their next Calculate (CL :372); the section directly after the return's Planetary Positions (PRG :148-188); a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v1.6, among them the planet-specific frameworks, the section list and the Per-Planet Word Targets.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.6 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). F (T-D6, T2-2): a return is cast at its passage's first pass, the others printed as Other passes; Mercury, like Venus, returns about once a year as seen from the Earth, so its return number stays near the age and its scope is about a year (cycle notes, preamble, return-number context, glossary). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. H (R8): the dated windows name the major period when a node's firdaria period is undivided. Older lines made true: the export carries the natal planet's longitude, not the natal chart; its time-lord sections are printed "(at Return Ingress)" for the year running at the return; it prints the Return-to-Natal Declination Aspects table itself; the dignity label is "Exalted".

Its changes, and the earlier releases', are listed in the 1.6 revision history (in Prompts.zip).

## Extract 1 — Header (v1.6 lines 1-9)

Source lines 1-9 of v1.6; lines 1, 3, 5-6 and 9 are replaced by v1.7's lines 1, 3, 5-6 and 9.

~~~~text
# AstroCast Planetary Return Interpretation Prompt — v1.6

**Operator documentation:** P08 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.6
**Date:** 2026-10-15
**Method:** Five-layer (Structural / Energetic / Relational / Hidden / Synthesis) with planet-specific framework
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Planetary Return Export, AstroCast 1.3.4 (earlier exports: v1.0.141+)
~~~~

## Extract 2 — Default Glossary (v1.6 line 706)

Source line 706 of v1.6, replaced by v1.7's line 755.

~~~~text
**9. Lots and Hellenistic Points**
~~~~
