using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Single source of truth for chart-angle proximity ("angularity").
/// Determines which planets sit within <see cref="AngularOrb"/> degrees of
/// any of the four chart angles (ASC, MC, DSC, IC), measured in ecliptic
/// longitude.
/// </summary>
/// <remarks>
/// <para>
/// Angularity by orb is deliberately independent of Placidus house number.
/// A planet a few arcminutes on the cadent side of an angle cusp occupies
/// the preceding house while being maximally angular by orb; consumers that
/// need classical angular strength (the Angular Planets export table and the
/// electional Malefic Avoidance scorer) must use this routine, never house
/// membership.
/// </para>
/// <para>
/// Extracted from <c>ChartContextService</c> during the 2026-06-10 electional
/// live-run review so the export formatter and the electional scorer share
/// one implementation (Defect A, requirement A1).
/// </para>
/// </remarks>
public static class AngularityService
{
    /// <summary>
    /// Maximum orb in degrees for a planet to be considered angular.
    /// </summary>
    public const double AngularOrb = 5.0;

    /// <summary>
    /// Finds all planets within <see cref="AngularOrb"/> degrees of any of the
    /// four chart angles (ASC, MC, DSC, IC). Results are ordered by orb
    /// (tightest first).
    /// </summary>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="houses">House data containing angle longitudes.</param>
    /// <returns>List of angular planets ordered by proximity to angles.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/> or <paramref name="houses"/> is null.
    /// </exception>
    public static IReadOnlyList<AngularPlanet> FindAngularPlanets(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);

        (string Name, double Longitude)[] angles =
        [
            ("ASC", houses.Ascendant),
            ("MC", houses.Midheaven),
            ("DSC", houses.Descendant),
            ("IC", houses.ImumCoeli),
        ];

        List<AngularPlanet> result = [];

        foreach (PlanetPosition planet in positions)
        {
            foreach ((string angleName, double angleLongitude) in angles)
            {
                double separation = AspectService.AngularSeparation(planet.Longitude, angleLongitude);
                if (separation <= AngularOrb)
                {
                    result.Add(new AngularPlanet
                    {
                        PlanetName = planet.Name,
                        Angle = angleName,
                        Orb = separation,
                    });
                }
            }
        }

        return result.OrderBy(a => a.Orb).ToList();
    }
}
