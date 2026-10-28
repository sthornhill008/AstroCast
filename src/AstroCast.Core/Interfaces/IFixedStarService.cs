using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for detecting fixed star conjunctions with chart points.
/// Calculates the ecliptic longitude of each cataloged star for a given Julian Day,
/// then checks for conjunctions to all chart points (planets, angles, nodes, Chiron)
/// within the defined orb.
/// </summary>
/// <remarks>
/// Fixed stars are traditionally interpreted only by conjunction (no other aspects).
/// The default orb is 1 degree 00 arc-minutes, applied to ecliptic longitude only.
/// </remarks>
public interface IFixedStarService
{
    /// <summary>
    /// Find all fixed star conjunctions with chart points for a given chart.
    /// </summary>
    /// <param name="positions">All planetary positions for the chart.</param>
    /// <param name="houses">House cusps and angles (ASC, MC, DSC, IC) for the chart.</param>
    /// <param name="julianDayUt">Julian Day number in Universal Time for the chart.</param>
    /// <returns>
    /// A sorted list of all fixed star conjunctions found within the default orb.
    /// Sorted by chart point priority (ASC, MC, DSC, IC, Sun, Moon, etc.)
    /// then by orb (tightest first). Empty list if no conjunctions found.
    /// </returns>
    IReadOnlyList<FixedStarConjunction> FindConjunctions(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        double julianDayUt);
}
