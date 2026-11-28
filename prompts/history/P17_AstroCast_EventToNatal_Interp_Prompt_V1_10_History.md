# P17 Revision History and Extracted Records

Current release: 1.10 (2026-11-28). Previous release: 1.9 (2026-10-20).

Source: P17_AstroCast_EventToNatal_Interp_Prompt_V1_9.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. One item of the run's list B (the orb table's pointer) and one of the review's findings (Q17). Line numbers are v1.10's; the replaced v1.9 passages are in the v1.9 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v1.10, the documentation line links the V1_10 guide and history, the Version line reads 1.10 and the Date line 2026-11-28.
- **:10, Compatible Export.** AstroCast 1.7.2; older exports lack nothing this prompt relies on.
- **:55, Prompt Configuration (Q17).** Protocol 5, Dual-Context Discipline, is active whenever both event and natal contexts are supplied, which is the protocol's own rule (:147-149). The table had said "when 2+3 or 3+4 co-occur", so the ban on a joint-fate forecast could be skipped.
- **:84-94, Routing decision sequence (Q17).** The three event routes are defined here (A, a named individual's own event; B, a mundane event of public record; C, an organization's event, and the route when the kind cannot be told). v1.9 defined them only by pointing at P16's decision tree, under a rule that no companion document is needed.
- **:183-187 (Q17).** The synastry export is all the prompt needs: it embeds both charts whole, so the event's own Ascendant, chart ruler and angular planets are read from the event chart inside it. v1.9 asked for the standalone event chart export as well.
- **:227-243, Cross-aspect analysis (list B).** The synastry orb table is copied in from P12 (v1.13 :237-252, verbatim). v1.9 pointed at `Synastry/AstroCast_SynastryInterp_Prompt_V1_12.md`, the synastry prompt's old file name and version, and at a techniques reference that is not in the library; that reference is gone.

The User Guide 1.3 names the export and says that one export is enough.

Unchanged, by instruction: every other rule of v1.9, among them the other places that name the parent prompt's decision tree.

## Previous changes — 1.9 (2026-10-20)

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`): the Type B export is a synastry export, and with the Points switch on it carries each chart's `## Asteroids and Lilith` section and `## Inter-Chart Aspects: Asteroids and Lilith` (branches Asteroids and AsteroidsExport). This is run 4 of 4 of the 1.6.0 library work, which it completes (Steve's decision L1: P12, P13, P16 and P17 here; runs 1 to 3 accepted). The decision it rests on: topic 6d's families (Steve, 2026-10-20), P16 and P17 get one line setting the section aside and no shared block. The truth, at fe6e238, under `tests\AstroCast.Tests\Formatting\Golden\Goldens\`: the golden `SynastryMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md` (SG); and CLAUDE.md's *Minor points (asteroids and Lilith)* entry and its exports part (CL); every quoted export value below is on the line cited, and a script checked each against its line. Line numbers are v1.9's; the replaced v1.8 passages follow as extracted records with their v1.8 line numbers. The changes:

- **:1, :3, :5, :6, header.** The title reads v1.9, the documentation line links the V1_9 guide and history, the Version line reads 1.9 and the Date line 2026-10-20 (Extract 1).
- **:10, Compatible Export.** AstroCast 1.6.0, in runs 1 to 3's words: older synastry exports still read, and the asteroids and Lilith appear only in exports from 1.6.0 made with the Points switch on (CL :227-229; CL :354-361); the earlier exports are kept, with 1.3.4, 1.4.0 and 1.5.0 named (Extract 1).
- **:172, Instructions for Use.** P17's family line as Steve wrote it, one paragraph directly after the paragraph that begins "Paste the complete AstroCast synastry export after this": each chart's `## Asteroids and Lilith` (SG :57 and :541) and `## Inter-Chart Aspects: Asteroids and Lilith` (SG :1062; CL :279-283), with Ceres, Pallas, Juno, Vesta and Black Moon Lilith, the mean lunar apogee (SG :59), set aside, the prompt reading the event's own techniques. Nothing was replaced.

The User Guide 1.2 gains *Producing the export (AstroCast 1.6.0)*: a Type B synastry export made with Points ticked on the Chart panel's Houses section ("Include asteroids and Lilith", `src\AstroCast\MainWindow.xaml` :634 at fe6e238; CL :355-356) carries each chart's section and the inter-chart section (SG :57, :541 and :1062), which this prompt sets aside.

Unchanged, by instruction: every other line of v1.8. No shared block is added.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.8 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. S-D5 (R6, kept): the cross-aspect section points to the synastry orb table in the synastry prompt V1_12 and the reference's §24, with no copy. R8: the event significators are the engine's (Sun: Political; Moon: RealEstate, Relocation; Mercury: Contract, TechnologyLaunch, Education; Venus: Wedding, CreativeProject; Mars: Surgery; Jupiter: BusinessLaunch, Travel, LegalFiling, Investment; Saturn: JobStart, Organization; none for Birth, Event, Horary, Electional, Other). Older lines made true (Y1-4, Y1-5): each pair is listed once, headed A and B, the ASC and MC the only angles paired; the Inter-Chart Declination table prints only when a pair is within 1°; the Event Type field is the **Type:** line.

Its changes, and the earlier releases', are listed in the 1.8 revision history (in Prompts.zip).

## Extract 1 — Header (v1.8 lines 1-10)

Source lines 1-10 of v1.8; lines 1, 3, 5-6 and 10 are replaced by v1.9's lines 1, 3, 5-6 and 10.

~~~~text
# AstroCast Event-to-Natal Interpretation Prompt (Type B) — v1.8

**Operator documentation:** P17 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.8
**Date:** 2026-10-15
**Derived From:** AstroCast Event-to-Natal Interpretation Prompt v1.2
**Method:** Five-layer Event-to-Natal (Brennan / Lilly / Forrest / Ebertin / Robson / Brady / Classical Electional & Sect)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Synastry Export (Type B), AstroCast 1.3.4 (earlier exports: with declination data v1.0.42+, parans v1.0.61+, techniques data v1.0.101+)
~~~~
