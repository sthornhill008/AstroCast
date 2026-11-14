# AstroCast Prompt Usage Map

**Date:** 2026-10-22
**AstroCast:** 1.7.0
**Current manifest:** [MANIFEST.md](<MANIFEST.md>)

Which prompt reads which AstroCast export, where in AstroCast the export is made, and how each
prompt is used. Earlier prompt versions, and the 2026-10-20 and 2026-10-21 editions of this map,
are in `Prompts.zip` (kept by the maintainer, outside this repository).

## How to use a prompt

1. Make the export in AstroCast (the panel and buttons are in the first table below) and save
   the Markdown file.
2. Open a new AI chat.
3. Paste the **whole prompt**, then the export, then any request of your own (a focus, a
   context, a voice). **Three prompts differ:** P12 (synastry), P14 (electional) and P15
   (horary) have you copy only their Preamble (Section 1), add one of their submission templates
   (Section 2), and paste the export where the template says.
4. The three medical prompts (P23, P24, P25) are for education and research: say so in your
   request.

Optional helpers for writing the request are under **Helpers**, at the end.

What each prompt returns, and the options it takes, are in its User Guide in `guides/`.

**The asteroids and Lilith (AstroCast 1.6.0).** Tick **Points** ("Include asteroids and Lilith")
in the Chart panel's Houses section before exporting to add Ceres, Pallas, Juno, Vesta and Black
Moon Lilith to the reports that carry them. It is unticked at every launch and never saved;
ticking it recasts the chart on screen, and the other panels pick it up at their next Calculate.
The last column of the second table says what each prompt does with them. The Davison chart
(AstroCast 1.7.0) carries them as any real chart does.

## By export: where it is made, which prompt reads it

The panels are named as in AstroCast's left-hand bar. The Export panel repeats most export
buttons in one place (and holds Export JSON and Export PDF).

| Panel | Buttons | Export | Prompt |
|---|---|---|---|
| Chart | Calculate & Save (or Load a saved chart), then Save Markdown | Chart (Type: Birth) | P01 (full natal reading), P02 (shorter natal reading), P26 (factual chart PDF), P27 (chart wheel) |
| Export | Export JSON | Chart JSON | P26, P27 |
| Chart | Type: Horary, with the Horary Question's Category and Question; Calculate & Save, then Save Markdown | Horary chart | P15 |
| Chart | Type: Event, or a named kind (Wedding / Marriage, Surgery and the rest); Calculate & Save, then Save Markdown | Event chart | P16 (the event's own chart), P25 (decumbiture: a chart cast for an illness's onset), P27 |
| Synastry | Calculate Synastry, then Export Synastry MD | Synastry (two people) | P12, P27 |
| Synastry | Calculate Synastry with an event chart as one of the two, then Export Synastry MD | Synastry, Type B (an event to a natal chart) | P17 |
| Synastry | Calculate Composite, then Export Composite MD | Composite | P13, P27 |
| Synastry | Davison Chart section: Calculate Davison, then Export Davison MD | Davison chart (the relationship's real chart, cast at the midpoint of the two births in time and place) | P30 |
| Transits | Calculate with "Include transits" ticked, then Export Report MD | Transit Report | P03, P04, P27 |
| Transits | Calculate with "Include transits" unticked (solar arcs, progressions or primary directions ticked), then Export Report MD | Solar Arc / Progressions / Primary Directions report of a period | P28 |
| Transits | Calculate, then Export Timeline MD | Transit Timeline | P04; with the Medical export, P24 |
| Transits | Calculate, then Export Daily Horoscope MD | Daily Horoscope | P05 |
| Transits | Return & Ingress Timeline: Calculate Timeline, then Export MD | Return & Ingress Timeline | P10 |
| Returns | Calculate Solar Return, then Export Solar Return MD | Solar Return | P06, P27 |
| Returns | Calculate Lunar Returns, then Export Lunar Return MD | Lunar Return | P07, P27 |
| Returns | Planetary Return: Calculate Return, then Export Return MD | Planetary Return | P08, P27 |
| Returns | Planetary Return: Calculate Timeline, then Export Timeline MD | Planetary Cycle Timeline | P09 |
| Returns | Mundane Aspect Finder: Find Aspects, then Export MD | Mundane Aspect Series | P18 |
| Returns | Great Conjunctions: Find Conjunctions, then Export Series MD | Great Conjunction Series | P19 |
| Returns | Ingress Charts: Find Ingresses, then Export MD; or Calculate Cardinal Charts, then Export Cardinal MD | Ingress Series, or the four cardinal ingress charts | P20 |
| Returns | Lunation Charts: Find Lunations or Calculate Charts, then Export MD | Lunation Series | P21 |
| Returns | Eclipse Calculator: Find Eclipses, then Export MD | Eclipse Series | P22 |
| Relocate | Calculate Relocation, then Export Relocation MD | Relocation | P11, P27 |
| Primary | Calculate Directions, then Export Directions MD | Primary Directions Report, whole life | P29 |
| Election | Calculate Electional, then Export Electional MD | Electional Report | P14, P27 (each candidate) |
| Medical | Export Medical Report (Markdown) | Medical | P23; with the Transit Timeline, P24 |

No prompt of its own: the Map panel's astrocartography (Export ACG MD), the Returns panel's
Synodic Cycles, the chart PDF (Export PDF), and Export Backup.

**Routing by the report's header.** A report from the Transits panel whose `**Techniques:**`
line names Transits goes to P03; one without Transits goes to P28. The Primary panel's report,
whose header has a `**Span:**` line (birth to age 100), goes to P29.

## By prompt

| ID | Prompt (current file) | What it does | Asteroids and Lilith |
|---|---|---|---|
| P01 | [P01_AstroCast_EnhancedBirthInterp_Prompt_v3_7.md](<Natal/P01_AstroCast_EnhancedBirthInterp_Prompt_v3_7.md>) | The full natal interpretation: five traditions, optional health layer, current timing | reads them, as minor points |
| P02 | [P02_AstroCast_BasicInterp_Prompt_V1_5.md](<Natal/P02_AstroCast_BasicInterp_Prompt_V1_5.md>) | A shorter natal interpretation: blended (1A), Ebertin (1B) or traditional (1C) | reads them under 1A |
| P03 | [P03_AstroCast_TransitInterp_Prompt_v3_12.md](<Transit/P03_AstroCast_TransitInterp_Prompt_v3_12.md>) | Interprets a transit report's period, with its solar arcs, progressions and primary directions | reads transits and directions to them, never as a convergence |
| P04 | [P04_AstroCast_TransitTimeline_Prompt_V2_12.md](<Timeline/P04_AstroCast_TransitTimeline_Prompt_V2_12.md>) | A dated reference table of the period's events, clusters and castable dates | a short table of their events |
| P05 | [P05_AstroCast_DailyHoroscopeInterp_Prompt_V2_4.md](<Daily/P05_AstroCast_DailyHoroscopeInterp_Prompt_V2_4.md>) | A short daily horoscope for each day of the period | never carried |
| P06 | [P06_AstroCast_SolarReturnInterp_Prompt_V1_14.md](<Solar Return/P06_AstroCast_SolarReturnInterp_Prompt_V1_14.md>) | The year ahead from the solar return | reads the return's own |
| P07 | [P07_AstroCast_LunarReturnInterp_Prompt_V1_13.md](<Lunar Return/P07_AstroCast_LunarReturnInterp_Prompt_V1_13.md>) | The month ahead from the lunar return | reads the return's own |
| P08 | [P08_AstroCast_PlanetaryReturnInterp_Prompt_V1_7.md](<Planetary Returns/P08_AstroCast_PlanetaryReturnInterp_Prompt_V1_7.md>) | The cycle a planet's return opens (Jupiter, Saturn and the rest) | reads the return's own |
| P09 | [P09_AstroCast_PlanetaryCycleTimelineInterp_Prompt_V1_5.md](<Planetary Returns/P09_AstroCast_PlanetaryCycleTimelineInterp_Prompt_V1_5.md>) | The full cycle between two returns of a planet, by its phase points | never carried |
| P10 | [P10_AstroCast_ReturnIngressTimelineInterp_Prompt_V1_5.md](<Timeline/P10_AstroCast_ReturnIngressTimelineInterp_Prompt_V1_5.md>) | A calendar of returns and ingresses | never carried |
| P11 | [P11_AstroCast_RelocationInterp_Prompt_V1_10.md](<Relocation/P11_AstroCast_RelocationInterp_Prompt_V1_10.md>) | What changes when the birth chart is cast at a new place | reads their relocated houses |
| P12 | [P12_AstroCast_SynastryInterp_Prompt_V1_13.md](<Synastry/P12_AstroCast_SynastryInterp_Prompt_V1_13.md>) | The relationship between two people (copy the Preamble, add a template) | reads both people's and their contacts |
| P13 | [P13_AstroCast_CompositeInterp_Prompt_V1_12.md](<Composite/P13_AstroCast_CompositeInterp_Prompt_V1_12.md>) | The relationship as its own chart (midpoints) | reads the composite's |
| P30 | [P30_AstroCast_DavisonInterp_Prompt_V1_0.md](<Davison/P30_AstroCast_DavisonInterp_Prompt_V1_0.md>) | The relationship as a real chart, cast for the midpoint of the two births in time and place | reads the Davison chart's |
| P14 | [P14_AstroCast_ElectionalInterp_Prompt_V2_15.md](<Election/P14_AstroCast_ElectionalInterp_Prompt_V2_15.md>) | Ranks the electional candidates and recommends a moment (copy the Preamble, add a template) | never carried |
| P15 | [P15_AstroCast_HoraryInterp_Prompt_V2_10.md](<Horary/P15_AstroCast_HoraryInterp_Prompt_V2_10.md>) | Judges a horary question, after Lilly and Goldstein-Jacobson (copy the Preamble, add a template) | never carried |
| P16 | [P16_AstroCast_EventChartInterp_Prompt_V3_8.md](<Event_Type_A/P16_AstroCast_EventChartInterp_Prompt_V3_8.md>) | Reads an event's own chart (Type A) | sets them aside |
| P17 | [P17_AstroCast_EventToNatal_Interp_Prompt_V1_9.md](<Event_Type_B/P17_AstroCast_EventToNatal_Interp_Prompt_V1_9.md>) | How an event touches a person's chart (Type B) | sets them aside |
| P18 | [P18_AstroCast_MundaneAspectInterp_Prompt_V1_6.md](<Mundane/P18_AstroCast_MundaneAspectInterp_Prompt_V1_6.md>) | A series of aspects between two planets in the world | never carried |
| P19 | [P19_AstroCast_GreatConjunctionInterp_Prompt_V1_5.md](<Mundane/P19_AstroCast_GreatConjunctionInterp_Prompt_V1_5.md>) | Jupiter-Saturn conjunctions: the series or one conjunction's chart | never carried |
| P20 | [P20_AstroCast_IngressInterp_Prompt_V1_5.md](<Mundane/P20_AstroCast_IngressInterp_Prompt_V1_5.md>) | Ingress charts: the cardinal Sun ingresses and outer planets entering a sign | never carried |
| P21 | [P21_AstroCast_LunationInterp_Prompt_V1_5.md](<Mundane/P21_AstroCast_LunationInterp_Prompt_V1_5.md>) | New and Full Moons: the year's series or one lunation's chart | never carried |
| P22 | [P22_AstroCast_Eclipse_Prompt_1_7.md](<Mundane/P22_AstroCast_Eclipse_Prompt_1_7.md>) | An eclipse series, optionally against a natal chart | never carried |
| P23 | [P23_AstroCast_MedicalInterpretation_Prompt_V1_7.md](<Medical/P23_AstroCast_MedicalInterpretation_Prompt_V1_7.md>) | Traditional medical astrology, for education and research | never carried |
| P24 | [P24_AstroCast_MedicalTransitTimeline_Prompt_V1_6.md](<Medical/P24_AstroCast_MedicalTransitTimeline_Prompt_V1_6.md>) | The medical export read against a transit timeline (education and research) | never carried |
| P25 | [P25_AstroCast_DecumbitureInterp_Prompt_V1_7.md](<Medical/P25_AstroCast_DecumbitureInterp_Prompt_V1_7.md>) | A decumbiture: the chart of an illness's onset (education and research) | never carried |
| P26 | [P26_AstroCast_BasicChart_Prompt_3_5.md](<Natal/P26_AstroCast_BasicChart_Prompt_3_5.md>) | A factual chart PDF: wheel and tables, no interpretation | draws and tabulates them |
| P27 | [P27_AstroCast_ChartGraphic_Prompt_1_6.md](<Chart Wheel/P27_AstroCast_ChartGraphic_Prompt_1_6.md>) | A chart wheel graphic for any chart-bearing export | draws them |
| P28 | [P28_AstroCast_DirectionsInterp_Prompt_V1_4.md](<Directions/P28_AstroCast_DirectionsInterp_Prompt_V1_4.md>) | A period's solar arcs, progressions and primary directions, without transits | reads directions to them, never as a convergence |
| P29 | [P29_AstroCast_PrimaryDirectionsInterp_Prompt_V1_0.md](<Directions/P29_AstroCast_PrimaryDirectionsInterp_Prompt_V1_0.md>) | The whole life's primary directions, with rectification notes | never carried |

## Helpers

Five files beside the prompts, from the 2026-09-08 release. Each is optional: every prompt works
without them.

| File | Use |
|---|---|
| [AstroCast_Submission_Helper_v1_0.md](<helpers/AstroCast_Submission_Helper_v1_0.md>) | A fill-in request (subject, focus, context, period, voice, output) to paste after the whole prompt, with the export |
| [AstroCast_Universal_Compliance_Wrapper_v1_1.md](<helpers/AstroCast_Universal_Compliance_Wrapper_v1_1.md>) | A short request that holds the AI to the prompt's own method and output, with a line for your overrides |
| [AstroCast_VoiceDefaults_UserGuide_v1_1.md](<helpers/AstroCast_VoiceDefaults_UserGuide_v1_1.md>) | How the prompts choose a voice (second or third person), and what "unvarnished and honest" changes |
| [AstroCast_MedicalResearch_Submission_Helper_v1_0.md](<helpers/AstroCast_MedicalResearch_Submission_Helper_v1_0.md>) | The education-and-research request for P23, P24 and P25 |
| [SHARED_BEHAVIOR_CONTRACT.md](<SHARED_BEHAVIOR_CONTRACT.md>) | The rules the interpretive prompts share; for maintaining the library, not for a submission |

Each prompt's User Guide and History are in `guides/` and `history/`, named with its ID and version (for
example `P01_AstroCast_EnhancedBirthInterp_Prompt_v3_7_UserGuide.md`); the prompt's own header
links them.
