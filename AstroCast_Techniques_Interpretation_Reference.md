# AstroCast — Techniques Data Reference for Interpretation Prompts

**Version:** 1.3.4
**Date:** 2026-10-14
**Scope:** The data AstroCast's chart exports carry, in the chart JSON (`ChartResult` as `ChartJsonFormatter` writes it) and the Markdown, with each field's astrological meaning and how an interpretation prompt should use it: §1–§10 the chart data added by Stages 1–6 of the Techniques Implementation Plan, §11 the electional score, §12 declination, §13–§23 the midpoints, parans, time lords, transit, progression, timeline, return, cycle, composite and relocation exports, and §24 the synastry orbs.

---

## How to Use This Document

Every field described below is computed for **every cast chart** (Birth, Event, Horary, Electional, and all specific event subtypes), with one exception: a composite, whose bodies are midpoints and not points in the sky, has no planet conditions, Moon mansion, planetary hour, Lots, antiscia, stations, parans, right ascension or declination, and its export prints no applying marks and no Moon's next aspect or void of course (ruling S-D4; its header's "Not shown" line). Your interpretation prompt should **select which fields to emphasize** based on the chart type:

- **Horary:** Prioritize radicality check (§6), Moon mansion (§5), combustion/besiegement of significators (§4), Arabic Parts matching the question topic (§7), antiscia for hidden perfection (§8), extended mutual reception (§9), and planetary sect (out-of-sect malefic identification, §2).
- **Event:** Prioritize planet conditions (combustion of key planets, §4), stations near angles (§10), Moon mansion nature (§5), Arabic Parts (Fortune/Spirit describe what the event sets in motion, §7), and antiscia for hidden connections (§8).
- **Electional:** Prioritize Moon mansion favorability for the activity (§5), planetary hour alignment with the election purpose (§6), absence of combustion/besiegement on significators (§4), and Arabic Parts (Fortune/Spirit angular = strong election, §7). These are reading priorities: the electional score (§11) reads neither the mansion nor the Lots.
- **Birth/Natal:** All data is available for natal interpretation. Sect analysis (§2), triplicity rulers (§3), and the full dignity table (including triplicity and decan tiers, §1) are particularly valuable for natal delineation; declination (§12), midpoints (§13), parans (§14) and the time lords (§15) complete the natal export.
- **Transits and progressions:** the transit report (§16), its midpoint activations (§13), progressions and solar arcs (§17), and the transit timeline (§18).
- **Returns and cycles:** the planetary return (§19), the planetary cycle timeline (§20) and the return and ingress timeline (§21); each return prints the time lords at its ingress (§15).
- **Composite, relocation and synastry:** the composite (§22), the relocation (§23) and the synastry orbs (§24).

**Dates and clocks** (T-D11, kept: UT throughout): the transit, timeline, return and series exports print every event's date and time in UT, most labelled UTC, after the time or in a "Date (UTC)" column; the great-conjunction series and the cycle timeline print UT dates with no label. A chart's own Date/Time line, the stationing table, the horary times, the daily horoscope and the electional candidates print local readings, each export naming its clock.

---

## 1. Extended Essential Dignities

**Location in JSON:** `dignities[]`
**Location in Markdown:** Essential Dignities table (columns: Planet, Sign, Dignity, Score, Dispositor); `triplicityRuler` and `decanRuler` are in the JSON only

### What Changed

The dignity system now evaluates **five levels** of essential dignity instead of two. Every planet's `DignityInfo` includes:

| Field | Type | Description |
|-------|------|-------------|
| `triplicityRuler` | string | The active Dorothean triplicity ruler for the sign this planet occupies (day ruler in day charts, night ruler in night charts; a relocated chart takes the birth chart's day or night, ruling S-D6) |
| `isTriplicityRuler` | bool | True if this planet IS the active triplicity ruler of its sign |
| `decanRuler` | string | The Chaldean decan (face) ruler of the 10° segment this planet occupies |
| `isInOwnDecan` | bool | True if this planet IS the decan ruler of its segment |

### Updated Score and Label

The `score` and `dignity` fields (the Markdown's Score and Dignity columns) now include the new tiers:

| Dignity | Score | Label |
|---------|-------|-------|
| Domicile | +5 | "Domicile" |
| Exaltation | +4 | "Exalted" |
| **Triplicity** | **+3** | **"Triplicity"** |
| **Face (Decan)** | **+1** | **"Face"** |
| Peregrine | 0 | "Peregrine" |
| Fall | −4 | "Fall" |
| Detriment | −5 | "Detriment" |

The highest applicable dignity takes precedence. A planet with both domicile and triplicity scores +5 (domicile). A planet with only triplicity scores +3. A planet with only its own decan scores +1.

### Updated Peregrine Definition

A planet is peregrine only if it has **no dignity at any level** — not domicile, not exalted, not triplicity ruler, not in its own decan, not in detriment, not in fall. Previously, a planet that was the triplicity ruler of its sign still showed as peregrine; now it correctly shows as "Triplicity" with score +3.

### Interpretation Guidance

- A significator with triplicity dignity (+3) has moderate strength — it can function but lacks the authority of domicile or exaltation. In horary, this is enough to act on the matter but may need support from other testimonies.
- A significator with only face/decan dignity (+1) is barely above peregrine — it has minimal resources. Lilly calls face dignity "a man about to be turned out of doors."
- The `triplicityRuler` field tells you which planet supports the sign's elemental nature for this chart's sect. In a day chart with Ascendant in Aries (fire), the triplicity ruler is the Sun — the Sun has special responsibility for carrying the chart's fire-element themes.
- The `decanRuler` adds a sub-tone to any planet in that decan. Moon in the Saturn decan of Taurus has a more austere emotional quality than Moon in the Venus decan.

---

## 2. Planetary Sect

**Location in JSON:** `chartContext.planetarySect[]`
**Location in Markdown:** Planetary Sect table (inside Chart Context section; columns: Planet, Sect, In Sect, Role, Hayz); `houseOfJoy` is in the JSON only
**Relocation:** a relocated chart's sect and Planetary Sect table are the birth chart's (ruling S-D6)

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet` | string | Planet name |
| `naturalSect` | string | "Diurnal" or "Nocturnal" — the planet's inherent sect affiliation |
| `isInSect` | bool | True if this planet's sect matches the chart sect (day planet in day chart, or night planet in night chart). A day chart has the Sun on the upper side of the horizon by its longitude, in every house system |
| `isBenefic` | bool | True if Jupiter or Venus |
| `isMalefic` | bool | True if Mars or Saturn |
| `isInSectMalefic` | bool | True if this is the less-harmful malefic (Saturn in day charts, Mars in night charts) |
| `isOutOfSectMalefic` | bool | True if this is the most-harmful malefic (Mars in day charts, Saturn in night charts) |
| `isInSectBenefic` | bool | True if this is the strongest benefic (Jupiter in day charts, Venus in night charts) |
| `isHayz` | bool | True if the planet meets the full hayz condition: correct sect + correct hemisphere (by the planet's altitude: a diurnal planet above the horizon, a nocturnal one below; a composite planet, which has no altitude, by its longitude) + correct sign gender |
| `houseOfJoy` | int? | The house where this planet functions most comfortably (Mercury=1, Moon=3, Venus=5, Mars=6, Sun=9, Jupiter=11, Saturn=12) |

### Sect Assignments

| Planet | Natural Sect | Notes |
|--------|-------------|-------|
| Sun | Diurnal | |
| Jupiter | Diurnal | |
| Saturn | Diurnal | |
| Moon | Nocturnal | |
| Venus | Nocturnal | |
| Mars | Nocturnal | |
| Mercury | Adapts | By zodiacal longitude: behind the Sun (so rising before it, a morning star) diurnal; ahead of the Sun (an evening star) nocturnal. The longitude rule stands on the rare chart where Mercury's latitude reverses the rising order |

### Interpretation Guidance

- **The out-of-sect malefic is the most dangerous planet in the chart.** In a day chart, Mars is out of sect and operates with less restraint — it is the planet most likely to cause harm, accidents, or conflict. In a night chart, Saturn is out of sect and its themes of delay, restriction, and loss are amplified.
- **The in-sect benefic is the strongest helper.** In a day chart, Jupiter is the primary source of support, growth, and positive outcomes. In a night chart, Venus takes this role.
- **Hayz is the strongest accidental sect dignity.** A planet in hayz operates with full authority in its preferred mode. This is rare and notable when it occurs.
- A significator that is in sect is inherently more functional than one that is out of sect. An out-of-sect significator may produce the result but in a more difficult or circuitous way.
- In horary, always identify the out-of-sect malefic — if it aspects a significator or occupies a key house, it represents the primary obstacle or source of harm in the matter.

---

## 3. Ascendant Triplicity Rulers

**Location in JSON:** `chartContext.ascendantTriplicityRulers`
**Location in Markdown:** Ascendant Triplicity Rulers (inside Chart Context section)

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `element` | string | "Fire", "Earth", "Air", or "Water" |
| `dayRuler` | string | Triplicity ruler active in day charts |
| `nightRuler` | string | Triplicity ruler active in night charts |
| `participatingRuler` | string | Secondary ruler active in both day and night |
| `activeRuler` | string | The ruler selected for this chart's sect (day ruler or night ruler); for a relocated chart, the place's Ascendant sign read with the birth chart's sect |

### Dorothean Triplicity Table

| Element | Signs | Day Ruler | Night Ruler | Participating |
|---------|-------|-----------|-------------|---------------|
| Fire | Aries, Leo, Sagittarius | Sun | Jupiter | Saturn |
| Earth | Taurus, Virgo, Capricorn | Venus | Moon | Mars |
| Air | Gemini, Libra, Aquarius | Saturn | Mercury | Jupiter |
| Water | Cancer, Scorpio, Pisces | Venus | Mars | Moon |

### Interpretation Guidance

- The active triplicity ruler of the Ascendant sign is the planet that supports the chart's overall direction at the elemental level. In a day chart with Aries rising, the Sun is the fire triplicity ruler — it carries the chart's initiative, action, and drive.
- In natal charts, the triplicity rulers of the Ascendant sign describe three phases of life: the day ruler governs the first third, the night ruler the second third, and the participating ruler the final third. (This is Dorotheus's system for life delineation.)
- In horary, the active triplicity ruler can function as a secondary significator when the primary significator (house ruler) is peregrine or combust.

---

## 4. Planet Conditions (Bonification and Maltreatment)

**Location in JSON:** `planetConditions[]`
**Location in Markdown:** Planet Conditions table

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet` | string | Planet name |
| `isCazimi` | bool | Within 0°17′ of Sun — exceptionally powerful, "in the heart of the king" |
| `isCombust` | bool | Within 8° of Sun (not cazimi) — severely weakened, hidden, overwhelmed |
| `isUnderTheBeams` | bool | 8°–17° from Sun — moderately weakened, partially hidden |
| `isBesieged` | bool | Enclosed between Mars and Saturn with no benefic intervention |
| `isConjunctBenefic` | bool | Conjunct Jupiter or Venus within 8° |
| `isConjunctMalefic` | bool | Conjunct Mars or Saturn within 8° |
| `hasApplyingBeneficAspect` | bool | Applying trine or sextile from Jupiter or Venus |
| `hasApplyingMaleficAspect` | bool | Applying opposition or square from Mars or Saturn |
| `accidentalScore` | int | Composite score (positive = strengthened, negative = weakened) |
| `conditions` | string[] | Human-readable list of active conditions |

### Accidental Score Table

| Condition | Score |
|-----------|-------|
| Cazimi | +5 |
| Conjunct benefic | +2 |
| Applying benefic aspect | +1 |
| Free (no conditions) | 0 |
| Under the beams | −2 |
| Conjunct malefic | −2 |
| Applying malefic aspect | −1 |
| Combustion | −5 |
| Besiegement | −5 |

Scores stack. A combust planet also conjunct a malefic scores −7.

### Special Rules

- **The Moon is excluded from combustion and under-the-beams.** The Moon has its own condition system (phase, VoC, Via Combusta). However, the Moon CAN be cazimi — a Moon at 0°17′ from the Sun at New Moon is exceptionally powerful.
- **Cazimi overrides combustion.** A planet within 17 arc-minutes of the Sun is not combust — it is cazimi, the most powerful accidental dignity.
- **Applying means the orb is shrinking at the chart's instant.** That is the test behind the two applying-aspect flags above and every Ap/Sp label. For an aspect to the Ascendant or Midheaven the angle is held still, so the label describes the planet's own motion toward the angle's degree. A progressed angle is the exception: the progressed Midheaven moves at the progressed Sun's speed and the Ascendant at that times dASC/dMC (branch G, 1.3.4). A composite prints no Ap/Sp label (S-D4).

### Interpretation Guidance

- **Combustion is one of the most debilitating conditions.** A combust significator in horary means the person or thing represented is overwhelmed, hidden, unable to act effectively. The querent's significator combust = the querent is powerless in this matter. The quesited's significator combust = the desired thing is inaccessible or hidden.
- **Besiegement is equally severe.** A besieged significator is trapped between two harmful forces with no escape. In horary, this is a strong negative testimony — the matter is blocked from both directions.
- **Cazimi is the opposite of combustion.** A cazimi planet has direct access to the highest authority — it acts with the king's own power. In horary, a cazimi significator can overcome obstacles that would stop any other planet.
- **Benefic conjunction or aspect softens negative conditions.** A combust planet that is also conjunct Jupiter has its combustion partially offset (+2 against −5 = net −3). The interpretation should note both the weakness and the mitigation.
- **Use the `conditions` array for narrative.** It lists the active conditions as bare labels — "Cazimi", "Combust", "Under the Beams", "Besieged", "Conjunct benefic", "Conjunct malefic", "Applying benefic aspect", "Applying malefic aspect" — with no orb or distance.

---

## 5. Moon Mansion

**Location in JSON:** `moonMansion`
**Location in Markdown:** Moon Mansion line (inside Moon Conditions section)

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `number` | int | Mansion number (1–28) |
| `name` | string | Traditional Arabic name (e.g., "Al-Sharatain") |
| `meaning` | string | English translation (e.g., "The Two Signs") |
| `nature` | string | "Benefic", "Malefic", or "Neutral" |
| `keywords` | string | Interpretive keywords for the mansion |
| `isBenefic` | bool | True if benefic nature; written only when true |
| `isMalefic` | bool | True if malefic nature; written only when true |

### Key Mansions

**Strongly Benefic (4):** #3 Al-Thurayya, #7 Al-Dhira, #14 Al-Simak (Spica region), #24 Sa'd al-Su'ud ("Luckiest of the Lucky")

**Strongly Malefic (7):** #4 Al-Dabaran, #5 Al-Haqa, #10 Al-Jabhah, #12 Al-Sarfah, #16 Al-Zubana, #18 Al-Qalb (Antares region), #19 Al-Shaulah

### Mansion–Activity Correspondence

| Activity | Favorable Mansions | Unfavorable Mansions |
|----------|-------------------|---------------------|
| Travel / journeys | 1, 7, 9 | 13, 28 |
| Marriage / love | 3, 7, 14 | 16, 19 |
| Business / contracts | 2, 11, 24 | 12, 16 |
| Legal disputes | 17 (possible victory) | 18, 19 |
| Lost objects / recovery | 21, 25 | 12 |
| Health / healing | 14, 22 | 18 |
| Overcoming enemies | 8, 19 | — |

### Interpretation Guidance

- The mansion functions as a **qualifying testimony** — it does not override significator analysis but supports or cautions against the main reading. In close calls, the mansion's alignment with the question type can be determinative.
- In horary: check whether the Moon's mansion is favorable or unfavorable for the question topic. Moon in mansion 14 (Al-Simak, benefic) for a health question is supportive testimony. Moon in mansion 18 (Al-Qalb, malefic, Antares region) for a health question is a serious caution.
- In electional: mansion selection is a primary criterion. Choose a time when the Moon is in a mansion favorable for the activity.
- In event charts: the mansion describes the qualitative texture of the moment. A war beginning with Moon in mansion 18 (Antares/Heart of the Scorpion) is particularly ominous.
- Always include the mansion number, name, and nature in the interpretation output. The keywords provide ready-made descriptive language.

---

## 6. Planetary Hour and Radicality

**Location in JSON:** `planetaryHour`, absent when the chart has no hour: where the Sun does not rise or set that day (the polar day or night), or the sunrise and sunset found cannot bracket the instant
**Location in Markdown:** Planetary Hour section
**Relocation:** a relocated chart carries the birth chart's own hour, none computed at the place (ruling S-D6), so its radicality is judged against the natal chart ruler

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `rulingPlanet` | string | The planet ruling this hour (e.g., "Jupiter") |
| `hourNumber` | int | Hour number within the day/night cycle (1–12) |
| `isDaytime` | bool | True if the hour lies between sunrise and sunset (D4). It can differ from the chart's day or night (`chartContext.isDayChart`), which is the Sun's longitude on the upper half of the ecliptic (D3) |
| `hourStart` | string | Local time when the hour begins (HH:mm:ss) |
| `hourEnd` | string | Local time when the hour ends (HH:mm:ss) |
| `hourStartUtc` | string? | The instant the hour begins, round-trip UTC ("2000-01-01T11:07:57.0000000Z"): one instant even where `hourStart` falls in a repeated or skipped wall hour; before 1582-10-15 its Julian label, as `chart.utcDateTime`, beside `"calendar": "Julian"`. **Omitted when the hour has none** |
| `hourEndUtc` | string? | The instant the hour ends, as `hourStartUtc` (across New York's 2026 fall-back the two are 68 minutes apart where the wall clocks read eight) |
| `hourTimeZone` | string? | The IANA zone whose clock `hourStart` and `hourEnd` are on. **Omitted for a chart cast at an explicit offset** |
| `hourOffsetLabel` | string? | For a chart cast at an explicit offset only, that offset's label, the clock `hourStart` and `hourEnd` are on ("UTC−5 (explicit)") |
| `isRadical` | bool? | True if the hour ruler connects to the chart ruler. **Omitted for non-Horary charts** |
| `radicalityLevel` | string? | "Radical", "Supportive", "Weak", or "Not Radical". **Omitted for non-Horary charts** |
| `radicalityNote` | string? | Human-readable explanation. **Omitted for non-Horary charts** |

### Radicality Levels (Horary Only)

| Level | Match | Meaning |
|-------|-------|---------|
| **Radical** | Hour ruler = chart ruler | Direct match — chart is fit to be judged |
| **Supportive** | Hour ruler shares the Ascendant sign's triplicity | Elemental sympathy — chart is acceptable |
| **Weak** | Hour ruler is the chart ruler's dispositor | Indirect connection — chart can be judged with caution |
| **Not Radical** | No connection | Chart may not be fit to be judged — note this but do not refuse to interpret |

### Interpretation Guidance

- **Horary:** Always report the radicality check as one of the first items in the horary judgment. A radical chart is confirmed as fit. A non-radical chart should be noted as a caution, but most modern practitioners still proceed with interpretation.
- **Electional:** The planetary hour should match the election's purpose. Venus hour for love/beauty elections, Mercury for contracts/communication, Jupiter for legal/financial success, Mars for surgery/conflict, Moon for public matters. Report whether the hour aligns with the election type. The electional score reads the hour by its own table (§11): 100 when the hour's ruler is the type's significator (Travel's is Mercury), 75 for Jupiter or Venus, 50 for the Sun, Moon or Mercury, and 25 for Mars or Saturn.
- **Event:** The hour ruler identifies which planet "presides over" the event at the most immediate level. Incorporate this as narrative color.
- The `radicalityNote` field provides a pre-formatted explanation suitable for direct inclusion in interpretation text.

---

## 7. Arabic Parts (Lots) and Hellenistic Lots

**Location in JSON:** `arabicParts[]`
**Location in Markdown:** Arabic Parts / Lots table

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Display name (e.g., "Part of Fortune", "Lot of Eros") |
| `group` | string | "Arabic" (traditional 8) or "Hermetic" (7 Hellenistic Lots) |
| `longitude` | double | Ecliptic longitude in degrees |
| `sign` | string | Zodiac sign |
| `signDegrees` | int | Degrees within the sign |
| `signMinutes` | int | Arc-minutes within the degree |
| `house` | int? | House placement (1–12) |
| `formula` | string | The formula used (e.g., "Asc + Moon − Sun") — reflects day/night reversal; a relocated chart's Lots take the birth chart's day or night at the place's Ascendant (ruling S-D6) |
| `domain` | string | Life area this Part describes |
| `ruler` | string | Traditional ruler of the sign the Part occupies |

### Part Catalog

**Arabic Parts (8):**

| Name | Day Formula | Reverses at Night | Domain |
|------|-----------|-------------------|--------|
| Part of Fortune | Asc + Moon − Sun | Yes | General wellbeing, material fortune |
| Part of Spirit | Asc + Sun − Moon | Yes | Conscious will, intention, career |
| Part of Marriage | Asc + Desc − Venus | No | Relationships, partnership |
| Part of Children | Asc + Jupiter − Saturn | Yes | Offspring, creativity |
| Part of Death | Asc + Cusp8 − Moon | No | Mortality, endings, major loss |
| Part of Illness | Asc + Mars − Saturn | Yes | Health crises |
| Part of Vocation | MC + Moon − Sun | No | Career, public role |
| Part of Exaltation | Asc + 19°Ari − Sun | Yes | Honours, recognition |

By night Part of Children (Asc + Saturn − Jupiter) and Part of Exaltation (Asc + Sun − 19° Aries) take the app's own forms, kept by ruling L1 rather than taken from a source.

**Hermetic Lots (7):**

| Name | Day Formula | Domain |
|------|-----------|--------|
| Lot of Fortune | Asc + Moon − Sun | Body, material circumstance |
| Lot of Spirit | Asc + Sun − Moon | Soul, intention, action |
| Lot of Eros | Asc + Venus − Spirit | Desire, love, pleasure |
| Lot of Necessity | Asc + Fortune − Mercury | Compulsion, obligation, enemies |
| Lot of Courage | Asc + Fortune − Mars | Daring, conflict, initiative |
| Lot of Victory | Asc + Jupiter − Spirit | Success, allies, recognition |
| Lot of Nemesis | Asc + Fortune − Saturn | Hidden enemies, fate, retribution |

Note: Lot of Fortune and Lot of Spirit (Hermetic) are identical calculations to Part of Fortune and Part of Spirit (Arabic). Both appear in the output with their respective group labels. The Hermetic Lots of Eros, Necessity, Courage, Victory, and Nemesis use the already-computed Fortune and Spirit as factors in their formulas. All seven Hermetic Lots reverse by night: the two factors after the Ascendant swap, so by night the Lot of Necessity is Asc + Mercury − Fortune and the Lot of Courage Asc + Mars − Fortune. Necessity and Courage follow Paulus Alexandrinus (ch. 23), as Eros, Victory and Nemesis already do.

### Interpretation Guidance

- **Part of Fortune** is the most important Lot in any chart. Its sign, house, and ruler describe where material fortune flows. Fortune angular (houses 1, 4, 7, 10) is powerful testimony for material success. Fortune in a cadent house (3, 6, 9, 12) suggests fortune is diffuse or requires effort to access.
- **Part of Spirit** describes conscious will and intention. Where Fortune shows what happens to you, Spirit shows what you make happen. Spirit angular = strong capacity for directed action.
- **In horary, use the topic-specific Lot.** Marriage question → check Part of Marriage (sign, house, ruler's dignity and aspects). Illness question → check Part of Illness. Legal question → check Lot of Necessity (compulsion, what cannot be escaped) and Lot of Victory.
- **The ruler of the Lot is critical.** A Lot whose ruler is dignified and well-aspected activates the Lot's themes positively. A Lot whose ruler is combust, besieged, or peregrine indicates the Lot's domain is obstructed.
- **Lot conjunct an angle or its own ruler** = highly activated. Report this when it occurs.
- In electional charts, ensure Fortune and Spirit are well-placed (preferably angular or in the house matching the election's purpose).

---

## 8. Antiscia and Contra-Antiscia

**Location in JSON:** `antisciaPoints[]` and `antisciaConnections[]`
**Location in Markdown:** Antiscia Connections table

### Antiscia Points Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet` | string | Planet name |
| `longitude` | double | Planet's actual longitude |
| `antisciaLongitude` | double | Mirror point across solstice axis (0° Cancer / 0° Capricorn) |
| `antisciaSign` | string | Sign of the antiscion point |
| `contraAntisciaLongitude` | double | Mirror point across equinoctial axis (antiscion + 180°) |
| `contraAntisciaSign` | string | Sign of the contra-antiscion point |

### Antiscia Connections Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet1` | string | First planet |
| `planet2` | string | Second planet |
| `type` | string | "Antiscia" (hidden conjunction) or "ContraAntiscia" (hidden opposition) |
| `orb` | double | Angular distance in degrees (maximum 2°) |
| `notation` | string | Pre-formatted description |

### Sign Pairings (Solstice Mirror)

| Sign ↔ Mirror |
|---|
| Aries ↔ Virgo |
| Taurus ↔ Leo |
| Gemini ↔ Cancer |
| Libra ↔ Pisces |
| Scorpio ↔ Aquarius |
| Sagittarius ↔ Capricorn |

### Interpretation Guidance

- **Antiscia connections are hidden conjunctions.** Two planets in antiscion act as if conjunct, but the connection is invisible to standard aspect scanning. In horary, this can resolve "no applying aspect" situations — the significators may perfect the matter through their antiscia connection, while it is applying, even though they form no classical aspect.
- **Contra-antiscia connections are hidden oppositions.** They reveal concealed disagreement or tension beneath an apparently calm surface.
- **Tight orbs are more significant.** Classical sources use 1°–2° maximum. A connection at 0°30′ is much more powerful than one at 1°45′.
- In horary: always check whether the two main significators (querent and quesited) have an antiscia connection. If they do and it is applying — the sum of the two longitudes, moving at the planets' speeds, still closing on 180° for an antiscion or 0° for a contra-antiscion — the matter can perfect even if no standard applying aspect exists. Report this as: "Although [planet A] and [planet B] form no direct aspect, their antiscia connection at [orb] indicates a hidden bond — the matter may perfect through unseen channels." A separating connection is testimony, not perfection: the horary analysis notes it ("The Mars–Venus antiscion (orb 0°30′) is separating: testimony, not perfection.") and does not count it.
- In event charts: antiscia between the event chart's significators reveal hidden forces at work behind the event.

---

## 9. Extended Mutual Reception

**Location in JSON:** `chartContext.mutualReceptions[]`
**Location in Markdown:** Mutual Receptions list (inside Chart Context section), each line its `notation`, which names the type and not the strength; `strength` is in the JSON only

### Updated Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet1` | string | First planet |
| `planet1Sign` | string | Sign occupied by first planet |
| `planet2` | string | Second planet |
| `planet2Sign` | string | Sign occupied by second planet |
| `receptionType` | string | Type of reception (see below) |
| `strength` | string | "Strongest", "Strong", or "Moderate" |
| `notation` | string | Pre-formatted description including type |

### Reception Types

| Type | Description | Strength |
|------|-------------|----------|
| **Domicile** | Each planet in the other's ruling sign | Strongest |
| **Exaltation** | Each planet in the sign where the other is exalted | Strong |
| **Mixed (Domicile-Exaltation)** | One by domicile, the other by exaltation | Strong |
| **Triplicity** | Each planet is the triplicity ruler of the other's sign | Moderate |
| **Mixed (Domicile-Triplicity)** | One by domicile, the other by triplicity | Moderate |
| **Mixed (Exaltation-Triplicity)** | One by exaltation, the other by triplicity | Moderate |

Only the strongest applicable type is reported for each planet pair (no duplicates).

### Interpretation Guidance

- **Mutual reception by domicile is the strongest form.** The two planets effectively "exchange places" — each has full authority in the other's domain. In horary, domicile mutual reception between significators is one of the most reliable perfection mechanisms.
- **Exaltation reception is strong but less authoritative.** The planets support each other at a high level but without the ownership that domicile provides.
- **Mixed reception is meaningful.** A planet in another's domicile while the other is in the first's exaltation still creates a cooperative bond — the exchange is just asymmetric.
- **Triplicity reception is moderate.** It indicates elemental sympathy rather than direct rulership exchange. Useful when no stronger reception exists. In horary it is testimony only: it does not perfect.
- In horary: a mutual reception between the querent's and quesited's significators by domicile, exaltation, or mixed domicile–exaltation = the matter can perfect even without a direct applying aspect. The horary analysis counts these three types and no others — a reception by triplicity, alone or mixed, does not perfect — and whatever the perfection, its Reception line names the type of such a reception between the significators: "Mutual by domicile", "Mutual by exaltation" or "Mutual by domicile and exaltation" (QI-2). Always report the reception type; its strength is in the JSON only.

---

## 10. Stationing Planets

**Location in JSON:** `stations[]`
**Location in Markdown:** Stationing Planets table (columns: Planet, Station, Phase, Station Time, Station Position, Speed, Proximity)

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `planet` | string | Planet name |
| `stationType` | string | "Station Retrograde" or "Station Direct" — the type of the nearest station **event**, read from the moment the daily speed actually crosses zero (positive → negative = Station Retrograde, negative → positive = Station Direct), whichever side of the chart instant that moment lies. It is not derived from the sign of the current speed: a direct planet slowing toward its retrograde turn is "Station Retrograde", not "Station Direct". |
| `phase` | string | "Approaching" — the station is still ahead and \|speed\| is falling; or "Separating" — the station is behind and \|speed\| is rising |
| `stationTimeUtc` | string | ISO 8601 UTC instant of the station, to the second; before 1582-10-15 its Julian label, as `chart.utcDateTime`. The Markdown table shows it to the minute on the chart's own clock. |
| `stationJulianDay` | double | Julian Day (UT) of the station |
| `stationLongitude` | double | Ecliptic longitude of the station in decimal degrees; `stationFormattedPosition` gives the same point as DD°MM′SS″ Sign |
| `daysToStation` | double | Signed days from the chart instant to the station: positive = ahead, zero or negative = now or behind |
| `isStationing` | bool | Always true (only stationing planets are included) |
| `dailySpeed` | double | Current speed in degrees/day (negative = retrograde). This field, not `stationType`, carries the current direction of motion; the Markdown table prints it signed. |
| `stationThreshold` | double | Planet-specific threshold below which it is considered stationing |
| `stationProximity` | double | 0.0 = exact stop, 1.0 = at threshold boundary (the Markdown table shows it rounded to the nearest percent) |

### Station Thresholds

| Planet | Threshold (°/day) |
|--------|-------------------|
| Mercury | 0.15 |
| Venus | 0.12 |
| Mars | 0.08 |
| Jupiter | 0.02 |
| Saturn | 0.01 |
| Uranus | 0.004 |
| Neptune | 0.003 |
| Pluto | 0.002 |

Sun, Moon, True Node, and Chiron never station and are excluded. Only planets that ARE stationing appear in the output — an empty list means no planets are near a station point. A composite has no stations at all, and its header's "Not shown" line says so (S-D4).

### Interpretation Guidance

- **A stationing planet is at its most powerful and concentrated state.** Its themes are magnified and focused. A stationing planet on an angle or conjunct a significator operates with greater intensity than a fast-moving planet at the same position.
- **Station Retrograde:** The planet's nearest station is a station retrograde — `phase` says whether it is still ahead (Approaching: the planet is direct and slowing toward its turn) or already behind (Separating: the planet has just turned retrograde and is accelerating). Matters associated with this planet are slowing down, internalizing, or about to reverse direction. In horary, a significator stationing retrograde = the matter is about to stall or reverse.
- **Station Direct:** The planet's nearest station is a station direct — `phase` says whether it is still ahead (Approaching: the planet is retrograde and slowing toward its turn) or already behind (Separating: the planet has just turned direct and is accelerating). Matters that were stalled are reviving. In horary, a significator stationing direct = the matter, apparently lost or abandoned, revives and perfects. **This overrides the standard retrograde debility.**
- **Proximity indicates intensity.** A planet at 10% of threshold (stationProximity = 0.1) is nearly stopped — its station influence is at maximum. A planet at 90% (stationProximity = 0.9) is just entering the station zone.
- In event charts: a planet stationing within 1° of an event chart angle crystallizes that planet's themes as a central narrative of the event.

---

## 11. The Electional Score

**Location in Markdown:** each candidate instant's `**Score:**` line and its Score Breakdown table; the report header's
`**Score ceiling:**` line.
**Source:** `ElectionalScorer.cs`, `ScoreWeights.cs` (weights), `MoonConditionEvaluator.cs` (phase, Via Combusta),
`AngularityService.cs` (the angular orb), `DignityInfo.cs` (essential dignity points).

### What Is Scored

Every eligible sample of the search grid is scored on seven dimensions, each from 0 to 100. The score is their weighted
sum, the weights depending on the election type. Three dimensions stop short of 100 (Ascendant 85, Significator 90,
Natal Harmony 70), so the score's ceiling is below 100: from 90.5 (Other) to 95.5 (Surgery), printed in the report
header. A sample on a day when the Sun does not rise or set has no planetary hour and is not scored. A sample inside a
void-of-course period, or inside a Mercury retrograde when the search avoids it, is never scored, so the Moon's void
penalty below never applies in a search.

The export prints the score to two decimals at most (82.25, 88.5, 91), every dimension as a whole number, every weight
as a whole percent, and every weighted cell to two decimals at most, so the cells add up to the score.

### Election Types

| Type | Significator | Preferred rising signs | Moon phase preferred |
|------|--------------|------------------------|----------------------|
| BusinessLaunch | Jupiter | Leo, Sagittarius, Aries | waxing |
| Contract | Mercury | Gemini, Virgo, Libra | waning |
| Wedding | Venus | Taurus, Libra, Cancer, Pisces | waxing |
| Surgery | Mars | Scorpio, Aries, Capricorn | waning |
| RealEstateListing | Venus | Taurus, Cancer, Leo | waxing |
| RealEstateClosing | Saturn | Capricorn, Taurus, Virgo | waning |
| CreativeLaunch | Sun | Leo, Pisces, Libra | waxing |
| Travel | Mercury | Sagittarius, Gemini, Aquarius | waxing |
| Other | Moon | none (every rising sign scores 50) | waning |

### Weights

| Type | Moon | Ascendant | Significator | Natal Harmony | Malefic Avoidance | Mercury | Planetary Hour |
|------|------|-----------|--------------|---------------|-------------------|---------|----------------|
| BusinessLaunch | 25% | 10% | 20% | 10% | 15% | 10% | 10% |
| Contract | 20% | 10% | 15% | 10% | 10% | 25% | 10% |
| Wedding | 30% | 10% | 25% | 10% | 10% | 5% | 10% |
| Surgery | 25% | 10% | 15% | 5% | 20% | 10% | 15% |
| RealEstateListing | 25% | 10% | 20% | 10% | 15% | 10% | 10% |
| RealEstateClosing | 20% | 10% | 20% | 10% | 15% | 15% | 10% |
| CreativeLaunch | 25% | 15% | 20% | 10% | 10% | 10% | 10% |
| Travel | 25% | 10% | 15% | 10% | 15% | 15% | 10% |
| Other | 25% | 10% | 15% | 10% | 15% | 15% | 10% |

### Moon

Start at 50; add every line that applies; the result is held to 0–100.

| Condition | Points |
|-----------|--------|
| Waxing (elongation 0°–180°) and the type prefers waxing | +15 |
| Waning (180°–360°) and the type prefers waning | +10 |
| Waning and the type prefers waxing | −10 |
| Waxing and the type prefers waning | 0 |
| Full Moon (elongation 180°–225°), any type | +5 |
| Void of course (never within a search: void samples are excluded) | −40 |
| Via Combusta (15° Libra to 15° Scorpio) | −20 |
| In Cancer / in Taurus | +25 / +20 |
| In Capricorn / in Scorpio | −20 / −25 |
| Next applying aspect a trine or sextile to Jupiter or Venus | +10 |
| … a trine or sextile to any other body | +5 |
| … a square or opposition to Mars or Saturn | −10 |
| … a square or opposition to any other body | −5 |
| … a conjunction, or no applying aspect | 0 |

The next applying aspect is the first exact major aspect the Moon makes before it leaves its sign, as the Moon
Conditions block reports it. The highest Moon score is 100.

### Ascendant

| Rising sign | Points |
|-------------|--------|
| One of the type's preferred signs | 85 |
| Any other sign | 45 |
| Type Other, any sign | 50 |

### Significator

Start at 50; add every line that applies; held to 0–100. A significator missing from the chart scores 50.

| Condition | Points |
|-----------|--------|
| In its domicile | +25 |
| In its exaltation | +20 |
| In its detriment | −20 |
| In its fall | −25 |
| Retrograde | −30 |
| Combust: within 8° of the Sun (not for the Sun itself; the Moon included; no cazimi exception) | −15 |
| In an angular house (1, 4, 7, 10) | +15 |
| In a cadent house (3, 6, 9, 12) | −10 |

The four dignity levels use the traditional rulers (Mars for Scorpio, Saturn for Aquarius, Jupiter for Pisces) and are
tested in that order, so a sign that is both domicile and exaltation scores +25 (Mercury in Virgo) and one that is both
detriment and fall scores −20 (Mercury in Pisces). Triplicity and face do not count here. The highest significator score
is 90.

### Natal Harmony

Only the significator is read, against four natal planets, by its separation in longitude (0°–180°, applying or
separating alike), with an orb of 5°. Start at 50; held to 0–100. With no natal chart, 50.

| Natal planet | Aspect within 5° | Points |
|--------------|------------------|--------|
| Jupiter or Venus | trine or sextile | +10 |
| Jupiter or Venus | otherwise, conjunction | +8 |
| Mars or Saturn | square or opposition | −10 |
| Mars or Saturn | otherwise, conjunction | −8 |

The natal Sun, Moon, Mercury, outer planets, nodes and angles are not read, and the Natal Harmony table in the export
(every election body against every natal body, with the synastry orbs) is a separate thing. Scores run from 30 to 70.

### Malefic Avoidance

Start at 100. For Mars and for Saturn, if it lies within 5° of the Ascendant, Midheaven, Descendant or IC by longitude
(the export's Angular Planets table), subtract its penalty; held to 0–100.

| Penalty term | Points |
|--------------|--------|
| Base | 30 |
| It is the out-of-sect malefic (Mars by day, Saturn by night) | +20 |
| Its essential dignity score is −4 or lower (fall or detriment) | +10 |
| It is the chart ruler | +10 |
| Its orb is over 3° (up to 5°) | the penalty × 0.6 |
| Its essential dignity score is +3 or higher (triplicity or better) | the penalty halved |

Essential dignity scores are section 1's (domicile +5 … detriment −5); a positive dignity takes precedence over a
negative one.

### Mercury

Start at 80; held to 0–100.

| Condition | Points |
|-----------|--------|
| Retrograde | −50 |
| Combust: within 8° of the Sun (no cazimi exception) | −15 |
| In Gemini or Virgo | +25 |
| In Sagittarius or Pisces | −20 |

The highest Mercury score is 100.

### Planetary Hour

| Hour ruler | Points |
|------------|--------|
| The type's significator | 100 |
| Jupiter or Venus | 75 |
| Sun, Moon or Mercury | 50 |
| Mars or Saturn | 25 |

### Combustion

The score's combustion is 8°: a planet whose separation from the Sun is less than 8° is combust. It applies to the
significator (any but the Sun, the Moon included) and to Mercury. Unlike the Planet Conditions table (section 4), the
score makes no exception for cazimi, and it does not exempt the Moon when the Moon is the significator.

### The Ceiling

Each dimension's highest score, weighted:

| Type | Ceiling |
|------|---------|
| BusinessLaunch | 93.5 |
| Contract | 94 |
| Wedding | 93 |
| Surgery | 95.5 |
| RealEstateListing | 93.5 |
| RealEstateClosing | 93.5 |
| CreativeLaunch | 92.75 |
| Travel | 94 |
| Other | 90.5 |

The ceiling adds each dimension's highest score; no moment need reach it, and for five types none can. For Contract,
Surgery, RealEstateClosing and Other the Moon's best score needs a Full Moon in Cancer, with the Sun in Sagittarius or
Capricorn, where Mercury, never more than 28° from the Sun, cannot be in Gemini or Virgo. For CreativeLaunch the Sun's
best (in Leo) and the Moon's best (waxing, in Cancer) exclude each other.

### What the Score Does Not Read

The Moon's mansion, besiegement, the Lots, stations, triplicity and face for the significator, benefics in sect, the
Ascendant ruler's condition, and every natal point but Jupiter, Venus, Mars and Saturn. Strengths and compromises listed
under each candidate are read from the dimension scores: a strength at 70 or more (Moon, Ascendant, Significator,
Planetary Hour) or 90 or more (Malefic Avoidance); a compromise below 50 (Moon, Mercury), 60 (Malefic Avoidance) or
40 (Natal Harmony).

### Interpretation Guidance

- The score ranks moments by these rules and no others. Read each dimension against the tables that ground it (the
  algorithm audit), and treat a high score as a starting point.
- Compare a score with the type's ceiling, not with 100.

---

## 12. Declination

**Location in JSON:** `positions[].declination` (decimal degrees, north positive). The JSON carries no out-of-bounds
flag, declination aspect or cluster.
**Location in Markdown:** the Planetary Positions table (columns Dec and OOB); Out-of-Bounds Planets (columns: Planet,
Declination, Excess); Declination Aspects (±1° orb) (columns: Body 1, Dec 1, Aspect, Body 2, Dec 2, Orb); Declination
Clusters (columns: Cluster Zone, Points, Count, Aspects).
**Source:** `PositionFormatter.IsOutOfBounds` (out of bounds), `DeclinationAspectService` (aspects, the cross-chart
method `FindCrossChartDeclinationAspects`, and `IdentifyClusters`), `ChartMarkdownFormatter` (the tables).

### Fields

| Column | Table | Description |
|--------|-------|-------------|
| Dec | Planetary Positions | The body's declination, north positive |
| OOB | Planetary Positions | "OOB" when the body is out of bounds, otherwise blank |
| Excess | Out-of-Bounds Planets | \|declination\| − the chart's true obliquity, in degrees to two decimals |
| Aspect | Declination Aspects | "Par" (parallel) or "CPar" (contra-parallel) |
| Orb | Declination Aspects | \|a − b\| for a parallel, \|a + b\| for a contra-parallel, a and b the two declinations |
| Cluster Zone | Declination Clusters | The members' lowest to highest absolute declination |
| Points | Declination Clusters | The members, each with its signed declination |
| Count | Declination Clusters | The number of members |
| Aspects | Declination Clusters | How many of the members' pairs form a declination aspect |

### Out of Bounds

A body is out of bounds when the absolute value of its declination exceeds the chart's own true obliquity of the
ecliptic (`ChartResult.Obliquity`: nutation included, at the chart's Julian Day; a composite body has no declination
and is never flagged, ruling S-D4). Equal is in bounds. The Sun is never out of bounds: the bounds are its own greatest declination. Out-of-Bounds
Planets lists each body out of bounds with its Excess, or says "No planets are out-of-bounds."

### Parallels and Contra-Parallels

A pair forms one declination aspect at most, by side of the equator; a body at exactly 0° counts as north. A parallel
needs both bodies on the same side and |a − b| ≤ 1°; a contra-parallel needs opposite sides and |a + b| ≤ 1°. That
difference or sum is the orb, and the table lists the aspects by orb, closest first. The cross-chart method applies
the same rule and orb between two charts: synastry's Inter-Chart Declination Aspects, the three returns'
Return-to-Natal Declination Aspects and the electional report's Candidate-to-Natal Declination Aspects (columns: each
chart's body and declination, Aspect, Orb). The transit report's Transit-to-Natal Declination Aspects apply the same
rule, one row per pair, since branch D (1.3.4).

### Clusters

Three or more bodies whose absolute declinations, north and south together, lie within a span of 2° or less. A window
runs from the absolute declination s of a body not yet in a cluster to s + 2°, both ends included. The window with the
most members is chosen; on a tie, the narrower span, then the lower start. A window of three or more is a cluster and
its bodies leave the pool, and the choice repeats until no window of three is left. Clusters are listed in the order
chosen, largest first; the table appears only when there is one.

### Chart Types

- **Natal, event and horary charts, and every export that embeds a full chart:** the OOB column, Out-of-Bounds
  Planets, Declination Aspects and, when there is one, Declination Clusters, for each chart printed.
- **Two charts:** synastry, the three returns and the electional report add the cross-chart table above; the
  electional report's natal summary carries the OOB column.
- **Composite:** a composite body is a midpoint, not a point in the sky (ruling S-D4; branch L2, 1.3.4): the
  composite export prints no Dec or OOB column, no Equatorial Coordinates, Out-of-Bounds Planets, Declination
  Aspects or Declination Clusters, and its header's "Not shown" line, after the Method line, says so. Until 1.3.4 it
  printed declinations that were the means of the two charts' values, not the composite point's, with the OOB
  column, Out-of-Bounds Planets and clusters read from them, and an always-empty Declination Aspects section.

### Interpretation Guidance

- **Out of bounds:** the body has passed the Sun's own greatest declination; the Excess says by how much. Read the flag
  from the export, not against a fixed 23.44°: the bounds are the chart's own obliquity.
- **A parallel is read like a conjunction and a contra-parallel like an opposition,** weighed by orb within the ±1°
  the engine uses.
- **One aspect per pair:** never read a pair as both parallel and contra-parallel.
- **A cluster** groups bodies at like declination, north and south together; its Aspects count says how many of its
  pairs also form a declination aspect.
- **In a composite,** do not interpret declinations, out-of-bounds flags or clusters (S-D4).

---

## 13. Midpoints and Midpoint Activations

**Location in JSON:** `midpointActivations[]` (keys: `midpoint`, `midpointLongitude`, `activator`, `activatorLongitude`,
`harmonicAngle`, `orb`, `notation`). The transit activations are in the Markdown only.
**Location in Markdown:** Midpoint Activations (45 Dial) (columns: Midpoint, Activator, Harmonic, Orb), in every export
that embeds a full chart; the transit report's Transit Midpoint Activations (45° Dial), one subsection per activation,
headed like `TR Saturn = NA Sun/ASC`, with "(priority)" when the pair holds the natal Sun, Moon, ASC or MC.
**Source:** `MidpointService` (the chart), `TransitMidpointScanner` and `TransitService` (the transits).

### The Chart's Activations (Ebertin, COSI)

Every pair of the twelve bodies and the ASC and MC has a midpoint, on the shorter arc. A point activates a midpoint
when it lies 0°, 45°, 90°, 135° or 180° from the midpoint axis, within ±1.5° at 0° and 180° and ±1° at 45°, 90° and
135°; neither body of the pair activates its own midpoint. The table lists the activations by orb, closest first. Its
Harmonic column reads "direct" for 0° and the angle otherwise, its Orb is truncated D°MM′ (O-D3), and the notation is
COSI's, `A/B = C`.

### Transit Activations (branch D, 1.3.4; rulings T-D3, T-D4, T-D5)

- The transiting bodies are the transit report's (§16): Jupiter to Pluto, Chiron, the North Node, and Mars when it
  passes the report's Mars gate.
- A natal midpoint's position m on the 45° dial stands for the eight longitudes m + 45k. An activation is one passage
  of the body over one of them, within 10′ (`TransitOrbs.MidpointOrb`); in-orb periods less than 365 days apart are
  one passage.
- Each lists the transit planet and the midpoint on the dial, the orb at report start, the entry, every exact pass and
  the exit, each marked "(before report period)" or "(beyond report period)" where it falls outside it. The entry and
  exit are bisected to the second; the passes are found on a half-day grid and bisected.
- A Mars activation is listed only if, on a report date between its entry and exit, Mars is in orb at 12:00 UT and
  moving slower than 0.3°/day (T-D5).
- The activations print outermost planet first.

---

## 14. Parans

**Location in JSON:** `parans` (keys: `latitude`, `longitude`, `orbMinutes`, `bodyCount`, `totalCrossings`, and
`events[]` with `bodyA`, `angleA`, `bodyB`, `angleB`, `orbMinutes`, `description`); absent when the calculation failed.
**Location in Markdown:** Parans (Paranatellonta) — 4.0 min orb (columns: Body A, Angle A, Position A, Body B, Angle B,
Position B, Orb (min)), or "No paran events detected within the configured orb."
**Source:** `ParanService`, `ParanOptions` (ruling D16, kept).

A paran is two bodies on two different angles at one moment, as seen from the chart's place. From the chart's instant
the engine finds, for each of the twelve bodies, its next rising, culmination (the upper meridian, "Culminating"),
setting and anti-culmination (the lower meridian, "Anti-Culm.") within one sidereal day, at the chart's latitude and
longitude: rising and setting by the centre of the disc, with refraction at a standard 1013.25 mbar and 15 °C. A body
that does not rise or set there has no such crossing. Every pair of crossings by two bodies at two different angles
within 4 minutes of time (about 1° of right ascension) is a paran, listed once, closest first.

- **Position** is each body's ecliptic longitude at its own crossing, not at the chart's instant.
- **The orb is minutes of time,** printed to two decimals, not degrees; the one-orb format (O-D3) leaves it so.
- A relocated chart's parans are the place's (S-D6); a composite has none (S-D4).

---

## 15. Time Lords: Profections, Firdaria and Zodiacal Releasing

**Location in Markdown:** Annual Profection (with Lord's Natal Aspects, Profection Table, Summary), Firdaria (Major
Period Timeline, Sub-Periods (… Period), Summary) and Zodiacal Releasing (Spirit) (Level 1 Timeline, Level 2
Sub-Periods (…), Summary), in the natal export and every export that embeds a natal chart (transit, timeline,
synastry, medical, relocation); the three returns print them "(at Return Ingress)". Not in the JSON.
**Source:** `ProfectionAnalyzer`, `FirdariaAnalyzer`, `ZodiacalReleasingAnalyzer` (branch H, 1.3.4; rulings T-D8, T-D9,
T-D10, T6-4, T6-5, T6-6, R-D6).

### Counting the Years

The three count years by one rule (R-D6). The birth day is the local birth reading's day; anniversary N is the birth
day plus N years (29 February falls on 28 February in a common year); the age at a reference date is the greatest N
whose anniversary is on or before it. The natal export's reference is the day it is made, the profection's "as of"
date; a return's is its instant, read on the natal chart's clock. A reference day before the birth day is refused
(T6-6). Every boundary is a day, printed on the calendar in force. One exception: a solar return's profection takes
the years from the birth instant to the return rounded to the nearest whole, the year the return opens, so it can read
one more than the firdaria in the same export; a lunar or planetary return's is the year running at the return. A
relocation's three sections are the birth chart's (S-D6).

### Profections (T-D9)

The profected house is age mod 12 + 1, counted in whole signs from the Ascendant's sign; its sign is the Ascendant's
sign plus (house − 1) signs, and its lord that sign's traditional ruler. The cusps are not read.

### Firdaria (T-D8: Abu Ma'shar, al-Biruni, Bonatti)

By day: Sun 10, Venus 8, Mercury 13, Moon 9, Saturn 11, Jupiter 12, Mars 7, North Node 3, South Node 2. By night (Abu
Ma'shar): Moon 9, Saturn 11, Jupiter 12, Mars 7, Sun 10, Venus 8, Mercury 13, North Node 3, South Node 2. Each period
runs from one anniversary to another, and the 75 years repeat. A planet's period divides into seven sub-periods of a
seventh of its span in days, in Chaldean order from its lord (Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon); a
node's period is not divided, and the export reads "**Current Sub-Period:** none (a node's period is not divided)",
with no Sub-Periods table.

### Zodiacal Releasing (T-D10: Valens, *Anthology* IV; the Lot of Spirit only)

Level 1 starts at Spirit's sign, each sign running its years × 360 days from the birth day. Level 2, within the current
Level 1 period, starts at that period's sign and runs each sign's years as 30-day months in zodiacal order; after one
round of 211 months it passes to the sign opposite the Level 1 sign (the loosing of the bond), and its last
sub-period is cut at the Level 1 end. The Duration column prints whole months ("27m"). Angularity counts from
Fortune's sign: the 1st, 4th, 7th and 10th signs from it are angular, the 2nd, 5th, 8th and 11th succedent, the 3rd,
6th, 9th and 12th cadent (T6-4).

---

## 16. The Transit Report

**Location in Markdown:** the transit export: its header (Report Period, Aspects, Progressions), the natal chart in
full, then Transiting Planets (At Report Start, Transit Dignities, Stations During Report Period, Ingresses During
Report Period), Active Transits (one subsection per transit, headed like `TR PL opp NA NN`), Transit House Overlays (at
the report's midpoint date), Transit Midpoint Activations (45° Dial) (§13), Transit-to-Natal Declination Aspects (±1°
orb), Transit-to-Transit Aspects when there are any, and, when progressions are included, §17's two sections. Not in
the JSON.
**Source:** `TransitService`, `TransitAspectFinder`, `TransitIngressDetector`, `TransitStationDetector`,
`TransitMarsEvaluator`, `TransitMarkdownFormatter` (branch D, 1.3.4; rulings T-D3 option a, T-D4, T-D5 option c, D13,
T-D15).

### Transits

- **Bodies:** Jupiter, Saturn, Uranus, Neptune, Pluto, Chiron and the North Node, and Mars when it stations in the
  report or is in orb of a natal aspect moving slower than 0.3°/day (the Mars gate). **Targets:** the twelve natal
  bodies, the ASC and the MC. **Aspects:** the five majors, and with minors on the semi-sextile, semi-square,
  sesquiquintile (108°), sesquiquadrate and quincunx.
- **A transit is one passage of the body over one point** (T-D3): for a natal longitude n and an aspect a the points
  are n + a and n − a. The in-orb periods at one point less than 365 days apart are one transit, so a report may list
  two transits of one body, point and aspect. A transit is listed when the body is in orb at 12:00 UT on a report date.
- **Lifecycle:** the entry and exit are the start of its first in-orb period and the end of its last, bisected to the
  second (a walk that reaches 3653 days or the span's edge still in orb keeps noon UT); each exact pass is a sign
  change of the separation to the transit's own point, numbered 1..n within the transit, with its residual orb; every
  station of the body between entry and exit at which it is in orb is listed (T-D4). The North Node has no stations.
- **Orb at report start** and applying or separating are measured to the nearer of an aspect's two points.
- **Mars** (T-D5): a Mars transit is listed only if, on a report date between its entry and exit, Mars is in orb at
  12:00 UT and moving slower than 0.3°/day.
- **Ingresses** are dated by their instant, bisected to the second; the table prints the date. **Declination aspects**
  (D13, §12): one row per pair, by side of the equator, at each exact pass of the listed transits, before and beyond
  the report too. **Transit-to-transit aspects:** the majors between transiting bodies, the North Node aside, exact in
  the report period.

### Transit Orbs (T-D15)

| Transiting body | Conjunction, opposition | Other majors | Minors |
|-----------------|-------------------------|--------------|--------|
| Jupiter, Saturn | 2° | 1.5° | 1° |
| Mars, Uranus, Neptune, Pluto, Chiron, North Node | 1.5° | 1° | 0°45′ |

A midpoint activation's orb is 10′ (§13). Every date and time in the report is UT, labelled UTC (T-D11).

---

## 17. Secondary Progressions and Solar Arcs

**Location in Markdown:** in the transit report when progressions are included: Secondary Progressions (at the
report's midpoint date), with its Method, Progressed Date and Solar Arc lines, then Progressed Positions, Progressed
Moon, Progressed-to-Natal Aspects (major, 1° orb), Progressed Sign Changes (within 6 months) and, when there are any,
Progressed Stations (within 1 year); Solar Arc Directions (at the same date), with Directed Positions and
Solar-Arc-to-Natal Aspects (major, 1° orb). Not in the JSON.
**Source:** `ProgressionService`, `SolarArcService`, `ProgressedDateCalculator` (branch G, 1.3.4; rulings T-D1 option b,
T-D2).

- **The key** (T-D2): a day for a year of 365.25 days. The progressed instant is the natal instant plus the age in
  days, the age from the natal Julian Day to the report's midpoint day at 12:00 UT. The solar arc is the progressed Sun
  minus the natal Sun.
- **The angles** (T-D1 option b): the progressed Midheaven is the natal Midheaven plus the solar arc; the progressed
  Ascendant is the Ascendant for that Midheaven at the birth latitude with the natal true obliquity. Solar Arc
  Directions direct every natal point by the arc, the Ascendant included.
- **So P MC and the solar-arc MC are one point,** and an aspect of one is the same contact as the other's; P ASC and
  SA ASC differ (J2000 NYC at 2026-04-15: P ASC 02°50′ Aquarius, SA ASC 01°00′ Aquarius).
- **Aspects:** the five majors within 1°. Applying or separating comes from the speeds: a natal point is still, the
  progressed Midheaven moves at the progressed Sun's speed, the Ascendant at that times dASC/dMC, a solar-arc point at
  the arc's rate.
- **Dates:** every progressed event is dated by its instant, the midpoint date at 12:00 UT plus the progressed offset
  × 365.25 real days, and prints that instant's UT date. The column header still reads "Approximate Date", by
  decision; the date is exact.
- **Windows:** the progressed Moon's next sign change up to 90 real days ahead; the other bodies' and the angles' sign
  changes within six months either side; the stations of Mercury, Venus and Mars within a year either side.

---

## 18. The Transit Timeline

**Location in Markdown:** the timeline export: the natal chart in full, then Transit Windows (one subsection per
transit, with its entry, stations in orb, exact passes and exit, its duration and its natal and transit houses), Events
(columns: Date, Transit, Event, Position, Orb) and Clusters (one subsection per cluster, "{first} to {last} — N
events", with its events and recommended dates). Not in the JSON.
**Source:** `TransitTimelineMarkdownFormatter`, over `TransitService`'s transits (branch D, 1.3.4).

- **The transits are the transit report's** (§16): one passage of a body over one point, its entry, passes and exit
  bisected to the second and its in-orb stations listed; each line outside the report is marked "(before report
  period)" or "(beyond report period)".
- **Events** lists every event of each listed transit, inside the report and outside it, with the declination aspects
  by date; the orb at an entry or exit is the orb limit. An ingress sorts at 12:00 of its date.
- **A cluster** is a run of in-period events each within 3 days (`ClusterWindowDays`) of the cluster's first event;
  the first event outside the window starts a new cluster, and a cluster of one is dropped. Its recommended dates are
  the first three distinct dates of an exact pass or an entry in it, any body.
- Times print to the minute, UT, labelled UTC (T-D11).

---

## 19. The Planetary Return

**Location in Markdown:** the planetary return export: Return Details (Planet, Return Number with the age, Natal and
Return longitude, Precision, Return Date/Time, Other passes, Return Location), the time lords "(at Return Ingress)"
(§15), the Return Chart in full, and Return-to-Natal Declination Aspects (±1° orb) (§12). Not in the JSON.
**Source:** `ReturnPassageFinder`, `PlanetaryReturnService`, `PlanetaryReturnMarkdownFormatter` (branch F, 1.3.4;
ruling T-D6).

- **A return is a passage of the planet over its natal longitude, and its instant is the passage's first pass**
  (T-D6). After birth, a direct crossing begins a passage; a retrograde crossing, and the direct one after it, belong
  to the passage in progress, so a passage has one pass or three. The birth's own passage is not a return.
- **Return #N** is the Nth passage after birth, for each of Mercury, Venus, Mars, Jupiter and Saturn; Mercury and
  Venus return about once a year as seen from the Earth. **By year**, the return is the first passage with a pass, any
  of its passes, whose UT date lies in the year.
- The chart is cast at the first pass, at the return location. The other passes print on the Other passes line,
  "none" or each with its UT time and direction.
- The age is whole years from the birth instant to the return's, both UTC.
- J2000 NYC: Saturn #1 is 2028-07-21 08:08:04 UTC (age 28), its other passes 2028-09-24 18:23:07 UTC (retrograde) and
  2029-04-01 21:20:28 UTC (direct).

---

## 20. The Planetary Cycle Timeline

**Location in Markdown:** the cycle timeline export, "{Planet} Cycle Timeline: {name}, Return #N → #N+1": its header
(Natal planet, Cycle Duration, Cycle Period), Phase Points (columns: Phase, Angle, Target, Date, Days, Cycle %,
Passes), Retrograde Passes when a point has more than one pass, and Phase Snapshots when they are asked for. Not in
the JSON.
**Source:** `PlanetaryCycleTimelineService`, `PlanetaryCycleTimelineFormatter` (branch F, 1.3.4; ruling T-D6).

- The cycle runs from return #N's first pass to return #N+1's (§19); its two return rows carry their passage's pass
  count and passes.
- The phase points are the Return (0°), Opening Square (90°), Opposition (180°), Closing Square (270°) and Next
  Return (360°): the planet's crossings of the natal longitude plus the angle within the cycle. Each takes its first
  pass, with the others listed; a point for which no pass is found is placed at its estimate, with 0 passes.
- The cycle for a date is return #N where N is the number of passages whose first pass is at or before the date; a
  date before the first return is refused, naming it.
- Days and Cycle % count from the cycle's start. The dates are UT dates, with no label (T-D11).

---

## 21. The Return and Ingress Timeline

**Location in Markdown:** "Return & Ingress Timeline: {name}, {years}": its header (Period, Events Found, Return
Planets, Ingress Planets), Timeline (columns: #, Date (UTC), Event, Planet, Position, Label) and Summary by Planet
(Returns: Planet, Natal Position, Count, Next Return; Ingresses: Planet, Sign Changes, Details). Not in the JSON.
**Source:** `ReturnIngressTimelineService`, `ReturnIngressTimelineFormatter` (branches A, B and F; ruling T-D6).

- **The window** runs from 1 January 00:00 UT of its first year to 1 January 00:00 UT after its last, exclusive.
- **Returns** of Mercury, Venus, Mars, Jupiter and Saturn: one entry per passage whose first pass lies in the window,
  at the first pass, labelled "Return #N" or "Return #N (first of 3 passes)", N counted from birth (T-D6).
- **Ingresses** of Jupiter, Saturn, Uranus, Neptune, Pluto and Chiron: each crossing of a sign boundary in the window,
  at its instant. A boundary crossed moving forward enters the sign beginning there; crossed retrograde, the sign
  ending there.
- The Summary's Next Return is the first return in the window.
- J2000 NYC, 2026–2027: Mercury #26, #27 and #28, Venus #27 and #28, Mars #14.

---

## 22. The Composite Chart

**Location in Markdown:** "Composite Chart: {A} & {B}": its header (Method, Not shown, a Note for each of Mercury and
Venus moved to the Sun's side, Reference Location), Composite Chart (the chart, without the sections the Not shown line
names), and Source Chart A and Source Chart B (each one's Planetary Positions and House Cusps). Not in the JSON.
**Source:** `CompositeService`, `CompositeMarkdownFormatter`, `ChartMarkdownFormatter` (branches L2 and L3, 1.3.4;
rulings S-D2, S-D3, S-D4, Q1, Q10).

- **A composite body is a midpoint, not a point in the sky** (S-D4). Each body takes its short-arc midpoint; then
  Mercury and Venus each take the far-side midpoint (the short-arc one + 180°) when it is nearer the composite Sun
  (S-D2), and a Note names the move ("Venus takes the midpoint nearer the composite Sun, 00°13′17″ Aries; the
  short-arc midpoint, 00°13′17″ Libra, lies opposite."). Latitude, distance and speeds are the means.
- **The angles** (S-D3, Q1): the MC is the short-arc midpoint of the two Midheavens; the Ascendant and cusps are cast
  for it at the mean of the two birth latitudes, in the house system both charts share, else Placidus. So A & B and
  B & A are one composite. The Reference Location's longitude is the mean of the two, a label.
- **Printed:** positions (Body, Position, Lat, House, Speed, Dir), dignities, the Moon's phase, sign and via combusta,
  the chart context with day or night, the Planetary Sect table and the triplicity tier, the houses, the aspects
  (Body 1, Aspect, Body 2, Orb), midpoints, fixed stars and the medical correspondences, without a Temperament table.
- **Not shown:** declinations, right ascensions, out-of-bounds, declination aspects, stations, planet conditions,
  applying/separating marks, the Moon's next aspect and void of course.

### What Not to Read Into It

- Do not interpret declinations, out-of-bounds flags, applying marks, stations or a void-of-course Moon for a
  composite: it has none (S-D4).

---

## 23. Relocation

**Location in Markdown:** "Relocation: {name}": its header (Birth Location, Relocation Target, Birth Date/Time, UTC
Moment, Julian Day), Angle Changes (columns: Angle, Natal, Relocated, Changed; then the chart ruler's change), House
Changes (columns: Planet, Natal House, Relocated House, Note) and Relocated Chart, the chart in full, titled "{name} —
Relocated to {place}". Not in the JSON.
**Source:** `IChartEngine.CalculateRelocated`, `RelocationService`, `RelocationMarkdownFormatter` (branch K2, 1.3.4;
rulings S-D6, Q3, Q11).

- **The relocated chart is the natal chart's input with only the place changed,** cast through the same pipeline, so
  it is the same instant and Julian Day, and its Date/Time line prints the birth clock (S-D6).
- **Follow the place:** the angles, cusps, houses and placements, altitudes, aspects to the angles, angular planets,
  Lots and parans, the chart ruler, the mutual receptions and the Ascendant's triplicity rulers.
- **Stay the birth chart's:** day or night, the dignities, the Planetary Sect table, the planetary hour (the natal
  chart's own; none is computed at the place), the printed clock and the time-lord sections (§15). The Lots take the
  birth's day or night at the place's Ascendant. A horary chart keeps its category, and its perfection is judged at
  the place. The relocated chart prints no Temperament table.
- **House Changes** lists every planet whose house or angularity changed. A planet is angular within 5° of an angle
  (Q11): "Became angular" is angular at the place and not natally, "Left angle" the reverse, and the rows sort became
  angular, then left angle, then by name. J2000 NYC to Los Angeles: Venus became angular (ASC 1°32′), Jupiter and
  Mercury left angle.

### What Not to Read Into It

- A relocation changes neither day or night, the dignities, the planetary hour nor the time lords (S-D6): read them as
  the birth chart's.

---

## 24. Synastry Orbs

**Location in Markdown:** the synastry export's Inter-Chart Aspects (columns: A Body, Aspect, B Body, Orb, Notes); the
same orbs set the electional export's Natal Harmony table (§11).
**Source:** `SynastryOrbs`, `SynastryService` (ruling S-D5).

The orb is the wider of the two bodies' categories: luminaries (Sun, Moon), personal (Mercury, Venus, Mars), outer
(Jupiter to Pluto, Chiron, the True Node) and angles (ASC, MC).

| Aspect | Luminary | Personal | Outer | Angle |
|--------|----------|----------|-------|-------|
| Conjunction, opposition | 8° | 7° | 5° | 5° |
| Trine, square | 7° | 6° | 4° | 4° |
| Sextile | 5° | 4° | 3° | 3° |
| Semi-square, sesquiquadrate, semi-sextile, quincunx | 2° | 2° | 1.5° | 1.5° |

The Notes column reads "exact" below 10′ and "wide" above 5° (`SynastryService`). There is no applying or separating
between two charts: the comparison is static.

---

## Summary: New Data by Chart Type Priority

### Horary (Highest Priority)

1. **Radicality check** (planetary hour, §6) — report first
2. **Moon mansion** (§5) — does it favor the question topic?
3. **Combustion/besiegement** of significators (§4) — are they functional?
4. **Arabic Parts** matching the question topic (Marriage, Illness, etc.; §7)
5. **Antiscia** between significators (§8) — hidden perfection possible, if the connection is applying?
6. **Extended mutual reception** (§9) — perfection via reception (domicile, exaltation, or mixed domicile–exaltation; never triplicity)?
7. **Planetary sect** (§2) — identify the out-of-sect malefic
8. **Stations** (§10) — significator stationing direct overrides Rx debility

### Event (High Priority)

1. **Planet conditions** (§4) — combustion/cazimi of key planets
2. **Stations** near angles (§10) — crystallized themes
3. **Moon mansion** nature (§5) — qualitative texture of the event
4. **Arabic Parts** (Fortune/Spirit; §7) — what the event sets in motion
5. **Antiscia** (§8) — hidden connections between event factors
6. **Planetary sect** (§2) — the out-of-sect malefic as primary danger source
7. **Extended dignity** (triplicity/decan; §1) — nuanced strength assessment

### Electional (High Priority)

1. **Moon mansion** (§5) — must be favorable for the activity
2. **Planetary hour** (§6) — must align with the election purpose
3. **Planet conditions** (§4) — no combustion/besiegement on significators
4. **Arabic Parts** (Fortune/Spirit; §7) — should be angular
5. **Planetary sect** (§2) — benefics in sect, malefics not angular
6. **Stations** (§10) — avoid stationing retrograde on key significators
7. **Extended dignity** (§1) — seek triplicity dignity at minimum for significators

The electional score (§11) reads only part of this list: the planetary hour; combustion, not besiegement; malefics on the angles, with an out-of-sect penalty, not benefics in sect; retrograde motion, not stations; and the significator's dignity at four levels (domicile, exaltation, detriment, fall), not triplicity. It does not read the Moon's mansion or the Lots. The list is guidance for the interpretation, not the score.

### Natal (Available)

All data is computed and available. Sect analysis (§2), triplicity rulers (three life-phase rulers, §3), full 5-level dignity assessment (§1), and planet conditions (§4) are particularly valuable for natal delineation, with declination (§12), midpoints (§13), parans (§14) and the time lords (§15).

### Transits, Progressions and Timelines

1. **Transit report** (§16) — one passage of a body over one point, with its lifecycle and the transit orbs
2. **Transit midpoint activations** (§13) — the 45° dial, within 10′
3. **Progressions and solar arcs** (§17) — P MC and the solar-arc MC are one point
4. **Transit timeline** (§18) — the windows, events and clusters

### Returns and Cycles

1. **Planetary return** (§19) — the passage's first pass, its other passes printed
2. **Planetary cycle timeline** (§20) — the phase points between two returns
3. **Return and ingress timeline** (§21) — the passages and sign entries in a window

### Composite, Relocation and Synastry

1. **Composite** (§22) — midpoints, not points in the sky: no declinations, marks or void of course
2. **Relocation** (§23) — the place's angles and houses; the birth's sect, dignities, hour and time lords
3. **Synastry orbs** (§24) — by the wider of the two bodies' categories
