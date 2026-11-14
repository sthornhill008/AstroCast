# P11 Revision History and Extracted Records

Current release: 1.10 (2026-10-22). Previous release: 1.9 (2026-10-20).

Source: P11_AstroCast_RelocationInterp_Prompt_V1_9.md (in Prompts.zip). The source remains unchanged.

## Current changes

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
