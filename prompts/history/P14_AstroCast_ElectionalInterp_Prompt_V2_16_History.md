# P14 Revision History and Extracted Records

Current release: 2.16 (2026-11-28). Previous release: 2.15 (2026-10-15).

Source: P14_AstroCast_ElectionalInterp_Prompt_V2_15.md (in Prompts.zip). The source remains unchanged.

## Current changes

The 1.7.2 library run. Source: the 1.7.2 library review of 2026-11-27 and Steve's rulings of 2026-11-27 and 2026-11-28. Four of the review's findings (Q2, Q3, Q14, Q15) and one change of AstroCast 1.7.2 (code finding N11: the electional export prints display names). Line numbers are v2.16's; the replaced v2.15 passages are in the v2.15 prompt (in Prompts.zip, and in the repository's history). The changes:

- **:1, :3, :5, :6, header.** The title reads v2.16, the documentation line links the V2_16 guide and history, the Version line reads 2.16 and the Date line 2026-11-28.
- **:7, Compatible Export (N11).** AstroCast 1.7.2; the line says that an older export prints the code words ("RealEstateListing", "WaningCrescent") where 1.7.2 prints words.
- **:63, :1168 (Q15).** The Quick Judgment template is 4C; both lines called it 2C.
- **:76-79, Instructions (Q3, ruling 6).** Paste the whole prompt, then one template from the Chart Submission Templates section (Section 4). v2.15 said "Copy the preamble (Section 1)" and "Add the submission template (Section 2)"; Section 2 is the Interpretation Structure, and the shared operating rules the Preamble applies stand above Section 1.
- **:172, Health-election caveat (Q2).** One sentence: for a medical election the comparison of the candidates is given as the historical method reads them, and the Recommendation (Layer 6b), the Timing Notes and the gate's lines on them do not apply. v2.15 forbade a date for such an election and, in Layer 6b, the gate and template 4C, required one.
- **:240-243, the Moon's phase (N11).** The balsamic Moon is the export's Waning Crescent, printed so from 1.7.2 and as "WaningCrescent" before.
- **:449-456, Working-type derivation (N11).** The nine Election Type values as 1.7.2 prints them, the four code words of older exports, and one sentence mapping a type printed as words to the table's one-word row.
- **:526-528, Election Type Table (Q14).** Three rows, RealEstateListing, RealEstateClosing and CreativeLaunch, with the significators the prompt's own audit list gives them (Venus, Saturn, the Sun), which are the engine's. v2.15 had no row for three of the nine types the export prints, so they fell to keyword guessing. The key houses are this run's choice from the tradition (the 4th for property, the 7th for the other party, the 10th for price and standing, the 5th for creative work).
- **:1148-1150, :1222 (Q15).** Five fixed sections, as the table lists them (it said six), and 6 sections in all for one candidate.
- **:1251-1253, Compliance Gate (Q15).** The planetary hour is named for the candidate instant only, never as a buffer or a supportive window, as the Planetary Hour rule already said; the gate had asked for "the planetary-hour buffer".
- **:1335-1337, :1360-1362, templates 4A and 4B (Q15).** Notes and added context are the querent's own account, claims to weigh and not verified fact, and a practical constraint is applied before the candidates are ranked, as the family rule says. The templates had read "verified information" and "letting the chart evidence lead".

The User Guide 1.2 states the one method and says the same for its user.

Unchanged, by instruction: every other rule of v2.15, among them the audit list and the keyword map, which keep their one-word type names.

## Previous changes — 2.15 (2026-10-15)

Aligned with the AstroCast 1.3.4 exports; a Compatible Export line names 1.3.4 (R3). O-D3 (R1): every orb prints as truncated degrees and minutes with the prime; a compact `147'` or a decimal belongs to an older export. Before 1.3.4 the electional embed rounded. R2: a header carrying `**Calendar:** dates before 1582-10-15 are on the Julian calendar` means every date is on the calendar in force; nothing to convert. A range bound on 5–14 October 1582 is refused (R3, R-D7). Older lines made true: the Planetary Sect table's Role and Hayz columns, the Proximity column and the Score and Conditions cells (the field names are the JSON's); the Natal Harmony table lists the tightest 15 major aspects; the natal slot holds the chart picked, else the last calculated; the export prints the natal Name and never natal Notes; it names the last phase Waning Crescent; the Natal Chart Summary carries positions, cusps, dignities and key aspects only, so the natal scans go as far as it allows.

Shared operating rules 1.0 are embedded where applicable. History and operator evaluation material have been extracted. Historical excerpts below are records, not current instructions; older policies, examples, ratings, and version references may be superseded by the active prompt and current guide.

## Previous changes — 2.14 (2026-10-13)

Aligned with the AstroCast 1.3.3 export. Q (S-D12): combustion is 8°, not 8°30′ (two lines); the header's Score ceiling and the plain score (82.25, no "/100"); the algorithm audit says what each dimension actually reads (the rising sign only for the Ascendant; the type's one significator; the Moon block without the mansion; the significator's contacts to natal Jupiter, Venus, Mars and Saturn only for Natal Harmony) and that triplicity, cazimi and the Natal Harmony table are evidence the score does not read; the nine Election Type values; the planetary-hour buffer is gone, the hour being unscored. P (S-D11): the header and the Moon Conditions block name the same first applying aspect, the first exact one before the Moon leaves its sign at any distance; the three-places orb example replaced. O (S-D10): instants, not windows; every grid sample is scored, so clustering is never a sampling gap. O2, O3, O1: the Candidate instants, Separation, Event Time Zone and Samples-without-a-planetary-hour header lines, a repeated time's offset, the "Disqualifying rules" line, the candidate midpoints, a Mercury retrograde period from station to station, the zero-candidate rule, and the natal chart's house system in place of Placidus (three lines). F7 (D14, F5-1): a cluster spans at most 2°; out of bounds from the export's OOB column. O-2: the export version is the engine line. F5 (D17): a candidate cast in a repeated hour carries a Time Note, a calculation note, not supplied context. C: no instruction changed; the void calendar is now exact.

## Previous changes — 2.13 (2026-09-08)

Research describes the querent's decision and candidate tradeoffs. Treat availability, age, and practical constraints as supplied facts, not facts needing chart proof. Preserve candidate ranking, placeholder detection, elected-act distinction, and no-feasible-candidate outcome.

## Extract 1 — Change Log

Source line 48. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2026-03-01 | Six-layer method (Robson/Lilly, Hand, Greene/Sasportas, Ebertin, Forrest, Synthesis). |
| 2.1 | 2026-03-06 | Pre-Analysis Scan added (3 scans). |
| 2.2 | 2026-03-06 | Compliance Requirements section added. |
| 2.3 | 2026-03-06 | Under-the-beams guidance in Layer 1. |
| 2.4 | 2026-03-07 | Paran integration with interpretive framework and comparative guidance. Scan #4 added. |
| 2.5 | 2026-03-08 | Moon Phase Degree Sensitivity (early/mid/late within 45° phases). |
| 2.6 | 2026-03-16 | Techniques Implementation: five-level dignity, planet conditions, sect analysis, Moon mansion, stationing planets, antiscia, Arabic Parts/Lots (15 total), Ascendant triplicity rulers. 8 scans. Glossary expanded to 25 terms. |
| 2.7 | 2026-03-22 | Gender/name handling added. Part of Fortune / Lot of Fortune synonym note. Stationing Planets and Antiscia sections noted as conditionally present in scans. Pre-computed declination clusters referenced. Version history converted to table format. |
| 2.8 | 2026-04-14 | Chronological order requirement added to Practical Timing Advice (Layer 6d). Compliance checkbox added. |
| 2.9 | 2026-04-18 | Ten revisions absorbing library-wide standards plus two Electional-specific additions: voice default, front-matter block, unified structure-plus-compliance, per-section depth suggestions, topic-grouped curated glossary, positive instructions, Context Fit Rule, placeholder-natal handling, health-election caveat, editorial. |
| 2.10 | 2026-06-10 | Thirteen revisions from the Doctor Visit live-run review (2026-06-11–2026-08-10, two candidates). (1) **Self-containment restored** — Pre-Analysis Scans 1–8 and paran handling (angle meanings, interpretive priority, convergence signals, integration style) restored verbatim from v2.8 and inlined; bracketed see-elsewhere references removed. v2.8 specifics recovered in the process: ±2° declination-cluster band, stationProximity scale semantics (0.0 = nearly stopped / 1.0 = threshold), Royal Stars list and Algol warning, OOB interpretive detail, pre-v1.0.61 export skip rules. Adjustments layered on: "all three candidates" → "all candidates," Scan 7 absence statement, Scan 8 topical-keyword rule. (2) **Candidate count generalized** — structure, length targets, and compliance gate are data-driven on the export's Candidates field (N ≥ 1); hardwired three-candidate assumptions removed. (3) **Working-Type Derivation** — interim keyword classifier for Election Type Other/General/unmapped, word-boundary matching, health-priority fail-safe, derivation named in Overview. Engine-side classification remains the preferred long-term fix. (4) **Algorithm audit step** — Layer 6b now requires cross-checking each score dimension against its corresponding export tables and naming contradictions. (5) **Infeasible-time protocol** — Layer 6d/6e define the elected act, authorize the booking-moment reading, and require an explicit feasibility statement plus re-run suggestion with time-of-day constraints. (6) **Health-caveat detector expanded** — checkup, physical, screening, clinic, specialist, physician, bloodwork, lab work, scan, MRI, biopsy, wellness visit, medical follow-up added. (7) **Conditionally-absent sections extended** to Midpoint Activations and Stationing Planets, with prescribed absence language. (8) **Balsamic/waning Moon qualified by activity type** — decrease elections (treating, ending, releasing, removing) read waning as supportive. (9) **Planetary-hour buffer** — Timing advice states the hour window around the candidate minute and confirms it clears void periods. (10) **Candidate-clustering note** — when all candidates fall within a small fraction of the window, name it and offer a re-run. (11) **Export field discrepancy rule** — aspects table is authoritative for orbs; discrepancies noted. (12) **{Election_Goal_Short} derivation rule** specified. (13) Compliance gate reworded for N candidates; v2.10 checkboxes added. Editorial: version header, change log, footer updated. |
| 2.11 | 2026-06-10 | Reception Explanation Standard adopted from EnhancedBirthInterp v3.1 item 16: (1) both halves of any mutual reception named in plain terms on first mention; (2) same-sign receptions always explicitly explained with shared-ground framing ("exchange places" reserved for cross-sign domicile-domicile); (3) convention note: extended reception is dignity-placement-based, no aspect required (Lilly's stricter definition acknowledged). Propagated via Claude Code, 2026-06-10. |
| 2.12 | 2026-06-12 | v3.1 library standards adopted from EnhancedBirthInterp v3.1 (items 1-18), per applicability: Unvarnished Mode honesty register; Evidentiary Firewall (incl. mid-conversation context updates with hedge preservation); PDF Design System compact block (full spec in EnhancedBirthInterp Appendix A); generation colophon; Fixed Star Lore Policy. Propagated via Claude Code, 2026-06-12. |

---
~~~~

## Extract 2 — Adoption Notes (remove after review)

Source line 1423. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Adoption Notes (remove after review)

1. **Scan/paran restoration verified against v2.8 (2026-06-10).** The
   inlined Scans 1–8 and paran-handling text were restored verbatim
   from the v2.8 original, with only the V2.10 adjustments applied
   ("all three candidates" → "all candidates"; Scan 7 absence
   statement; Scan 8 topical-keyword rule; Scan 1 working-type keyword
   sentence). Two values an earlier reconstruction had wrong were
   corrected from the original: declination-cluster band is ±2°, and
   stationProximity runs 0.0 (nearly stopped, maximum influence) to
   1.0 (threshold boundary). No further diff required.
2. **Working-Type Derivation is interim.** When the engine-side
   classification service ships (shared across Event, Horary, and
   Electional), revise rule 5's final paragraph and re-test with a
   resolved-type export; the keyword map then becomes legacy-export
   fallback only.
3. **Engine items tracked separately (not addressed by this prompt):**
   Malefic Avoidance scorer keyed to house number rather than the
   Angular Planets table; candidate-generation time-of-day window
   constraint; orb-rounding consistency across header / Moon
   Conditions / aspects table; Election Type taxonomy emission.

---
~~~~

## Extract 3 — Version History

Source line 1447. Operational method, output requirements, and essential definitions are retained in the active prompt or explicitly reconciled by its shared/family rules.

~~~~text
## Version History

See the Change Log near the top of the spec.

---

*v2.12 (2026-06-12): v3.1 library standards adopted.*
*v2.11 (2026-06-10): Reception Explanation Standard.*
*v2.10 (2026-06-10): Thirteen revisions from the Doctor Visit live run (self-containment restored, N-candidate generalization, Working-Type Derivation, elected-act feasibility protocol).*
~~~~
