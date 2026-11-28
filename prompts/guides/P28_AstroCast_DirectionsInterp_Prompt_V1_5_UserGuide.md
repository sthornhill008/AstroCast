# P28 AstroCast_DirectionsInterp_Prompt_V1_5 — User Guide

Version: 1.5. Date: 2026-11-28.

Use the [complete current prompt](<../Directions/P28_AstroCast_DirectionsInterp_Prompt_V1_5.md>), followed by the report (a Transits-panel report made without Transits; see below) and your request. All operational rules are embedded in that file. Version 1.5 reads the reports of AstroCast 1.7.2: the marks and footnotes of the Primary Directions table, and the export's own table of star natures. When a period holds no exact hit the reading now says so and reads the aspects in orb at the report's start. The revision history lists the changes.

## Producing the export (AstroCast 1.6.0)

For the asteroids and Lilith, first tick **Points** on the Chart panel, in the Houses section ("Include asteroids and Lilith"). The box is off at every launch; ticking it recasts the chart on screen without saving it, and Clear Form and Load keep it. Then calculate on the Transits panel as below and export: a report calculated before the tick carries no asteroids until it is calculated again. The report then carries the natal chart's `## Asteroids and Lilith` section and, with **Solar Arc** or **Progressions** checked, the four sub-sections for the five after their twins; primary directions never reach them. A horary chart never carries them. With the box unticked, the default, the report is as before.

In the Transits panel (Transits & Progressions), set the Start Date and End Date, **uncheck Transits** (Include transits), and check any of **Solar Arc** (Include solar arc directions), **Progressions** (Include secondary progressions) and **Directions** (Include primary directions, new in 1.5.0 and checked by default); **Minor** (Include minor aspects) applies to transits only and is disabled without them. Directions takes the settings of the Directions panel (the **Primary** button on the sidebar, Alt+0): **Key** (Naibod by default, Ptolemy, or the true solar arc in right ascension), **Planets** (Add Mercury to Pluto as significators), **Age limit** (1 to 120 years) and **Uncertainty** (0 to 120 minutes; above 0 adds a Range column); while it is checked, Calculate stays disabled until those settings are valid. The settings last for the session only.

Press **Calculate**, then **Export Report MD**. The file and its title name the techniques: `{Name}_SolarArcs_{Start}_{End}.md` ("# Solar Arc Report"), `{Name}_Progressions_{Start}_{End}.md` ("# Secondary Progressions Report"), `{Name}_SolarArcsProgressions_{Start}_{End}.md` ("# Solar Arc and Progressions Report"), and with primary directions `{Name}_PrimaryDirections_{Start}_{End}.md` ("# Primary Directions Report"), `_SolarArcsPrimaryDirections_`, `_ProgressionsPrimaryDirections_` or `_SolarArcsProgressionsPrimaryDirections_`. The header carries a `**Report Period:**` line, and its `**Techniques:**` line names the techniques without Transits. **Export Timeline MD** and the Daily Horoscope's export are disabled for these reports. A report produced with Transits checked is a Transit Report and is read with P03.

Do not confuse a report of primary directions alone with the Directions panel's whole-life list: both are titled "# Primary Directions Report", but the whole-life list (**Export Directions MD**, on the Directions panel or in the Export panel's **Primary Directions** group, file `{Name}_PrimaryDirections_0-100.md`) carries a `**Span:**` line instead of a report period, and is read with P29.

From AstroCast 1.7.2 a figure in the Primary Directions table may carry a mark: a dagger (†) on a row whose date turns within the birth-time window, and a double dagger (‡) beside a dash where a figure is missing, each explained in a footnote under the table. When a listed direction is from a fixed star, a `### Natures of the Promissor Stars` table follows; the reading takes a promissor star's nature from it and, for an older export without it, states none.

A longer period suits these reports: the directions move about a degree a year, so a year or two shows several exact hits where a month may show none.

If the chart's Birth Time Source was left blank, the export prints "Not specified" and the reading treats the time as unconfirmed: it carries the birth-time caveat, and the dates of contacts with the Ascendant or Midheaven, primary directions included, are marked approximate.

## Submission

Specify the subject, report period where relevant, context, focus, and any output override. For personal research use `Context: unvarnished and honest`. This does not permit external biography or activate medical analysis. An explicit voice instruction overrides the preset. For a blind test, remove names and biography while retaining chart settings and birth-time reliability.

## Family scope

Subject: the native during the supplied period. Keep solar arc, secondary progression, and primary direction evidence distinct; the directions describe readiness and timing, never event certainty, and the report holds no transits to trigger them.

## Review

Check technical consistency, subject/voice, synthesis, relevant context, uncertainty, and output separately. Mark unavailable methods N/A (a report without progressions has no progressed Moon; one without solar arcs has no solar arc pictures; one without primary directions has no Section 5A). Record prompt version, model when available, input identity, date, resolved settings, and prose word count. Subjective resonance is not factual or scientific validation. Compare defaults, research, explicit voice override, missing data, and format override using the same inputs. Inspect every PDF page for clipping, wrapping, glyphs, stranded headings, and attribution.
