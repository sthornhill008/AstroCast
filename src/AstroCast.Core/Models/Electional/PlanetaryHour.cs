namespace AstroCast.Core.Models.Electional;

/// <summary>
/// The Chaldean planetary hour at a given moment. Planetary hours divide
/// the daylight period into 12 equal hours and the nighttime period into
/// 12 equal hours, assigning rulers in the Chaldean order starting from
/// the day ruler.
/// </summary>
public sealed class PlanetaryHour
{
    /// <summary>The ruling planet of this hour (e.g., "Saturn", "Jupiter").</summary>
    public required string RulingPlanet { get; init; }

    /// <summary>Hour number within the day/night cycle (1–12).</summary>
    public required int HourNumber { get; init; }

    /// <summary>Whether this is a daytime or nighttime hour.</summary>
    public required bool IsDaytime { get; init; }

    /// <summary>Local time when this planetary hour begins.</summary>
    public required DateTime HourStart { get; init; }

    /// <summary>Local time when this planetary hour ends.</summary>
    public required DateTime HourEnd { get; init; }

    /// <summary>
    /// The instant this planetary hour begins, in UTC (<see cref="DateTimeKind.Utc"/>):
    /// <see cref="HourStart"/> is the same instant read on the hour's clock.
    /// </summary>
    /// <remarks>
    /// A wall clock in an hour the zone repeats names two instants, so <see cref="HourStart"/>
    /// alone cannot say which offset a bound is on; this instant can. The chart export reads
    /// each bound's offset and zone name from it, to label an hour that straddles a change of
    /// the zone's offset. <see langword="null"/> on an hour not built by
    /// <c>PlanetaryHourCalculator</c>.
    /// </remarks>
    public DateTime? HourStartUtc { get; init; }

    /// <summary>
    /// The instant this planetary hour ends, in UTC (<see cref="DateTimeKind.Utc"/>):
    /// <see cref="HourEnd"/> is the same instant read on the hour's clock.
    /// </summary>
    /// <remarks>
    /// See <see cref="HourStartUtc"/>. <see langword="null"/> on an hour not built by
    /// <c>PlanetaryHourCalculator</c>.
    /// </remarks>
    public DateTime? HourEndUtc { get; init; }
}
