# P05 Revision History and Extracted Records

Current release: 2.5 (2026-11-28). Previous release: 2.4 (2026-10-15).

Source: P05_AstroCast_DailyHoroscopeInterp_Prompt_V2_4.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. Two of the review's findings (Q8, Q9), the first with a change of AstroCast 1.7.2 (code finding N15: with a Birth Time Source of Unknown the daily export prints no house anywhere). Line numbers are V2.5's; the replaced V2.4 passages are in the V2.4 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v2.5, the documentation line links the V2_5 guide and history, the Version line reads 2.5 and the Date line 2026-11-28.
- **:8, Input.** AstroCast 1.7.2, older exports still read; the line says what an older export with an Unknown source still printed (a House column in the backdrop table, `(H7)` in the Entering orb and Exiting orb lines).
- **:233-255, Birth Time Source (Q8), new.** V2.4 named the Birth Time Source nowhere and required houses, angles and the sect throughout. With `Unknown` or `Not specified` they are not known. With `Unknown` the export prints no house, and each day is written by planet, sign and theme; with `Not specified` the printed houses are unconfirmed and softened or left out, as P03 reads that source; in both the sect calibration is not applied.
- **:413-421, Dated timing windows (Q9).** The rule is limited to the dates the daily export prints. "(before)" and "(beyond)" are said in plain words, and no entry or exit date is supplied. V2.4 required dated profection, firdaria and releasing periods, which the daily export does not carry.

The User Guide 1.2 names the Daily Horoscope export (it said "the matching chart export") and says what the reading does with an unconfirmed birth time.

Unchanged, by instruction: every other rule of V2.4.

## Previous changes — 2.4 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; the Input line names 1.3.4 (R3). R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. A daily report across October 1582 runs Thursday 4 then Friday 15 October, and a bound in 5–14 October is refused (R3, R-D7). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. D (T-D3, T-D5; R10): the backdrop's In Orb cell is the passage's entry and exit, not the whole report; Mars only while slow or stationing. T-D15 (R5): a transit orb table from TransitOrbs with the Mars rule (listed only while slow or stationing). Older lines made true: the backdrop table prints no orb (the Active rows' orbs are at 12:00); the daily export prints no transiting positions or dignities; it lists no Sun, Moon, Mercury or Venus transits; the Method line names the chart's own house system.

Shared operating rules 1.0 are embedded where applicable. History and operator evaluation material have been extracted. Historical excerpts below are records, not current instructions; older policies, examples, ratings, and version references may be superseded by the active prompt and current guide.

## Previous changes — 2.3 (2026-10-13)

Aligned with the AstroCast 1.3.3 export; the Input line names 1.3.3. E: every time in the export is on the **Timezone:** clock (the zone named first), HH:mm, never UTC; the Moon's sign change is noted after the phase as (Moon enters [Sign] at HH:MM) at any hour, the sign named first being the sign at 12:00. C: void-of-course times are exact event times, already local. O-2 (E2-9): the export ends with its engine line. The Mars and backdrop-passage lines await 1.3.4 (branch D); the orb table (T-D15) is a documentation-pass item.

## Previous changes — 2.2 (2026-09-08)

Add third-person research across every day; correct the v2.0 title and remove chart/demographic inference as evidence of biography. Preserve quiet days, repetition discipline, short daily scope, and no-glossary default.

## Extract 1 — Change Log

Source line 43. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-03-13 | Initial release. Five-section per-day format, three pre-writing scans, compliance requirements. |
| 1.1 | 2026-03-13 | Mandatory AI attribution added. |
| 1.2 | 2026-03-13 | Filename and AI attribution aligned with other prompts. |
| 1.3 | 2026-03-16 | Techniques Integration — transiting planet dignity and natal planetary sect calibrate prose tone. |
| 1.4 | 2026-03-21 | Gender/name handling. Moon phase qualifier guidance (early/mid/late). Longer report period acknowledgment. VoC timezone clarification. Override Protocol block. Self-audit expanded. |
| 1.5 | 2026-04-10 | Lunar Lore & Health Tip added as second line within Action Tip. Combines Moon-sign body-region lore, Moon-phase timing, and transit-context-aware health-practical suggestions. Styling specified (italic, body-text size, dark grey). Word count allowance +30–50 words/day. Colophon updated with lore disclaimer. Self-audit expanded. |
| 1.6 | 2026-04-20 | Nine revisions absorbing library-wide standards plus two spec-specific additions. Seven library standards from EnhancedBirthInterp v2.9 / Eclipse v1.1 / Horary v2.1 / Electional v2.9 / Transit v3.3 / TransitTimeline v2.1 / SynastryInterp v1.7 / CompositeInterp v1.6 / SolarReturnInterp v1.7: (1) Daily Voice Convention promoted to top of preamble with override matrix (2nd-person default; 3rd-person for gift reports; public-figure surname-based auto-trigger). (2) AstroCast Front-Matter block added near top. (3) Compliance Requirements, Mandatory Deliverables, and Self-Audit merged into single unified compliance gate organized by category. (4) Per-day word targets preserved from v1.5 with clarity-over-brevity framing added. (5) Glossary marked explicitly as N/A in front-matter — daily horoscope is the one library spec with no output glossary by design (no jargon in reader-facing text). (6) "Do NOT" style instructions rewritten as positive statements; safety-gating prohibitions preserved (PDF enforcement, no medical/legal/financial predictions, lore disclaimer, compliance-checklist-not-in-output). (7) Context Fit Rule — biographical claims in prose must trace to natal data or operator-provided notes; no invented life detail. Plus two spec-specific additions: (8) Daily Voice Convention formalized with 4 concrete rules (2nd-person with named-subject grounding; no generic Sun-sign language; day-opening variety; voice consistency across full report). (9) Daily Repetition Discipline formalized with 6 concrete rules addressing the unique repetition-risk of daily columns where the same backdrop transits persist across many days (backdrop referenced not repeated; daily novelty anchor; peak-day amplification; quiet-day permission; thematic arc across period; honest difficulty without catastrophizing). Plus (10) editorial: version header, change log, footer. |
| 1.7 | 2026-04-22 | Formalized Daily's no-output-glossary design choice as a first-class override value "N/A with justification" in the Front-Matter Configuration. Closes Std 5 partial from Opus 4.7 audit campaign. Added Glossary Section Note explaining why daily output carries no jargon in reader-facing prose (astrological mechanisms live in the Pre-Writing Analytical Scan). No functional astrological methodology changes. |
| 1.8 | 2026-04-23 | Added formal Content Discipline section closing Std 6 gap identified by v2 scanner 2026-04-23. No functional changes to the astrological methodology or Daily voice. |
| 1.9 | 2026-05-12 | Activity-intrinsic Exception added to §6 (Void of Course Moon Handling) closing a frame-import gap where the writer would default to the restrictive half of classical VoC doctrine even when the operator's Interpretation Context described a presence-oriented pursuit. Classical writers pair "nothing comes of the matter" with explicit positive favorability for prayer, contemplation, rest, and pursuits valued for the doing itself; v1.8 carried only the restrictive half. Mirror clause added to Scan 3 in §3 so the polarity check happens before drafting. Cover Method note version reference brought current (v1.7 → v1.9). The fix has a family resemblance to the Context Fit Rule and the Daily Repetition Discipline — all three protect against the writer importing unstated assumptions. Test case: a private chart, May 11–June 12, 2026, daily with birding-as-presence Interpretation Context. |
| 2.0 | 2026-06-04 | Major revision from a full prompt review run during a 32-day interpretation. Tier 1: the day-header summary line no longer prints the banned words ’aspect’/’transit’ — it is now a plain-language theme (§5/§8); the Lunar Lore line is constrained to a non-clinical self-care register, with medication/treatment/appointment directives removed from its guidance and examples (§5, Content Discipline). Tier 2: the VoC business-hour test is generalized to the reader’s active hours (§6); the activity-intrinsic exception now resolves mixed contexts per activity (§3/§6); a Directional Context rule lets a stated operator priority thread the report and reorder per-day emphasis (§4.2); a reader-facing time-display rule is added (§6); §8 confirms Moon sign/phase names are permitted. Tier 3: the cover Method note is corrected to ’Three-scan method · v2.0’ (§9); quiet-day Career/Love notes must earn Moon-sign novelty (§5); long-report word-band guidance is added (§5). Compliance Gate and A/B matrix updated. |
| 2.1 | 2026-06-12 | v3.1 library standards adopted from EnhancedBirthInterp v3.1 (items 1-18), per applicability: Unvarnished Mode honesty register; Evidentiary Firewall (incl. mid-conversation context updates with hedge preservation); PDF Design System compact block (full spec in EnhancedBirthInterp Appendix A); generation colophon; dated timing windows. Propagated via Claude Code, 2026-06-12. |

---
~~~~

## Extract 2 — 12. A/B Test Matrix

Source line 903. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## 12. A/B Test Matrix

| Variable | Options | Notes |
|----------|---------|-------|
| Prompt version | v2.0 / v1.9 / v1.8 / future | Track iteration |
| Directional Context (v2.0) | Emphasis reorder on / fixed five-section order | Does a stated priority improve relevance? |
| Voice convention (v1.6) | 2nd-person default / 3rd-person override / public-figure | Which voice serves the reader? |
| Report length | 7 days / 14 days / 28 days | Does Daily Repetition Discipline hold? |
| Lunar Lore | With / Without | Does the lore tip add value? |
| Backdrop density | 1–2 backdrop transits / 4–6 backdrop transits | How does the discipline handle dense backdrops? |
| Voice slippage | Monitor 3rd-person overrides for consistency | Does voice hold across 15+ days? |
| Peak-day treatment | Amplified / uniform | Does amplification help or overbear? |
| Generic-sign-column test | Compare to newspaper Sun-sign column | Is every claim personal-chart-specific? |
| AI service | Claude / ChatGPT / Copilot | Cross-platform comparison |
| Quiet-day length | 150–200 words / uniform 300 words | Does permitted variability improve readability? |

---

*v2.1 (2026-06-12): v3.1 library standards adopted.*
*v2.0 (2026-06-04): Major revision from the 32-day-report review (day-header theme line, Lunar Lore register, VoC active-hours generalization, Directional Context rule, time-display rule).*
*v1.9 (2026-05-12): Activity-intrinsic VoC exception.*
*v1.8 (2026-04-23): Content Discipline section.*
*v1.7 (2026-04-22): N/A-with-justification glossary override; Glossary Section Note.*
*v1.6 (2026-04-20): Library-wide standards absorbed; Daily Voice Convention; Daily Repetition Discipline.*

*Paste the AstroCast Daily Horoscope export below this line.*
~~~~
