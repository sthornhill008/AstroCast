using System.Globalization;

using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Station status for a single planet. Describes whether the planet is
/// stationing (pausing before changing direction) and the type of station.
/// </summary>
/// <remarks>
/// <para>
/// A stationing planet is at its most powerful and concentrated. In horary,
/// a significator stationing direct = the matter revives and perfects.
/// A significator stationing retrograde = the matter is about to stall
/// or reverse.
/// </para>
/// <para>
/// A planet is reported at all only when |speed| is below its planet-specific
/// threshold. The station type then names the station <b>event</b> nearest the
/// chart instant, which <see cref="Services.StationFinder"/> locates from the
/// moment the daily speed actually crosses zero: a crossing from positive to
/// negative is a Station Retrograde, from negative to positive a Station
/// Direct, whichever side of the chart instant the crossing lies. The sign of
/// the current speed is <i>not</i> used for the type — it only says which way
/// the planet is moving now, which is the type of the station it last made.
/// <see cref="Phase"/> says whether the named station is still ahead
/// (Approaching) or already behind (Separating); <see cref="StationTimeUtc"/>,
/// <see cref="StationLongitude"/> and <see cref="DaysToStation"/> place it; the
/// signed <see cref="DailySpeed"/> carries the current direction of motion.
/// </para>
/// <para>
/// The Sun and Moon never station. Chiron and True Node are excluded.
/// </para>
/// </remarks>
public sealed class StationInfo
{
    /// <summary>Name of the planet (e.g., "Saturn", "Mercury").</summary>
    public required string PlanetName { get; init; }

    /// <summary>
    /// The station type: "Station Retrograde", "Station Direct", or "None".
    /// "None" means the planet is moving at normal speed and is not near
    /// a station point.
    /// </summary>
    public required string StationType { get; init; }

    /// <summary>
    /// True if the planet is stationing — its daily speed is below the
    /// planet-specific station threshold.
    /// </summary>
    public required bool IsStationing { get; init; }

    /// <summary>
    /// The planet's daily speed in longitude (degrees/day).
    /// Negative values indicate retrograde motion.
    /// </summary>
    public required double DailySpeed { get; init; }

    /// <summary>
    /// The station threshold for this planet (degrees/day).
    /// A planet with |speed| below this value is considered stationing.
    /// </summary>
    public required double StationThreshold { get; init; }

    /// <summary>
    /// How close the planet is to a complete stop, expressed as the ratio
    /// of |speed| to the station threshold. Lower values mean closer to
    /// exact station. Range: 0.0 (exact station) to 1.0 (at threshold).
    /// Null if the planet is not stationing.
    /// </summary>
    public double? StationProximity { get; init; }

    /// <summary>
    /// Whether the planet is moving toward the station named by
    /// <see cref="StationType"/> (<see cref="StationPhase.Approaching"/>) or
    /// away from it (<see cref="StationPhase.Separating"/>).
    /// Null if the planet is not stationing.
    /// </summary>
    public StationPhase? Phase { get; init; }

    /// <summary>
    /// UTC instant of the station (<see cref="DateTimeKind.Utc"/>), rounded to
    /// the nearest second. Null if the planet is not stationing.
    /// </summary>
    public DateTime? StationTimeUtc { get; init; }

    /// <summary>
    /// Julian Day (UT) of the station. Null if the planet is not stationing.
    /// </summary>
    public double? StationJulianDay { get; init; }

    /// <summary>
    /// Ecliptic longitude at which the station occurs, in decimal degrees
    /// (0–360). Null if the planet is not stationing.
    /// </summary>
    public double? StationLongitude { get; init; }

    /// <summary>
    /// Signed days from the chart instant to the station. Positive means the
    /// station is ahead of the chart; zero or negative means it is now or
    /// behind. Null if the planet is not stationing.
    /// </summary>
    public double? DaysToStation { get; init; }

    /// <summary>
    /// Human-readable summary
    /// (e.g., "Saturn — Station Retrograde (0.005°/day, 50% of threshold)").
    /// </summary>
    public string Summary
    {
        get
        {
            if (!IsStationing)
                return string.Create(CultureInfo.InvariantCulture, $"{PlanetName} — Not stationing ({Math.Abs(DailySpeed):F4}°/day)");

            int proximityPct = StationProximity.HasValue
                ? (int)Math.Round(StationProximity.Value * 100.0, MidpointRounding.AwayFromZero)
                : 0;

            return string.Create(CultureInfo.InvariantCulture, $"{PlanetName} — {StationType} ({Math.Abs(DailySpeed):F4}°/day, {proximityPct}% of threshold)");
        }
    }
}
