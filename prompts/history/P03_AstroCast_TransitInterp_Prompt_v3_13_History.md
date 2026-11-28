# P03 Revision History and Extracted Records

Current release: 3.13 (2026-11-28). Previous release: 3.12 (2026-10-22).

Source: P03_AstroCast_TransitInterp_Prompt_v3_12.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. Three of the review's findings (Q1, Q16, Q26) and two changes of AstroCast 1.7.2: the marks, footnotes and natures table of the Primary Directions section (code finding A5), and a transit report of a single day (N6). Line numbers are v3.13's; the replaced v3.12 passages are in the v3.12 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v3.13, the documentation line links the v3_13 guide and history, the Version line reads 3.13 and the Date line 2026-11-28.
- **:9, Compatible Export.** AstroCast 1.7.2; the line says what an older export lacks: the `†` and `‡` marks with their footnotes and the `### Natures of the Promissor Stars` table, and a report of a single day. The 1.6.0 to 1.7.1 exports join the list of earlier exports.
- **:467-472, Antiscia and stars (Q26).** A promissor star's nature is the one the export's Natures of the Promissor Stars table gives; none is stated for an export without the table. Until now the nature of a promissor star came from the assistant's memory, since the natal Fixed Stars table lists only the stars on natal points.
- **:550, Sensitive-Period Framing, item (e) (Q1).** The leftover sentence that activated Layer 7 on Notes health content "even without an explicit Interpretation Context request" is deleted (v3.12 :546-549). It contradicted the layer's own rule (:487-489) and the first sentence of the same item.
- **:558.** "The Month Ahead" becomes "the Opening Overview (Section 1)", since Section 1 is now titled for the period.
- **:705-707, Handling Interpretation Context (Q1).** Layer 7 remains inactive and activates on an operator's request only.
- **:746-756, the Primary Directions section under an unconfirmed time (A5).** The Range's `—` is explained by its own conditions (it read "on the same two conditions" as Months/min); from 1.7.2 the `—` carries `‡` with a footnote giving the reason; a row marked `†` is read by its year, with no timing taken from either figure.
- **:1212, :1218, :1238-1243, Interpretation Structure (Q16, N6).** Section 1 is "The Period Ahead", titled for the report's period; Section 6 builds "toward the period's end"; a closing paragraph sizes the reading to the Report Period, lifts the 2,500-word floor for a week or less, and allows a page or two for a single day.
- **:1221, Section 9 (Q16).** A primary direction is listed by its year, not its day, with its Range when printed; the prompt's own Timing rule (:446-448) already reads it as a span of about a year.
- **:1222, Section 10 (Q26).** A promissor star's nature comes from the export's table, or reads "not given in the export".
- **:1288-1289, :1320-1321, Compliance Gate (Q16, Q1).** The gate's line on primary directions follows Section 9; the Layer 7 line reads "on an operator's request only".

The User Guide 1.6 says the same for its user: the health layer on request only, a report of a single day, the marks and the natures table.

Unchanged, by instruction: every other rule of v3.12.

## Previous changes — 3.12 (2026-10-22)

A ruling carried over from the 1.5.0 library work's leftovers to the 1.7.0 library run (Steve, 2026-10-22: "Read it as Unknown everywhere"). The transit report prints `**Birth Time Source:** Not specified` when the chart records no source (`src\AstroCast.Core\Formatting\TransitMarkdownFormatter.cs` :59 at 9e227eb), and the Primary Directions section already called that time unconfirmed (v3.11 :729-733, unchanged); the general birth-time rule and the final check now read Not specified as Unknown too. No other rule changed; this is the whole of the release.

- **:1, :3, :5, :6, header.** The title reads v3.12, the documentation line links the v3_12 guide and history, the Version line reads 3.12 and the Date line 2026-10-22 (Extract 1).
- **:715-717, Handling Birth Time Source.** The Unknown bullet becomes **Unknown, or Not specified**: the report's own words when the chart records no source, read as Unknown throughout the prompt; the bullet's rule is otherwise unchanged (Extract 2).
- **:1255-1256, Compliance Gate.** The birth time caveat item reads "Unknown or Not specified" (Extract 3).

The User Guide 1.5 says that a blank Birth Time Source prints as Not specified and that the reading treats the time as unconfirmed throughout, the caveat and the softened houses and angles included, not only in the Primary Directions section.

Unchanged, by instruction: every other rule of v3.11, among them the Transit Orbs section with its heading naming AstroCast 1.4.0 (unchanged since 1.3.4, which stays true), the asteroids and Lilith rules of v3.11, and the section list with its word ranges.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 3.11 (2026-10-20)

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`): the transits, progressions and solar arcs to the natal asteroids and Lilith read as minor themes when the report carries them, never counted as a convergence (run 2 of 4 of the 1.6.0 library work). Its changes, and the earlier releases', are listed in the 3.11 revision history (in Prompts.zip).

## Extract 1 — Header (v3.11 lines 1-9)

Source lines 1-9 of v3.11; lines 1, 3, 5-6 are replaced by v3.12's lines 1, 3, 5-6.

~~~~text
# AstroCast Transit Interpretation Prompt — v3.11

**Operator documentation:** P03 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 3.11
**Date:** 2026-10-20
**Method:** Seven-layer (Hand / Greene / Sasportas / Ebertin / Forrest / Tyl / Galenic Medical)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Transit Report, AstroCast 1.6.0 (older transit reports still read; the asteroids and Lilith appear only in exports from AstroCast 1.6.0 made with the Points switch on) — a report whose header's `**Techniques:**` line names Transits, with or without Solar Arcs, Secondary Progressions and Primary Directions (earlier exports: AstroCast 1.5.0, the same report without the asteroids and Lilith; AstroCast 1.4.0, the same report without Primary Directions; AstroCast 1.3.4 with its `**Progressions:**` line; Spec v1.1 with declination data v1.0.42+, parans v1.0.61+, medical correspondences v1.0.79+, techniques data v1.0.101+). A report of a period whose Techniques line lacks Transits — a Solar Arc, Secondary Progressions or Primary Directions Report, or one of their combinations — is read with P28, the Directions prompt; the whole-life Primary Directions Report, whose header carries a `**Span:**` line, is read with P29, the Primary Directions prompt. Neither is read with this one.
~~~~

## Extract 2 — Handling Birth Time Source (v3.11 line 715)

Source line 715 of v3.11, replaced by v3.12's lines 715-717.

~~~~text
- **Unknown:** House cusps and angles (ASC, MC) are uncertain. Add an
~~~~

## Extract 3 — Compliance Gate (v3.11 line 1253)

Source line 1253 of v3.11, replaced by v3.12's lines 1255-1256.

~~~~text
- [ ] Birth time caveat included when Birth Time Source is Unknown
~~~~
