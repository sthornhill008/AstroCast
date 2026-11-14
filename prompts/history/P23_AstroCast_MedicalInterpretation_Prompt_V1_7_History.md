# P23 Revision History and Extracted Records

Current release: 1.7 (2026-10-15). Previous release: 1.6.

Source: AstroCast_MedicalInterpretation_Prompt_V1_6.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with the AstroCast 1.3.4 exports; a Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. The Hard Aspects orb was a decimal before 1.3.4. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. Older line made true: a Hyleg only when the operator supplies one.

Shared operating rules 1.0 are embedded where applicable. History and operator evaluation material have been extracted. Historical excerpts below are records, not current instructions; older policies, examples, ratings, and version references may be superseded by the active prompt and current guide.

## Previous changes — 1.6 (2026-10-13)

Aligned with the AstroCast 1.3.3 export. F5 (D17): the Time Note sentences. N (S-D9): the temperament scheme is AstroCast's own, and a tie prints as a tie (three lines). O-7 (O-D8): the export's sections as it prints them, and the **Health Context:** header line. No Hyleg is exported; the Health Houses table has the 6th, 8th and 12th.

## Previous changes — 1.5 (2026-09-08)

Resolve the public-figure/referral policy as an explicit library decision; align voice across front matter and body. Separate exported symbolic scores from medical risk, remove confirmation-by-correspondence wording, and avoid inferring anatomy from gender.

## Extract 1 — Evaluation Rubric (12 dimensions, 1–5 scale)

Source line 424. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Evaluation Rubric (12 dimensions, 1–5 scale)

| Dimension | 1 (Poor) | 3 (Adequate) | 5 (Excellent) |
|-----------|----------|--------------|---------------|
| **Temperament Integration** | Temperament scores listed but not interpreted | Described but generic | Constitutional type vividly described with quality tendencies, illness predisposition, and corrective guidance specific to this chart's balance |
| **Vitality Assessment** | Vitality pillars not assessed | Some mention of Sun/Moon | All three pillars + Hyleg assessed with dignity, conditions, house, and composite meaning; vitality rating justified with evidence |
| **Vulnerability Specificity** | Body systems listed without interpretation | Some systems interpreted | Top 3–5 systems interpreted with chart evidence, traditional correspondence, and practical meaning; Health Context matched when present |
| **Health House Analysis** | Houses not discussed | Some houses mentioned | Each significant house interpreted with ruler condition, malefic presence, and domain-specific meaning |
| **Sect Integration** | No sect analysis | Out-of-sect malefic mentioned | Out-of-sect malefic = primary risk with house + body regions; in-sect benefic = primary support; both woven into relevant body system discussion |
| **Health Context Integration** | Context ignored when provided | Context mentioned briefly | Context anchors the interpretation; chart confirmations of stated conditions noted prominently; all sections speak to the person's lived experience |
| **Ethical Framing** | Diagnostic or predictive language used | Mostly appropriate | Flawless: all language uses "tendency," "sensitivity," "area to monitor"; no causation claims; disclaimer prominent; wellness framed as traditional perspective |
| **Wellness Recommendations** | No recommendations | Generic health advice | Temperament-specific diet/movement/environment/lifestyle recommendations grounded in this chart's specific constitutional type and vulnerabilities |
| **Accessible Language** | Heavy jargon | Mixed | Consistently clear; all terms defined; a person with no astrology knowledge can understand the health insights |
| **Affliction Interpretation** | Afflictions listed but not explained | Some explanation | High-severity afflictions described in practical health terms; severity-proportional treatment; convergent themes identified |
| **Honest Complexity** | Only strengths or only vulnerabilities | Both acknowledged | Constitutional strengths AND vulnerabilities named proportionally; areas of resilience highlighted alongside areas of sensitivity |
| **Synthesis Quality** | No wellness synthesis | Some grouping | Key Takeaways has exactly 5 bullets; wellness perspective is specific to this constitution; overall profile is clear and actionable |

### Scoring Guide
- **48–60:** Publication quality. Ready for client delivery.
- **36–47:** Good draft. Minor revisions needed.
- **24–35:** Adequate but needs more depth or specificity.
- **Below 24:** Needs fundamental rework.

---
~~~~

## Extract 2 — Version History

Source line 462. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.4 | 2026-06-12 | v3.1 library standards adopted from EnhancedBirthInterp v3.1 (items 1-18), per applicability: Unvarnished Mode honesty register; Evidentiary Firewall (incl. mid-conversation context updates with hedge preservation); PDF Design System compact block (full spec in EnhancedBirthInterp Appendix A); generation colophon; Fixed Star Lore Policy; dated timing windows. Propagated via Claude Code, 2026-06-12. |
| 1.3 | 2026-04-23 | See Change Log below. |
| 1.2 | 2026-04-22 | See Change Log below. |
| 1.1 | 2026-04-05 | Health timing context: profection year health activation, lord of the year body system convergence, firdaria Mars/Saturn health periods, ZR peak period visibility. |
| 1.0 | 2026-03-28 | Initial release. Five-layer method: constitutional temperament, vitality assessment, body system vulnerabilities, health houses, wellness synthesis. Nine output sections. Navy/gold PDF styling. 12-dimension evaluation rubric. Temperament wellness reference table. |

---
~~~~

## Extract 3 — Change Log

Source line 474. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Change Log

### v1.4 (2026-06-12)
- v3.1 library standards adopted from EnhancedBirthInterp v3.1 (items 1-18), per applicability: Unvarnished Mode honesty register (front-matter row, override-list entry, Style & Tone block); Evidentiary Firewall anchored to the Style & Tone "do not invent data" rule; PDF Design System compact block and generation colophon (with compliance-gate checkbox) in the Output File section; Fixed Star Lore Policy in Content Discipline; dated timing windows for the Health Timing Context section. Propagated via Claude Code, 2026-06-12.
- No functional astrological methodology changes; medical safety gates (no diagnosis, no treatment prescription, Medical Branch Collision public-figure refusal) unchanged and never lifted by the honesty register.

### v1.3 (2026-04-23)
- Renamed "What NOT to Include" heading to "Content Discipline" with positive "Do include" and "Safety-gated prohibitions (non-overridable)" subsections. Library-standard framing. Closes Std 6 Gap 0.5 identified by v2 scanner 2026-04-23. No change to the set of prohibited content; Medical Branch Collision refusal language preserved verbatim.
- Heading promoted from h3 subsection (within Compliance Requirements) to h2 top-level section to match library-wide Content Discipline placement pattern.
- Added Version History row for v1.3.

### v1.2 (2026-04-22)
- Added AstroCast Front-Matter Configuration table (Opus 4.7 audit, Std 2 gap closure).
- Added Medical Branch Collision -- Public-Figure Refusal section (Opus 4.7 audit, Option c resolution). Readings on public-figure natal charts are now refused; operator is directed to request a decumbiture chart instead. Non-overridable via spec parameters.
- No functional astrological methodology changes to medical interpretation when the reading is not refused.

---

*Paste the AstroCast Medical Markdown below this line.*
~~~~
