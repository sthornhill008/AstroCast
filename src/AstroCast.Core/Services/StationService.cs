using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Classifies the station status of each planet. A planet with |speed| below a
/// planet-specific threshold is reported as stationing, and the nearest station
/// is then located in the ephemeris by <see cref="StationFinder"/> so that its
/// type, time, position and phase describe a real event rather than the sign
/// of the current speed.
/// </summary>
/// <remarks>
/// <para>
/// Station thresholds are set at approximately 10–15% of each planet's mean
/// daily motion. Only planets that can retrograde are evaluated: Mercury
/// through Pluto. The Sun, Moon, True Node, and Chiron are excluded.
/// </para>
/// <para>
/// The service returns only stationing planets. Non-stationing planets are
/// omitted from the result to keep the output focused on exceptional conditions.
/// </para>
/// </remarks>
public sealed class StationService : IStationService
{
    /// <summary>
    /// Station speed thresholds by planet: |speed| below this value = stationing.
    /// Set at ~10–15% of each planet's mean daily motion.
    /// </summary>
    private static readonly Dictionary<CelestialBodyId, double> StationThresholds = new()
    {
        [CelestialBodyId.Mercury] = 0.15,
        [CelestialBodyId.Venus]   = 0.12,
        [CelestialBodyId.Mars]    = 0.08,
        [CelestialBodyId.Jupiter] = 0.02,
        [CelestialBodyId.Saturn]  = 0.01,
        [CelestialBodyId.Uranus]  = 0.004,
        [CelestialBodyId.Neptune] = 0.003,
        [CelestialBodyId.Pluto]   = 0.002,
    };

    /// <inheritdoc />
    public IReadOnlyList<StationInfo> Evaluate(
        IReadOnlyList<PlanetPosition> positions, double julianDayUt, IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(ephemeris);

        List<StationInfo> results = [];

        foreach (PlanetPosition pos in positions)
        {
            if (!StationThresholds.TryGetValue(pos.Body, out double threshold))
                continue; // Not a planet that stations (Sun, Moon, Node, Chiron)

            double absSpeed = Math.Abs(pos.SpeedInLongitude);

            if (absSpeed >= threshold)
                continue; // Normal speed — not stationing

            StationSearchResult station = StationFinder.FindNearestStation(ephemeris, pos.Body, julianDayUt);

            string stationType = station.IsRetrogradeStation
                ? "Station Retrograde"
                : "Station Direct";

            double proximity = absSpeed / threshold; // 0.0 = exact stop, 1.0 = at threshold

            results.Add(new StationInfo
            {
                PlanetName = pos.Name,
                StationType = stationType,
                IsStationing = true,
                DailySpeed = Math.Round(pos.SpeedInLongitude, 6),
                StationThreshold = threshold,
                StationProximity = Math.Round(proximity, 4),
                Phase = station.Phase,
                StationTimeUtc = station.StationTimeUtc,
                StationJulianDay = station.StationJulianDay,
                StationLongitude = station.StationLongitude,
                DaysToStation = station.DaysToStation,
            });
        }

        return results;
    }
}
