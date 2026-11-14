# P22 Revision History and Extracted Records

Current release: 1.7 (2026-10-15). Previous release: 1.6.

Source: AstroCast_Eclipse_Prompt_1_6.md (in Prompts.zip). The source remains unchanged.

## Current changes

Aligned with the AstroCast 1.3.4 exports; the Chart Types line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. The series prints no orb; the optional natal export does. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. Older line made true: a solar eclipse's type can be Hybrid.

Shared operating rules 1.0 are embedded where applicable. History and operator evaluation material have been extracted. Historical excerpts below are records, not current instructions; older policies, examples, ratings, and version references may be superseded by the active prompt and current guide.

## Previous changes — 1.6 (2026-10-13)

Aligned with the AstroCast 1.3.3 export. B (T-D13, T4-4): the solar Duration is the central eclipse over the whole Earth, not totality, and the phase rows read "Central eclipse begins/ends (whole Earth)"; the "1 year per minute of totality" rule is no longer applied to that figure (twelve places); a lunar eclipse's Duration stays totality.

## Previous changes — 1.5 (2026-09-08)

Research changes the natal overlay's audience while mundane sections remain collective. Replace forced natal-context matching; preserve eclipse-type depth scaling and the intentional Biographical Fit naming exception.

## Extract 1 — Change Log

Source line 13. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-04-11 | Initial production release. Six-section structure (Overview, Significance Ranking, Pairs/Axis, Individual Interpretations, Natal Overlay, Synthesis). Navy/gold design system. Significance scale (1–10). Eclipse pair / nodal axis analysis. Phase timing tables for Total / Annular. Override protocol. Self-audit checklist. Production tested 2026-04-17 on a private chart's lunar-eclipse-only overlay. |
| 1.1 | 2026-04-18 | Two fixes from the 2026-04-17 production run, plus adoption of library-wide standards from EnhancedBirthInterp v2.9. (1) **Mandatory Glossary** — new Appendix A, topic-grouped, targeted at basic astrology understanding, with Full Glossary override for readers with no astrology knowledge. (2) **Client-name in filename** when natal overlay is present: `Eclipses_{StartYear}_{EndYear}_{ClientName}_EclipseInterpClaude.pdf`. (3) **AstroCast Front-Matter block** — single-glance defaults and overrides table near top of spec. (4) **Voice default declared explicitly**: 3rd-person impersonal for mundane-only reports; 2nd-person addressing the native when natal overlay is present; 3rd-person for public-figure natal overlays. (5) **Compliance checklist merged with Report Structure** — single authoritative numbered list. (6) **Per-section depth targets as suggestions** with "clarity over brevity" framing. (7) **Biographical Fit Rule** — when natal overlay is present, every Note-field fact is grounded in at least one named chart signature. (8) **"Do NOT" instructions rewritten as positive statements** (safety-gating prohibitions preserved). |
| 1.2 | 2026-04-22 | Added formal Content Discipline section (positive form, safety-gating preserved) with "Do include" and "Safety-gated prohibitions" subsections, extracted from scattered Compliance-checklist items and preamble guidance. Closes single remaining gap from Opus 4.7 audit campaign (Std 6). No functional astrological methodology changes; eclipse type scaling, Saros series context, and personal-overlay Biographical Fit Rule preserved. |
| 1.3 | 2026-04-23 | Added scoped-exception comment at the personal-overlay BIOGRAPHICAL FIT RULE subsection clarifying intentional "Biographical Fit Rule" usage within the narrow personal-overlay scope. Closes Std 7 Gap 0.5 documented-exception flag from v2 scanner 2026-04-23. No change to the rule itself or to any methodology. |
| 1.4 | 2026-06-12 | TODO #5 closure plus v3.1 library standards. Glossary reader level: default upgraded from basic-astrology to NO astrological knowledge assumed (eclipse reports reach general readers — stricter than the library default; glossary remains mandatory and topic-grouped; "Assume basic astrology knowledge" now an override; the Full Glossary override's distinct effect becomes methodology-wide scope expansion). Filename rule parameterized from EclipseInterpClaude to EclipseInterp{AI} in front-matter, PROMPT body, and compliance gate (client-name-in-filename rule unchanged from v1.1). v3.1 standards per applicability: Unvarnished Mode honesty register; Evidentiary Firewall (incl. mid-conversation context updates with hedge preservation); PDF Design System compact block (full spec in EnhancedBirthInterp Appendix A); generation colophon; Fixed Star Lore Policy; dated timing windows. Design-system row shade reconciled #EEEEEE → cream #F0EDE4 (operator decision 2026-06-12). Worked-case lineage: a private chart's lunar-eclipse overlay production run 2026-04-17 per the campaign continuity doc. Propagated via Claude Code, 2026-06-12. |

---
~~~~

## Extract 2 — Testing Checklist

Source line 741. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Testing Checklist

After the AI generates the PDF, verify:

**Structural:**
- [ ] Filename follows the convention, branching correctly on natal
      overlay presence
- [ ] All sections present in the specified order (6 for mundane,
      7 for natal overlay)
- [ ] Appendix A — Glossary of Terms is present, topic-grouped
- [ ] Footer on every page with correct attribution

**Content:**
- [ ] All eclipses from the export are interpreted
- [ ] Significance ranking covers all eclipses with 1–10 scale
- [ ] Two ranking tables (general + personal) when natal overlay active
- [ ] Eclipse pairs correctly identified with nodal axis
- [ ] Phase timing tables present for Total and Annular eclipses
- [ ] Penumbral eclipses given brief treatment unless escalated by
      natal geometry (and escalations named explicitly)
- [ ] Personal natal overlay present if natal data provided
- [ ] Biographical Fit Rule satisfied — every Note-field fact
      grounded in at least one chart signature
- [ ] Synthesis section with forecast and historical parallels

**Design:**
- [ ] Navy/gold design system applied consistently
- [ ] Gold-bordered natal-contact call-outs present where contacts exist
- [ ] All table cells are Paragraph objects
- [ ] No invented data

**Glossary:**
- [ ] Topic-grouped, not alphabetical (unless override invoked)
- [ ] No-astrological-knowledge reader level — foundational terms
      defined (unless a reader-level override is invoked)
- [ ] Curated to terms used in body (unless Full Glossary override)
- [ ] Chart-specific references where the term is used in the report
- [ ] "On Reading This Report" topic group present

---
~~~~

## Extract 3 — Historical revision footer

Source line 814. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
*v1.4 (2026-06-12): Reader level default upgraded to no-astrological-knowledge; filename parameterized to {AI}; v3.1 library standards adopted; row shade reconciled to cream #F0EDE4.*
~~~~

## Extract 4 — Historical revision footer

Source line 815. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
*v1.3 (2026-04-23): Biographical Fit Rule scoped-exception comment.*
~~~~

## Extract 5 — Historical revision footer

Source line 816. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
*v1.2 (2026-04-22): Content Discipline section.*
~~~~

## Extract 6 — Historical revision footer

Source line 817. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
*v1.1 (2026-04-18): Mandatory topic-grouped glossary; client-name filename; library-wide standards from EnhancedBirthInterp v2.9.*
~~~~
