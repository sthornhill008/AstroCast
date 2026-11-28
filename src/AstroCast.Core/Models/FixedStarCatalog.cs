namespace AstroCast.Core.Models;

/// <summary>
/// Static catalog of 55 fixed stars used for conjunction detection.
/// Includes the 15 Behenian stars, 4 Royal Stars, and additional stars
/// with established interpretive traditions in Robson, Brady, and classical sources.
/// </summary>
/// <remarks>
/// Star names are matched against the Swiss Ephemeris sefstars.txt file.
/// The <see cref="FixedStarEntry.SwissEphName"/> field contains the exact name
/// to pass to <c>swe_fixstar2_ut()</c>. Some stars require alternate names
/// that differ from their common display names.
/// </remarks>
public static class FixedStarCatalog
{
    /// <summary>
    /// The complete catalog of 55 unique fixed stars, ordered by approximate
    /// ecliptic longitude (J2000.0). Actual positions for any chart date are
    /// calculated by the Swiss Ephemeris with proper precession correction.
    /// </summary>
    public static IReadOnlyList<FixedStarEntry> Stars { get; } = CreateCatalog();

    /// <summary>
    /// Default conjunction orb in degrees (1 degree 00 arc-minutes).
    /// Applied to ecliptic longitude only, per Robson's traditional standard.
    /// </summary>
    public const double DefaultOrb = 1.0;

    /// <summary>
    /// Where the catalog's natures come from, in the words of <see cref="FixedStarEntry.Nature"/>'s own comment
    /// (branch DirectionNotes, 1.7.2; finding A5): the primary directions exports print it, with a full stop,
    /// under their table of the promissor stars' natures.
    /// </summary>
    public const string NatureSource = "Ptolemaic planetary nature from Robson/classical sources";

    /// <summary>
    /// Chart point names in sort-priority order for output ordering.
    /// ASC and MC first, then planets in traditional order, then nodes and Chiron.
    /// </summary>
    public static IReadOnlyList<string> ChartPointSortOrder { get; } =
    [
        "ASC", "MC", "DSC", "IC",
        "Sun", "Moon", "Mercury", "Venus", "Mars",
        "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto",
        "True Node", "Chiron"
    ];

    /// <summary>
    /// Gets the sort-priority index for a chart point name.
    /// Returns 99 for unrecognized names (sorted last).
    /// </summary>
    /// <param name="chartPointName">Name of the chart point.</param>
    /// <returns>Sort-priority index (lower = higher priority).</returns>
    public static int GetChartPointSortIndex(string chartPointName)
    {
        for (int i = 0; i < ChartPointSortOrder.Count; i++)
        {
            if (ChartPointSortOrder[i] == chartPointName)
                return i;
        }
        return 99;
    }

    /// <summary>
    /// Builds the 55-star catalog. Stars are ordered by approximate ecliptic longitude.
    /// </summary>
    private static FixedStarEntry[] CreateCatalog() =>
    [
        // #1 — Difda (Diphda, beta Ceti), ~02 Ari 35'. Not "Deneb Kaitos": sefstars.txt gives that name to iota Ceti.
        new FixedStarEntry
        {
            DisplayName = "Difda",
            SwissEphName = "Difda",
            Constellation = "Cetus",
            Magnitude = 2.04,
            Nature = "Saturn",
            Keywords = "Compulsive self-destruction, inhibition, restraint",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #2 — Mirach, ~00 Tau 24'
        new FixedStarEntry
        {
            DisplayName = "Mirach",
            SwissEphName = "Mirach",
            Constellation = "Andromeda",
            Magnitude = 2.07,
            Nature = "Venus",
            Keywords = "Happiness in marriage, beauty, artistic talent",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #3 — Sharatan, ~03 Tau 58'
        new FixedStarEntry
        {
            DisplayName = "Sharatan",
            SwissEphName = "Sheratan",
            Constellation = "Aries",
            Magnitude = 2.64,
            Nature = "Mars-Saturn",
            Keywords = "Violence, defeat, injury, danger",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #4 — Hamal, ~07 Tau 40'
        new FixedStarEntry
        {
            DisplayName = "Hamal",
            SwissEphName = "Hamal",
            Constellation = "Aries",
            Magnitude = 2.01,
            Nature = "Mars-Saturn",
            Keywords = "Independence, cruelty, premeditation, violence",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #5 — Almach (Alamak), ~14 Tau 14'
        new FixedStarEntry
        {
            DisplayName = "Almach",
            SwissEphName = "Almach",
            Constellation = "Andromeda",
            Magnitude = 2.10,
            Nature = "Venus",
            Keywords = "Eminence, artistic ability, honors",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #6 — Algol, ~26 Tau 10' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Algol",
            SwissEphName = "Algol",
            Constellation = "Perseus",
            Magnitude = 2.09,
            Nature = "Saturn-Jupiter",
            Keywords = "Misfortune, violence, decapitation, losing one's head",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #7 — Alcyone (Pleiades), ~00 Gem 00' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Alcyone",
            SwissEphName = "Alcyone",
            Constellation = "Taurus",
            Magnitude = 2.85,
            Nature = "Moon-Mars",
            Keywords = "Ambition, disgrace, sorrow, blindness",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #8 — Hyades (Prima Hyadum), ~05 Gem 48'
        new FixedStarEntry
        {
            DisplayName = "Hyades",
            SwissEphName = "Prima Hyadum",
            Constellation = "Taurus",
            Magnitude = 3.65,
            Nature = "Saturn-Mercury",
            Keywords = "Tears, sudden events, violence, scandal",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #9 — Aldebaran, ~09 Gem 47' (Royal Star, Behenian)
        new FixedStarEntry
        {
            DisplayName = "Aldebaran",
            SwissEphName = "Aldebaran",
            Constellation = "Taurus",
            Magnitude = 0.87,
            Nature = "Mars",
            Keywords = "Royal star: integrity leads to success; corruption leads to downfall",
            IsRoyalStar = true,
            IsBehenianStar = true
        },
        // #10 — Rigel, ~16 Gem 50'
        new FixedStarEntry
        {
            DisplayName = "Rigel",
            SwissEphName = "Rigel",
            Constellation = "Orion",
            Magnitude = 0.18,
            Nature = "Jupiter-Saturn",
            Keywords = "Benevolence, honors, mechanical ability, glory",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #11 — Bellatrix, ~20 Gem 57'
        new FixedStarEntry
        {
            DisplayName = "Bellatrix",
            SwissEphName = "Bellatrix",
            Constellation = "Orion",
            Magnitude = 1.64,
            Nature = "Mars-Mercury",
            Keywords = "Quick decision, rash actions, great civil/military honors",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #12 — Capella, ~21 Gem 51' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Capella",
            SwissEphName = "Capella",
            Constellation = "Auriga",
            Magnitude = 0.08,
            Nature = "Mercury-Mars",
            Keywords = "Honors, wealth, eminence, curiosity, love of learning",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #13 — Alnilam, ~23 Gem 28'
        new FixedStarEntry
        {
            DisplayName = "Alnilam",
            SwissEphName = "Alnilam",
            Constellation = "Orion",
            Magnitude = 1.69,
            Nature = "Jupiter-Saturn",
            Keywords = "Brief fame, quick gain then loss",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #14 — Polaris, ~28 Gem 34'
        new FixedStarEntry
        {
            DisplayName = "Polaris",
            SwissEphName = "Polaris",
            Constellation = "Ursa Minor",
            Magnitude = 1.97,
            Nature = "Saturn-Venus",
            Keywords = "Sickness, trouble, loss, affliction, spiritual gifts",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #15 — Betelgeuse, ~28 Gem 45'
        new FixedStarEntry
        {
            DisplayName = "Betelgeuse",
            SwissEphName = "Betelgeuse",
            Constellation = "Orion",
            Magnitude = 0.45,
            Nature = "Mars-Mercury",
            Keywords = "Everlasting fame, honors, fortune, wealth, military success",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #16 — Sirius, ~14 Can 05' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Sirius",
            SwissEphName = "Sirius",
            Constellation = "Canis Major",
            Magnitude = -1.46,
            Nature = "Jupiter-Mars",
            Keywords = "Ambition, pride, fame, custodian, leadership, burning",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #17 — Canopus, ~14 Can 58'
        new FixedStarEntry
        {
            DisplayName = "Canopus",
            SwissEphName = "Canopus",
            Constellation = "Carina",
            Magnitude = -0.72,
            Nature = "Saturn-Jupiter",
            Keywords = "Voyages, education, fame followed by notoriety",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #18 — Castor, ~20 Can 14'
        new FixedStarEntry
        {
            DisplayName = "Castor",
            SwissEphName = "Castor",
            Constellation = "Gemini",
            Magnitude = 1.58,
            Nature = "Mercury",
            Keywords = "Sudden fame or loss, violence, mischief, broken limbs",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #19 — Pollux, ~23 Can 13' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Pollux",
            SwissEphName = "Pollux",
            Constellation = "Gemini",
            Magnitude = 1.16,
            Nature = "Mars",
            Keywords = "Subtle, spirited, audacious, cruel, rash, disgrace",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #20 — Procyon, ~25 Can 47' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Procyon",
            SwissEphName = "Procyon",
            Constellation = "Canis Minor",
            Magnitude = 0.40,
            Nature = "Mercury-Mars",
            Keywords = "Sudden rise followed by disgrace, violence, canine bites",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #21 — Praesepe, ~07 Leo 20'
        new FixedStarEntry
        {
            DisplayName = "Praesepe",
            SwissEphName = "Praesepe Cluster",
            Constellation = "Cancer",
            Magnitude = 3.94,
            Nature = "Mars-Moon",
            Keywords = "Mass events, adventure, brutishness, blindness",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #22 — North Asellus, ~07 Leo 32'
        new FixedStarEntry
        {
            DisplayName = "North Asellus",
            SwissEphName = "Asellus Borealis",
            Constellation = "Cancer",
            Magnitude = 4.66,
            Nature = "Mars-Sun",
            Keywords = "Daring, military preferment, care and responsibility",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #23 — South Asellus, ~08 Leo 43'
        new FixedStarEntry
        {
            DisplayName = "South Asellus",
            SwissEphName = "Asellus Australis",
            Constellation = "Cancer",
            Magnitude = 3.94,
            Nature = "Mars-Sun",
            Keywords = "Military preferment, aggressive nature, fevers",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #24 — Alphard, ~27 Leo 17'
        new FixedStarEntry
        {
            DisplayName = "Alphard",
            SwissEphName = "Alphard",
            Constellation = "Hydra",
            Magnitude = 1.99,
            Nature = "Saturn-Venus",
            Keywords = "Wisdom, appreciation of art, lack of self-control, poisoning",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #25 — Regulus, ~29 Leo 50' (Royal Star, Behenian)
        new FixedStarEntry
        {
            DisplayName = "Regulus",
            SwissEphName = "Regulus",
            Constellation = "Leo",
            Magnitude = 1.36,
            Nature = "Mars-Jupiter",
            Keywords = "Royal star: nobility and success if no vengeance",
            IsRoyalStar = true,
            IsBehenianStar = true
        },
        // #26 — Zosma, ~11 Vir 19'
        new FixedStarEntry
        {
            DisplayName = "Zosma",
            SwissEphName = "Zosma",
            Constellation = "Leo",
            Magnitude = 2.56,
            Nature = "Saturn-Venus",
            Keywords = "Egotism, keen intellect, melancholy, disgrace",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #27 — Denebola, ~21 Vir 37'
        new FixedStarEntry
        {
            DisplayName = "Denebola",
            SwissEphName = "Denebola",
            Constellation = "Leo",
            Magnitude = 2.14,
            Nature = "Saturn-Venus",
            Keywords = "Swift judgment, despair, regret, misfortune",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #28 — Vindemiatrix, ~09 Lib 56'
        new FixedStarEntry
        {
            DisplayName = "Vindemiatrix",
            SwissEphName = "Vindemiatrix",
            Constellation = "Virgo",
            Magnitude = 2.83,
            Nature = "Saturn-Mercury",
            Keywords = "Falsity, disgrace, stealing, widowhood",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #29 — Algorab, ~13 Lib 27' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Algorab",
            SwissEphName = "Algorab",
            Constellation = "Corvus",
            Magnitude = 2.94,
            Nature = "Mars-Saturn",
            Keywords = "Scavenging, destructiveness, repulsiveness, lying",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #30 — Spica, ~23 Lib 50' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Spica",
            SwissEphName = "Spica",
            Constellation = "Virgo",
            Magnitude = 0.98,
            Nature = "Venus-Mars",
            Keywords = "Success, renown, riches, love of science and art",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #31 — Arcturus, ~24 Lib 14' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Arcturus",
            SwissEphName = "Arcturus",
            Constellation = "Bootes",
            Magnitude = -0.05,
            Nature = "Mars-Jupiter",
            Keywords = "Prosperity through navigation, voyages, justice, fame",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #32 — Alphecca, ~12 Sco 18' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Alphecca",
            SwissEphName = "Alphecca",
            Constellation = "Corona Borealis",
            Magnitude = 2.22,
            Nature = "Venus-Mercury",
            Keywords = "Honor, dignity, artistic ability, poetic skills",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #33 — South Scale (Zuben Elgenubi), ~15 Sco 05'
        new FixedStarEntry
        {
            DisplayName = "South Scale",
            SwissEphName = "Zuben Elgenubi",
            Constellation = "Libra",
            Magnitude = 2.75,
            Nature = "Saturn-Mars",
            Keywords = "Malevolence, obstruction, unforgiving, disease",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #34 — North Scale (Zuben Eschamali), ~19 Sco 22'
        new FixedStarEntry
        {
            DisplayName = "North Scale",
            SwissEphName = "Zuben Eschamali",
            Constellation = "Libra",
            Magnitude = 2.61,
            Nature = "Jupiter-Mercury",
            Keywords = "Good fortune, ambition, honors, lasting happiness",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #35 — Unukalhai, ~22 Sco 05'
        new FixedStarEntry
        {
            DisplayName = "Unukalhai",
            SwissEphName = "Unukalhai",
            Constellation = "Serpens",
            Magnitude = 2.63,
            Nature = "Saturn-Mars",
            Keywords = "Immorality, accidents, danger of poison",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #36 — Toliman (Alpha Centauri), ~29 Sco 29'
        new FixedStarEntry
        {
            DisplayName = "Toliman",
            SwissEphName = "Toliman",
            Constellation = "Centaurus",
            Magnitude = -0.01,
            Nature = "Venus-Jupiter",
            Keywords = "Beneficent, friends, learning, self-knowledge",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #37 — Antares, ~09 Sag 46' (Royal Star, Behenian)
        new FixedStarEntry
        {
            DisplayName = "Antares",
            SwissEphName = "Antares",
            Constellation = "Scorpius",
            Magnitude = 1.06,
            Nature = "Mars-Jupiter",
            Keywords = "Royal star: success through obsession or destruction",
            IsRoyalStar = true,
            IsBehenianStar = true
        },
        // #38 — Ras Algethi, ~16 Sag 09'
        new FixedStarEntry
        {
            DisplayName = "Ras Algethi",
            SwissEphName = "Ras Algethi",
            Constellation = "Hercules",
            Magnitude = 2.78,
            Nature = "Mars",
            Keywords = "Boldness, ambition, strength, power-seeking",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #39 — Sabik, ~17 Sag 58'
        new FixedStarEntry
        {
            DisplayName = "Sabik",
            SwissEphName = "Sabik",
            Constellation = "Ophiuchus",
            Magnitude = 2.43,
            Nature = "Saturn-Venus",
            Keywords = "Wasteful, lost through friends, envious",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #40 — Ras Alhague, ~22 Sag 27'
        new FixedStarEntry
        {
            DisplayName = "Ras Alhague",
            SwissEphName = "Rasalhague",
            Constellation = "Ophiuchus",
            Magnitude = 2.08,
            Nature = "Saturn-Venus",
            Keywords = "Misfortune through women, perverted tastes, mental depravity",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #41 — Lesath, ~24 Sag 01'
        new FixedStarEntry
        {
            DisplayName = "Lesath",
            SwissEphName = "Lesath",
            Constellation = "Scorpius",
            Magnitude = 2.69,
            Nature = "Mercury-Mars",
            Keywords = "Danger, desperation, immorality",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #42 — Etamin (Eltanin), ~27 Sag 58'
        new FixedStarEntry
        {
            DisplayName = "Etamin",
            SwissEphName = "Eltanin",
            Constellation = "Draco",
            Magnitude = 2.24,
            Nature = "Mars-Moon",
            Keywords = "Pioneering, administrative, executive ability",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #43 — Vega, ~15 Cap 19' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Vega",
            SwissEphName = "Vega",
            Constellation = "Lyra",
            Magnitude = 0.03,
            Nature = "Venus-Mercury",
            Keywords = "Beneficent, idealistic, hopefulness, refined, changeable",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #44 — Altair, ~01 Aqu 47' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Altair",
            SwissEphName = "Altair",
            Constellation = "Aquila",
            Magnitude = 0.76,
            Nature = "Mars-Jupiter",
            Keywords = "Boldness, confidence, valiance, sudden but ephemeral riches",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #45 — Dorsum, ~13 Aqu 51'
        new FixedStarEntry
        {
            DisplayName = "Dorsum",
            SwissEphName = "Dorsum",
            Constellation = "Capricornus",
            Magnitude = 4.08,
            Nature = "Saturn-Jupiter",
            Keywords = "Minor influence, included for completeness",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #46 — Nashira, ~21 Aqu 47'
        new FixedStarEntry
        {
            DisplayName = "Nashira",
            SwissEphName = "Nashira",
            Constellation = "Capricornus",
            Magnitude = 3.67,
            Nature = "Saturn-Jupiter",
            Keywords = "Overcoming, success from writing and speaking",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #47 — Deneb Algedi, ~23 Aqu 33' (Behenian)
        new FixedStarEntry
        {
            DisplayName = "Deneb Algedi",
            SwissEphName = "Deneb Algedi",
            Constellation = "Capricornus",
            Magnitude = 2.85,
            Nature = "Saturn-Jupiter",
            Keywords = "Sorrow, life of changes, culminating in disgrace",
            IsRoyalStar = false,
            IsBehenianStar = true
        },
        // #48 — Sadalsuud, ~23 Aqu 24'
        new FixedStarEntry
        {
            DisplayName = "Sadalsuud",
            SwissEphName = "Sadalsuud",
            Constellation = "Aquarius",
            Magnitude = 2.90,
            Nature = "Saturn-Mercury",
            Keywords = "Trouble, disgrace, unfortunate events",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #49 — Sadalmelek, ~03 Pis 21'
        new FixedStarEntry
        {
            DisplayName = "Sadalmelek",
            SwissEphName = "Sadalmelek",
            Constellation = "Aquarius",
            Magnitude = 2.95,
            Nature = "Saturn-Mercury",
            Keywords = "Persecution, sudden destruction, the death penalty",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #50 — Fomalhaut, ~03 Pis 52' (Royal Star)
        new FixedStarEntry
        {
            DisplayName = "Fomalhaut",
            SwissEphName = "Fomalhaut",
            Constellation = "Piscis Austrinus",
            Magnitude = 1.17,
            Nature = "Venus-Mercury",
            Keywords = "Royal star: fame, great ideals if not corrupted by materialism",
            IsRoyalStar = true,
            IsBehenianStar = false
        },
        // #51 — Deneb Adige, ~05 Pis 20'
        new FixedStarEntry
        {
            DisplayName = "Deneb Adige",
            SwissEphName = "Deneb,alCyg",
            Constellation = "Cygnus",
            Magnitude = 1.25,
            Nature = "Venus-Mercury",
            Keywords = "Ingenious, studious, quick learner, artistic",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #52 — Skat, ~08 Pis 52'
        new FixedStarEntry
        {
            DisplayName = "Skat",
            SwissEphName = "Skat",
            Constellation = "Aquarius",
            Magnitude = 3.27,
            Nature = "Saturn-Jupiter",
            Keywords = "Psychic sensitivity, good fortune, lasting friendships",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #53 — Achernar, ~15 Pis 19'
        new FixedStarEntry
        {
            DisplayName = "Achernar",
            SwissEphName = "Achernar",
            Constellation = "Eridanus",
            Magnitude = 0.45,
            Nature = "Jupiter",
            Keywords = "Royal status, success in public office, religious benefits",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #54 — Markab, ~23 Pis 29'
        new FixedStarEntry
        {
            DisplayName = "Markab",
            SwissEphName = "Markab",
            Constellation = "Pegasus",
            Magnitude = 2.49,
            Nature = "Mars-Mercury",
            Keywords = "Violence, dishonor, danger from fire, fever",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
        // #55 — Scheat, ~29 Pis 22'
        new FixedStarEntry
        {
            DisplayName = "Scheat",
            SwissEphName = "Scheat",
            Constellation = "Pegasus",
            Magnitude = 2.44,
            Nature = "Mars-Mercury",
            Keywords = "Extreme misfortune, murder, suicide, drowning",
            IsRoyalStar = false,
            IsBehenianStar = false
        },
    ];
}

/// <summary>
/// Immutable record representing a fixed star in the catalog.
/// Contains the star's identity, magnitude, Ptolemaic nature, and classification flags.
/// </summary>
public sealed class FixedStarEntry
{
    /// <summary>Display name for output (e.g., "Regulus", "Difda", "South Scale").</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Name to pass to <c>swe_fixstar2_ut()</c>.
    /// May differ from <see cref="DisplayName"/> when the Swiss Ephemeris uses
    /// an alternate spelling (e.g., "Deneb Kaitos" for Difda, "Sheratan" for Sharatan).
    /// </summary>
    public required string SwissEphName { get; init; }

    /// <summary>Constellation the star belongs to (e.g., "Leo", "Orion").</summary>
    public required string Constellation { get; init; }

    /// <summary>Visual magnitude (lower = brighter). Negative values are possible for very bright stars.</summary>
    public required double Magnitude { get; init; }

    /// <summary>Ptolemaic planetary nature from Robson/classical sources (e.g., "Mars-Jupiter").</summary>
    public required string Nature { get; init; }

    /// <summary>Brief interpretive keywords from traditional sources.</summary>
    public required string Keywords { get; init; }

    /// <summary>True if this is one of the four Royal Stars (Aldebaran, Regulus, Antares, Fomalhaut).</summary>
    public required bool IsRoyalStar { get; init; }

    /// <summary>True if this is one of the 15 traditional Behenian stars.</summary>
    public required bool IsBehenianStar { get; init; }
}
