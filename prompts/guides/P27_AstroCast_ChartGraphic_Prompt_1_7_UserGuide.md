# P27 AstroCast_ChartGraphic_Prompt_1_7 — User Guide

Version: 1.3. Date: 2026-11-28.

Use the [complete current prompt](<../Chart Wheel/P27_AstroCast_ChartGraphic_Prompt_1_7.md>), followed by the matching chart export and your request. All operational rules are embedded in that file. Historical excerpts are retained separately; they are not submission instructions. Version 1.7 adds a single image on request and says what an assistant may deliver when it cannot draw; the revision history lists the changes.

## Producing the export (AstroCast 1.6.0)

On the Chart panel, in the Houses section, tick **Points**, "Include asteroids and Lilith". The box is off at every launch; ticking it recasts the chart on screen without saving it, and Clear Form and Load keep it. Then export the chart's Markdown or JSON (**Save Markdown** under the Chart panel's Actions, or **Export Markdown** or **Export JSON** in the Export & Backup panel's Chart Export group). The Markdown then carries a `## Asteroids and Lilith` section directly after the Out-of-Bounds Planets table, and the JSON a `minorPoints` array after `positions`. For a relocation, return, synastry, composite or transit graphic, tick the box before you calculate on that panel: the other panels take the new chart at their next Calculate, and each chart whose export section carries the five is drawn with them. A horary chart never carries them. With the box unticked, the default, the exports and the graphics are as before.

## Submission

Supply the complete current prompt and matching export. Specify any page/theme/output variant. The default is a two-page PDF in black and white. Ask for a "Single image" to get instead one square PNG (1600 × 1600 pixels, sized for forums and social sites) of the full chart with coloured aspect lines, the chart's name above it and a small credit line below; add "no name" to leave the name off. An assistant that cannot run the drawing tools should give you a script to run or say plainly that it cannot draw the chart: a wheel drawn freehand or by an image generator is not a chart, and the prompt forbids both. The interpretive research preset is not applicable. Validate exact chart time, roles, settings and source-to-renderer agreement.

## Family scope

Data/graphic generation only. No interpretive research preset. Preserve exact chart identity and calculation settings.

## Review

Check technical consistency, subject/voice, synthesis, relevant context, uncertainty, and output separately. Mark unavailable methods N/A. Record prompt version, model when available, input identity, date, resolved settings, and prose word count. Subjective resonance is not factual or scientific validation. Compare defaults, research, explicit voice override, missing data, and format override using the same inputs. Inspect every PDF page for clipping, wrapping, glyphs, stranded headings, and attribution.
