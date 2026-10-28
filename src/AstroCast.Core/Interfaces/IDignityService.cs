using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for calculating essential dignity information for planets.
/// Uses the traditional domicile/exaltation/detriment/fall system
/// based on fixed zodiac sign lookup tables.
/// </summary>
public interface IDignityService
{
    /// <summary>
    /// Calculate the essential dignity for a single planet in a given sign.
    /// </summary>
    /// <param name="planetName">The planet name (e.g., "Sun", "Moon", "Mars").</param>
    /// <param name="sign">The zodiac sign the planet occupies.</param>
    /// <returns>Dignity information including status, score, and dispositor.</returns>
    DignityInfo GetDignity(string planetName, ZodiacSign sign);

    /// <summary>
    /// Calculate essential dignities for all planets in a chart.
    /// </summary>
    /// <param name="positions">The planetary positions from a chart calculation.</param>
    /// <returns>Dignity information for each planet, in the same order as the input.</returns>
    IReadOnlyList<DignityInfo> GetDignities(IReadOnlyList<PlanetPosition> positions);

    /// <summary>
    /// Get the traditional ruler (domicile lord) of a zodiac sign.
    /// Uses traditional rulership: Saturn rules Aquarius, Mars rules Scorpio, etc.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>The name of the ruling planet.</returns>
    string GetSignRuler(ZodiacSign sign);

    /// <summary>
    /// Get the triplicity rulers for a zodiac sign's element using the Dorothean system.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <param name="isDayChart">True for day chart (selects day ruler as active).</param>
    /// <returns>Complete triplicity ruler information for the sign's element.</returns>
    TriplicityRulerInfo GetTriplicityRulers(ZodiacSign sign, bool isDayChart);

    /// <summary>
    /// Get the Chaldean decan (face) ruler at a given ecliptic longitude.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees (0-360).</param>
    /// <returns>Name of the decan ruler planet.</returns>
    string GetDecanRuler(double longitude);

    /// <summary>
    /// Get the element of a zodiac sign: "Fire", "Earth", "Air", or "Water".
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>Element name.</returns>
    string GetElement(ZodiacSign sign);

    /// <summary>
    /// Get the active (sect-appropriate) triplicity ruler for a sign.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <param name="isDayChart">True for day chart.</param>
    /// <returns>Name of the active triplicity ruler planet.</returns>
    string GetActiveTriplicityRuler(ZodiacSign sign, bool isDayChart);

    /// <summary>
    /// Calculate essential dignity with triplicity and decan analysis.
    /// </summary>
    /// <param name="planetName">Planet name.</param>
    /// <param name="sign">Zodiac sign occupied.</param>
    /// <param name="longitude">Ecliptic longitude for decan calculation.</param>
    /// <param name="isDayChart">True for day chart.</param>
    /// <returns>Extended dignity information.</returns>
    DignityInfo GetDignityExtended(string planetName, ZodiacSign sign, double longitude, bool isDayChart);

    /// <summary>
    /// Calculate extended dignities for all planets in a chart.
    /// </summary>
    /// <param name="positions">Planetary positions.</param>
    /// <param name="isDayChart">True for day chart.</param>
    /// <returns>Extended dignity information for each planet.</returns>
    IReadOnlyList<DignityInfo> GetDignitiesExtended(IReadOnlyList<PlanetPosition> positions, bool isDayChart);
}
