using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Analyzes planetary sect conditions for a chart, determining which
/// planets are in their preferred sect and their resulting accidental
/// dignity or debility.
/// </summary>
public interface ISectService
{
    /// <summary>
    /// Computes sect analysis for all classical planets in the chart.
    /// </summary>
    /// <param name="positions">
    /// All planetary positions from the chart. Hayz reads a planet's hemisphere from its
    /// <see cref="PlanetPosition.Altitude"/>: above the horizon when it is positive.
    /// </param>
    /// <param name="isDayChart">True if the Sun is above the horizon (day chart).</param>
    /// <param name="houses">
    /// The chart's houses. A planet with no altitude (a composite's) takes its hemisphere from its longitude
    /// against them; without them, from its house number.
    /// </param>
    /// <returns>Sect information for each classical planet.</returns>
    IReadOnlyList<SectInfo> Analyze(
        IReadOnlyList<PlanetPosition> positions,
        bool isDayChart,
        HouseData? houses = null);
}
