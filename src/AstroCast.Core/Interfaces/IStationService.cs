using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Classifies the station status of each planet: whether it is near a station,
/// and the type, time, position and phase of the nearest one.
/// </summary>
public interface IStationService
{
    /// <summary>
    /// Evaluates the station status of all applicable planets.
    /// Returns only planets that are stationing; non-stationing planets
    /// are omitted from the result.
    /// </summary>
    /// <param name="positions">All planetary positions at the chart instant.</param>
    /// <param name="julianDayUt">
    /// The chart instant as a Julian Day in Universal Time. Required so the
    /// nearest station can be searched for in the ephemeris rather than
    /// guessed from the sign of the current speed.
    /// </param>
    /// <param name="ephemeris">Ephemeris used to sample speeds around the chart instant.</param>
    /// <returns>Station information for each stationing planet.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/> or <paramref name="ephemeris"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a flagged planet has no speed sign change within the search
    /// window — an invariant breach that is surfaced rather than masked.
    /// </exception>
    IReadOnlyList<StationInfo> Evaluate(
        IReadOnlyList<PlanetPosition> positions, double julianDayUt, IEphemerisService ephemeris);
}
