# P04 AstroCast_TransitTimeline_Prompt_V2_12 — User Guide

Version: 1.5. Date: 2026-10-20.

Use the [complete current prompt](<../Timeline/P04_AstroCast_TransitTimeline_Prompt_V2_12.md>), followed by the matching chart export and your request. All operational rules are embedded in that file. Version 2.12 lists the transits to the natal asteroids and Lilith in a short table of their own when the timeline carries them; the revision history lists the changes.

## Producing the export (AstroCast 1.6.0)

For the asteroids and Lilith, first tick **Points** on the Chart panel, in the Houses section ("Include asteroids and Lilith"). The box is off at every launch; ticking it recasts the chart on screen without saving it, and Clear Form and Load keep it. Then calculate on the Transits panel as below and export: a report calculated before the tick carries no asteroids until it is calculated again. The timeline then carries the natal chart's `## Asteroids and Lilith` section, `## Transit Windows: Asteroids and Lilith` after the Transit Windows and `## Events: Asteroids and Lilith` after the Events table; the full Transit Report carries `## Transits to the Asteroids and Lilith` instead. A horary chart never carries them. With the box unticked, the default, the exports are as before.

In the Transits panel (Transits & Progressions), set the Start Date and End Date, check **Transits** (Include transits) — the timeline needs it — and, as wanted, **Minor** (Include minor aspects, transits only), **Solar Arc** (Include solar arc directions), **Progressions** (Include secondary progressions) and **Directions** (Include primary directions, new in 1.5.0 and checked by default; it takes the Directions panel's settings). Press **Calculate**, then **Export Timeline MD** for the Enhanced Transit Timeline Export (`{Name}_TransitTimeline_{Start}_{End}.md`, which prints no progressions, solar arcs or primary directions) or **Export Report MD** for the full Transit Report (`{Name}_Transits_{Start}_{End}.md`, whose `**Techniques:**` line names the techniques run and whose progressions and solar arcs sections this prompt reads for the header block; this prompt does not read its Primary Directions section). Export Timeline MD and the Daily Horoscope's export are disabled for a report produced without Transits; such a report (`_SolarArcs_`, `_Progressions_`, `_PrimaryDirections_` and their combinations) is read with P28. The Directions panel's whole-life list (the **Primary** button, Alt+0, then **Export Directions MD**; also in the Export panel's **Primary Directions** group), `{Name}_PrimaryDirections_0-100.md`, carries a `**Span:**` line and is read with P29.

## Submission

Specify the subject, report period where relevant, context, focus, and any output override. For personal research use `Context: unvarnished and honest`. This does not permit external biography or activate medical analysis. An explicit voice instruction overrides the preset. For a blind test, remove names and biography while retaining chart settings and birth-time reliability.

## Family scope

Subject: the named base-chart person or entity. Preserve one-to-two-sentence event descriptions, sorting, all exact passes, residual orbs, cluster ranks, and castable timestamps.

## Review

Check technical consistency, subject/voice, synthesis, relevant context, uncertainty, and output separately. Mark unavailable methods N/A. Record prompt version, model when available, input identity, date, resolved settings, and prose word count. Subjective resonance is not factual or scientific validation. Compare defaults, research, explicit voice override, missing data, and format override using the same inputs. Inspect every PDF page for clipping, wrapping, glyphs, stranded headings, and attribution.
