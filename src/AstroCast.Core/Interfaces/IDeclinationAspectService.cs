using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Detects declination aspects (parallels and contraparallels) between
/// celestial bodies based on their declination values: a parallel between two
/// bodies on the same side of the celestial equator, a contraparallel between two
/// on opposite sides (a declination of exactly 0° counts as north), so each pair
/// forms at most one aspect.
/// </summary>
public interface IDeclinationAspectService
{
    /// <summary>
    /// Calculates all declination aspects (parallels and contraparallels)
    /// between the given planetary positions.
    /// </summary>
    /// <param name="positions">All planetary positions with declination values.</param>
    /// <returns>
    /// List of declination aspects sorted by orb (tightest first).
    /// Only includes bodies that have declination values calculated.
    /// </returns>
    IReadOnlyList<DeclinationAspect> Calculate(IReadOnlyList<PlanetPosition> positions);
}
