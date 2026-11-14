# P02 Revision History and Extracted Records

Current release: 1.5 (2026-10-20). Previous release: 1.4 (2026-10-15).

Source: AstroCast_BasicInterp_Prompt_V1_4.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose chart export carries Ceres, Pallas, Juno, Vesta and Black Moon Lilith, the mean lunar apogee, when its Points switch is on (branches Asteroids, AsteroidsExport, AsteroidsTiming and AsteroidsPanel). This is run 1 of 4 of the 1.6.0 library work (Steve's decision L1, 2026-10-20: P01, P02, P26 and P27 here; runs 2 to 4 take the timing, the returns and relocation, and the relationship and event charts). The decisions it rests on: topic 6d (Steve, 2026-10-19), a short shared block in each reader, saying what the five mean, weighing them as minor, conjunctions to the personal points and angles first, at most a short paragraph each, never rulership or dignity; L2, the block itself, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and Claude's default for this run, which Steve may overrule: P02 reads the five under its approach 1A only. The truth, at fe6e238: the golden `ChartMarkdownFormatter_ToMarkdown_J2000NYC_MinorPoints.golden.md` under `tests\AstroCast.Tests\Formatting\Golden\Goldens\` (G) and CLAUDE.md's *Minor points (asteroids and Lilith)* entry with its Points row part (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.5's; the replaced v1.4 passage follows as an extracted record with its v1.4 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.5, the documentation line links the V1_5 guide and history, the Version line reads 1.5 and the Date line 2026-10-20 (Extract 1).
- **:7, Compatible Export.** AstroCast 1.6.0; older chart exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229, the switch, default false and never saved; CL :354-361, the Points row) (Extract 1).
- **:96-134, the shared block.** At the end of 1A, Blended Approach (Recommended), after its code block and before the rule that precedes 1B: the block's 37 lines verbatim under its own heading, *Asteroids and Lilith (minor points, AstroCast 1.6.0)* (L2), then the line naming this family's section, `## Asteroids and Lilith` (G :49), to which the block refers. The block's statements of the export rest on G :51 (the opening line: the mean lunar apogee; the five Ptolemaic aspects at a 2°00′ orb, to the Sun to Pluto, the Ascendant and the Midheaven only), G :53-71 (the positions, then the aspects) and CL :231 and :292 (a horary chart never has the five). Nothing was replaced.
- **:171-173, 1B.** Inside 1B's preamble, after its CONTEXT HANDLING line, one sentence: when present, set the export's `## Asteroids and Lilith` section (G :49) aside; Ebertin's method uses none of these points (the export gives the five no midpoints, CL :234-236). Nothing was replaced.
- **:210-212, 1C.** Inside 1C's preamble, after its CONTEXT HANDLING line, the same sentence for the traditional delineation, which uses none of these points (B :14-15: traditional astrology knows none of them; CL :234-236: the export gives the five no dignities, conditions or Lots). Nothing was replaced.
- **:353, glossary.** One entry, Asteroids and Black Moon Lilith: the first four asteroids discovered (B :8-9) and the mean lunar apogee, the far point of the Moon's orbit (G :51; B :9-10), minor modern points printed in the export's `## Asteroids and Lilith` section (G :49) when the Points switch is on, defined only when the body uses them. Nothing was replaced.
- **:369, Compliance Requirements.** One Pre-Delivery item: under 1A the five read as the block says when the export carries `## Asteroids and Lilith` (G :49), and never mentioned when it does not; under 1B and 1C the section set aside. Nothing was replaced.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: on the Chart panel's Houses section tick Points, "Include asteroids and Lilith" (CL :355-356), off at every launch, never saved, kept by Clear Form and Load (CL :360-361); ticking recasts the chart on screen without saving it (CL :368-372); then export the chart's Markdown (**Save Markdown**, `src\AstroCast\MainWindow.xaml` :738 at fe6e238; **Export Markdown** in the Export & Backup panel's Chart Export group, :2390, :2396 and :2403); the section sits directly after the Out-of-Bounds Planets table (G :43-49; CL :262-263); a horary chart never carries the five (CL :231); 1A reads them, 1B and 1C set them aside.

Unchanged, by instruction: every other rule of V1.4, among them the orb sentence of the shared rules (:21), which names 1.3.4 and stays true, and the three preambles' methods.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.4 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; a Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. Older lines made true: the three preambles and the standard submission name the chart's own house system (Placidus by default); the biography field of the standard submission is Notes, the export's label. Run 1b (2026-10-15): the focus-area submission's "Note and/or Source fields" reads Notes, the export's label.

Its changes, and the earlier releases', are listed in the 1.4 revision history (in Prompts.zip).

## Extract 1 — Header (v1.4 lines 1-7)

Source lines 1-7 of v1.4; lines 1, 3 and 5-7 are replaced by v1.5's lines 1, 3 and 5-7.

~~~~text
# AstroCast — Birth Chart Interpretation Prompt — v1.4

**Operator documentation:** P02 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.4
**Date:** 2026-10-15
**Compatible Export:** AstroCast Chart Markdown Export, AstroCast 1.3.4
~~~~
