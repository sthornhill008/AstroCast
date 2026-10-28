using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Analyzes planetary sect conditions. Determines each classical planet's
/// natural sect, whether it is in sect for the chart type, its benefic/malefic
/// role in the sect scheme, and whether it achieves hayz (full sect dignity).
/// </summary>
/// <remarks>
/// <para>
/// Sect is a foundational classical concept. Diurnal planets (Sun, Jupiter,
/// Saturn) function best in day charts; nocturnal planets (Moon, Venus, Mars)
/// function best in night charts. Mercury adapts based on its position
/// relative to the Sun: a morning star (behind the Sun in zodiacal order, so
/// rising before it) = diurnal; an evening star (ahead of the Sun) = nocturnal.
/// </para>
/// <para>
/// <b>Hayz</b> requires three conditions simultaneously:
/// (1) planet is in its correct sect, (2) planet is in the correct hemisphere
/// (diurnal planet above horizon / nocturnal planet below horizon),
/// (3) planet is in a sign of matching gender (diurnal = masculine sign,
/// nocturnal = feminine sign). "Above the horizon" is the planet's true
/// altitude (finding F4-6, branch F6, 1.3.3); a composite planet, which has
/// no altitude, goes by its longitude. Until 1.3.3 it was the house number,
/// wrong for bodies with latitude in most systems and in Whole Sign and
/// Morinus for every body.
/// </para>
/// <para>
/// Masculine signs: Aries, Gemini, Leo, Libra, Sagittarius, Aquarius (odd signs).
/// Feminine signs: Taurus, Cancer, Virgo, Scorpio, Capricorn, Pisces (even signs).
/// </para>
/// </remarks>
public sealed class SectService : ISectService
{
    /// <summary>
    /// Classical planets eligible for sect analysis.
    /// </summary>
    private static readonly string[] ClassicalPlanets =
        ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn"];

    /// <summary>
    /// Houses of joy for each classical planet.
    /// </summary>
    private static readonly Dictionary<string, int> HousesOfJoy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mercury"] = 1,
        ["Moon"] = 3,
        ["Venus"] = 5,
        ["Mars"] = 6,
        ["Sun"] = 9,
        ["Jupiter"] = 11,
        ["Saturn"] = 12,
    };

    /// <inheritdoc />
    public IReadOnlyList<SectInfo> Analyze(
        IReadOnlyList<PlanetPosition> positions,
        bool isDayChart,
        HouseData? houses = null)
    {
        ArgumentNullException.ThrowIfNull(positions);

        PlanetPosition? sun = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Sun", StringComparison.OrdinalIgnoreCase));

        List<SectInfo> results = [];

        foreach (string planetName in ClassicalPlanets)
        {
            PlanetPosition? planet = positions.FirstOrDefault(
                p => string.Equals(p.Name, planetName, StringComparison.OrdinalIgnoreCase));

            if (planet is null)
                continue;

            string naturalSect = GetNaturalSect(planetName, planet, sun);
            bool isInSect = (naturalSect == "Diurnal" && isDayChart)
                         || (naturalSect == "Nocturnal" && !isDayChart);

            bool isBenefic = planetName is "Jupiter" or "Venus";
            bool isMalefic = planetName is "Mars" or "Saturn";

            // In a day chart: Saturn is in-sect malefic (less harmful),
            // Mars is out-of-sect malefic (most harmful). Reverse at night.
            bool isInSectMalefic = isMalefic && isInSect;
            bool isOutOfSectMalefic = isMalefic && !isInSect;

            // In a day chart: Jupiter is in-sect benefic (strongest helper),
            // Venus is out-of-sect benefic. Reverse at night.
            bool isInSectBenefic = isBenefic && isInSect;

            // Hayz: in sect + correct hemisphere + matching sign gender
            bool isAboveHorizon = IsAboveHorizon(planet, houses);
            bool isMasculineSign = IsMasculineSign(planet.Sign);
            bool isHayz = isInSect
                && ((naturalSect == "Diurnal" && isAboveHorizon && isMasculineSign)
                 || (naturalSect == "Nocturnal" && !isAboveHorizon && !isMasculineSign));

            HousesOfJoy.TryGetValue(planetName, out int houseOfJoy);

            results.Add(new SectInfo
            {
                PlanetName = planetName,
                NaturalSect = naturalSect,
                IsInSect = isInSect,
                IsBenefic = isBenefic,
                IsMalefic = isMalefic,
                IsInSectMalefic = isInSectMalefic,
                IsOutOfSectMalefic = isOutOfSectMalefic,
                IsInSectBenefic = isInSectBenefic,
                IsHayz = isHayz,
                HouseOfJoy = houseOfJoy > 0 ? houseOfJoy : null,
            });
        }

        return results;
    }

    /// <summary>
    /// Whether a planet is above the horizon, for hayz: by its true altitude when it has one; otherwise, with the
    /// chart's houses, by its longitude (a composite planet); otherwise by its house number, 7 to 12 (a fixture
    /// without houses).
    /// </summary>
    /// <param name="planet">The planet.</param>
    /// <param name="houses">The chart's houses, or null.</param>
    /// <returns>True above the horizon.</returns>
    private static bool IsAboveHorizon(PlanetPosition planet, HouseData? houses)
    {
        if (planet.Altitude is double altitude)
        {
            return altitude > 0.0;
        }

        if (houses is not null)
        {
            return ChartSect.IsAboveHorizonByLongitude(planet.Longitude, houses);
        }

        return planet.House.HasValue && planet.House.Value >= 7;
    }

    /// <summary>
    /// Determines a planet's natural sect. Sun, Jupiter, Saturn are diurnal.
    /// Moon, Venus, Mars are nocturnal. Mercury is diurnal as a morning star
    /// (behind the Sun in zodiacal order, accounting for wrap, so rising before
    /// it) and nocturnal as an evening star (ahead of the Sun).
    /// </summary>
    /// <param name="planetName">Name of the planet.</param>
    /// <param name="planet">Planet's position.</param>
    /// <param name="sun">Sun's position (needed for Mercury's sect determination).</param>
    /// <returns>"Diurnal" or "Nocturnal".</returns>
    private static string GetNaturalSect(string planetName, PlanetPosition planet, PlanetPosition? sun)
    {
        return planetName switch
        {
            "Sun" or "Jupiter" or "Saturn" => "Diurnal",
            "Moon" or "Venus" or "Mars" => "Nocturnal",
            "Mercury" => DetermineMercurySect(planet, sun),
            _ => "Diurnal", // Fallback — should not reach for classical planets
        };
    }

    /// <summary>
    /// Determines Mercury's sect by its zodiacal longitude relative to the Sun
    /// (Ptolemy, <i>Tetrabiblos</i> I.7; the Techniques reference). Mercury behind
    /// the Sun in zodiacal order — the forward arc from the Sun to Mercury greater
    /// than 180° — rises before the Sun: a morning star, diurnal. Ahead of the Sun
    /// — a forward arc less than 180° — it rises after the Sun: an evening star,
    /// nocturnal.
    /// </summary>
    /// <param name="mercury">Mercury's position.</param>
    /// <param name="sun">Sun's position.</param>
    /// <returns>"Diurnal" or "Nocturnal".</returns>
    /// <remarks>
    /// A forward arc of exactly 0° (Mercury on the Sun's longitude) is not greater
    /// than 180°, so it falls to nocturnal. The test is by longitude: close to the
    /// Sun, Mercury's latitude can make it rise on the other side of the Sun from
    /// where its longitude places it, and the longitude decides.
    /// </remarks>
    private static string DetermineMercurySect(PlanetPosition mercury, PlanetPosition? sun)
    {
        if (sun is null) return "Diurnal"; // Fallback if no Sun

        // Forward arc from Sun to Mercury in zodiacal (increasing longitude) direction
        double forwardArc = (mercury.Longitude - sun.Longitude + 360.0) % 360.0;

        // Behind the Sun (forward arc > 180°) = a morning star, rising before the Sun = diurnal
        return forwardArc > 180.0 ? "Diurnal" : "Nocturnal";
    }

    /// <summary>
    /// Returns true if the zodiac sign is masculine (Fire/Air = odd signs).
    /// Aries=1, Gemini=3, Leo=5, Libra=7, Sagittarius=9, Aquarius=11.
    /// </summary>
    /// <param name="sign">The zodiac sign to check.</param>
    /// <returns>True if the sign is masculine.</returns>
    private static bool IsMasculineSign(ZodiacSign sign) =>
        (int)sign % 2 == 1;
}
