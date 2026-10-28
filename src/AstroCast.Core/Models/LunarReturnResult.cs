namespace AstroCast.Core.Models;

/// <summary>
/// Complete lunar return chart result containing the return chart data,
/// the natal Moon longitude for reference, precision metadata, and the
/// return's sequence number within the requested date range.
/// </summary>
/// <remarks>
/// A lunar return chart is cast for the exact UTC moment the transiting Moon
/// returns to its natal ecliptic longitude. The Moon completes a full cycle
/// every ~27.32 days, producing approximately 13 returns per year. The chart's
/// house cusps depend on the location where the return is cast.
/// </remarks>
public sealed class LunarReturnResult
{
    /// <summary>The natal chart used as the reference for the Moon's return longitude.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>The lunar return chart (full chart at the exact return moment).</summary>
    public required ChartResult ReturnChart { get; init; }

    /// <summary>
    /// Sequence number of this return within the requested date range (1-based).
    /// For example, if 13 returns were found in a year, they are numbered 1–13.
    /// </summary>
    public required int SequenceNumber { get; init; }

    /// <summary>The natal Moon ecliptic longitude that the transiting Moon returns to.</summary>
    public required double NatalMoonLongitude { get; init; }

    /// <summary>
    /// The transiting Moon longitude at the exact return moment.
    /// Should match <see cref="NatalMoonLongitude"/> to within ~1 arc-second.
    /// </summary>
    public required double ReturnMoonLongitude { get; init; }

    /// <summary>
    /// Location where the lunar return chart was cast.
    /// This affects house cusps and angular planets.
    /// </summary>
    public required string ReturnLocationName { get; init; }

    /// <summary>Latitude of the return location.</summary>
    public required double ReturnLatitude { get; init; }

    /// <summary>Longitude of the return location.</summary>
    public required double ReturnLongitude { get; init; }

    /// <summary>
    /// Precision of the return time in arc-seconds of ecliptic longitude
    /// (difference between natal Moon longitude and return Moon longitude).
    /// </summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
