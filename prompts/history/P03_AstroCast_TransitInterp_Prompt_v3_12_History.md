# P03 Revision History and Extracted Records

Current release: 3.12 (2026-10-22). Previous release: 3.11 (2026-10-20).

Source: P03_AstroCast_TransitInterp_Prompt_v3_11.md (in Prompts.zip). The source remains unchanged.

## Current changes

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
