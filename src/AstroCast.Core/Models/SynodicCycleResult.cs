namespace AstroCast.Core.Models;

/// <summary>
/// Represents a single conjunction event in a synodic cycle series.
/// </summary>
public sealed class SynodicConjunction
{
    /// <summary>Sequential number in the series (1-based).</summary>
    public required int Number { get; init; }

    /// <summary>UTC date/time of the conjunction.</summary>
    public required DateTime DateTimeUtc { get; init; }

    /// <summary>Julian Day of the conjunction.</summary>
    public required double JulianDay { get; init; }

    /// <summary>Ecliptic longitude of the conjunction midpoint (degrees).</summary>
    public required double Longitude { get; init; }

    /// <summary>Formatted position (e.g., "22°46′ Capricorn").</summary>
    public required string Position { get; init; }

    /// <summary>Zodiac sign of the conjunction.</summary>
    public required string Sign { get; init; }

    /// <summary>Element of the conjunction sign (Fire, Earth, Air, Water).</summary>
    public required string Element { get; init; }

    /// <summary>Modality of the conjunction sign (Cardinal, Fixed, Mutable).</summary>
    public required string Modality { get; init; }

    /// <summary>Longitude of the faster-moving body (Body A) at conjunction.</summary>
    public required double BodyALongitude { get; init; }

    /// <summary>Longitude of the slower-moving body (Body B) at conjunction.</summary>
    public required double BodyBLongitude { get; init; }

    /// <summary>Angular separation at the computed conjunction moment (arc-seconds).</summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>
    /// Synodic period (days): for a cycle's first pass, the time since the previous cycle's first
    /// pass. Null for a later pass, which has no period of its own, and for the first cycle in the
    /// series.
    /// </summary>
    public double? SynodicPeriodDays { get; init; }

    /// <summary>
    /// Synodic period in years (<see cref="SynodicPeriodDays"/> / 365.25).
    /// Null where <see cref="SynodicPeriodDays"/> is.
    /// </summary>
    public double? SynodicPeriodYears { get; init; }

    /// <summary>
    /// Which pass of its cycle this conjunction is, from 1. Consecutive conjunctions belong to one
    /// cycle until the pair has passed through an opposition, so the passes of a triple conjunction
    /// are passes 1, 2 and 3 of one cycle, which begins at its first pass. A pair that can never be
    /// in opposition (two of the Sun, Mercury and Venus) has one pass per cycle.
    /// </summary>
    public int PassNumber { get; init; } = 1;

    /// <summary>
    /// How many passes this conjunction's cycle has in the series: 1 for a single conjunction, 3 for
    /// a triple. A cycle cut by the start or end of the series counts only the passes inside it.
    /// </summary>
    public int PassCount { get; init; } = 1;
}

/// <summary>
/// Result of a synodic cycle calculation for a planet pair, containing all
/// conjunctions found in the search period and summary statistics.
/// </summary>
public sealed class SynodicCycleSeries
{
    /// <summary>Display name of the faster-moving body (Body A).</summary>
    public required string BodyAName { get; init; }

    /// <summary>Display name of the slower-moving body (Body B).</summary>
    public required string BodyBName { get; init; }

    /// <summary>All conjunctions found in the search period, in chronological order.</summary>
    public required IReadOnlyList<SynodicConjunction> Conjunctions { get; init; }

    /// <summary>Start of the search period.</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End of the search period.</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>Total number of conjunctions found.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// Average synodic period over the cycles' first passes (days): the mean time from one cycle's
    /// first pass to the next's. Null if the series has fewer than two cycles.
    /// </summary>
    public double? AverageSynodicPeriodDays { get; init; }

    /// <summary>
    /// Average synodic period over the cycles' first passes (years). Null if the series has fewer
    /// than two cycles.
    /// </summary>
    public double? AverageSynodicPeriodYears { get; init; }

    /// <summary>Shortest synodic period between cycles' first passes (days). Null if fewer than two cycles.</summary>
    public double? MinSynodicPeriodDays { get; init; }

    /// <summary>Longest synodic period between cycles' first passes (days). Null if fewer than two cycles.</summary>
    public double? MaxSynodicPeriodDays { get; init; }

    /// <summary>Engine version string (e.g., "AstroCast 1.0.181 (SwissEph 2.10.03)").</summary>
    public required string EngineVersion { get; init; }

    /// <summary>UTC timestamp when the calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
