using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for calculating astrological aspects between chart points.
/// Detects major Ptolemaic aspects and Ebertin cosmobiology aspects,
/// determines orb, and classifies each aspect as applying or separating.
/// </summary>
public interface IAspectService
{
    /// <summary>
    /// Calculate all aspects between planets in the given position list.
    /// Each pair is evaluated once (no duplicates, no self-aspects).
    /// </summary>
    /// <param name="positions">Planet positions with longitude and speed data.</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: applying or separating is decided by the orb's
    /// rate at the instant, from the positions' speeds (<see cref="AstroCast.Core.Utilities.AspectDirection"/>).
    /// </param>
    /// <returns>Detected planet-to-planet aspects, sorted by orb (tightest first).</returns>
    IReadOnlyList<AspectResult> CalculatePlanetAspects(
        IReadOnlyList<PlanetPosition> positions,
        double julianDayUt);

    /// <summary>
    /// Calculate all aspects between planets and the chart angles (ASC, MC).
    /// </summary>
    /// <param name="positions">Planet positions with longitude and speed data.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes.</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: applying or separating is decided by the orb's
    /// rate at the instant, from the positions' speeds, with the angles held still
    /// (<see cref="AstroCast.Core.Utilities.AspectDirection"/>).
    /// </param>
    /// <returns>Detected planet-to-angle aspects, sorted by orb (tightest first).</returns>
    IReadOnlyList<AspectResult> CalculateAngleAspects(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houseData,
        double julianDayUt);

    /// <summary>
    /// Calculate all aspects (planet-to-planet + planet-to-angle) in one call.
    /// </summary>
    /// <param name="positions">Planet positions with longitude and speed data.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes.</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: applying or separating is decided by the orb's
    /// rate at the instant, from the positions' speeds, with the angles held still
    /// (<see cref="AstroCast.Core.Utilities.AspectDirection"/>).
    /// </param>
    /// <returns>All detected aspects, sorted by orb (tightest first).</returns>
    IReadOnlyList<AspectResult> CalculateAllAspects(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houseData,
        double julianDayUt);

    /// <summary>
    /// Calculate the aspects of the minor points, Ceres, Pallas, Juno, Vesta and Lilith, to a chart's planets
    /// and angles (R5, branch Asteroids, 1.6.0): from each point to the positions whose body is the Sun to
    /// Pluto (never the True Node, Chiron, another of the five or the Part of Fortune), then to the Ascendant
    /// ("ASC") and the Midheaven ("MC"), held still; the five Ptolemaic aspects, the definitions of
    /// <see cref="AspectDefinition.All"/> whose category is major, in that order, the first match winning, each
    /// at an orb of 2°, inclusive; applying when the orb is decreasing at the instant, by the point's speed and
    /// the target's (<see cref="AstroCast.Core.Utilities.AspectDirection"/>).
    /// </summary>
    /// <param name="minorPoints">The minor points, with longitude and speed.</param>
    /// <param name="positions">The chart's positions; only those of the Sun to Pluto are targets.</param>
    /// <param name="houses">House data containing the ASC and MC longitudes.</param>
    /// <returns>
    /// The aspects, <see cref="AspectResult.Body1Name"/> the point and <see cref="AspectResult.Body2Name"/> the
    /// target, sorted by orb (tightest first); empty when <paramref name="minorPoints"/> is.
    /// </returns>
    IReadOnlyList<AspectResult> CalculateMinorPointAspects(
        IReadOnlyList<PlanetPosition> minorPoints,
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses);
}
