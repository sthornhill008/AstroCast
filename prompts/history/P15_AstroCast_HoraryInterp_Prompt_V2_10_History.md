# P15 Revision History and Extracted Records

Current release: 2.10 (2026-10-15). Previous release: 2.9.

Source: AstroCast_HoraryInterp_Prompt_V2_9.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with the AstroCast 1.3.4 engine; the prompt's own "What changed in v2.10" lists the changes (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. The Perfection and Application fails lines and the analyser's notes are D°MM′, no longer decimal. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. K2: a relocated horary chart keeps its category, its perfection judged at the place. R4: the Behenian entry keeps Agrippa's fifteen and says the export flags 16. Older lines made true: the **Radicality:** and **Note:** lines, the Dignity and Score columns and the Score and Conditions columns (the field names are the JSON's); the Sun's distance beyond the 8° conjunction orb is read from the longitudes; the Mutual Receptions list names the type; the Moon's phase qualifier prints after the angle; the example's title is the question's first line, and its Planetary Hour carries the Hour Period line.

## Previous changes (2.9, 2026-10-13)

Aligned with the AstroCast 1.3.3 engine; the prompt's own "What changed in v2.9" lists the changes. QI-1: when the Moon rules both the 1st and the quesited house, the Moon signifies the querent and Jupiter, exalted in Cancer, the quesited, with a Note (the Notes list, the engine-authority line, the manual fallback's scope, the output and checklist lines, the glossary). QI-2: a mutual reception is named by its type whatever the perfection, with a Detail; the "Neutral with no Detail" caveat now applies to 1.3.2 exports only. F5 (D17): the Time Note sentences; a Time Note is never part of the question; the example header reads EDT (UTC−4) with the matching Julian Day. F7 (D14, D13, F5-1): clusters span at most 2°, a pair is listed once on the same side, out of bounds from the OOB column. F1 (D8): Mercury's sect (two lines). F3 (D5, F3-1): an aspect to an angle with the angle held still. F4 (F9-3): the Planetary Hour and radicality lines when present. F8 (L1): Necessity and Courage by Paulus.

## Previous changes (2.8, 2026-10-03)

Aligned with the AstroCast 1.3.2 engine. A direct aspect perfects only if it reaches exact before either significator leaves its sign, unless either significator stands in the other's domicile or exaltation (otherwise a note gives the exact time and the sign changes); only the seven traditional planets translate or collect, and only when every application the mechanism relies on completes, a failure and a retrograde intermediary being named in Notes; mutual reception perfects by domicile, exaltation or mixed domicile–exaltation only, and the Reception line names the type; antiscia perfect only while applying, a separating one being testimony; prohibition also applies before a translation or collection completes, the intermediary excepted; refranation also includes a significator's station after the turnaround but before the linear perfection estimate; reception is judged both ways, so Quality may read Mixed, and hostility is worded from the side of the planet standing in the sign ("the quesited is averse to the querent" for Lx in L1's detriment or fall); an unrecognised category takes the 7th house with a note; the Moon's First Applying and Void of Course follow their targets' motion; a failed application's turnaround is timed to the minute, and an Uncertain judgment with no denial names the application's fate. The data handling lists the new Notes kinds and Reception Quality values, and Step 4 notes that a mutual reception by exaltation prints Neutral when reception is not the perfection. Replaced "What changed in v2.7" with "What changed in v2.8" and retired "Interim engine caveats", all four now fixed, moving its two still-true definitions (the Moon's first applying aspect; the antiscia applying check) to the data handling and Step 4d. Aligned the manual path and the glossary (Perfection, Prohibition, Refranation, Translation and Collection of Light) with the same rules. Prepared via Claude Code, 2026-10-03.

## Previous changes (2.7, 2026-10-01)

Aligned with the AstroCast 1.3.1 engine. The Stationing Planets table is read as the nearest station event (Station, Phase, Station Time, Station Position, signed Speed, Proximity as a percentage of the threshold); a direct applying aspect counts as perfection only when the engine finds it reaches exact (Perfects / Application fails / Application unresolved lines); refranation is an event — a station of L1 or Lx, in either direction, after which the applying aspect fails — and the near-station caution is gone; prohibition is Mars or Saturn exact to L1 or Lx before the L1–Lx perfection, named with planet, aspect and local time; the engine's Notes are testimony, not denial; station direct on a significator is testimony of revival that offsets retrograde debility and no longer overrides the perfection verdict (doctrine confirmed by the operator, 2026-10-01). Added "What changed in v2.7" and "Interim engine caveats" (Translation/Collection intermediaries, Mutual Reception strength, the Moon's First Applying scan, antiscia applying/separating). Corrected the conditions-array example and the scan/perfection note on declination. Same-day corrections: denial-line timing sentence, intro wording, antiscia caveat. Prepared via Claude Code, 2026-10-01.

## Previous changes (2.6, 2026-09-08)

Research describes the querent/question, not a natal personality. Replace forced context matches; preserve the perfection engine, significator assignment, receptions, and judgment transparency. Fix stale v2.1/Template 2C references.

Shared operating rules 1.0 are embedded where applicable. History and operator evaluation material have been extracted. Historical excerpts below are records, not current instructions; older policies, examples, ratings, and version references may be superseded by the active prompt and current guide.

## Extract 1 — Change Log

Source line 56. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-24 | Initial release. Lilly/Goldstein-Jacobson method. |
| 1.1 | 2026-03-06 | Pre-Judgment Analytical Scan added. |
| 1.2 | 2026-03-06 | Compliance Requirements section added. |
| 1.3 | 2026-03-07 | Five corrections from A/B test. |
| 1.4 | 2026-03-16 | Techniques: five-level dignity, planet conditions, sect analysis, Moon mansion, antiscia, extended mutual reception, stations, Arabic Parts (15 total), triplicity rulers. 8 scans, 11 output sections, 11-dimension rubric. |
| 1.5 | 2026-03-22 | Gender/name handling. Part of Fortune / Lot of Fortune synonym note. Moon phase qualifier. Stationing Planets and Antiscia conditionally present. Declination clusters referenced. |
| 2.0 | 2026-03-30 | Unified prompt incorporating AstroCast v1.0.128+ Horary Perfection Analysis section. Engine data handling protocol: use as authoritative baseline, elaborate with full framework, explain disagreements. Steps 2 and 4 dual-path (with/without perfection data). New stress-test prompts for engine/AI interaction. Rubric updated for perfection integration quality. Submission templates updated. Glossary expanded with perfection, prohibition, refranation, translation, and collection definitions. |
| 2.1 | 2026-04-18 | Ten revisions absorbing library-wide standards and two long-pending HoraryInterp TODOs. Seven library standards from EnhancedBirthInterp v2.9 and Eclipse v1.1: (1) Default voice declared explicitly (2nd-person to querent; public-figure exception). (2) AstroCast Front-Matter block added near top of spec. (3) Compliance checklist merged with Interpretation Structure — single numbered list covers all 13 deliverable sections including Fixed Stars Referenced and Glossary. (4) Per-section depth targets added as suggestions with "clarity over brevity" framing. (5) Glossary revised: topic-grouped, basic-astrology reader level, curated by default, with "Full Glossary" and "Alphabetical glossary" overrides. (6) "Do NOT" instructions rewritten as positive statements (safety-gating prohibitions preserved). (7) Context Fit Rule — every Notes-field factual claim (beyond the question itself) grounded in chart signatures. Plus two horary-specific additions: (8) Mercury's condition as supplementary denial / affirmation testimony on communication, scheduling, documents, and technology-purchase questions — activated by question-topic keywords and independent of whether Mercury is a primary significator. (9) 4th-house emotional/psychological resolution guidance — the 4th can describe how the querent feels about the outcome regardless of its logistical form. (10) Editorial: version header, change log, footer. |
| 2.2 | 2026-04-22 | Added formal Content Discipline section (positive form, safety-gating preserved) with "Do include" and "Safety-gated prohibitions" subsections. Closes single remaining gap from Opus 4.7 audit campaign (Std 6). No functional astrological methodology changes. |
| 2.3 | 2026-06-01 | Seven clarity/accuracy refinements surfaced by the Sabrent 4TB external-drive test chart (2026-06-01), a purchase horary phrased as "now or wait?". (1) Timing-choice question handling: a "not perfected" verdict resolves to "wait / not yet" — and the judgment names which limb of the question a "No" answers — unless the chart shows outright denial. (2) Engine VoC reconciliation: the AstroCast engine does not apply the Goldstein-Jacobson VoC-sign exceptions; when it denies on VoC with the Moon in Cancer / Taurus / Sagittarius / Pisces, soften and reassess. (3) Engine category vs. natural-house divergence: when the engine's derived house differs from the Step 2b natural house for the topic (notably purchases, a 2nd-house matter), report the engine baseline, note the alternative framing, and confirm convergence. (4) Moon-phase acquisition direction: waxing favours acquiring/initiating, waning favours completing/releasing — applied to purchase and election-flavoured questions. (5) Same-ruler-fallback edge case: when the natural significator is the same planet as L1, read the quesited as an extension of the querent's own situation. (6) Topic-conditional Mercury positive case: a direct, unafflicted Mercury on a tech/purchase question means the common purchase-timing cautions are absent — say so. (7) OOB interpretive texture (OOB Mercury = judgment outside normal bounds; OOB Moon = unsettled mood; OOB Venus = desire outside its measure) plus reading the Moon's first aspect after sign egress on a void chart. No structural section changes; rubric dimensions 5, 7, and 9 extended. |
| 2.4 | 2026-06-10 | Reception Explanation Standard adopted from EnhancedBirthInterp v3.1 item 16: (1) both halves of any mutual reception named in plain terms on first mention; (2) same-sign receptions always explicitly explained with shared-ground framing ("exchange places" reserved for cross-sign domicile-domicile); (3) convention note: extended reception is dignity-placement-based, no aspect required (Lilly's stricter definition acknowledged). Plus V1.4 items: Mercury Rx as scheduling co-significator; exaltation mutual reception always checked; 4th house can describe emotional resolution, not only logistical outcome; judgment-transparency clause for aspect-required reception convention. Propagated via Claude Code, 2026-06-10. |
| 2.5 | 2026-06-12 | Rubric dimension 9 reworded to cleanly separate co-significator handling from supplementary-testimony integration (retrograde Mercury on scheduling/communication questions = CO-SIGNIFICATOR woven into the judgment per Step 3(c); otherwise Mercury's full condition = SUPPLEMENTARY TESTIMONY; conflation now penalized at the 3-anchor; all v2.3 keyword content preserved) — closes the dimension-9 residue flagged in Reception_Propagation_Summary_2026-06-10 Discrepancy 2; wording operator-approved 2026-06-12. Plus v3.1 library standards per applicability: Unvarnished Mode honesty register; Evidentiary Firewall (incl. mid-conversation context updates with hedge preservation); PDF Design System compact block (full spec in EnhancedBirthInterp Appendix A); generation colophon; Fixed Star Lore Policy. Propagated via Claude Code, 2026-06-12. |

---
~~~~

## Extract 2 — 7. Evaluation Rubric

Source line 1339. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## 7. Evaluation Rubric

Score each judgment 1–5 across these dimensions:

| # | Dimension | 1 (Poor) | 3 (Adequate) | 5 (Excellent) |
|---|-----------|----------|--------------|---------------|
| 1 | **Radicality Assessment** | Skipped or glossed | Reported without weighing | All considerations weighed, verdict explicit, special-radicality notes (e.g., hour ruler = natural significator) named when applicable |
| 2 | **Significator Accuracy** | Wrong or arbitrary | Correct primary sigs | Correct primary sigs, same-ruler fallback named, Moon co-significator weighted, natural sigs cross-confirmed |
| 3 | **Dignity Analysis** | Peregrine treated as malefic, or dignity dumps | Five-level dignity reported | Five-level dignity applied interpretively; condition language matches the planet's actual testimony |
| 4 | **Reception Integration** | Reception ignored | Reception noted | Reception type, strength, and implication woven into perfection analysis; Goldstein-Jacobson's MR-by-degree used when relevant |
| 5 | **Perfection Verdict Clarity** | Equivocal or contradictory | Clear but unsupported | Unambiguous verdict with named mechanism(s) and specific chart evidence; for timing-choice questions, the verdict is mapped to the correct limb ("now" vs. "wait") rather than left as a flat denial |
| 6 | **Engine Integration (when present)** | Engine ignored or contradicted silently | Engine reported, no elaboration | Engine as baseline, elaborated with additional testimony, any disagreement explained with chart evidence |
| 7 | **Moon Reading** | Moon ignored or glossed | First applying aspect noted | Full applying sequence, speed, phase, mansion integrated into the sequence of events; phase read as acquisition direction (waxing/waning) on election-flavoured questions; first post-egress aspect noted on a void chart |
| 8 | **4th House Both Registers** | Skipped or logistics only | Logistics covered | Logistical and emotional/psychological resolutions both named and grounded |
| 9 | **Topic-Conditional Mercury** | Not checked when question is scheduling/communication/tech-purchase | Mentioned but not integrated, or co-significator and supplementary roles conflated | Mercury's role correctly tiered by question type: retrograde Mercury elevated to scheduling/communication CO-SIGNIFICATOR (delays, revisions, postponements, matters returning — woven into the judgment, not appended); otherwise Mercury's full condition integrated as SUPPLEMENTARY TESTIMONY — failure-mode interpretation when afflicted (retrograde → rescheduling, combust → miscommunication, Scheat → mechanical/data failure, etc.), and the explicit "common purchase-timing cautions are absent" reassurance when Mercury is direct and unafflicted |
| 10 | **Sect Analysis** | Skipped | Out-of-sect malefic / in-sect benefic noted | Hayz, sect status of each significator, out-of-sect malefic role in the chart's obstacle structure |
| 11 | **Arabic Parts** | Skipped | Part of Fortune reported | Topic-specific Lot selected and integrated; Lot ruler assessed; angle/ruler contact named |
| 12 | **Timing** | Skipped | Degree count provided | Degree count, sign/house unit, Moon-speed modifier, confidence qualification |
| 13 | **Voice Consistency** | Voice drifts or uses wrong default | Voice mostly consistent | 2nd-person to querent (or override) held throughout; no drift within sections |
| 14 | **Context Fit** | Notes ignored or facts asserted without grounding | Some Notes grounded | Every Notes-field fact grounded in at least one named chart signature (vacuous when Notes = question only) |

Scoring: 60–70 Exceptional | 45–59 Strong | 30–44 Moderate | 14–29 Weak.

---
~~~~

## Extract 3 — 8. A/B Test Variations

Source line 1364. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## 8. A/B Test Variations

| Test | Variable | Variation A | Variation B | Variation C |
|------|----------|-------------|-------------|-------------|
| T1 | Prompt version | v2.1 | v2.0 | v1.5 |
| T2 | Engine presence | v1.0.128+ export with perfection section | v1.0.127 or earlier (no perfection) | Manual override ("Ignore perfection") |
| T3 | Template | 4A Standard | 4B Context | 4C Quick |
| T4 | Question type | Relationship | Career | Purchase |
| T5 | Reception scenario | Mutual reception present | No reception | Reception by degree (Goldstein-Jacobson swap) |
| T6 | Moon state | Moon VoC | Moon fast and direct | Moon slow and cadent |
| T7 | Denial presence | Clear perfection | Clear denial | Ambiguous / qualified |
| T8 | AI service | Claude Opus 4.7 | ChatGPT | Copilot |
| T9 | Audience level | Default (basic) | "Assume professional" | "Assume no astrology" |
| T10 | Engine / AI agreement | Engine and AI agree | Engine and AI disagree (expect explanation) | Engine says Uncertain |
| T11 | Timing estimation | Perfection orb given | Perfection orb absent | Denial verdict (no timing) |
| T12 | **Voice override** | Default 2nd-person | `"Use 3rd-person about [Name]"` | Public-figure auto 3rd-person |
| T13 | **Glossary override** | Default (curated, topic-grouped, basic) | `"Full Glossary"` | `"Alphabetical glossary"` |
| T14 | **Topic-conditional Mercury** | Communication-device purchase with Mercury in detriment/fall | Scheduling question with Mercury Rx | Non-Mercury-topic question (should not over-weight Mercury) |

T12, T13, and T14 are new in v2.1 and validate Changes 1, 5, and 8
respectively. Run T14 against the Apple Neo laptop chart
(2026-04-14) as the first v2.1 validation target.

---
~~~~

## Extract 4 — 9. Appendix: Quick-Reference Prompts

Source line 1389. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## 9. Appendix: Quick-Reference Prompts

### One-Line Variations

```
Judge this horary chart following Lilly and Goldstein-Jacobson. Give
a clear yes/no/qualified answer with chart evidence. Use the
perfection analysis section when present.
```

```
Assess the considerations before judgment. Is the chart radical?
Proceed to judgment.
```

```
This is a purchase horary phrased as "now or wait?". Assess whether
the buy is well-timed. Map a "not perfected" verdict to "wait / not
yet" unless the chart shows outright denial, and name the improving
window. Pay special attention to Mercury's condition (and say so when
the usual tech-purchase cautions are absent), the Moon's phase as
acquisition direction, and the 4th house (end of the matter, both
logistically and emotionally).
```

```
Read the Moon's applying aspects and describe the sequence of events.
```

### Stress-Test Prompts

```
This horary has multiple considerations against judgment. Should it
be read? When you proceed, how do the cautions affect your confidence?
```

```
The significators do not form a direct applying aspect. Is there any
other perfection mechanism? Check translation, collection, mutual
reception, and antiscia.
```

```
The engine says "Uncertain." Assess whether the full analysis (Moon
sequence, Arabic Parts, 4th house, sect, topic-conditional Mercury)
tips the judgment toward Yes or No.
```

```
The engine says "Yes" but there are multiple considerations against.
Should the considerations override the perfection verdict? Weigh the
evidence.
```

```
This is a communication/technology question. Even if Mercury is not
a primary significator, assess Mercury's condition as supplementary
testimony. What failure modes does Mercury's specific condition
describe?
```

---
~~~~

## Extract 5 — Version History

Source line 1511. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Version History

See the Change Log near the top of the spec.

---

*v2.5 (2026-06-12): Rubric dimension 9 reworded (co-significator vs. supplementary testimony); v3.1 library standards adopted.*
*v2.4 (2026-06-10): Reception Explanation Standard; Mercury-Rx scheduling co-significator; exaltation reception always checked; 4th-house divergence clause.*
*v2.3 (2026-06-01): Seven clarity/accuracy refinements from the Sabrent purchase-horary live run.*
*v2.2 (2026-04-22): Content Discipline section added.*
*v2.1 (2026-04-18): Library-wide standards absorbed; topic-conditional Mercury check; 4th-house emotional resolution.*
~~~~
