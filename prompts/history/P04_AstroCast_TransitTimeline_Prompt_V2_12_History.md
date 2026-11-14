# P04 Revision History and Extracted Records

Current release: 2.12 (2026-10-20). Previous release: 2.11 (2026-10-20).

Source: AstroCast_TransitTimeline_Prompt_V2_11.md (in Prompts.zip). The source remains unchanged.

## Current changes

One correction, by Steve's decision 4 of 2026-10-20, made in run 3 of 4 of the 1.6.0 library work. Run 2's prompt had V2.11's step 2I extract "entry, exact passes, stations, exit" (V2.11 :247) from `## Events: Asteroids and Lilith`, but that table holds exact passes and entries and exits only: the slip was Claude's, in run 2's prompt, and Maple found it in run 2, whose report noted that the timeline golden's Events table for the five holds no station rows. The truth, at fe6e238: `src\AstroCast.Core\Formatting\TransitTimelineMarkdownFormatter.cs` (TF) and the golden `TransitTimelineMarkdownFormatter_J2000NYC_2026April_MinorPoints.golden.md` under `tests\AstroCast.Tests\Formatting\Golden\Goldens\` (TLG); every quoted value below is on the line cited, and a script checked each against its line. Line numbers are V2.12's; the replaced V2.11 lines follow as extracted records. The changes:

- **:1, :3, :5, header.** The title reads v2.12, the documentation line links the V2_12 guide and history, and the Version line reads 2.12. The Date line already read 2026-10-20, the run's date, and the Compatible Export line already named 1.6.0, so neither changed (Extract 1).
- **:247, Step 2I.** The rows extracted from the five's Events table are now entry, exact passes and exit, and the sentence says the table holds no station rows and that the five's in-orb stations show only in `## Transit Windows: Asteroids and Lilith`. The five's Events table is filled by `ExtractExactPasses` (TF :316) and `ExtractEntryExitEvents` (TF :317) alone and printed under `## Events: Asteroids and Lilith` (TF :319; TLG :792-812, with no station row); the five's in-orb stations print only in their windows, such as `- **Station Retrograde:** 2025-05-04 15:27 UTC (in orb)` (TLG :641) under `## Transit Windows: Asteroids and Lilith` (TLG :636) (Extract 2).

The User Guide 1.5 is 1.4 with the new names: the title, the prompt link, and Version 2.12 for Version 2.11 in its first paragraph.

Unchanged, by instruction: every other line of V2.11.

Shared operating rules 1.0 are embedded where applicable. Historical excerpts below are records, not current instructions; the active prompt supersedes them.

## Previous changes — 2.11 (2026-10-20)

Run 2 of 4 of the 1.6.0 library work: the shared block and P04's family line before the Transit Orbs, step 2I for the five's own events, row 4A for them after the Timeline Table, the Transit Windows Block holding the chart's own windows only, and the five's events never in a cluster. Its changes, and the earlier releases', are listed in the 2.11 revision history (in Prompts.zip).

## Extract 1 — Header (V2.11 lines 1-5)

Source lines 1-5 of V2.11; lines 1, 3 and 5 are replaced by V2.12's lines 1, 3 and 5.

~~~~text
# AstroCast Transit Timeline Reference Prompt — v2.11

**Operator documentation:** P04 guide (in Prompts.zip) · Revision history (in Prompts.zip). These are optional references.

**Version:** 2.11
~~~~

## Extract 2 — Step 2I (V2.11 line 247)

Source line 247 of V2.11, replaced by V2.12's line 247.

~~~~text
When the export carries `## Events: Asteroids and Lilith` (the enhanced timeline export prints it directly after the Events table, with the columns `| Date | Transit | Event | Position | Orb |` and rows such as `TR Pluto tri NA Ceres`), extract its rows (entry, exact passes, stations, exit) as the five's own events, with the fields of 2A and 2F, for row 4A of the output. Never extract them into the main Timeline Table or the cluster analysis. A full transit export carries the same contacts in `## Transits to the Asteroids and Lilith`, in blocks of the Active Transits' form; read their lifecycle lines the same way.
~~~~
