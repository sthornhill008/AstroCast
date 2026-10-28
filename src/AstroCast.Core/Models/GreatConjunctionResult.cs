namespace AstroCast.Core.Models;

/// <summary>
/// A single Jupiter-Saturn great conjunction with its exact moment,
/// zodiacal position, element, and mutation cycle context.
/// </summary>
/// <remarks>
/// Great conjunctions of Jupiter and Saturn occur approximately every 20 years
/// and shift through the four elements (Fire, Earth, Air, Water) in a ~200-year
/// mutation cycle. The element shift ("mutation") marks significant historical
/// transitions in mundane astrology.
/// </remarks>
public sealed class GreatConjunctionResult
{
    /// <summary>The exact UTC date/time of the conjunction.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>The ecliptic longitude where the conjunction occurs.</summary>
    public required double Longitude { get; init; }

    /// <summary>The formatted position (e.g., "00°29' Aquarius").</summary>
    public required string PositionFormatted { get; init; }

    /// <summary>The zodiac sign of the conjunction.</summary>
    public required string Sign { get; init; }

    /// <summary>The element of the conjunction sign (Fire, Earth, Air, Water).</summary>
    public required string Element { get; init; }

    /// <summary>The modality of the conjunction sign (Cardinal, Fixed, Mutable).</summary>
    public required string Modality { get; init; }

    /// <summary>
    /// Whether this conjunction represents a mutation (element shift) —
    /// the first conjunction in a new element after the previous conjunction.
    /// </summary>
    public required bool IsMutation { get; init; }

    /// <summary>
    /// The approximate year interval since the previous great conjunction
    /// (typically ~20 years, but varies from ~18 to ~22). Zero for the first in a series.
    /// </summary>
    public required double YearsSincePrevious { get; init; }

    /// <summary>Jupiter's longitude at the exact moment.</summary>
    public required double JupiterLongitude { get; init; }

    /// <summary>Saturn's longitude at the exact moment.</summary>
    public required double SaturnLongitude { get; init; }

    /// <summary>Precision in arc-seconds (Jupiter-Saturn separation at exact moment).</summary>
    public required double PrecisionArcSeconds { get; init; }
}

/// <summary>
/// A series of great conjunctions within a date range, with mutation cycle analysis.
/// </summary>
public sealed class GreatConjunctionSeries
{
    /// <summary>The list of conjunctions found, chronologically ordered.</summary>
    public required IReadOnlyList<GreatConjunctionResult> Conjunctions { get; init; }

    /// <summary>Start of the scan range.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>End of the scan range.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Number of conjunctions found.</summary>
    public required int Count { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When this was calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
