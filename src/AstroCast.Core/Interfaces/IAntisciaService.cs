using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates antiscia and contra-antiscia points for all planets and
/// detects connections (hidden conjunctions/oppositions) between planet
/// pairs within the classical 2° orb.
/// </summary>
public interface IAntisciaService
{
    /// <summary>
    /// Calculates antiscia/contra-antiscia points for all positions and
    /// detects connections between planet pairs.
    /// </summary>
    /// <param name="positions">All planetary positions.</param>
    /// <returns>Tuple of all antiscia points and all detected connections.</returns>
    (IReadOnlyList<AntisciaPoint> Points, IReadOnlyList<AntisciaConnection> Connections)
        Calculate(IReadOnlyList<PlanetPosition> positions);
}
