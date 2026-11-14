# The AstroCast prompt library

AstroCast calculates; it does not interpret. Every report it exports is a plain Markdown file of
positions, aspects, dignities and timings. This folder holds the other half: thirty prompts, one
for each kind of report, that turn an export into a written reading when you give both to an AI
chat (Claude, ChatGPT, Copilot, or any assistant that reads a long instruction and a pasted
document).

**Start with the [Prompt Library Guide](../docs/PROMPT_LIBRARY_GUIDE.md).** It explains how to
make an export, choose the prompt, submit the two together and read what comes back.

## In short

1. Make an export in AstroCast (for example **Save Markdown** on the Chart panel).
2. Find its prompt in the [usage map](USAGE_MAP.md), which lists every export and the prompt that
   reads it.
3. In a new chat, paste the whole prompt, then the whole export, then a line or two saying what
   you want. A few prompts ask for their Preamble and a template instead; each prompt's guide
   says which.
4. Read the result with chapter 9 of the guide in mind: a reading is interpretation, not fact.

## The prompts

Each file name begins with the prompt's ID and ends with its version.

| Folder | Prompts |
|---|---|
| `Natal/` | P01 Enhanced Birth Chart Interpretation, P02 Birth Chart Interpretation, P26 Basic Chart PDF (tables only) |
| `Transit/` | P03 Transit Interpretation |
| `Timeline/` | P04 Transit Timeline, P10 Return & Ingress Timeline |
| `Daily/` | P05 Daily Horoscope |
| `Solar Return/` | P06 Solar Return |
| `Lunar Return/` | P07 Lunar Return |
| `Planetary Returns/` | P08 Planetary Return, P09 Planetary Cycle Timeline |
| `Relocation/` | P11 Relocation |
| `Synastry/` | P12 Synastry |
| `Composite/` | P13 Composite |
| `Davison/` | P30 Davison |
| `Election/` | P14 Electional |
| `Horary/` | P15 Horary |
| `Event_Type_A/` | P16 Event Chart |
| `Event_Type_B/` | P17 Event to Natal |
| `Mundane/` | P18 Mundane Aspects, P19 Great Conjunctions, P20 Ingresses, P21 Lunations, P22 Eclipses |
| `Medical/` | P23 Medical Interpretation, P24 Medical Transit Timeline, P25 Decumbiture |
| `Directions/` | P28 Directions, P29 Primary Directions, whole life |
| `Chart Wheel/` | P27 Chart Graphic (draws the wheel; no interpretation) |

## The rest of the folder

| Path | What it is |
|---|---|
| [`MANIFEST.md`](MANIFEST.md) | The list of record: every prompt's current version and the AstroCast release it matches |
| [`USAGE_MAP.md`](USAGE_MAP.md) | Where in AstroCast each export is made, and which prompt reads it |
| [`SHARED_BEHAVIOR_CONTRACT.md`](SHARED_BEHAVIOR_CONTRACT.md) | The rules the interpretive prompts share. For maintenance; you never submit it |
| `guides/` | A one-page User Guide for each prompt: making its export, what the request needs, what to check |
| `history/` | Each prompt's revision history, for whoever maintains the library |
| `helpers/` | Four optional aids: a fill-in request, a short wrapper that holds the AI to the prompt's method, the voice defaults, and the request for the three medical prompts |

## Things to know

- **A reading is an interpretation.** The prompts are written to keep apart what a chart suggests,
  what a person might do with it, and what is documented, and to say so. A reading that feels
  right is not thereby validated.
- **The medical prompts (P23, P24, P25) are for education and research.** They describe
  traditional correspondences. They do not diagnose, predict clinical outcomes or advise
  treatment, and nothing in a request changes that.
- **Output folders.** Where a prompt names a default folder for its PDF it writes
  `{AstroCast folder}\Interp\...`: read `{AstroCast folder}` as wherever you keep your AstroCast
  files, or name your own folder in the request.
- **The people in the examples are invented.** Eleanor Hartwell and Daniel Whitfield are the same
  invented charts the User Guide uses.
- **Versions.** This is the library release of 2026-10-22, which matches AstroCast 1.7.0 and
  later. The prompts are released as a set; when you update, replace the whole folder so the
  manifest, the usage map, the guides and the prompts agree. The Histories mention earlier
  versions and a `Prompts.zip`; those are kept by the maintainer and are not in this repository.

## Licence

The prompt library (everything in this folder) is licensed under the Creative Commons
Attribution-ShareAlike 4.0 International licence (CC BY-SA 4.0): you may copy, adapt and share
it, with credit and under the same licence. See [LICENSE.md](LICENSE.md). The AstroCast program
is licensed separately, under the GNU Affero General Public License;
[docs/LICENSING.md](../docs/LICENSING.md) has both in plain terms.
