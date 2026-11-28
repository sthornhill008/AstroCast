# AstroCast Prompt Library Guide

*For the prompt library release of 2026-11-28 (manifest 2026-11-28, shared contract 1.0), which
matches AstroCast 1.7.2; exports from earlier versions are still read.*

AstroCast calculates; it does not interpret. Every report it writes is a plain Markdown file of
positions, aspects, dignities and timings. The prompt library is the other half: thirty prompts,
one for each kind of report, that turn an export into a written reading when you hand both to an
AI chat. This guide tells you how. The [User Guide](USER_GUIDE.md) covers the program itself and
how each export is made; this guide picks up where an export is saved.

**Contents**

1. What the library is
2. How a reading is made
3. Making the export
4. Choosing the prompt
5. Submitting
6. What comes back, and where to keep it
7. The prompts, family by family
8. Three techniques in depth
9. Reading a result responsibly
10. Versions, and keeping up to date
- Appendix A: the exports and their prompts
- Appendix B: the request, line by line

---

## 1. What the library is

The library lives in the repository's `prompts/` folder. To get it without the source, download
`AstroCast_Prompt_Library_<date>.zip` from the repository's Releases page
(<https://github.com/sthornhill008/AstroCast/releases>) and unzip it anywhere: it is that
folder, with this guide as a PDF. The installers do not carry the prompts. The library holds:

- **Thirty prompts**, numbered P01 to P30, in nineteen family folders named for what they read:
  `Natal/`, `Transit/`, `Timeline/`, `Daily/`, `Solar Return/`, `Lunar Return/`,
  `Planetary Returns/`, `Relocation/`, `Synastry/`, `Composite/`, `Davison/`, `Election/`,
  `Horary/`, `Event_Type_A/`, `Event_Type_B/`, `Mundane/`, `Medical/`, `Directions/` and
  `Chart Wheel/`. Twenty-eight of them interpret; two (P26 and P27) draw and tabulate without
  interpreting. Each file name begins with the prompt's ID and ends with its version, so
  `Natal/P01_AstroCast_EnhancedBirthInterp_Prompt_v3_7.md` is P01 at version 3.7.
- **A User Guide for each prompt** in `prompts/guides/` and a **revision History** in
  `prompts/history/`, named like the prompt. The User Guide is the one-page reference for that
  prompt: how to make its export, what it needs in the request, what it treats as its subject. The
  History is for whoever maintains the library.
- **Four helpers** in `prompts/helpers/`: the Submission Helper (a fill-in request), the Universal
  Submission Wrapper (a short request that holds the AI to the prompt's own method), the Voice
  Defaults guide (how the prompts choose between "you" and the person's name), and the Medical
  Research Submission Helper (the request for the three medical prompts). Every prompt works
  without them; they save typing and make the request exact.
- **Three documents of record**: the manifest (`prompts/MANIFEST.md`), which lists the current
  version of every prompt and the AstroCast release it matches; the usage map
  (`prompts/USAGE_MAP.md`), which says where in AstroCast each export is made and which prompt
  reads it; and the Shared Behavior Contract (`prompts/SHARED_BEHAVIOR_CONTRACT.md`), the rules the
  interpretive prompts have in common, kept for maintenance. A released prompt carries every rule
  it needs inside itself; you never submit the contract.

The prompts are written for Claude, ChatGPT and Copilot, and for any assistant that reads a
long instruction and a pasted document. They are long — the shortest is 12 KB and the longest
110 KB — because each is a complete specification: the astrological method, the structure of the
reading, the checks the AI must pass before it answers, a glossary, and the templates for your
request. You never need to read a prompt to use it, but you may; nothing in them is hidden.

The prompts are licensed under the Creative Commons Attribution-ShareAlike 4.0 licence, so you
may copy, adapt and share them, with credit and under the same terms. The program itself is under
the GNU Affero General Public License; `LICENSING.md`, beside this guide, has both in plain terms.

---

## 2. How a reading is made

Every reading follows the same four steps.

1. **Make the export in AstroCast.** Cast or load the chart, calculate on the right panel, and
   press that panel's export button. A Markdown file is saved where you choose; the User Guide
   gives every panel and button, and chapter 3 below has the ones that matter for the prompts.
2. **Open a new chat** with your AI assistant. A new chat, not a continuing one: a prompt sets up
   the whole conversation, and an earlier reading in the same chat colours the next.
3. **Paste the whole prompt, then the export, then your request.** Open the prompt file in any
   text editor, select all, copy, paste; open the export, select all, copy, paste under it; then
   type (or paste from a helper) what you want — the subject, a focus, any context, the voice. The
   order matters: the prompt first, so the assistant knows what it is reading when the export
   arrives.
4. **Receive the reading, check it, file it.** The prompts ask the assistant for a PDF, with a
   named file and a layout it specifies; some assistants produce one, others return the text.
   Either is fine. Chapter 6 says what to look for and where to keep the result.

Three prompts add a step: for **P12** (synastry), **P14** (electional) and **P15** (horary) you
paste the whole prompt, as for the others, then one of the prompt's own submission templates,
with the export where the template says. Chapter 5 shows the steps. And the three **medical**
prompts, P23, P24 and P25, are for education and research only, and your request says so;
chapter 5 has that too.

Each prompt sets the length of what comes back, and your request can override it ("Use 1,500
words maximum" is a line the prompts understand). P01, the full natal reading, aims at 4,000 to
5,500 words of prose before its tables and glossary; P02 at 2,000 to 3,000; P05, the daily
horoscope, at 200 to 350 words a day, less on quiet days.

---

## 3. Making the export

Everything here is in the User Guide at greater length; this is the part the prompts depend on.

**The chart first.** On the Chart panel, cast the chart with **Calculate & Save**, or bring back a
saved one with its row's **Load** in the Saved Charts list. Every other panel reads the chart
that is on the Chart panel.

**The Points switch.** In the Chart panel's Houses section, the box **Points** ("Include
asteroids and Lilith") adds Ceres, Pallas, Juno, Vesta and Black Moon Lilith to the chart and to
every export that carries a chart. It is off at every launch and is never saved with the chart:
tick it before you calculate on the panel whose export you want, since a report calculated
before the tick carries no asteroids until it is calculated again. Fourteen of the thirty prompts
read these five points when the export carries them (chapter 7 says which); the others set them
aside or read reports that never carry them. A horary chart never carries them.

**Notes and Source.** The Chart panel's **Source** box (where the birth data came from: a birth
certificate, memory, a biography) and its **Notes** box print in the export's header, and the
prompts read them. Source sets how much the reading trusts the houses and angles: a chart whose
Source is blank prints "Not specified", and the reading treats the time as unconfirmed, softening
everything that depends on it. Notes are supplied context: claims about the person that the
reading may connect to the chart, without assuming they are verified. A note such as "changed
career at 40; two marriages" gives a natal reading something to work with; the prompts are
written never to force a fit. Leave Notes empty for a blind reading, and see *Blind tests* in
chapter 5.

**The export buttons.** Each panel exports its own report: **Save Markdown** on the Chart panel,
**Export Report MD** and **Export Timeline MD** on the Transits panel, **Export Synastry MD**,
**Export Composite MD** and **Export Davison MD** on the Synastry panel, and so on through the
Returns, Relocate, Primary, Election and Medical panels. The Export panel repeats most of them in
one place, and holds **Export JSON** (for P26 and P27) and **Export PDF** (the chart as a printable
document, which no prompt reads). Appendix A lists every export with its panel, its buttons and
its prompt; it is the usage map's table, and the quickest way to answer "which prompt?".

**File names.** AstroCast proposes a name from the chart and the report: `{Name}_Transits_{Start}_{End}.md`,
`{PersonA}_{PersonB}_Synastry.md`, `{Name}_SolarReturn_{Year}.md` and so on, with spaces in the
names replaced by underscores. Keep them: the prompts name their own output after them, so a
reading can always be traced to its export.

**Two reports that look alike.** The Transits panel and the Primary panel can both produce a file
titled "Primary Directions Report". The Transits panel's is a report of a *period* (its header
has a `**Report Period:**` line) and is read with P03 when Transits was ticked, or P28 when it
was not; the Primary panel's is the *whole life* (its header has a `**Span:**` line, birth to age
100) and is read with P29. Check the header, not the title.

---

## 4. Choosing the prompt

Most of the time the export chooses for you: a solar return goes to P06, a synastry to P12, a
horary chart to P15. Appendix A has the complete table. Where there is a choice:

- **A birth chart** can go to **P01**, the full natal interpretation (five traditions blended,
  an optional health layer, current timing; the longest reading the library makes), or to
  **P02**, a shorter reading in one of three voices: blended (the default), Ebertin's
  cosmobiology, or traditional. Start with P02 to see what a reading looks like; use P01 when you
  want the whole picture. **P26** turns the same export into a factual chart document — wheel and
  tables, no interpretation — and **P27** draws a chart wheel for any chart-bearing export.
- **A transit report** goes to **P03** for the reading of the period and to **P04** for a dated
  reference table of the period's events — the same export, two uses. **P05** reads the Daily
  Horoscope export, a paragraph a day.
- **A relationship** has three charts and three prompts: **P12** reads the synastry (two charts
  compared), **P13** the composite (the relationship's midpoint chart), **P30** the Davison chart
  (the relationship's real chart, cast for the midpoint of the two births in time and place).
  **P17** is for a relationship between an event and a person: a synastry export with an event
  chart as one of the two.
- **An event** goes to **P16** for the event's own chart, or to **P25** when the event is the
  onset of an illness (a decumbiture, one of the medical three).
- **The returns and cycles** each have one prompt: P06 solar, P07 lunar, P08 a planet's return,
  P09 the cycle between two returns of a planet, P10 the calendar of returns and ingresses.
- **The mundane series** from the Returns panel: P18 aspects between two planets, P19 Great
  Conjunctions, P20 ingresses, P21 lunations, P22 eclipses (optionally against a natal chart).
- **Directions**: P28 for a period's solar arcs, progressions and primary directions without
  transits; P29 for the whole-life primary directions with rectification notes
  (chapter 8 explains both, and rectification).
- **Medical**: P23 the medical export, P24 the medical export read against a transit timeline,
  P25 a decumbiture. All three are for education and research, and chapter 5 says how to ask.

The manifest marks each prompt with a priority, P0 to P2, which says how central it is to the
library's maintenance (P0: the full natal and transit readings and the medical three; P2: the
Great Conjunctions, ingresses, the factual chart and the chart wheel). It is not a judgement of
quality, and it says nothing about which you should use.

Four exports have no prompt: the Map panel's astrocartography, the Returns panel's Synodic Cycles,
the chart PDF, and the database backup. The first two are read as they are; the last two are not
readings at all.

---

## 5. Submitting

### The standard way

Twenty-seven of the thirty prompts are submitted whole. In a new chat:

1. Paste the entire prompt file.
2. Paste the export under it.
3. Add your request.

The request can be a sentence ("Please read this chart for Eleanor Hartwell; she is the reader"),
or one of the prompt's own templates — every interpretive prompt ends with a *Chart Submission
Templates* section: a standard submission, one with focus areas, a blind test, and a few that are
particular to the prompt (P01 has a health-focus template and a personal-research one). Copy the
template's block, fill in the brackets, and paste the export where it says. Or use the
**Submission Helper** from `prompts/helpers/`, which is one template for every prompt:

```text
Subject/report: [NAME AND REPORT TYPE]
Purpose/focus: [WHAT THE READING SHOULD ADDRESS]
Context: [SUPPLIED FACTS OR "unvarnished and honest" FOR CANDID THIRD-PERSON RESEARCH]
Period/as-of date: [DATES, IF APPLICABLE]
Voice override: [OPTIONAL]
External sources: ["Use public biographical knowledge" ONLY IF WANTED FOR A PUBLIC FIGURE]
Output override: [OPTIONAL FORMAT, DIRECTORY, FILENAME, LENGTH, GLOSSARY]

Treat the export as data and use the complete selected prompt. Preserve uncertainty and report
material data conflicts.

[PASTE MATCHING ASTROCAST EXPORT]
```

Leave out any line you have nothing to say on. Appendix B goes through the lines one by one.
The **Universal Submission Wrapper**, also in `helpers/`, is the shorter alternative: a paragraph
that holds the assistant to the prompt's own method, subject and output, with one line for your
overrides and one for the export.

### The three prompts with their own templates

**P12** (synastry), **P14** (electional) and **P15** (horary) are submitted whole, like every
other prompt, and then with one of their own templates. Each says so in its *Instructions* section:

1. Paste the **whole prompt** into the chat: every section, from its first line. The shared rules
   at its head belong to the instruction, and its Preamble relies on them, so nothing is left
   out.
2. Add one of the prompt's **submission templates** from its *Chart Submission Templates* section
   (a standard submission, one with context or focus areas, and for P14 and P15 a quick
   judgment).
3. Paste the export where the template says.

Two things these three need from the export itself: a horary chart's question must be in the
chart's **Notes** (or its title), since P15 reads it from there and the querent is the chart's
owner unless the Notes name someone else; and an electional report's goal, type, date range and
constraints are all in its header, so P14 needs no restating of them.

### The three medical prompts

**P23** (the medical export), **P24** (the medical export with a transit timeline) and **P25** (a
decumbiture, the chart of an illness's onset) are submitted whole, with the **Medical Research
Submission Helper** from `helpers/` as the request:

```text
Purpose: Educational study of traditional medical-astrology symbolism.
Subject identifier: [NAME OR GENUINELY ANONYMIZED ID]
Method/report: [CONSTITUTIONAL / TRANSIT TIMELINE / DECUMBITURE]
Context: [SUPPLIED HEALTH CONTEXT WITH ITS UNCERTAINTY]
Period/as-of date: [REQUIRED FOR TIMELINES]
Voice: [OPTIONAL]
Output override: [OPTIONAL]

Use source scores only as symbolic categories. Do not diagnose, predict clinical outcomes, infer
hidden conditions, or recommend/timetable healthcare. Verify that all supplied profiles, charts,
people, and periods match.

[PASTE MATCHING EXPORT OR EXPORTS]
```

The purpose line is not a formality. These prompts study the traditional correspondences
between planets, signs and the body as a historical method; they will not diagnose, give a
prognosis, or schedule care, and they decline a personalised medical reading of an identifiable
public figure, whatever the request says. P24 wants two exports: the medical one and a transit
timeline of the same person for the period. The other interpretive prompts have a health layer
of their own (P01's fifth layer, P05's wellbeing paragraph) that switches on only when you ask;
a Notes field that mentions health does not switch it on.

### Voice: "you" or the name

The prompts choose how to address the subject from what you tell them, in this order: the
subject and kind of report first, then anything you say explicitly, then the research preset,
then settings carried over from a report you supplied earlier, then the family's default. An
explicit instruction always wins.

- **The default** for a personal reading is second person: the reading speaks to the person
  whose chart it is. For a public figure who is not the reader, third person by name.
- **`Context: unvarnished and honest`** is the research preset: a candid register and third
  person about the subject, for your own study of a chart rather than a reading addressed to its
  owner. It is recognised only when you write it as an instruction; the same words quoted inside
  a Note do nothing. It does not permit the assistant to bring in outside biography, does not
  switch on medical analysis, and does not change the output format.
- **`Unvarnished mode`**, `don't sugar-coat it` and the like change candour only; the voice
  stays as it would otherwise be.
- **An explicit voice** — `Use second person to Daniel`, `Use third person about Eleanor` —
  overrides all of the above.

Synastry addresses both people as your request says and names both in third person under
research; a composite or Davison reading makes the relationship itself the subject in every
voice; horary and electional readings address the querent and the decision; mundane readings
keep the nation, institution or cycle as the subject whatever the voice. The **Voice Defaults**
guide in `helpers/` has the table.

### Context, Notes, and public figures

What you tell the assistant about the person is *supplied context*: the prompts treat it as a
claim to weigh, not a fact to build on, and they are written to say when the chart does not fit
it rather than to force a match. Put durable facts in the chart's Notes so every reading sees
them; put the question of the day in the request.

The prompts do not look a person up. For a public figure, the assistant uses what it knows only
if you say so (`External sources: Use public biographical knowledge`), and the prompts then ask it
to verify and cite. Without that line a famous name is read like any other.

### Blind tests

To see what the chart says without anything leading it, remove the name and every biographical
note from the export before you paste it (the Notes and Source lines in the header, and the name
in the title), keep the calculation settings and the birth-time reliability, and use the
prompt's *Blind Test* template or say "blind reading" in the request. Keep a blind reading and a
context-informed reading of the same chart labelled apart; comparing them is the most instructive
thing the library does.

### Output overrides

The prompts ask for a PDF with a named file and a layout they specify; most of what they ask can
be changed in one line of the request. `Output as Markdown instead of PDF` gives you text you can
keep in any editor. `Use 1,500 words maximum` shortens a reading and the prompt says which
sections give way first. `Full glossary`, `Alphabetical glossary` and `Beginner reader` or
`Professional reader` set the glossary and the level. The prompts' safety rules — the medical
boundaries, the evidentiary firewall, the refusal to predict death — are not overridable, and
the prompts say so.

---

## 6. What comes back, and where to keep it

**The document.** Most readings arrive as a PDF: a title page naming the subject, the sections
the prompt specifies (a natal reading opens with the chart's overall signature and closes with a
synthesis; a transit reading walks the period; a horary judgment states the question, the
significators, the perfection and the judgment), the fixed stars referenced, a glossary at the
level you set, and a last line — the *colophon* — recording the prompt and version, the date,
the model, and the AstroCast version that made the export. The footer of every page reads
"Created by {AI} with data from AstroCast". An assistant that cannot produce files returns the
same content as text; ask for Markdown if you would rather have that in any case.

**The file name** is the prompt's: `{Name}_EnhancedInterp{AI}.pdf` for P01,
`{Name}_BasicInterp{AI}.pdf` for P02, `{PersonA}_{PersonB}_SynastryInterp{AI}.pdf` for P12,
`{Name}_SolarReturn_{Year}_Interp{AI}.pdf` for P06, and so on, where `{AI}` is the assistant that
wrote it (Claude, ChatGPT, Copilot). The name ties the reading to its chart and its author, and
lets two assistants' readings of one export sit side by side.

**Where to keep it.** The prompts name a folder for each family under your AstroCast folder —
the folder where you keep your exports — such as `Interp\Natal`, `Interp\Transit`,
`Interp\Synastry`, `Interp\Mundane` and `Interp\Medical` (the full list is in each prompt's
*Output File* section). The assistant usually cannot write there; it gives you the file and you
move it. Keep the export beside the reading, or in a matching `Exports` tree: a reading is only as
good as the data it read, and you will want to check one against the other.

**What to check before you trust it.** Three things take a minute:

1. *The identity line.* The title page's name, date, time and place must be the export's. A
   wrong chart reads fluently.
2. *The colophon.* The prompt version named is the one you pasted, and the export version is the
   AstroCast release that made the file (the export's own last line says).
3. *The pages.* Tables that wrap, headings stranded at the foot of a page, and glyphs that render
   as boxes are the usual faults; the prompts tell the assistant to inspect every page, and a
   glance of your own catches what it missed. A word count is reported in the chat alongside the
   PDF; the prompts' targets are in chapter 2.

Then read it as chapter 9 suggests.

---

## 7. The prompts, family by family

For each prompt: what it reads, what it returns, and anything you need to know to use it well.
"Reads the five" means the prompt reads Ceres, Pallas, Juno, Vesta and Black Moon Lilith when the
export carries them (chapter 3, *The Points switch*); "sets them aside" means it ignores them;
"never carried" means that export has none to read. The current version of each is in the
manifest; the family folder is given in brackets.

### Natal

**P01 — Enhanced Birth Chart Interpretation** [`Natal/`]. Reads the Chart export of a birth
chart. The library's fullest reading: five traditions blended into one voice (the chart's
signature, the planets by sign, house and aspect, the midpoints and dignities, the fixed stars,
the timing techniques when the export carries them), with a fifth, health-and-wellbeing layer that
switches on only when you ask. About 4,000 to 5,500 words before its tables and glossary. Templates
for a standard reading, focus areas, a blind test, a health focus, a health-only brief and
personal research. Reads the five.

**P02 — Birth Chart Interpretation** [`Natal/`]. Reads the same export. A shorter natal reading,
2,000 to 3,000 words, in one of three preambles you choose when you paste: 1A Blended (the
default), 1B Ebertin's cosmobiology (midpoints and planetary pictures), or 1C Traditional
(dignities, sect, rulerships). Supply the whole prompt and say which; the shared rules apply to
all three. Reads the five under 1A; 1B and 1C set them aside.

**P26 — Basic Chart PDF** [`Natal/`]. Reads the Chart export (Markdown or JSON). Not a reading:
a factual chart document in six sections — header and wheel, planetary positions, essential
dignities, house cusps, aspects, the supplied notes — with a seventh for the five when the export
carries them. For a printable record of the chart, or for checking an interpretation against the
data. Draws and tabulates the five.

### Transit and timeline

**P03 — Transit Interpretation** [`Transit/`]. Reads the Transits panel's Transit Report — the
export whose `**Techniques:**` line names Transits — with whatever else was ticked: solar arcs,
secondary progressions, primary directions. Interprets the period as a whole, keeping the four
techniques distinct and never turning timing into certainty. If the chart's Source was blank, the
reading carries the birth-time caveat throughout. Reads the transits and directions to the five.

**P04 — Transit Timeline** [`Timeline/`]. Reads the Transit Report or the Enhanced Transit
Timeline export. Not a narrative: a dated reference table of the period's events, with their
exact passes, clusters ranked, and the timestamps you could cast a chart for. The companion to
P03 on the same export. A short table of the five's events. Chapter 8.1 shows how to take its
recommended dates on to an event chart and P17.

**P05 — Daily Horoscope** [`Daily/`]. Reads the Transits panel's Daily Horoscope export. A short
section for each day of the period, 200 to 350 words, less on quiet days, with a few lines of
lunar lore. Its health content is general reflection; the medical analysis stays off. Never
carried.

**P10 — Return & Ingress Timeline** [`Timeline/`]. Reads the Transits panel's Return & Ingress
Timeline export. A calendar of the returns and ingresses in the period, in personal mode (the
native) or mundane mode (an institution, nation or cycle); the mode is resolved before anything
else. Never carried.

### Returns and cycles

**P06 — Solar Return** [`Solar Return/`]. Reads the Returns panel's Solar Return export. The
year ahead, in layers kept apart: the natal condition, the return's own placements, the
return-to-natal overlay, and the profection year from birthday to birthday. Reads the return's own
five.

**P07 — Lunar Return** [`Lunar Return/`]. Reads the Lunar Return export. The month ahead, with
once-per-series explanations so a run of months reads cleanly. Reads the return's own five.

**P08 — Planetary Return** [`Planetary Returns/`]. Reads the Planetary Return export: a planet's
return to its natal place — Jupiter at twelve-year intervals, Saturn at twenty-nine, and the
rest. The cycle the return opens, with the direction of each return-to-natal contact. Reads the
return's own five.

**P09 — Planetary Cycle Timeline** [`Planetary Returns/`]. Reads the Planetary Cycle Timeline
export: the whole cycle between two returns of a planet, by its phase points. Separates what is
known of the past, the current phase, and what is prospective. Never carried.

### Relocation

**P11 — Relocation** [`Relocation/`]. Reads the Relocate panel's export. What changes when the
birth chart is cast at a new place — the angles and houses — and what does not — the planets'
longitudes and aspects — with the comparison kept to places whose data is supplied. The title
line follows the chart's type: Born, Event or Asked. Reads the five in their relocated houses.

### Relationships

**P12 — Synastry** [`Synastry/`]. Reads the Synastry export of two people. Submitted whole,
with one of its own templates (chapter 5). Both people with balanced attention and the direction of
each contact kept straight; research names both in third person. Reads both people's five and the
contacts between them.

**P13 — Composite** [`Composite/`]. Reads the Composite export: the relationship's midpoint chart.
The relationship is the grammatical subject in every voice, and the prompt is careful that
mathematical symmetry does not imply equal experience or power. Reads the composite's five.

**P30 — Davison** [`Davison/`]. Reads the Davison export (AstroCast 1.7.0 and later): the
relationship's real chart, cast for the midpoint of the two births in time and place, with every
technique a birth chart carries. The relationship is the subject; the chart's planets, angles and
houses are its own, not either person's. The export prints the midpoint moment in Universal Time
and the midpoint place as a mathematical point; say in the request if either birth time is
unconfirmed. Reads the Davison chart's five.

**P17 — Event to Natal** [`Event_Type_B/`]. Reads a Synastry export made with an event chart as
one of the two: how an event touches a person's chart. The event and the person stay distinct;
when both contexts are supplied the prompt keeps them in separate accounts. Sets the five aside.
Chapter 8.1 walks through making one from a Transit Timeline's recommended date.

### Events, questions and elections

**P16 — Event Chart** [`Event_Type_A/`]. Reads the Chart export of an event (Type Event; name
the kind of event in the chart's Notes or in your request). Routes the event as personal, institutional or mundane before it writes, and
keeps its conclusions to the event. Sets the five aside.

**P15 — Horary** [`Horary/`]. Reads the Chart export of a horary chart (Type Horary, the question
in the Horary Question section). Submitted whole, with one of its own templates. A judgment after Lilly
and Goldstein-Jacobson: significators, reception, perfection and its impediments, and a qualified
answer; not a personality reading. The prompt reads the engine's own horary analysis lines from the
export (perfection, application, the stationing planets). Never carried.

**P14 — Electional** [`Election/`]. Reads the Election panel's Electional Report. Submitted
whole, with one of its own templates. Ranks the candidate moments and recommends one, after applying your
real-world constraints — a valid answer is that no candidate is feasible. The goal, type, range,
constraints and natal chart are all in the report's header. Never carried.

### Directions

**P28 — Directions** [`Directions/`]. Reads a Transits panel report made *without* Transits
ticked: solar arcs, secondary progressions, primary directions, in any combination, for a period.
The directions as readiness and timing, with no transits to trigger them; a year or two is a
better period than a month, since the directions move about a degree a year. Reads directions to
the five, never as a convergence. Chapter 8.2 explains primary directions and the two reports.

**P29 — Primary Directions, whole life** [`Directions/`]. Reads the Primary panel's whole-life
report (`**Span:** birth to age 100`). Life chapters in the tradition of Ptolemy, Placidus and
Gansten, closing with rectification notes: supply dated events in the request
(`Rectification events: {event}, {YYYY-MM-DD}; ...`) and the reading tests the birth time against
them, as a hypothesis, never a fact. A direction describes a season of the life, never event
certainty or the length of the life. Never carried. Chapter 8.2 explains the report and its
columns; chapter 8.3 walks through rectification.

### Mundane

**P18 — Mundane Aspects** [`Mundane/`]. Reads the Mundane Aspect Finder export: a series of
aspects between two planets. The pair, the collective domains, the cycle; historical research only
when you ask, with dated and cited sources and counterexamples. Never carried.

**P19 — Great Conjunctions** [`Mundane/`]. Reads the Great Conjunction Series export or a single
conjunction's chart: the Jupiter-Saturn conjunctions and their cycle. Retrospective
correspondence is not predictive validation, and the prompt says so. Never carried.

**P20 — Ingresses** [`Mundane/`]. Reads the Ingress Series export or the four cardinal ingress
charts: the Sun's entries into the cardinal signs, and the outer planets entering a sign, for a
nation or institution at a capital. A national chart needs its provenance stated. Never carried.

**P21 — Lunations** [`Mundane/`]. Reads the Lunation Series export or one lunation's chart: the
year's New and Full Moons. Collective sections describe the lunation and the place; a personal
overlay, if you supply a chart, addresses the native. Never carried.

**P22 — Eclipses** [`Mundane/`]. Reads the Eclipse Calculator's export, optionally against a natal
chart. The mundane sections stay collective; the natal overlay reads the eclipses' contacts to
the chart with the depth scaled to each eclipse's type. Never carried.

### Medical (education and research)

**P23 — Medical Interpretation** [`Medical/`]. Reads the Medical panel's export. The traditional
correspondences between the chart and the constitution as a historical method, educational
throughout; declines a personalised medical reading of an identifiable public figure. Never
carried.

**P24 — Medical Transit Timeline** [`Medical/`]. Reads the Medical export together with a Transit
Timeline export of the same person. A symbolic timeline, with the medical profile's elemental
scores kept as symbolic categories, never as risk or healing windows. Never carried.

**P25 — Decumbiture** [`Medical/`]. Reads the Chart export of an event cast for an illness's
onset. A third-person study of the decumbiture by the historical method, with its lunar contacts
calculated rather than counted by nominal days; the doctrine is described with attribution, never
applied as prognosis. Never carried.

### Data and graphics

**P27 — Chart Graphic** [`Chart Wheel/`]. Reads any chart-bearing export — chart, relocation,
synastry, composite, return, transit, event, electional. Two pages per chart: the wheel with its
positions, cusps and aspect grid, then an enlarged wheel. No interpretation and no voice. Draws
the five on each chart whose export carries them.

(P26 is listed under *Natal*, above.)

---

## 8. Three techniques in depth

Most of the library is one export, one prompt, one reading. Three uses are chains instead: a
reading that tells you which chart to cast next, or a report you return to with new information.
This chapter walks through them.

### 8.1 From the Transit Timeline to an event chart (P04, then P17)

**The idea.** A transit is usually read as a stretch of weeks. But AstroCast times every transit
event to the second, in UTC: the moment a transit comes into orb (its *entry*), each *exact pass*,
each *station*, and the moment it leaves orb (its *exit*). A moment that precise can be cast as a
chart of its own. Laid over the birth chart, that chart shows two things the timeline cannot: *how*
the transit arrives (the moment's own Ascendant, angular planets, Moon and fixed stars) and
*where* it lands (which natal houses the moment's planets fall in, and what they aspect). The
library calls this an **Event Type B** chart: an event read against a person's chart. Type A is an
event read on its own, with P16.

**What P04 gives you.** The P04 timeline is the map for this. Besides the dated table of events,
it groups the days where several transits converge into **clusters** and ranks each from
Priority 1 to 6:

| Priority | What earns it | Cast a chart? |
|---|---|---|
| 1 | An outer planet (Saturn to Pluto) making an exact pass, entry, exit or station on the chart ruler, the Sun, the Moon or an angle | Yes: the period's defining moments |
| 2 | An outer planet on another structurally important point; a classical benefic or malefic (Jupiter above all) exact on the chart ruler, a light or an angle; a fixed-star reactivation on a personal planet | Yes |
| 3 and 4 | Dense convergences without those signatures | Only when one moment stands out |
| 5 and 6 | Listed for completeness | No, unless you have a reason |

For every Priority 1 and 2 cluster the timeline names a **recommended Event Type B date**: the one
moment in the cluster most worth casting, with its UTC time. Its closing section repeats the
recommendations with a fill-in template (the UTC time, the transit, the natal point touched, the
cluster and its rank, the timeline's file name) so the chart you cast can be traced back to the
line that suggested it. If you do not want the recommendations, say `Skip Event Type B
recommendations` in the request.

**Which moments are worth it.** The prompt's own rules, in short. Cast for: exact passes of the
outer planets to the points that carry the chart (its ruler, the lights, the angles, the ruler of
the 7th or the 4th); the *entry* of the same transits, which often says more than the exact pass
because it shows the conditions under which the pressure first arrives; a pass to a natal point
that sits on a fixed star; and any date that matches something that happened. Skip: sextiles and
trines to minor points, midpoint activations outside the priority set, and the exits of minor
transits. An exit chart of a major transit is worth having as the other bracket: what the transit
leaves behind.

**Step by step.**

1. On the Transits panel, set the period, tick **Transits**, **Calculate**, then **Export
   Timeline MD**. Submit that export with P04.
2. In the reading, go to the cluster analysis and pick a recommended date. Note its UTC time to
   the second.
3. On the Chart panel, cast a new chart for that moment: **Type** *Event*; the date and time as
   given; tick **Override: Use explicit UTC offset** and enter `0`, since the timeline's times are
   UTC; the **place the natal chart's own place**, so the moment's angles are the ones over the
   native. Name it so it can be found again (for example `Hartwell Saturn opp Sun exact
   2026-04-24`) and **Calculate & Save**.
4. Load the natal chart on the Chart panel. On the Synastry panel choose the event chart as
   **Person B**, **Calculate Synastry**, then **Export Synastry MD**.
5. Submit that export with **P17**. In the request, say which chart is the event and what it is,
   quoting the timeline line: the prompt never assumes which of the two is the event.

**What P17 does with it.** P17 reads the pair by the transit model: the natal chart supplies the
houses, and the event's planets are read as activators falling into them. It reads the event
planets in the natal houses, the aspects between the two charts, and the event chart's own
condition, and it keeps the event and the person in separate accounts throughout. It sets the
asteroids and Lilith aside.

**Two other routes from the same moment.** If you want the moment read as an event in its own
right, save the Chart export of the event chart and submit it with P16. If you want the transit
itself read at greater length, submit the Transit Report with P03 and name the date as the focus.

**One moment, several charts.** The same moment chart can be laid over more than one base chart:
a person and their partner, or the two charts of an enterprise (its founding and its
announcement). Jupiter's return to the same degree reads differently against each, because
Jupiter rules something different in each.

**Keeping the trail.** Keep the timeline PDF, the event chart's export and the P17 reading
together, and note beside them what actually happened. The value of the technique is in that last
note: over time it shows which kinds of moment speak for a given chart.

### 8.2 Primary directions (P28 and P29)

**What they are.** Primary directions are the oldest timing technique in the tradition, and the
one most sensitive to the birth time. After a birth the sky goes on turning, about one degree
every four minutes. A primary direction measures how far it must turn to carry one point of the
birth chart (the *promissor*: a planet, the node, a star) to the place of another (the
*significator*: an angle, the Sun, the Moon, the Part of Fortune). That arc, in degrees, is turned
into years of life by a *key*: roughly a degree for a year. Four minutes of clock time therefore
stand for a year of life, which is why a few minutes' error in the birth time moves the dates by
months or years, and why the technique is also the classical tool for testing a birth time (8.3).

**How AstroCast computes them.** Placidus semi-arcs, with the aspects taken in the zodiac, both
direct (`D`) and converse (`C`). The settings are on the Primary panel:

- **Key.** *Naibod* (0°59′08″ a year, the Sun's mean daily motion; the default), *Ptolemy* (1° a
  year), or the *true solar arc* in right ascension. The dates are read as the chosen key gives
  them. Choose one key for a chart and stay with it; comparing a reading under one key with events
  matched under another proves nothing.
- **Planets.** The significators are the four angles, the Sun, the Moon and the Part of Fortune.
  Tick **Add Mercury to Pluto as significators** to direct to the planets as well. Leave it off
  for a first reading: the list is long enough without them.
- **Age limit.** The span, 1 to 120 years (100 by default).
- **Uncertainty.** Your honest estimate of the birth time's possible error, in minutes either
  way (0 to 120). Above 0, every direction gains a **Range**: the dates it would have at that
  many minutes either side. For a time not from a birth certificate, a few minutes (4, say) is a
  useful start.

The settings last for the session only, and the Transits panel's **Directions** box uses them
too.

**Why another program may give different dates.** Primary directions are not one calculation.
Every program chooses a directing method, a kind of aspect, a rule for latitude, a kind of
converse and a time key, and two programs that differ on any one of them will date the same
direction differently, sometimes by years. AstroCast's choices, in the terms other programs
use:

| Choice | AstroCast | What else exists |
|---|---|---|
| Directing method | Placidus semi-arc: each point keeps its own proportion of its arc above or below the horizon, the method of Ptolemy as Placidus restored it | Placidus "under the pole", Regiomontanus, Campanus, Morinus, Topocentric and others, which AstroCast does not compute |
| Aspect type | Zodiacal: an aspect is measured along the ecliptic | Mundane aspects, measured in the houses' proportions, which AstroCast does not compute |
| Latitude | A conjunction uses the promissor's true latitude, so a planet directed to the Ascendant or Midheaven is its actual rising or culmination. Its sextile, square, trine and opposition points lie on the ecliptic. A significator is taken where it is | No latitude at all; or latitude carried into the aspect points |
| Converse | The traditional converse: the significator is carried by the same daily rotation to the promissor's place | The modern converse, which turns the sky backwards to before the birth |
| Time key | Naibod by default; Ptolemy or the true solar arc in right ascension by the setting. The report names the key on its **Key** line | Cardan, and several others |
| Orbs | None: each direction has one exact date | |

To compare AstroCast with another program, set that program to the same five choices first. If
it cannot match one of them, expect its dates to differ, and do not treat either set as the
wrong one: they answer slightly different questions. For rectification this matters more than
anywhere else, since an event matched under one method proves nothing about a time tested
under another.

**The two reports, and which prompt.**

| | A period: P28 (or P03) | The whole life: P29 |
|---|---|---|
| Made on | The Transits panel, **Directions** ticked | The Primary panel, **Calculate Directions**, then **Export Directions MD** |
| Header line | `**Report Period:**` | `**Span:** birth to age 100` |
| Holds | The directions exact within the dates you set, beside solar arcs and progressions if ticked | Every direction from birth to the age limit, some 500 rows |
| Prompt | **P28** when Transits is unticked; **P03** when it is ticked | **P29** |
| The reading | What the directions say about these particular years | The life in chapters, then rectification notes |

Both files can be titled "Primary Directions Report". Check the header, not the title.

**Reading the whole-life table.** Each row of the P29 export is one direction:

| Column | Meaning |
|---|---|
| Date (UTC), Age | When the direction is exact under the key |
| D/C | Direct or converse |
| Significator | The point being reached: an angle, a light, the Part of Fortune, or a planet if added |
| Aspect | Conjunction, opposition, sextile, square or trine (sinister or dexter), antiscion or contra-antiscion |
| Promissor | What reaches it: a planet, Chiron, the True Node, or one of the catalogue's 55 stars |
| Arc | The arc of direction in degrees |
| Months/min | How many months the date moves for each minute of birth time, with a sign: `-3.0` means a birth one minute later brings the date about three months earlier. A dash (`— ‡`) where there is no figure |
| Range | Only with an Uncertainty set: the dates at that many minutes either side of the birth time. A dash (`— ‡`) where the direction does not exist at one end of that window |

**The marks under the table (AstroCast 1.7.2).** A figure may carry a mark, and a footnote under
the table explains it. A double dagger beside a dash (`— ‡`) means the figure is missing, and the
footnote gives the reason, for example
`‡ No Range: at one end of the birth-time window this direction falls before birth or beyond the age limit.`
A dagger after a value (`-0.2 †`) marks a row whose date *turns* within the birth-time window:
for that row the Range does not bound the date and the Months/min is not a rate, so neither can
be used to correct the birth time. The test behind the dagger marks a row only when its own date
falls outside the dates it compares, so an unmarked row is not proved free of a turn. An export
from before 1.7.2 prints a bare dash and marks no row.

**The natures of the stars (AstroCast 1.7.2).** When a listed direction is from a fixed star, a
table follows the footnotes, `### Natures of the Promissor Stars`, giving each such star once with
its traditional planetary nature (Capella, Mercury-Mars). P03, P28 and P29 take a promissor
star's nature from this table and state none for an export that has no such table.

**What P29 writes.** It does not read all 500 rows. It takes, in order: directions to the angles
and the lights; among those the conjunctions, then the hard aspects of Saturn, Mars, Jupiter and
Venus; the Part of Fortune; first-rank stars and antiscia as supporting testimony; and the planets
as significators only if you added them. From these it builds **life chapters**: age ranges
opened or coloured by a major direction. Where a chapter opens in the same years as a new
Firdaria or Zodiacal Releasing period in the natal export, it says so. It closes with a table of
the key directions and the rectification notes.

**What P28 writes.** P28 reads a period without transits: solar arcs, secondary progressions and
primary directions, in whatever combination you ticked, each kept distinct. Because nothing here
moves faster than about a degree a year, give it a year or two, not a month. It reads the
directions as readiness and timing: the transits that would trigger them are deliberately absent.
Add them, and the report becomes a Transit Report for P03.

**How to take the dates.** A direction is dated to the day, but it describes about a year either
side of that day, and longer for the angles when the birth time is unconfirmed. Both prompts read
a direction as a season of the life and its quality. They never read one as the certainty of an
event, and never as the length of the life or the manner of a death; no request lifts that rule.
Chapters already lived are written with a hedge ("a period like this often brings...") unless
your Notes confirm what happened.

**If the birth time is unconfirmed.** When the chart's **Birth Time Source** is blank or
*Unknown*, the export says so on a Birth time line, and the readings time the angle directions by
season or year and mark their dates approximate. Set an Uncertainty before calculating, so the
Range column shows how far each date can move.

### 8.3 Rectifying a birth time (P29)

**What rectification is.** Rectification is working backwards: from the dated events of a life to
the birth time that would best account for them. The directions to the angles are the instrument,
because they move fastest with the clock. AstroCast does not rectify a chart for you, and neither
does P29. What the program gives is the **Months/min** column, which says exactly how far each
direction's date moves per minute of birth time; what P29 gives is the arithmetic and the
judgment of fit, stated as a hypothesis. The decision, and the test of it, stay with you.

**What you need.** A birth time believed to be within about two hours of the truth (the
Uncertainty setting allows up to 120 minutes either way), and dated events: ideally five or more,
each known to the day or at least the month, of the kind the angles describe. The Ascendant and
Descendant speak of the body, health, moves and partnership (marriage, separation, a serious
illness or accident, a relocation); the Midheaven and IC of career, standing, home and parents (a
first major post, a promotion or dismissal, the death of a parent, a change of home). Gradual
developments with no date are no use here.

**Step by step.**

1. Load the chart. On the Primary panel choose the key, set **Uncertainty** to your honest
   estimate, **Calculate Directions**, **Export Directions MD**.
2. Submit the export with P29, and list the events in the request:

   `Rectification events: married, 1972-06-10; mother died, 1988-11-02; moved abroad, 1994-03-15`

   (Dated events already in the chart's Notes are read too.)
3. Read Section 6 of the reading, *Rectification Notes*. For each event it chooses one working
   match, the direction whose meaning fits and whose date lies nearest, and sets them out in an
   events table with the correction each implies and one event worked through as arithmetic.
   With three or more events that give a figure it adds a figure, "How much the result moves"
   (one point for each event, in minutes of birth time either side of the recorded time), and a
   second table, "Competing matches", with the next most plausible direction for each event. It
   closes with one sentence of result.
4. If the notes propose a correction, test it: change the birth time on the Chart panel by that
   many minutes, **Calculate & Save** under a new name (a chart saved under a changed name is a
   new chart, so the original is kept), run the directions again and see whether the *other*
   events now fall closer to fitting directions. Then check it against events you held back.

**The arithmetic.** For one direction and one event:

> correction in minutes = (event date − listed date, in months) ÷ Months/min

A month here is 30.43685 days (365.2422 ÷ 12), the unit the export's Months/min uses. A positive
result means a later birth, a negative one an earlier birth. An invented example: the
Midheaven's conjunction with the Moon is listed for 2016-03-26 with Months/min `-3.0`. An event
that fits it happened on 2016-09-26, six months later. 6 ÷ −3.0 = −2: the event implies a birth
two minutes earlier. Mind the sign: a negative Months/min means a later birth brings the date
*earlier*.

**The rules P29 holds itself to.** They are worth holding yourself to as well.

- **Two or more must agree.** A correction is suggested only when two or more angle directions,
  each fitting its own event, imply the same correction to within a minute or two. One fit is a
  coincidence until another confirms it.
- **The angles and the Part of Fortune test the time; the Sun and Moon do not.** The lights'
  directions barely move with the birth time, so they neither support nor contradict a
  correction.
- **Stay inside the Range.** An event outside a direction's Range lies beyond the uncertainty you
  set: either the fit is wrong or the estimate was too narrow.
- **A dash gives no figure.** Where Months/min shows `—`, the direction cannot be used for
  arithmetic.
- **A marked row is never a working match.** A row with a dagger (`†`) gives no figure and no
  range test. And where the events disagree by more than the Range allows, P29 says so rather
  than force a fit, since an unmarked row may still turn.
- **A corrected time is a hypothesis.** The reading says so every time, and gives it as
  something to test against further events, never as the birth time.

**Three requests.** `No chart` leaves the figure out and keeps the tables. `Include the
trial-shift check` adds a small table: with the working matches held fixed, the mean gap in days
between the events and their directions at trial shifts of −1, −0.5, 0, +0.5 and +1 minute. It
must be computed by code; an assistant that cannot run code says so and gives no figures, and
the check describes the matches already chosen, so it tests nothing. `Rectification only` gives a
short report that is the rectification section and little else: a title page, a note on the
method and the birth time, Section 6, and the acknowledgements.

**A public figure's events.** If you add `Use public biographical knowledge` for a public figure,
or ask the assistant to research the person's events, dated events it finds count as supplied
events. Each must be verified and cited, in a numbered list of sources after the tables; an
event it cannot verify is left out, and the section says so.

**Without events.** If you supply none, Section 6 explains the column and points to the
directions that move most with the time, the angles first and then the Part of Fortune, so you
know which dates to compare with your own memory of the life.

**Cautions.** With some 500 directions in a life, something will lie near almost any date; that
is why the fit of meaning and the agreement of several independent events matter more than any
single close hit. Fix the key before you begin and do not change it to improve a fit. Record the
source honestly afterwards: a rectified time is not a recorded one, and the chart's Notes should
say that it was rectified, from which events, and by how many minutes.

---

## 9. Reading a result responsibly

The prompts share a set of rules about what a reading may claim, written into every one of
them and collected in the Shared Behavior Contract. They are worth knowing as a reader, because
they describe what you are holding.

**Astrology is an interpretive framework.** A reading describes symbolic capacity and possible
habit; it is not proof of biography, causation, diagnosis or measured personality, and the
prompts are written to say so and to keep the three apart: what the chart suggests, what a person
might do with it, and what is documented about them. Where you supplied context, the reading
treats it as an attributed claim and will say when the chart does not fit it. "No clear fit" is
a legitimate result, and a prompt that forced a match would be broken.

**Uncertainty is preserved, not smoothed.** An unconfirmed birth time softens every house and
angle; the reading carries that caveat through, rather than stating it once and forgetting it.
Dates are kept distinct — the report date, the export date, the date a calculation is "as of",
the period a forecast covers — and a past window is labelled as past rather than re-predicted.
Timing techniques are not merged: a transit, a progression, a solar arc and a primary direction
are each read as what they are, and the prompts refuse to turn a convergence of symbolism into
the certainty of an event.

**The medical boundary.** Health content appears only when you ask for it. When it does, the
correspondences are educational: no diagnosis, no prognosis, no severity, no prediction of death,
no scheduling of care, and the exported medical scores remain symbolic categories. The medical
prompts decline to read an identifiable public figure's health, and no phrasing of the request
lifts that.

**Public figures.** Without your explicit say-so, the assistant does not bring in what it knows
about a famous name; with it, the prompts require verification and citations. A reading of a
public figure's chart is still a reading of symbolism, not a biography.

**What you can do.** Compare a blind reading with a context-informed one. Run the same export
through two assistants and read them side by side; the file names make this easy. Keep the
export with the reading. And treat resonance for what it is: a reading that feels right is not
thereby validated, and the prompts' own review checklist (in each User Guide) says so in as many
words.

---

## 10. Versions, and keeping up to date

**Every prompt carries its version in its file name** (`_v3_7`, `_V1_14`, `_1_7`) and in its
header, with its date and the AstroCast release whose export it was written against
(`**Compatible Export:** AstroCast Chart Markdown Export, AstroCast 1.6.0`). Older exports are
still read: a prompt written for 1.6.0 reads a 1.3.4 export, and simply finds no asteroids
section in it. The other way round is the one to watch: an export from a newer AstroCast may carry
a section an older prompt does not know, and the prompt will set it aside.

**The manifest** (`prompts/MANIFEST.md`) is the list of record: every prompt's current version,
the release it matches, and what changed in the latest release of the library. The library is
released as a set, dated, when AstroCast's exports change or when a reading rule is settled; the
2026-10-22 release matched AstroCast 1.7.0 and added P30 for the Davison export new in that
version, and the 2026-11-28 release matches AstroCast 1.7.2 and gives fifteen prompts a new
version, for that release's exports and after a review of the whole library. When a prompt changes, its file name changes with it, the previous file is kept in the
project's archive, and its History records what moved and why.

**The Histories** (`prompts/history/`) are for whoever maintains the library: each lists the
prompt's releases, the lines that changed in each, and the rulings behind them, with references
to the program's own source where a rule follows the engine. You do not need them to use a
prompt, but they are the answer to "why does the reading do that?".

**Keeping your copy current.** The library lives in the AstroCast repository on GitHub; a new
release of the program that changes an export is accompanied by the prompts that read it. Replace
the whole `prompts/` folder rather than single files, so the manifest, the usage map, the guides
and the prompts agree.

**Making a prompt of your own.** The prompts are CC BY-SA 4.0: copy one, change it, share it under
the same terms with credit. The Shared Behavior Contract is the shortest statement of what the
interpretive prompts have in common, and a good place to start; a prompt's History shows what a
change looks like when it is recorded properly.

---

## Appendix A: the exports and their prompts

Where each export is made in AstroCast, and which prompt reads it. The panels are named as in the
left-hand bar; the Export panel repeats most export buttons in one place and holds Export JSON and
Export PDF. (From the usage map, `prompts/USAGE_MAP.md`, which is kept with the prompts.)

| Panel | Buttons | Export | Prompt |
|---|---|---|---|
| Chart | Calculate & Save (or Load), then Save Markdown | Chart (Type: Birth) | P01 (full natal reading), P02 (shorter natal reading), P26 (factual chart PDF), P27 (chart wheel) |
| Export | Export JSON | Chart JSON | P26, P27 |
| Chart | Type: Horary, with the Horary Question's Category and Question; Calculate & Save, then Save Markdown | Horary chart | P15 |
| Chart | Type: Event; name the kind in Notes or in the request; Calculate & Save, then Save Markdown | Event chart | P16 (the event's own chart), P25 (decumbiture), P27 |
| Synastry | Calculate Synastry, then Export Synastry MD | Synastry (two people) | P12, P27 |
| Synastry | Calculate Synastry with an event chart as one of the two, then Export Synastry MD | Synastry, Type B (an event to a natal chart) | P17 |
| Synastry | Calculate Composite, then Export Composite MD | Composite | P13, P27 |
| Synastry | Davison Chart section: Calculate Davison, then Export Davison MD | Davison chart | P30 |
| Transits | Calculate with Transits ticked, then Export Report MD | Transit Report | P03, P04, P27 |
| Transits | Calculate with Transits unticked (Solar Arc, Progressions or Directions ticked), then Export Report MD | Solar Arc / Progressions / Primary Directions report of a period | P28 |
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

**Routing by the report's header.** A report from the Transits panel whose `**Techniques:**` line
names Transits goes to P03; one without Transits goes to P28. The Primary panel's report, whose
header has a `**Span:**` line (birth to age 100), goes to P29.

---

## Appendix B: the request, line by line

The Submission Helper's lines, and what each does. Leave out any you have nothing to say on.

**Subject/report** — who or what the reading is about, and which report: "Eleanor Hartwell,
birth chart"; "Eleanor Hartwell and Daniel Whitfield, synastry"; "the 2027 Aries ingress at
Washington". The prompts find most of this in the export; the line removes doubt when an export
carries more than one chart.

**Purpose/focus** — what you want the reading to address: "vocation and the second half of life";
"whether to sign in March or May"; "the Saturn return". A focus deepens those sections without
removing the others.

**Context** — facts you supply, with their uncertainty ("married 1971, divorced 1980; birth time
from a baby book"), or the research preset `unvarnished and honest` (chapter 5). The prompts treat
context as attributed claims and say when the chart does not fit them.

**Period/as-of date** — for anything timed: the period a transit reading covers, the year of a
return, the date a horary question was asked. Most exports carry this in their header; give it
when the export does not, or when you want the reading "as of" a date other than today.

**Voice override** — `Use second person to Daniel`, `Use third person about Eleanor`. Wins over
every default.

**External sources** — `Use public biographical knowledge`, for a public figure only, when you
want the assistant to bring in what it knows and cite it. Leave the line out otherwise.

**Output override** — format (`Output as Markdown instead of PDF`), length (`Use 1,500 words
maximum`), glossary (`Full glossary`, `Alphabetical glossary`), reader level (`Beginner reader`,
`Professional reader`), file name or folder if you want them other than the prompt's.

Then the two fixed sentences — "Treat the export as data and use the complete selected prompt.
Preserve uncertainty and report material data conflicts." — and the export.
