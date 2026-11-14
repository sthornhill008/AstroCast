# P12 Revision History and Extracted Records

Current release: 1.13 (2026-10-20). Previous release: 1.12 (2026-10-15).

Source: AstroCast_SynastryInterp_Prompt_V1_12.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose synastry export, when both charts carry Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the Points switch on), prints each person's five and their inter-chart contacts in a section of their own (branches Asteroids and AsteroidsExport). This is run 4 of 4 of the 1.6.0 library work, which it completes (Steve's decision L1: P12, P13, P16 and P17 here; runs 1 to 3 accepted). The decisions it rests on: topic 6d and L2, the shared block, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and topic 6d's families (Steve, 2026-10-20): synastry reads each chart's five to the other's planets and angles, with a note on Juno (commitment and the terms of the bond) and Lilith (attraction, power and what each exposes in the other, never a prediction of infidelity). The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `SynastryMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md` (SG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry, its exports and Points row parts (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.13's; the replaced v1.12 passages follow as extracted records with their v1.12 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.13, the documentation line links the V1_13 guide and history, the Version line reads 1.13 and the Date line 2026-10-20 (Extract 1).
- **:8, Compatible Export.** AstroCast 1.6.0, in runs 1 to 3's words: older synastry exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:241, synastry orbs.** One line in the passage's indentation, inside the Preamble's code fence: the asteroids and Lilith take 2° in every pair, in their own section, never these categories (CL :280-281, the majors at 2.0° inclusive, never through the synastry orbs; SG :1064). Nothing was replaced.
- **:711-749, the shared block and the family line.** Inside the Preamble's code fence, which P12's Instructions have the operator copy, directly before `OUTPUT PDF STRUCTURE :`, a blank line on each side: the block's 37 lines verbatim under its own heading (L2), then P12's family line as Steve wrote it. It rests on each person's own section, `## Asteroids and Lilith` under `## Person A: J2000 NYC Fixture` (SG :9 and :57) and under `## Person B: Secondary Fixture` (SG :493 and :541); `## Inter-Chart Aspects: Asteroids and Lilith` (SG :1062), directly after the Inter-Chart Aspects (SG :988; CL :282-283), its opening line (SG :1064) and its columns `| A Body | Aspect | B Body | Orb | Notes |` (SG :1066), with rows such as `| A Ceres | cnj | B Uranus |` (SG :1068), `| A Lilith | opp | B Sun |` (SG :1070) and `| A Juno | opp | B Mars |` (SG :1071); each chart's five to the other chart's Sun to Pluto, Ascendant and Midheaven, never five to five (CL :279-280); and no house overlays for the five (CL :284, scoping decision 8). Nothing was replaced.
- **:769-772, Output PDF Structure.** After the section table, before the closing elements: the inter-chart contacts of the five read inside the existing sections where their themes fall, Juno with commitment and Lilith with attraction or challenge, at most a short paragraph each, with no section of their own (topic 6d). The table is unchanged. Nothing was replaced.
- **:825-827, Compliance Gate.** Under Methodology, one item: the five read as the block and the family line say when the export carries them, and never mentioned when it does not. Nothing was replaced.
- **:1040 and :1046, Default Glossary.** Group 7, Lots and Hellenistic Points, becomes Lots, Hellenistic Points and Minor Points (Extract 2), as in P06 to P08, and gains an entry, Asteroids and Black Moon Lilith: the first four asteroids discovered (B :8-9) and the mean lunar apogee, the far point of the Moon's orbit (SG :59; B :9-10), printed in each person's `## Asteroids and Lilith` section (SG :57) with their contacts in `## Inter-Chart Aspects: Asteroids and Lilith` (SG :1062) when the Points switch is on, defined only when the body uses them.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: tick Points on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356), off at every launch and kept by Clear Form and Load (CL :360-361), ticking recasting the chart on screen without saving it (CL :368-372); then, on the Synastry & Composite panel, **Calculate Synastry** and **Export Synastry MD** (:756, :802 and :812), Person B's chart cast with the box's value at that moment (CL :365-367); each person's section (SG :57 and :541) and the inter-chart section (SG :1062); a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v1.12, among them the synastry orb table, the Balance Protocol, the house overlays and the section list with its word targets.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.12 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). S-D5 (R7): the synastry orb table, the wider of the two bodies' categories (luminary, personal, outer, angle), the Notes column's "exact" below 10′ and "wide" above 5°, and no applying or separating between two charts. O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. R4: the Behenian entry keeps Agrippa's fifteen and says the export flags 16. Older lines made true (Y1-4, Y1-5): the inter-chart aspects include A's bodies to B's angles and B's bodies to A's; the inter-chart declination table pairs bodies only; each chart is in its own house system; the Dignity and Score, and Score and Conditions, columns; the reception strengths; the Relationship field's printed values; Part of Marriage; no cross-chart antiscia table, so they are found from the longitudes. Run 3, Part A (2026-10-15): the three submission templates' "Note and/or Source fields" read Notes, the export's label.

Its changes, and the earlier releases', are listed in the 1.12 revision history (in Prompts.zip).

## Extract 1 — Header (v1.12 lines 1-8)

Source lines 1-8 of v1.12; lines 1, 3, 5-6 and 8 are replaced by v1.13's lines 1, 3, 5-6 and 8.

~~~~text
# AstroCast — Synastry Interpretation Prompt — v1.12

**Operator documentation:** P12 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.12
**Date:** 2026-10-15
**Target Audience:** General audience with basic astrology interest; not professional astrologers
**Compatible Export:** AstroCast synastry export, AstroCast 1.3.4 (earlier exports: v1.0.101+; Relationship field optional)
~~~~

## Extract 2 — Default Glossary (v1.12 line 991)

Source line 991 of v1.12, replaced by v1.13's line 1040.

~~~~text
**7. Lots and Hellenistic Points**
~~~~
