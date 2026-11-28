# P11 Revision History and Extracted Records

Current release: 1.11 (2026-11-28). Previous release: 1.10 (2026-10-22).

Source: P11_AstroCast_RelocationInterp_Prompt_V1_10.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. One of the review's findings (Q20). Line numbers are v1.11's; the replaced v1.10 passages are in the v1.10 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v1.11, the documentation line links the V1_11 guide and history, the Version line reads 1.11 and the Date line 2026-11-28.
- **:9, Compatible Export (Q20).** AstroCast 1.7.2. The header's Event and Question labels began in AstroCast 1.6.1 (v1.10 named 1.6.0 for them); an export of 1.6.0 or earlier reads Birth for every chart, and the chart's kind is then taken from the relocated chart's `**Type:**` line.
- **:439, Title Page.** The same sentence for the subject line's word (Born, Event, Asked).
- **:487-499, Section 5, Health and Vitality (Q20).** The section keeps to vitality and energy unless the Medical Correspondence Safety Protocol is triggered by an operator's request. "Overexertion risk" and "12th house planets = hidden health factors" are gone; the medical correspondences data is read only when the protocol is triggered. Protocol 3 (:153-157) already made the medical layer request-only, yet v1.10 wrote these into every reading.
- **:616, :626, Compliance Gate (Q20).** The protocol's line and §5's content line follow Section 5. The first is not among the lines the review cited: it applied the protocol "if medical data present in export", and the export always holds that block, so the change to Section 5 could not stand without it.

AstroCast 1.7.2 no longer prints a second title or a second footer under `## Relocated Chart`; no sentence of the prompt described either, so nothing changed for it.

The User Guide 1.4 names the Relocation export (it said "the matching chart export") and says the same for its user.

Unchanged, by instruction: every other rule of v1.10, among them the Health houses list of the life-domain framework (:399-405), which a triggered reading still uses.

## Previous changes — 1.10 (2026-10-22)

A ruling carried over from the 1.5.0 library work's leftovers to the 1.7.0 library run (Steve, 2026-10-22). The relocation export's header lines read **Birth Location** and **Birth Date/Time** for a Birth chart, **Event Location** and **Event Date/Time** for an event chart, and **Question Location** and **Question Date/Time** for a horary (`src\AstroCast.Core\Formatting\RelocationMarkdownFormatter.cs` :47-57 and :133-136 at 9e227eb), but the prompt's title page line read "Born" for every chart. It now follows the export's word. No other rule changed; this is the whole of the release.

- **:1, :3, :5, :6, header.** The title reads v1.10, the documentation line links the V1_10 guide and history, the Version line reads 1.10 and the Date line 2026-10-22 (Extract 1).
- **:208-210, Instructions for Use.** The header bullet names the chart's location and date/time lines as reading Birth, Event or Question by the chart's type (Extract 2).
- **:439, Title Page / Header.** The subject line reads Born for a Birth chart as before; for an Event chart `**Subject:** {Name} — Event {date/time} at {location}`, for a horary `**Subject:** {Name} — Asked {date/time} at {location}`, after the export's own lines (Extract 3).

The User Guide 1.3 names the change in its opening paragraph.

Unchanged, by instruction: every other rule of v1.9, among them the Relocation Principles, the life-domain sections and their word targets, the Data Cross-Reference Table, and the asteroids and Lilith rules of v1.9.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 1.9 (2026-10-20)

Aligned with AstroCast 1.6.0 as landed (master `fe6e238`): the relocated houses of the asteroids and Lilith, and any angular placement, read as minor colour when the export carries them (run 3 of 4 of the 1.6.0 library work). Its changes, and the earlier releases', are listed in the 1.9 revision history (in Prompts.zip).

## Extract 1 — Header (v1.9 lines 1-9)

Source lines 1-9 of v1.9; lines 1, 3, 5-6 are replaced by v1.10's lines 1, 3, 5-6.

~~~~text
# AstroCast Relocation Chart Interpretation Prompt — v1.9

**Operator documentation:** P11 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 1.9
**Date:** 2026-10-20
**Method:** Life-domain relocation framework (Locational astrology / Jim Lewis tradition / Classical dignity integration)
**Target Audience:** General audience with basic astrology knowledge
**Compatible Export:** AstroCast Relocation Markdown Export, AstroCast 1.6.0 (older relocation exports still read; the asteroids and Lilith appear only in exports from AstroCast 1.6.0 made with the Points switch on; earlier exports: AstroCast 1.3.4, 1.4.0 and 1.5.0; v1.0.115+)
~~~~

## Extract 2 — Instructions for Use (v1.9 lines 208-209)

Source lines 208-209 of v1.9, replaced by v1.10's lines 208-210.

~~~~text
- **Header:** Birth location, relocation target, birth date/time,
  UTC moment, Julian Day
~~~~

## Extract 3 — Title Page / Header (v1.9 line 438)

Source line 438 of v1.9, replaced by v1.10's line 439.

~~~~text
- **Native:** {Name} — Born {birth date/time} at {birth location}
~~~~
