using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for analysing Moon conditions in an event chart.
/// Calculates phase, Void of Course status, first applying aspect,
/// and Via Combusta detection from the chart's planetary positions and aspects.
/// </summary>
public interface IMoonConditionService
{
    /// <summary>
    /// Analyse the Moon's condition from the chart's positions and aspects,
    /// following the Moon and every target in the ephemeris to find its first
    /// applying aspect and whether it is void of course.
    /// </summary>
    /// <param name="positions">All planetary positions from the chart calculation.</param>
    /// <param name="aspects">All detected aspects from the chart calculation.</param>
    /// <param name="ephemeris">Ephemeris the Moon and the targets are followed in after the chart instant.</param>
    /// <param name="julianDayUt">The chart instant as a Julian Day in Universal Time.</param>
    /// <returns>
    /// A <see cref="MoonConditionInfo"/> describing the Moon's phase, VoC status,
    /// first applying aspect, and Via Combusta status.
    /// Returns null if no Moon position is found in the positions list.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/>, <paramref name="aspects"/> or
    /// <paramref name="ephemeris"/> is null.
    /// </exception>
    MoonConditionInfo? GetMoonCondition(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects,
        IEphemerisService ephemeris,
        double julianDayUt);

    /// <summary>
    /// Analyse the Moon's condition from positions that have no motion of their
    /// own, holding every target at its given longitude while the Moon advances
    /// through its sign.
    /// </summary>
    /// <param name="positions">Planetary positions, such as a composite chart's midpoints.</param>
    /// <param name="aspects">Aspects between those positions.</param>
    /// <returns>
    /// A <see cref="MoonConditionInfo"/> as for
    /// <see cref="GetMoonCondition(IReadOnlyList{PlanetPosition}, IReadOnlyList{AspectResult}, IEphemerisService, double)"/>.
    /// Returns null if no Moon position is found in the positions list.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/> or <paramref name="aspects"/> is null.
    /// </exception>
    /// <remarks>
    /// Only for positions that no ephemeris can move — a composite chart's
    /// midpoints are the case it exists for. For a chart cast at an instant,
    /// targets move while the Moon crosses its sign, and holding them still
    /// gives the wrong first applying aspect on about 2% of instants and the
    /// wrong void-of-course status on about 0.5%; use the ephemeris overload.
    /// </remarks>
    MoonConditionInfo? GetMoonConditionWithFixedTargets(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects);
}
