namespace AstroCast.Core.Enums;

/// <summary>
/// Supported house systems for astrological chart calculation.
/// The character value corresponds to the Swiss Ephemeris <c>swe_houses()</c>
/// house system parameter.
/// </summary>
/// <remarks>
/// The default is <see cref="Placidus"/>, which is the most popular system
/// in modern Western astrology. For unknown birth times or extreme latitudes
/// (above ~66°), use <see cref="Equal"/> or <see cref="WholeSign"/>.
/// </remarks>
public enum HouseSystem
{
    /// <summary>Placidus — time-based quadrant division. Most popular modern Western system.</summary>
    Placidus = 'P',

    /// <summary>Koch — time-based quadrant division. Popular alternative to Placidus.</summary>
    Koch = 'K',

    /// <summary>Equal (from Ascendant) — each house is exactly 30°. Best for unknown birth times and extreme latitudes.</summary>
    Equal = 'E',

    /// <summary>Whole Sign — each sign is one house. Used in Hellenistic/traditional astrology.</summary>
    WholeSign = 'W',

    /// <summary>Regiomontanus — space-based. William Lilly's system. Traditional for horary.</summary>
    Regiomontanus = 'R',

    /// <summary>Campanus — prime vertical division. Space-based.</summary>
    Campanus = 'C',

    /// <summary>Alcabitius — semi-arc division. Medieval tradition.</summary>
    Alcabitius = 'B',

    /// <summary>Porphyry — trisection of quadrants. Oldest quadrant system.</summary>
    Porphyry = 'O',

    /// <summary>Morinus — equator-based. No latitude limitations.</summary>
    Morinus = 'M',
}
