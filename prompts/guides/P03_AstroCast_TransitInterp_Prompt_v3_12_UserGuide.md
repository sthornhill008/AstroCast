# P03 AstroCast_TransitInterp_Prompt_v3_12 — User Guide

Version: 1.5. Date: 2026-10-22.

Use the [complete current prompt](<../Transit/P03_AstroCast_TransitInterp_Prompt_v3_12.md>), followed by the matching chart export and your request. All operational rules are embedded in that file. Version 3.12 reads a Birth Time Source of Not specified as Unknown everywhere, as its Primary Directions part already did; the revision history lists the changes.

## Producing the export (AstroCast 1.6.0)

For the asteroids and Lilith, first tick **Points** on the Chart panel, in the Houses section ("Include asteroids and Lilith"). The box is off at every launch; ticking it recasts the chart on screen without saving it, and Clear Form and Load keep it. Then calculate the report on the Transits panel as below and export it: a report calculated before the tick carries no asteroids until it is calculated again. The report then carries the natal chart's `## Asteroids and Lilith` section, `## Transits to the Asteroids and Lilith` after the Active Transits, and, with **Solar Arc** or **Progressions** checked, the four sub-sections for the five after their twins. A horary chart never carries them. With the box unticked, the default, the report is as before.

In the Transits panel (Transits & Progressions), set the Start Date and End Date, then the rows: **Transits** (Include transits), **Minor** (Include minor aspects, transits only), **Solar Arc** (Include solar arc directions), **Progressions** (Include secondary progressions) and, new in 1.5.0, **Directions** (Include primary directions); at least one technique must be checked. Directions is checked by default, so most transit reports now carry a `## Primary Directions` section after the solar arcs, with the directions exact in the period. It takes the settings of the Directions panel (the **Primary** button on the sidebar, Alt+0): **Key** (Naibod by default, Ptolemy, or the true solar arc in right ascension), **Planets** (Add Mercury to Pluto as significators), **Age limit** (1 to 120 years) and **Uncertainty** (0 to 120 minutes; above 0 adds a Range column). While Directions is checked, Calculate stays disabled until those settings are valid. The settings last for the session only.

Press **Calculate**, then **Export Report MD**. With Transits checked the file is `{Name}_Transits_{Start}_{End}.md`, titled "# Transit Report", and its header's `**Techniques:**` line names the techniques run, Primary Directions last; this prompt reads it. A report produced without Transits is read with P28, the Directions prompt, even when it holds only primary directions and is titled "# Primary Directions Report". The whole-life list from the Directions panel's **Export Directions MD** (also in the Export panel's **Primary Directions** group), `{Name}_PrimaryDirections_0-100.md`, carries a `**Span:**` line instead of a report period and is read with P29.

If the chart's Birth Time Source was left blank, the report prints "Not specified" and the reading treats the time as unconfirmed throughout: it carries the birth-time caveat and softens the houses and angles; the Primary Directions section prints a Birth time line, and its dates move with the birth time by the Months/min column, so the reading times those directions, especially the angles', by season.

## Submission

Specify the subject, report period where relevant, context, focus, and any output override. For personal research use `Context: unvarnished and honest`. This does not permit external biography or activate medical analysis. An explicit voice instruction overrides the preset. For a blind test, remove names and biography while retaining chart settings and birth-time reliability.

## Family scope

Subject: the native during the supplied period. Keep transit, secondary progression, solar arc, and primary direction evidence distinct; do not turn timing symbolism into event certainty.

## Review

Check technical consistency, subject/voice, synthesis, relevant context, uncertainty, and output separately. Mark unavailable methods N/A. Record prompt version, model when available, input identity, date, resolved settings, and prose word count. Subjective resonance is not factual or scientific validation. Compare defaults, research, explicit voice override, missing data, and format override using the same inputs. Inspect every PDF page for clipping, wrapping, glyphs, stranded headings, and attribution.
