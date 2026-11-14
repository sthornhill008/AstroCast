# P01 Revision History and Extracted Records

Current release: 3.7 (2026-10-20). Previous release: 3.6 (2026-10-15).

Source: AstroCast_EnhancedBirthInterp_Prompt_v3_6.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`), whose chart export carries Ceres, Pallas, Juno, Vesta and Black Moon Lilith, the mean lunar apogee, when its Points switch is on (branches Asteroids, AsteroidsExport, AsteroidsTiming and AsteroidsPanel). This is run 1 of 4 of the 1.6.0 library work (Steve's decision L1, 2026-10-20: P01, P02, P26 and P27 here; runs 2 to 4 take the timing, the returns and relocation, and the relationship and event charts). The decisions it rests on: topic 6d (Steve, 2026-10-19), a short shared block in each reader, saying what the five mean, weighing them as minor, conjunctions to the personal points and angles first, at most a short paragraph each, never rulership or dignity; L2, the block itself, drafted by Claude and approved by Steve, 2026-10-20 (`v160-shared-block.md` in the maintainer's archive, 37 lines, SHA-256 fc43adedaf8f4a2e7db64ba5f272fc747ce70fdf06ef427d8610722b027106d5; B below), pasted verbatim; and Claude's default for this run, which Steve may overrule: P01 reads the five inside its existing sections, with no section of their own. The truth, at fe6e238: the golden `ChartMarkdownFormatter_ToMarkdown_J2000NYC_MinorPoints.golden.md` under `tests\AstroCast.Tests\Formatting\Golden\Goldens\` (G) and CLAUDE.md's *Minor points (asteroids and Lilith)* entry with its Points row part (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v3.7's; the replaced v3.6 passages follow as extracted records with their v3.6 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v3.7, the documentation line links the v3_7 guide and history, the Version line reads 3.7 and the Date line 2026-10-20 (Extract 1).
- **:7, Compatible Export.** AstroCast 1.6.0; older chart exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229, the switch, default false and never saved; CL :354-361, the Points row) (Extract 1).
- **:463-465, counting convention.** One sentence: the asteroids and Lilith never count toward house emphasis (topic 6d: minor points, never leading the reading). Nothing was replaced.
- **:902-940, the shared block.** At the end of Section 1, Preamble, after its code block and before the rule that precedes Content Discipline: the block's 37 lines verbatim under its own heading, *Asteroids and Lilith (minor points, AstroCast 1.6.0)* (L2), then the line naming this family's section, `## Asteroids and Lilith` (G :49), to which the block refers. The block's statements of the export rest on G :51 (the opening line: the mean lunar apogee; the five Ptolemaic aspects at a 2°00′ orb, to the Sun to Pluto, the Ascendant and the Midheaven only), G :53-71 (the positions, then the aspects) and CL :231 and :292 (a horary chart never has the five). Nothing was replaced.
- **:1072-1076, Section 2.** After the Global target lines: when the export carries the asteroids and Lilith (`## Asteroids and Lilith`, G :49) they are read inside sections 2 and 3 as the block says, with no section of their own; the list and its word ranges are unchanged (Claude's default). Nothing was replaced.
- **:1139-1141, Compliance Gate.** Under Content quality, one item: the five read as the block says when the export carries `## Asteroids and Lilith` (G :49), and never mentioned when it does not. Nothing was replaced.
- **:1358-1360, Default Glossary.** Group 5, Points and Lots, gains the asteroids Ceres, Pallas, Juno and Vesta and Black Moon Lilith among the terms to define when the body uses them (Extract 2).
- **:1761-1762, Appendix B.** The legend gains the column abbreviations of the five's two tables that it lacked, `Lat`, `RA`, `Dec` and `Dir` (G :55, `| Body | Position | Lat | RA | Dec | House | Speed | Dir | OOB |`), in its paired form. `OOB` was already there, and `Ap/Sp` (G :65, `| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |`) is read by the existing Ap and Sp rows; the cells' units (`/d`, G :57, and the right ascension's hours, minutes and seconds) are not column abbreviations and stay out, as before. Nothing was replaced.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: on the Chart panel's Houses section tick Points, "Include asteroids and Lilith" (CL :355-356), off at every launch, never saved, kept by Clear Form and Load (CL :360-361); ticking recasts the chart on screen without saving it (CL :368-372); then export the chart's Markdown (**Save Markdown**, `src\AstroCast\MainWindow.xaml` :738 at fe6e238; **Export Markdown** in the Export & Backup panel's Chart Export group, :2390, :2396 and :2403); the section sits directly after the Out-of-Bounds Planets table (G :43-49; CL :262-263); a horary chart never carries the five (CL :231).

Unchanged, by instruction: every other rule of v3.6, among them the orb sentence of the shared rules (:21) and Appendix B's orb paragraph (:1764), which name 1.3.4 and stay true, and the section list with its word ranges.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 3.6 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; a Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. H (T-D8, T-D9, T-D10, T6-4; R8): a node's firdaria period is undivided ("Current Sub-Period: none"), the periods in Abu Ma'shar's order with the nodes last; the profected sign advances one sign a year from the Ascendant's sign and its ruler is the lord of the year; zodiacal releasing runs from Spirit only in 360-day years, Level 2 in months with the loosing of the bond, Angular, Succedent and Cadent counted from Fortune (timing section, checklists, glossary). R4: the Behenian entry keeps Agrippa's fifteen and says the export flags 16. Older lines made true: the Mutual Receptions list names the type (the strength is the JSON's, and among the mixes only Mixed (Domicile-Exaltation) is Strong); the Planet Conditions table has no Sun row and its Score and Conditions columns are the JSON's accidentalScore and conditions; the station Proximity column reads "N% of threshold"; the paran angle prints "Anti-Culm."; the Mansion Keywords line; the declination zone in the export's form; the submission templates name the chart's own house system; the example header carries UTC Offset before Julian Day. Run 1b (2026-10-15): the four submission templates' "Note and/or Source fields" and the field guide's **Note** read Notes, the export's label.

Its changes, and the earlier releases', are listed in the 3.6 revision history (in Prompts.zip).

## Extract 1 — Header (v3.6 lines 1-7)

Source lines 1-7 of v3.6; lines 1, 3 and 5-7 are replaced by v3.7's lines 1, 3 and 5-7.

~~~~text
# AstroCast — Enhanced Birth Chart Interpretation Prompt — v3.6

**Operator documentation:** P01 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 3.6
**Date:** 2026-10-15
**Compatible Export:** AstroCast Chart Markdown Export, AstroCast 1.3.4
~~~~

## Extract 2 — Default Glossary (v3.6 line 1307)

Source line 1307 of v3.6, replaced by v3.7's lines 1358-1360.

~~~~text
  5. Points and Lots (Lot of Fortune, Lot of Spirit, topic-specific Lots)
~~~~
