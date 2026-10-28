namespace AstroCast.Core.Models;

/// <summary>
/// A single lunation (New Moon or Full Moon) with precise timing
/// and optional chart data.
/// </summary>
public sealed class LunationResult
{
    /// <summary>"New Moon" or "Full Moon".</summary>
    public required string Type { get; init; }

    /// <summary>True for New Moon, false for Full Moon.</summary>
    public required bool IsNewMoon { get; init; }

    /// <summary>The exact UTC date/time of the lunation.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>Sun longitude at the exact moment.</summary>
    public required double SunLongitude { get; init; }

    /// <summary>Moon longitude at the exact moment.</summary>
    public required double MoonLongitude { get; init; }

    /// <summary>The zodiac sign where the lunation occurs.</summary>
    public required string Sign { get; init; }

    /// <summary>The degree within the sign (0-30).</summary>
    public required double Degree { get; init; }

    /// <summary>The formatted position (e.g., "15°23' Aries").</summary>
    public required string PositionFormatted { get; init; }

    /// <summary>Element of the lunation sign.</summary>
    public required string Element { get; init; }

    /// <summary>Modality of the lunation sign.</summary>
    public required string Modality { get; init; }

    /// <summary>Sun-Moon separation in arc-seconds at the exact moment (precision measure).</summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>
    /// True if this lunation is near the lunar nodes (potential eclipse).
    /// New Moon within 18° of a node = potential solar eclipse.
    /// Full Moon within 12° of a node = potential lunar eclipse.
    /// </summary>
    public required bool IsNearNode { get; init; }

    /// <summary>
    /// Distance from the nearest lunar node in degrees.
    /// Null if node position not available.
    /// </summary>
    public double? NodeDistance { get; init; }

    /// <summary>
    /// The node flag: "Near a node (within 18°)" for a New Moon within 18° of the true node,
    /// "Near a node (within 12°)" for a Full Moon within 12°, or null. It measures the distance to
    /// the node, not whether an eclipse occurs: a flagged lunation may have none, and an unflagged
    /// Full Moon may have a penumbral eclipse.
    /// </summary>
    public string? EclipseIndicator { get; init; }

    /// <summary>
    /// Optional full chart cast at this lunation moment.
    /// Null if only timing was requested.
    /// </summary>
    public ChartResult? Chart { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// A series of lunations within a date range.
/// </summary>
public sealed class LunationSeries
{
    /// <summary>All lunations found, chronologically ordered.</summary>
    public required IReadOnlyList<LunationResult> Lunations { get; init; }

    /// <summary>New Moons only.</summary>
    public IReadOnlyList<LunationResult> NewMoons =>
        Lunations.Where(l => l.IsNewMoon).ToList();

    /// <summary>Full Moons only.</summary>
    public IReadOnlyList<LunationResult> FullMoons =>
        Lunations.Where(l => !l.IsNewMoon).ToList();

    /// <summary>Lunations near the nodes (potential eclipses).</summary>
    public IReadOnlyList<LunationResult> PossibleEclipses =>
        Lunations.Where(l => l.IsNearNode).ToList();

    /// <summary>Scan start date.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>Scan end date.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Total count.</summary>
    public required int Count { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
