# P07 Revision History and Extracted Records

Current release: 1.14 (2026-11-28). Previous release: 1.13 (2026-10-20).

Source: P07_AstroCast_LunarReturnInterp_Prompt_V1_13.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. One of the review's findings (Q19) and one line for a change of AstroCast 1.7.2 (code finding A2: the export's profection summary takes "a" or "an" by the ordinal). Line numbers are v1.14's; the replaced v1.13 passages are in the v1.13 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v1.14, the documentation line links the V1_14 guide and history, the Version line reads 1.14 and the Date line 2026-11-28.
- **:9, Compatible Export.** AstroCast 1.7.2; older exports lack nothing this prompt relies on, and the line says only that their own profection summary may read "a 8th".
- **:115-122, Sequence position (Q19).** The export's `**Sequence:** #n` counts the returns in the range asked for (the prompt's own :77-78 says so), so #1 is the first return of that range; the month's place in the solar return year comes from the return's date and the birthday. v1.13 called Sequence #1 the return "just after the birthday", which is false of every report whose range does not begin at a birthday: the golden's `**Sequence:** #1` is a return of 2026-04-04 for a birth on 1 January.
- **:301-304, Timing Context (A2).** The one-line timing reference takes "a" or "an" by the ordinal ("an 8th house").

The User Guide 1.3 names the Lunar Return export (it said "the matching chart export") and says what the Sequence number counts.

Unchanged, by instruction: every other rule of v1.13. A lunar return's three time-lord sections still read the year running at the return, in AstroCast 1.7.2 as before.

## Previous changes — 1.13 (2026-10-20)

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose lunar return, when the natal input carries Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the Points switch on), is cast with the return chart's own five (branches Asteroids and AsteroidsExport). This is run 3 of 4 of the 1.6.0 library work (Steve's decision L1: P06, P07, P08 and P11 here, with one correction to P04; runs 1 and 2 accepted). The decisions it rests on: topic 6d and L2, the shared block, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and topic 6d's families (Steve, 2026-10-20): a return chart's own five are read as the natal ones, briefly, for the return's period. The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `LunarReturnMarkdownFormatter_J2000NYC_2026April_MinorPoints.golden.md` (LRG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry, its exports and Points row parts (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.13's; the replaced v1.12 passages follow as extracted records with their v1.12 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.13, the documentation line links the V1_13 guide and history, the Version line reads 1.13 and the Date line 2026-10-20 (Extract 1).
- **:9, Compatible Export.** AstroCast 1.6.0, in runs 1 and 2's words: older lunar return exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:470-508, the shared block and the family line.** At the end of Your Four Analytical Layers, after the preamble's code fence that closes them and before the rule that precedes `## 5. Output PDF Structure`, as run 1 placed P01's: the block's 37 lines verbatim under its own heading (L2), then P07's family line as Steve wrote it. It rests on the return chart's `## Asteroids and Lilith` (LRG :173), directly after its Planetary Positions (LRG :135, under `## Return Chart`, LRG :127) and before `## Essential Dignities` (LRG :195); the limits of the opening line (LRG :175); each recast return carrying its own five (CL :246-248; CL :289-290); and the export's single section of that name, the return's (LRG :173 is the only line naming it). Nothing was replaced.
- **:528-532, Output PDF Structure.** After the section list: a contact to the return's Ascendant or Midheaven read in Section 1 (This Month's Framework), to its Moon in Section 2 (The Month's Emotional Focus) and to its Sun in Section 3 (Activities, Attention and Friction Points), where those are read; anything else in a sentence or two in Section 4, Underlying Patterns and Monthly Synthesis, the section that reads the chart's lesser factors (Arabic Parts, midpoints, declination, out of bounds); no section of their own (topic 6d). P07 has no key-factors table, so no listing is named. The list is unchanged. Nothing was replaced.
- **:615-617, Compliance Gate.** Under Methodology, one item, as P06 v1.14's. Nothing was replaced.
- **:727 and :730, Default Glossary.** Group 9, Lots and Hellenistic Points, becomes Lots, Hellenistic Points and Minor Points (Extract 2) and gains the entry Asteroids and Black Moon Lilith, as P06 v1.14's (B :8-10; LRG :173 and :175).

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: tick Points on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356), off at every launch and kept by Clear Form and Load (CL :360-361), ticking recasting the chart on screen without saving it (CL :368-372); then, on the Returns panel, **Calculate Lunar Returns** and **Export Lunar Return MD** (:1217 and :1227), the other panels taking the new chart only at their next Calculate (CL :372); the section directly after the return's Planetary Positions (LRG :135-173); a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v1.12, among them the section list and its word targets and the once-per-series rule.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.12 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. H (T-D8, T-D9; R8): the timing line names the profected sign and its ruler, the profected sign one sign a year from the Ascendant's; a node's firdaria period is undivided; the dated windows name the major period in a node's period. Older lines made true: the lunar return export prints the time-lord sections "(at Return Ingress)" and the Return-to-Natal Declination Aspects table itself.

Its changes, and the earlier releases', are listed in the 1.12 revision history (in Prompts.zip).

## Extract 1 — Header (v1.12 lines 1-9)

Source lines 1-9 of v1.12; lines 1, 3, 5-6 and 9 are replaced by v1.13's lines 1, 3, 5-6 and 9.

~~~~text
# AstroCast Lunar Return Interpretation Prompt — v1.12

**Operator documentation:** P07 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.12
**Date:** 2026-10-15
**Method:** Four-layer (Hand / Greene / Sasportas / Ebertin / Forrest / Tyl — condensed for monthly scope)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Lunar Return Export, AstroCast 1.3.4 (earlier exports: v1.0.41+, with declination data v1.0.42+, techniques data v1.0.101+)
~~~~

## Extract 2 — Default Glossary (v1.12 line 678)

Source line 678 of v1.12, replaced by v1.13's line 727.

~~~~text
**9. Lots and Hellenistic Points**
~~~~
