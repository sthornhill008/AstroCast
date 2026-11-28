# P29 AstroCast_PrimaryDirectionsInterp_Prompt_V1_1 — User Guide

Version: 1.1. Date: 2026-11-28.

Use the [complete current prompt](<../Directions/P29_AstroCast_PrimaryDirectionsInterp_Prompt_V1_1.md>), followed by the whole-life Primary Directions Report and your request. All operational rules are embedded in that file. P29 reads the report in the tradition of Ptolemy, Placidus and Martin Gansten, as life chapters, and closes with rectification notes. Version 1.1 gives those notes a figure, "How much the result moves", and a table of competing matches when you supply three or more dated events; adds three requests (below); and reads the marks, the footnotes and the star natures table of the AstroCast 1.7.2 report. The revision history lists the changes.

## Producing the export (AstroCast 1.5.0)

Load the chart on the Chart panel. Open the Directions panel with the **Primary** button on the sidebar (the arrow, after Relocate) or **Alt+0**. Under Settings choose:

- **Key** — Naibod (0°59′08″ a year), the default; Ptolemy (1° a year); or the true solar arc in right ascension.
- **Planets** — check **Add Mercury to Pluto as significators** to direct to the planets as well as to the angles, the lights and the Part of Fortune.
- **Age limit** — the span in years, 1 to 120 (100 by default).
- **Uncertainty** — the birth time's possible error in minutes either way, 0 to 120 (0 by default). Above 0 adds a Range column, the dates at that many minutes either side; for a time not on a birth certificate, a few minutes (for example 4) is useful.

Press **Calculate Directions** (disabled until a chart is loaded and the age limit and uncertainty are in range; **Cancel** stops a long run). The result line and the list appear on the panel. Press **Export Directions MD** (on the panel, or in the Export panel's **Primary Directions** group) and save `{Name}_PrimaryDirections_0-100.md`, its limit the age limit. The file is titled "# Primary Directions Report" and its header carries `**Span:** birth to age 100` and `**Techniques:** Primary Directions`. The settings last for the session only.

The Transits panel's **Directions** box (Include primary directions, checked by default) makes something else: a report of a period with the directions exact in it, which carries a `**Report Period:**` line and is read with P03 (with Transits) or P28 (without). Check the header, not the title: both kinds can be titled "# Primary Directions Report".

If the chart's Birth Time Source was left blank or reads Unknown, the export prints a Birth time line: every date moves with the birth time, by the Months/min column. The reading then times the angle directions by season or year and uses the Months/min column, and the Range when present, in its rectification notes.

From AstroCast 1.7.2 a figure in the table may carry a mark, explained in a footnote under it. A dagger (†) marks a row whose date turns within the birth-time window: neither its Range nor its Months/min can be used to correct the birth time, and the reading never builds a correction on such a row. A double dagger (‡) beside a dash marks a missing figure and gives the reason. A `### Natures of the Promissor Stars` table follows the footnotes; the reading takes a promissor star's nature from it and, for an older report without it, states none.

## Submission

Specify the subject, context, focus, and any output override. To have the reading test the birth time, supply dated events (`Rectification events: {event}, {YYYY-MM-DD}; …`), or record them in the chart's Notes; without them, Section 6 explains how to use the Months/min column. With three or more events that each give a figure, Section 6 holds the events table, the figure and the competing-matches table. Three requests change it: `No chart` leaves the figure out; `Include the trial-shift check` adds a table of five trial shifts, which the assistant must compute by code (one that cannot run code says so and gives no figures); `Rectification only` gives a short report that is Section 6 and little else. For a public figure, `Use public biographical knowledge` lets the assistant research dated events itself; each must be verified and cited. For personal research use `Context: unvarnished and honest`. This does not permit external biography or activate medical analysis, and it never lifts the rules on the length of life. An explicit voice instruction overrides the preset. For a blind test, remove names and biography while retaining chart settings and birth-time reliability.

## Family scope

Subject: the native across the span the report covers. A direction describes a season of the life and its quality, never event certainty, the length of the life or the manner of a death; a corrected birth time is a hypothesis, never a fact.

## Review

Check technical consistency, subject/voice, synthesis, relevant context, uncertainty, and output separately. Check that past chapters are hedged and that any rectification is stated as a hypothesis with its arithmetic. Check every value in the figure against the events table; check that no working match is a row marked with a dagger; and where events are researched, check each source. Record prompt version, model when available, input identity, date, resolved settings, and prose word count. Subjective resonance is not factual or scientific validation. Compare defaults, research, explicit voice override, missing data, and format override using the same inputs. Inspect every PDF page for clipping, wrapping, glyphs, stranded headings, and attribution.
