# P04 Revision History and Extracted Records

Current release: 2.13 (2026-11-28). Previous release: 2.12 (2026-10-20).

Source: P04_AstroCast_TransitTimeline_Prompt_V2_12.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. One item of the run's list B (the downstream prompts named by number) and three of the review's findings (Q10, Q11, Q12). Line numbers are V2.13's; the replaced V2.12 passages are in the V2.12 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v2.13, the documentation line links the V2_13 guide and history, the Version line reads 2.13 and the Date line 2026-11-28.
- **:9, Compatible Export.** AstroCast 1.7.2; the line says what an older timeline prints for a retrograde ingress (no `(retrograde)`, and 0° of the sign entered).
- **:171, :173, :313, :316, :317, :321 (Q10).** A transiting planet's house is given where the export prints it (a window's `Transit house` line), and a declination-cluster or OOB flag only from a declination the export prints (a Parallel or Contraparallel row). The timeline's Events rows carry neither, and the sample reading had filled the gap with its own ephemeris work, one house differing from the export's own line. Nothing is computed outside the export.
- **:206 (Q11).** The timeline prints no "(priority)" tag (the full transit report does), so in a timeline a pair holding the Sun, Moon, Ascendant or Midheaven is priority.
- **:216, Step 2E (Q12).** The step names the timeline's own ingress rows as well as the transit report's section. A retrograde row reads `Ingress Virgo → Leo (retrograde)` with the boundary crossed (`0°00' Virgo`) from AstroCast 1.7.2; for an older timeline, which gave 0° of the sign entered, the reading reproduces the row and names the boundary in its description.
- **:243, :597 (Q11).** The export's `*Recommended Event Type B dates: ...*` line is a first pick by date, not a ranking; its dates are given as the export's and kept apart from any the reading adds.
- **:520-522, Downstream Prompt Routing (list B).** The three downstream prompts are named by number: P03, P16 and P17. The third read "a dedicated Event-to-Natal prompt (when available)"; P17 exists and reads a Synastry export made with the event chart as one of the two.

The User Guide 1.6 says the same for its user in two sentences.

Unchanged, by instruction: every other line of V2.12.

## Previous changes — 2.12 (2026-10-20)

One correction, by Steve's decision 4 of 2026-10-20, made in run 3 of 4 of the 1.6.0 library work. Run 2's prompt had V2.11's step 2I extract "entry, exact passes, stations, exit" (V2.11 :247) from `## Events: Asteroids and Lilith`, but that table holds exact passes and entries and exits only: the slip was Claude's, in run 2's prompt, and the Windows machine's run found it in run 2, whose report noted that the timeline golden's Events table for the five holds no station rows. The truth, at fe6e238: `src\AstroCast.Core\Formatting\TransitTimelineMarkdownFormatter.cs` (TF) and the golden `TransitTimelineMarkdownFormatter_J2000NYC_2026April_MinorPoints.golden.md` under `tests\AstroCast.Tests\Formatting\Golden\Goldens\` (TLG); every quoted value below is on the line cited, and a script checked each against its line. Line numbers are V2.12's; the replaced V2.11 lines follow as extracted records. The changes:

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
