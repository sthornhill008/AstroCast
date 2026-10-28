namespace AstroCast.Core.Models;

/// <summary>
/// Complete result of a paran (paranatellonta) calculation for a given date,
/// geographic location, and body set. Contains all angular crossings detected
/// and all paran events found within the configured time orb.
/// </summary>
/// <remarks>
/// The result preserves both the raw angular crossings (useful for debugging
/// and detailed display) and the synthesised paran events (the primary output
/// for interpretation). Crossings are sorted by Julian Day; paran events are
/// sorted by orb (tightest first).
/// </remarks>
public sealed class ParanResult
{
    /// <summary>Geographic latitude used for the calculation (north positive).</summary>
    public required double Latitude { get; init; }

    /// <summary>Geographic longitude used for the calculation (east positive).</summary>
    public required double Longitude { get; init; }

    /// <summary>Julian Day (UT) of the start of the calculation window.</summary>
    public required double StartJulianDay { get; init; }

    /// <summary>Julian Day (UT) of the end of the calculation window.</summary>
    public required double EndJulianDay { get; init; }

    /// <summary>Time orb in minutes that was used to detect paran simultaneities.</summary>
    public required double OrbMinutes { get; init; }

    /// <summary>All angular crossings detected, sorted chronologically by Julian Day.</summary>
    public required IReadOnlyList<AngularCrossing> Crossings { get; init; }

    /// <summary>All paran events detected, sorted by orb (tightest first).</summary>
    public required IReadOnlyList<ParanEvent> Parans { get; init; }

    /// <summary>Number of celestial bodies included in the calculation.</summary>
    public required int BodyCount { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
