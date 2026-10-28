namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// One primary direction and the date it is exact (branch Primary, 1.5.0): a promissor point carried to a
/// significator, or the significator to the promissor, by the diurnal motion, its arc turned into years by the key.
/// Found by <see cref="Services.PrimaryDirections.PrimaryDirectionService"/>.
/// </summary>
public sealed class PrimaryDirectionHit
{
    /// <summary>
    /// The significator: "Ascendant", "Midheaven", "Descendant", "IC", a body's name ("Sun", "Moon", "Mercury" to
    /// "Pluto") or "Part of Fortune".
    /// </summary>
    public required string Significator { get; init; }

    /// <summary>The body the promissor point is taken from, by its position name ("Mars", "True Node", "Chiron"), or the star's catalog display name ("Regulus", "South Scale").</summary>
    public required string Promissor { get; init; }

    /// <summary>Whether the promissor is a chart body or a fixed star.</summary>
    public required PrimaryDirectionPromissorKind PromissorKind { get; init; }

    /// <summary>The promissor point's relation to its body: its place, an aspect point, an antiscion. A star's is always the conjunction.</summary>
    public required PrimaryDirectionAspect Aspect { get; init; }

    /// <summary>
    /// The aspect point's signed angle from the body's longitude: 0, +60, −60, +90, −90, +120, −120 or 180; 0 for an
    /// antiscion, a contra-antiscion and a conjunction.
    /// </summary>
    public required double AspectAngle { get; init; }

    /// <summary>Direct or converse.</summary>
    public required PrimaryDirectionMotion Motion { get; init; }

    /// <summary>The arc of direction, in degrees of right ascension, from 0° up to 360°.</summary>
    public required double Arc { get; init; }

    /// <summary>The years of life the arc stands for under the key: the age at which the direction is exact.</summary>
    public required double Years { get; init; }

    /// <summary>The instant the direction is exact as a Julian Day (UT): the natal Julian Day plus the years at 365.2422 days a year.</summary>
    public required double JulianDayUt { get; init; }

    /// <summary>
    /// The instant the direction is exact, UTC, to the nearest whole second
    /// (<see cref="Utilities.JulianDayUtility.JulianDayToInstant"/>): proleptic ticks, a label on the calendar in
    /// force being <see cref="Formatting.CalendarDateFormatter"/>'s.
    /// </summary>
    public required DateTime InstantUtc { get; init; }

    /// <summary>
    /// How far the date moves for a minute of birth time, in months of 365.2422 / 12 days, signed: half the
    /// difference between the dates at a minute after and a minute before the birth. Null when the direction does
    /// not exist at either of those instants, or when its significator is the Part of Fortune and the chart is a
    /// day chart at one instant and a night chart at the other.
    /// </summary>
    public double? MonthsPerMinute { get; init; }

    /// <summary>
    /// The earlier of the direction's dates at the birth instant moved by the birth-time uncertainty either way,
    /// UTC. Null when no uncertainty is set, under the conditions that leave <see cref="MonthsPerMinute"/> null, at
    /// those two instants.
    /// </summary>
    public DateTime? RangeStartUtc { get; init; }

    /// <summary>The later of the two dates <see cref="RangeStartUtc"/> is the earlier of; null when it is null.</summary>
    public DateTime? RangeEndUtc { get; init; }
}
