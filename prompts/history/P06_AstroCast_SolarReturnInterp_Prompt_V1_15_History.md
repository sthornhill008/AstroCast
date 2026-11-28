# P06 Revision History and Extracted Records

Current release: 1.15 (2026-11-28). Previous release: 1.14 (2026-10-20).

Source: P06_AstroCast_SolarReturnInterp_Prompt_V1_14.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. One change of AstroCast 1.7.2 (code finding A1: a solar return export reads its three time-lord sections for one year, the year the return opens), two of the review's findings (Q6, Q7) and Steve's three rulings on the voice, the data note, and orbs and dates. Line numbers are v1.15's; the replaced v1.14 passages are in the v1.14 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v1.15, the documentation line links the V1_15 guide and history, the Version line reads 1.15 and the Date line 2026-11-28.
- **:9, Compatible Export.** AstroCast 1.7.2; the line says that an older export may print two ages, the Firdaria summary's a year less than the profection's, when the return falls before the birthday.
- **:265-272, Profection Year Framing, item 1 (Q7, ruling 1, A2).** The required opening sentence follows the voice in force: "you are" in the personal client branch, which stays the default, and "[Name] is" in the third person. It takes "a" or "an" by the ordinal, as the export's own summary does from 1.7.2 ("an 8th house profection year"). v1.14 required the third-person form under a second-person default.
- **:302-317, item 6 (A1).** From 1.7.2 the profection's Age, the Firdaria summary's Age and the releasing's current periods are those of the birthday the return's year begins on, and the profection's "(as of ...)" date is that birthday when the return falls before it. The old convention (only the profection's Age rounded to the return; v1.13's R5) is kept for older exports. A lunar or planetary return is unchanged.
- **:331-339, Dated timing windows (Q6, ruling 3).** The as-of date of the three sections is the date the profection prints, never the export's Generated date: the sample reading had dated them to the day the export was made. Each dated window is stated once, in the timing passage that opens Section 1, and referred to elsewhere; an orb is given for the contacts the reading builds on, not for every one named.
- **:341-343, Data note (ruling 2), new.** A data note is written only for a conflict or a gap the reading actually met, quotes both values with their labels, and is left out when there is none.

The User Guide 1.3 names the Solar Return export (it said "the matching chart export") and says the same for its user: one age from 1.7.2, the voice, the as-of date, the data note.

Unchanged, by instruction: every other rule of v1.14, among them the peak-period sentence of item 5 and the section list with its word targets.

## Previous changes — 1.14 (2026-10-20)

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose solar return, when the natal input carries Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the Points switch on), is cast with the return chart's own five (branches Asteroids and AsteroidsExport). This is run 3 of 4 of the 1.6.0 library work (Steve's decision L1: P06, P07, P08 and P11 here, with one correction to P04; runs 1 and 2 accepted). The decisions it rests on: topic 6d and L2, the shared block, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and topic 6d's families (Steve, 2026-10-20): a return chart's own five are read as the natal ones, briefly, for the return's period. The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `SolarReturnMarkdownFormatter_J2000NYC_2026_MinorPoints.golden.md` (SRG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry, its exports and Points row parts (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.14's; the replaced v1.13 passages follow as extracted records with their v1.13 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.14, the documentation line links the V1_14 guide and history, the Version line reads 1.14 and the Date line 2026-10-20 (Extract 1).
- **:9, Compatible Export.** AstroCast 1.6.0, in runs 1 and 2's words: older solar return exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:578-616, the shared block and the family line.** At the end of Your Six Analytical Layers and directly before Return-to-Natal Overlay, inside the preamble's code fence as those headings are: the block's 37 lines verbatim under its own heading (L2), then P06's family line as Steve wrote it. It rests on the return chart's `## Asteroids and Lilith` (SRG :177), directly after its Planetary Positions (SRG :134, under `## Return Chart`, SRG :126) and before `## Essential Dignities` (SRG :202); its aspects to the return chart's own points, such as `| Ceres | sxt | ASC | 0°34′ | Ap |` (SRG :195), within the limits of the opening line (SRG :179); each recast return carrying its own five (CL :246-248; CL :289-290); and the export's single section of that name, the return's, with no natal five printed (SRG :177 is the only line naming it). Nothing was replaced.
- **:657-661, Output PDF Structure.** After the table: the five read inside the existing sections, a contact to the return's Ascendant, Midheaven, Sun or Moon where those are read (Sections 1 to 3), anything else in a sentence or two in Section 6, Hidden Patterns; no section of their own; Section 8, the Key Factors Reference Table, may list one, marked as a minor point (topic 6d). The table is unchanged. Nothing was replaced.
- **:725-727, Compliance Gate.** Under Methodology, one item: the five read as the block and the family line say when the export carries them, and never mentioned when it does not. Nothing was replaced.
- **:927 and :932, Default Glossary.** Group 8, Lots and Hellenistic Points, becomes Lots, Hellenistic Points and Minor Points (Extract 2) and gains an entry, Asteroids and Black Moon Lilith: the first four asteroids discovered (B :8-9) and the mean lunar apogee, the far point of the Moon's orbit (SRG :179; B :9-10), minor modern points printed in the return chart's `## Asteroids and Lilith` section (SRG :177) when the Points switch is on, defined only when the body uses them.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: tick Points on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356), off at every launch and kept by Clear Form and Load (CL :360-361), ticking recasting the chart on screen without saving it (CL :368-372); then, on the Returns panel, **Calculate Solar Return** and **Export Solar Return MD** (:1136 and :1146), the other panels taking the new chart only at their next Calculate (CL :372); the section directly after the return's Planetary Positions (SRG :134-177); a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v1.13, among them the Return-to-Natal Overlay and the section list with its word targets.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.13 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. H (T-D8, T-D9, T6-4; R8): a node's firdaria period is undivided; the profection advances one sign a year from the Ascendant's sign and the lord rules the profected sign; firdaria in Abu Ma'shar's order with the nodes last; Succedent added and the zodiacal releasing places counted from Fortune; the dated windows name the major period in a node's period. R5: only the solar return's profection Age is rounded to the return (Age 26 beside the Firdaria's Age 25 in the same export), the lunar and planetary returns reading the year running at the return; the opening uses the profection's Age. Older lines made true: the export's list names the time-lord sections and the Return-to-Natal Declination Aspects table, which the export prints without a synastry overlay; the station Proximity column reads "N% of threshold". Run 1b (2026-10-15): the glossary's Behenian Star keeps Agrippa's fifteen and says the export flags 16, as P01's does (R4).

Its changes, and the earlier releases', are listed in the 1.13 revision history (in Prompts.zip).

## Extract 1 — Header (v1.13 lines 1-9)

Source lines 1-9 of v1.13; lines 1, 3, 5-6 and 9 are replaced by v1.14's lines 1, 3, 5-6 and 9.

~~~~text
# AstroCast Solar Return Interpretation Prompt — v1.13

**Operator documentation:** P06 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.13
**Date:** 2026-10-15
**Method:** Six-layer (Hand / Greene / Sasportas / Ebertin / Forrest / Tyl)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Solar Return Export, AstroCast 1.3.4 (earlier exports: v1.0.40+, with declination data v1.0.42+, techniques data v1.0.101+, time-lord context for SR/LR/PR exports v1.0.232+, SR-correct profection age semantics v1.0.233+)
~~~~

## Extract 2 — Default Glossary (v1.13 line 878)

Source line 878 of v1.13, replaced by v1.14's line 927.

~~~~text
**8. Lots and Hellenistic Points**
~~~~
