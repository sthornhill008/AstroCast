# P13 Revision History and Extracted Records

Current release: 1.12 (2026-10-20). Previous release: 1.11 (2026-10-15).

Source: AstroCast_CompositeInterp_Prompt_V1_11.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose composite export, when both charts carry Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the Points switch on), prints the composite five (branches Asteroids and AsteroidsExport). This is run 4 of 4 of the 1.6.0 library work, which it completes (Steve's decision L1: P12, P13, P16 and P17 here; runs 1 to 3 accepted). The decisions it rests on: topic 6d and L2, the shared block, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and topic 6d's families (Steve, 2026-10-20): composite reads the composite five, with a note on Juno (commitment and the terms of the bond) and Lilith (never a prediction of infidelity). The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `CompositeMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md` (CG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry, its exports and Points row parts (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.12's; the replaced v1.11 passages follow as extracted records with their v1.11 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.12, the documentation line links the V1_12 guide and history, the Version line reads 1.12 and the Date line 2026-10-20 (Extract 1).
- **:9, Compatible Export.** AstroCast 1.6.0, in runs 1 to 3's words: older composite exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:533-534, Out-of-Bounds planets.** The composite asteroids and Lilith likewise have no out-of-bounds column (CG :43, `| Body | Position | Lat | House | Speed | Dir |`; CG :6, the `**Not shown:**` line; CL :272) (Extract 2).
- **:546-584, the shared block and the family line.** At the end of Your Six Analytical Layers and directly before Source Chart Context, inside the Preamble's code fence as those headings are (P13 has the whole prompt pasted), as run 3 placed P06's: the block's 37 lines verbatim under its own heading (L2), then P13's family line as Steve wrote it. It rests on the composite's `## Asteroids and Lilith` (CG :37), directly after its Planetary Positions (CG :20); each point the short-arc midpoint of the two charts' own, housed against the composite houses (CL :284-288); its positions (CG :43, no declination, right ascension or out-of-bounds) and its aspects `| Body 1 | Aspect | Body 2 | Orb |` (CG :53, no applying or separating mark), such as `| Ceres | tri | ASC |` (CG :56); and the export's single section of that name, with neither person's own five (CG :37 is the only line naming it). Nothing was replaced.
- **:688-692, Output PDF Structure.** After the table: a contact to the composite Ascendant, Midheaven, Sun or Moon read where those are read (Sections 1 to 3), anything else in a sentence or two in Section 5, Hidden Patterns; no section of their own; Section 7, the Key Factors Reference Table, may list one, marked as a minor point (topic 6d). The table is unchanged. Nothing was replaced.
- **:733-735, Compliance Gate.** Under Methodology, one item, as P12 v1.13's. Nothing was replaced.
- **:910, Default Glossary.** P13's glossary has no Lots group (Lots do not apply to a composite), so group 1, Composite Chart Basics, gains the entry beside the composite Ascendant, Sun and Moon: Composite Asteroids and Black Moon Lilith, the short-arc midpoints of the two charts' Ceres, Pallas, Juno and Vesta, the first four asteroids discovered (B :8-9), and of their Black Moon Lilith, the mean lunar apogee, the far point of the Moon's orbit (CG :39; B :9-10), printed in the composite's `## Asteroids and Lilith` section (CG :37) when the Points switch is on, defined only when the body uses them. Nothing was replaced.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: tick Points on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356), off at every launch and kept by Clear Form and Load (CL :360-361), ticking recasting the chart on screen without saving it (CL :368-372); then, on the Synastry & Composite panel, **Calculate Composite** and **Export Composite MD** (:756, :849 and :859), Person B's chart cast with the box's value at that moment (CL :365-367); the composite section (CG :37), neither person's own five printed; a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v1.11, among them the Composite Principle, the section list with its word targets and the techniques not applicable to composite charts.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.11 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 and notes that from 1.3.4 a composite carries no declination data (R3). L2 (S-D4; R12): the header's Not shown line is stated and the techniques it names are read as absent (declinations, right ascensions, out-of-bounds, declination aspects, stations, planet conditions, applying/separating marks, the Moon's next aspect and void of course); the three scans become the fixed-star scan. L3 (S-D2, S-D3, Q1; R12): Mercury and Venus may take the midpoint nearer the composite Sun, with a Note, and are then no blended middle ground; the MC is the midpoint of the two Midheavens; the houses are cast at the mean birth latitude in the shared system, else Placidus; the Reference Location is the mean of the two birthplaces. O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. R4: the Behenian entry. Older lines made true: the source summaries print no Type, Gender, Notes or Source field; the reception strengths; the last phase is named Waning Crescent. Run 3, Part A (2026-10-15): a composite planet under the beams is read from the composite longitudes, the export printing no planet conditions for a composite.

Its changes, and the earlier releases', are listed in the 1.11 revision history (in Prompts.zip).

## Extract 1 — Header (v1.11 lines 1-9)

Source lines 1-9 of v1.11; lines 1, 3, 5-6 and 9 are replaced by v1.12's lines 1, 3, 5-6 and 9.

~~~~text
# AstroCast Composite Chart Interpretation Prompt — v1.11

**Operator documentation:** P13 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.11
**Date:** 2026-10-15
**Method:** Six-layer (Hand / Greene / Sasportas / Ebertin / Forrest / Tyl)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Composite Chart Export, AstroCast 1.3.4 (earlier exports: v1.0.39+, with declination data v1.0.42+; from 1.3.4 a composite carries no declination data)
~~~~

## Extract 2 — Out-of-Bounds (v1.11 line 533)

Source line 533 of v1.11, replaced by v1.12's lines 533-534.

~~~~text
and the positions table has no OOB column.
~~~~
