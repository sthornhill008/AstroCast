namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// A dated, exact secondary progression contact: the instant at which a progressed body or angle makes
/// an exact major aspect to a natal point (branch Arcs, 1.4.0). Found by
/// <see cref="Services.ProgressedHitFinder"/>.
/// </summary>
public sealed class ProgressedHit
{
    /// <summary>The progressed point: "Sun", "Moon", "Mercury", "Venus", "Mars", "ASC" or "MC".</summary>
    public required string ProgressedPoint { get; init; }

    /// <summary>The aspect's angle in degrees: 0, 60, 90, 120 or 180.</summary>
    public required double AspectAngle { get; init; }

    /// <summary>The aspect's name ("Conjunction", "Sextile", "Square", "Trine", "Opposition").</summary>
    public required string AspectName { get; init; }

    /// <summary>The aspect's abbreviation ("cnj", "sxt", "sqr", "tri", "opp").</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>The natal point ("Venus", "True Node", "ASC", "MC"); a body's own natal place is allowed.</summary>
    public required string NatalPoint { get; init; }

    /// <summary>The exact instant as a Julian Day (UT), bisected to within a second.</summary>
    public required double JulianDayUt { get; init; }

    /// <summary>
    /// The exact instant, UTC, to the nearest whole second
    /// (<see cref="Utilities.JulianDayUtility.JulianDayToInstant"/>).
    /// </summary>
    public required DateTime InstantUtc { get; init; }
}
