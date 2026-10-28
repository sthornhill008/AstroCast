using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates essential dignity for planets based on the traditional
/// domicile/exaltation/detriment/fall system. Uses fixed lookup tables
/// — no astronomical calculation required.
/// </summary>
/// <remarks>
/// <para>
/// Uses traditional (Ptolemaic) rulership assignments:
/// Sun rules Leo, Moon rules Cancer, Mercury rules Gemini and Virgo,
/// Venus rules Taurus and Libra, Mars rules Aries and Scorpio,
/// Jupiter rules Sagittarius and Pisces, Saturn rules Capricorn and Aquarius.
/// </para>
/// <para>
/// Modern outer planets (Uranus, Neptune, Pluto) do not have traditional
/// dignities — they are always peregrine in this system. Chiron and
/// True Node are also always peregrine. The dispositor is still valid
/// for all planets — it is the traditional ruler of the sign occupied.
/// </para>
/// <para>
/// Mercury in Virgo is both domicile and exalted in the traditional system.
/// Both flags are set to true; the <see cref="DignityInfo.Score"/> and
/// <see cref="DignityInfo.Label"/> properties give domicile precedence
/// as the higher dignity (+5 vs +4).
/// </para>
/// </remarks>
public sealed class DignityService : IDignityService
{
    #region Lookup Tables

    /// <summary>
    /// Traditional domicile rulership: sign → ruling planet name.
    /// </summary>
    private static readonly Dictionary<ZodiacSign, string> DomicileRulers = new()
    {
        [ZodiacSign.Aries] = "Mars",
        [ZodiacSign.Taurus] = "Venus",
        [ZodiacSign.Gemini] = "Mercury",
        [ZodiacSign.Cancer] = "Moon",
        [ZodiacSign.Leo] = "Sun",
        [ZodiacSign.Virgo] = "Mercury",
        [ZodiacSign.Libra] = "Venus",
        [ZodiacSign.Scorpio] = "Mars",
        [ZodiacSign.Sagittarius] = "Jupiter",
        [ZodiacSign.Capricorn] = "Saturn",
        [ZodiacSign.Aquarius] = "Saturn",
        [ZodiacSign.Pisces] = "Jupiter",
    };

    /// <summary>
    /// Traditional exaltation: planet name → sign of exaltation.
    /// Only the seven classical planets have exaltations.
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign> Exaltations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sun"] = ZodiacSign.Aries,
        ["Moon"] = ZodiacSign.Taurus,
        ["Mercury"] = ZodiacSign.Virgo,
        ["Venus"] = ZodiacSign.Pisces,
        ["Mars"] = ZodiacSign.Capricorn,
        ["Jupiter"] = ZodiacSign.Cancer,
        ["Saturn"] = ZodiacSign.Libra,
    };

    /// <summary>
    /// Planets that have traditional dignities (the seven classical planets).
    /// All other bodies (Uranus, Neptune, Pluto, Chiron, True Node) are always peregrine.
    /// </summary>
    private static readonly HashSet<string> ClassicalPlanets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn",
    };

    /// <summary>
    /// Dorothean triplicity rulers by element.
    /// Key: element name, Value: (DayRuler, NightRuler, ParticipatingRuler).
    /// </summary>
    private static readonly Dictionary<string, (string Day, string Night, string Participating)>
        TriplicityRulers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fire"]  = ("Sun",    "Jupiter", "Saturn"),
        ["Earth"] = ("Venus",  "Moon",    "Mars"),
        ["Air"]   = ("Saturn", "Mercury", "Jupiter"),
        ["Water"] = ("Venus",  "Mars",    "Moon"),
    };

    /// <summary>
    /// Maps each zodiac sign to its element.
    /// </summary>
    private static readonly Dictionary<ZodiacSign, string> SignElements = new()
    {
        [ZodiacSign.Aries]       = "Fire",
        [ZodiacSign.Taurus]      = "Earth",
        [ZodiacSign.Gemini]      = "Air",
        [ZodiacSign.Cancer]      = "Water",
        [ZodiacSign.Leo]         = "Fire",
        [ZodiacSign.Virgo]       = "Earth",
        [ZodiacSign.Libra]       = "Air",
        [ZodiacSign.Scorpio]     = "Water",
        [ZodiacSign.Sagittarius] = "Fire",
        [ZodiacSign.Capricorn]   = "Earth",
        [ZodiacSign.Aquarius]    = "Air",
        [ZodiacSign.Pisces]      = "Water",
    };

    /// <summary>
    /// Chaldean decan rulers for all 36 decans (3 per sign x 12 signs).
    /// The Chaldean order (Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon)
    /// cycles continuously starting from Mars for the 1st decan of Aries.
    /// Index: decan number 0-35 (sign_index * 3 + decan_within_sign).
    /// </summary>
    private static readonly string[] DecanRulers =
    [
        // Aries:       Mars,    Sun,     Venus
        "Mars", "Sun", "Venus",
        // Taurus:      Mercury, Moon,    Saturn
        "Mercury", "Moon", "Saturn",
        // Gemini:      Jupiter, Mars,    Sun
        "Jupiter", "Mars", "Sun",
        // Cancer:      Venus,   Mercury, Moon
        "Venus", "Mercury", "Moon",
        // Leo:         Saturn,  Jupiter, Mars
        "Saturn", "Jupiter", "Mars",
        // Virgo:       Sun,     Venus,   Mercury
        "Sun", "Venus", "Mercury",
        // Libra:       Moon,    Saturn,  Jupiter
        "Moon", "Saturn", "Jupiter",
        // Scorpio:     Mars,    Sun,     Venus
        "Mars", "Sun", "Venus",
        // Sagittarius: Mercury, Moon,    Saturn
        "Mercury", "Moon", "Saturn",
        // Capricorn:   Jupiter, Mars,    Sun
        "Jupiter", "Mars", "Sun",
        // Aquarius:    Venus,   Mercury, Moon
        "Venus", "Mercury", "Moon",
        // Pisces:      Saturn,  Jupiter, Mars
        "Saturn", "Jupiter", "Mars",
    ];

    /// <summary>
    /// Domicile lookup: planet name → set of signs the planet rules.
    /// Built from <see cref="DomicileRulers"/> (inverted).
    /// </summary>
    private static readonly Dictionary<string, HashSet<ZodiacSign>> PlanetDomiciles;

    /// <summary>
    /// Fall lookup: planet name → sign of fall (opposite exaltation).
    /// Built from <see cref="Exaltations"/>.
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign> Falls;

    /// <summary>
    /// Detriment lookup: planet name → set of signs where the planet is in detriment
    /// (opposite each domicile sign). Built from <see cref="PlanetDomiciles"/>.
    /// </summary>
    private static readonly Dictionary<string, HashSet<ZodiacSign>> PlanetDetriments;

    /// <summary>
    /// Static constructor: build derived lookup tables from the primary tables.
    /// </summary>
    static DignityService()
    {
        // Build PlanetDomiciles: invert DomicileRulers
        PlanetDomiciles = new Dictionary<string, HashSet<ZodiacSign>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ZodiacSign, string> kvp in DomicileRulers)
        {
            if (!PlanetDomiciles.TryGetValue(kvp.Value, out HashSet<ZodiacSign>? signs))
            {
                signs = [];
                PlanetDomiciles[kvp.Value] = signs;
            }
            signs.Add(kvp.Key);
        }

        // Build Falls: opposite of exaltation sign
        Falls = new Dictionary<string, ZodiacSign>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ZodiacSign> kvp in Exaltations)
        {
            Falls[kvp.Key] = OppositeSign(kvp.Value);
        }

        // Build PlanetDetriments: opposite of each domicile sign
        PlanetDetriments = new Dictionary<string, HashSet<ZodiacSign>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HashSet<ZodiacSign>> kvp in PlanetDomiciles)
        {
            HashSet<ZodiacSign> detrimentSigns = [];
            foreach (ZodiacSign sign in kvp.Value)
            {
                detrimentSigns.Add(OppositeSign(sign));
            }
            PlanetDetriments[kvp.Key] = detrimentSigns;
        }
    }

    #endregion

    /// <inheritdoc />
    public DignityInfo GetDignity(string planetName, ZodiacSign sign)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planetName);

        string ruler = GetSignRuler(sign);
        bool isClassical = ClassicalPlanets.Contains(planetName);

        bool isDomicile = isClassical && IsPlanetRulerOf(planetName, sign);
        bool isExalted = isClassical && IsPlanetExaltedIn(planetName, sign);
        bool isDetriment = isClassical && IsPlanetInDetrimentIn(planetName, sign);
        bool isFall = isClassical && IsPlanetInFallIn(planetName, sign);

        return new DignityInfo
        {
            PlanetName = planetName,
            Sign = sign,
            IsDomicile = isDomicile,
            IsExalted = isExalted,
            IsInDetriment = isDetriment,
            IsInFall = isFall,
            Dispositor = ruler,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<DignityInfo> GetDignities(IReadOnlyList<PlanetPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        return positions.Select(p => GetDignity(p.Name, p.Sign)).ToList();
    }

    /// <inheritdoc />
    public string GetSignRuler(ZodiacSign sign) => sign switch
    {
        ZodiacSign.Aries => "Mars",
        ZodiacSign.Taurus => "Venus",
        ZodiacSign.Gemini => "Mercury",
        ZodiacSign.Cancer => "Moon",
        ZodiacSign.Leo => "Sun",
        ZodiacSign.Virgo => "Mercury",
        ZodiacSign.Libra => "Venus",
        ZodiacSign.Scorpio => "Mars",
        ZodiacSign.Sagittarius => "Jupiter",
        ZodiacSign.Capricorn => "Saturn",
        ZodiacSign.Aquarius => "Saturn",
        ZodiacSign.Pisces => "Jupiter",
        _ => throw new ArgumentOutOfRangeException(nameof(sign), sign, "Unknown zodiac sign."),
    };

    /// <inheritdoc />
    public TriplicityRulerInfo GetTriplicityRulers(ZodiacSign sign, bool isDayChart)
    {
        string element = GetElement(sign);
        (string day, string night, string participating) = TriplicityRulers[element];

        return new TriplicityRulerInfo
        {
            Element = element,
            DayRuler = day,
            NightRuler = night,
            ParticipatingRuler = participating,
            ActiveRuler = isDayChart ? day : night,
        };
    }

    /// <inheritdoc />
    public string GetDecanRuler(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        int signIndex = (int)(normalized / 30.0);
        if (signIndex >= 12) signIndex = 0;

        int degreesInSign = (int)(normalized % 30.0);
        int decanWithinSign = degreesInSign / 10; // 0, 1, or 2
        int decanIndex = signIndex * 3 + decanWithinSign;

        return DecanRulers[decanIndex];
    }

    /// <inheritdoc />
    public string GetElement(ZodiacSign sign) =>
        SignElements.TryGetValue(sign, out string? element)
            ? element
            : throw new ArgumentOutOfRangeException(nameof(sign), sign, "Unknown zodiac sign.");

    /// <inheritdoc />
    public string GetActiveTriplicityRuler(ZodiacSign sign, bool isDayChart)
    {
        string element = GetElement(sign);
        (string day, string night, _) = TriplicityRulers[element];
        return isDayChart ? day : night;
    }

    /// <summary>
    /// Calculate the essential dignity for a planet with triplicity and decan
    /// analysis (requires chart sect and exact longitude).
    /// </summary>
    /// <param name="planetName">The planet name.</param>
    /// <param name="sign">The zodiac sign the planet occupies.</param>
    /// <param name="longitude">The planet's ecliptic longitude for decan calculation.</param>
    /// <param name="isDayChart">True for day charts (triplicity ruler selection).</param>
    /// <returns>Dignity information including triplicity and decan status.</returns>
    public DignityInfo GetDignityExtended(string planetName, ZodiacSign sign, double longitude, bool isDayChart)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planetName);

        string ruler = GetSignRuler(sign);
        bool isClassical = ClassicalPlanets.Contains(planetName);

        bool isDomicile = isClassical && IsPlanetRulerOf(planetName, sign);
        bool isExalted = isClassical && IsPlanetExaltedIn(planetName, sign);
        bool isDetriment = isClassical && IsPlanetInDetrimentIn(planetName, sign);
        bool isFall = isClassical && IsPlanetInFallIn(planetName, sign);

        // Triplicity
        string activeTriplicityRuler = GetActiveTriplicityRuler(sign, isDayChart);
        bool isTriplicityRuler = isClassical
            && string.Equals(planetName, activeTriplicityRuler, StringComparison.OrdinalIgnoreCase);

        // Decan
        string decanRuler = GetDecanRuler(longitude);
        bool isInOwnDecan = isClassical
            && string.Equals(planetName, decanRuler, StringComparison.OrdinalIgnoreCase);

        return new DignityInfo
        {
            PlanetName = planetName,
            Sign = sign,
            IsDomicile = isDomicile,
            IsExalted = isExalted,
            IsInDetriment = isDetriment,
            IsInFall = isFall,
            Dispositor = ruler,
            TriplicityRuler = activeTriplicityRuler,
            IsTriplicityRuler = isTriplicityRuler,
            DecanRuler = decanRuler,
            IsInOwnDecan = isInOwnDecan,
        };
    }

    /// <summary>
    /// Calculate essential dignities with triplicity and decan for all planets.
    /// </summary>
    /// <param name="positions">The planetary positions.</param>
    /// <param name="isDayChart">True for day charts.</param>
    /// <returns>Extended dignity information for each planet.</returns>
    public IReadOnlyList<DignityInfo> GetDignitiesExtended(
        IReadOnlyList<PlanetPosition> positions, bool isDayChart)
    {
        ArgumentNullException.ThrowIfNull(positions);
        return positions.Select(p => GetDignityExtended(p.Name, p.Sign, p.Longitude, isDayChart)).ToList();
    }

    #region Private Helpers

    /// <summary>
    /// Returns the zodiac sign opposite the given sign (6 signs away).
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>The opposite zodiac sign.</returns>
    private static ZodiacSign OppositeSign(ZodiacSign sign)
    {
        // ZodiacSign values are 1-12. Opposite = ((value - 1 + 6) % 12) + 1
        int value = (int)sign;
        int opposite = ((value - 1 + 6) % 12) + 1;
        return (ZodiacSign)opposite;
    }

    /// <summary>
    /// Returns true if the planet rules the given sign (domicile).
    /// </summary>
    private static bool IsPlanetRulerOf(string planetName, ZodiacSign sign) =>
        PlanetDomiciles.TryGetValue(planetName, out HashSet<ZodiacSign>? signs) && signs.Contains(sign);

    /// <summary>
    /// Returns true if the planet is exalted in the given sign.
    /// </summary>
    private static bool IsPlanetExaltedIn(string planetName, ZodiacSign sign) =>
        Exaltations.TryGetValue(planetName, out ZodiacSign exaltSign) && exaltSign == sign;

    /// <summary>
    /// Returns true if the planet is in detriment in the given sign.
    /// </summary>
    private static bool IsPlanetInDetrimentIn(string planetName, ZodiacSign sign) =>
        PlanetDetriments.TryGetValue(planetName, out HashSet<ZodiacSign>? signs) && signs.Contains(sign);

    /// <summary>
    /// Returns true if the planet is in fall in the given sign.
    /// </summary>
    private static bool IsPlanetInFallIn(string planetName, ZodiacSign sign) =>
        Falls.TryGetValue(planetName, out ZodiacSign fallSign) && fallSign == sign;

    #endregion
}
