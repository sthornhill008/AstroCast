using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates Arabic Parts (Lots) from chart positions, house cusps,
/// and day/night sect status.
/// </summary>
public interface IArabicPartService
{
    /// <summary>
    /// Calculates all defined Arabic Parts and Hellenistic Lots.
    /// </summary>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="houses">House cusps and angles.</param>
    /// <param name="isDayChart">True for day charts (Sun above horizon).</param>
    /// <returns>Calculated Parts with longitudes, signs, houses, and rulers.</returns>
    IReadOnlyList<ArabicPart> CalculateAll(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        bool isDayChart);
}
