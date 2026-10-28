namespace AstroCast.Core.Models;

/// <summary>
/// Complete solar return chart result containing the return chart data,
/// the natal Sun longitude for reference, and precision metadata.
/// </summary>
/// <remarks>
/// A solar return chart is cast for the exact UTC moment the transiting Sun
/// returns to its natal ecliptic longitude. The return occurs once per year
/// (approximately ±1 day of the birthday). The chart's house cusps depend
/// on the location where the return is cast.
/// </remarks>
public sealed class SolarReturnResult
{
    /// <summary>The natal chart used as the reference for the Sun's return longitude.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>The solar return chart (full chart at the exact return moment).</summary>
    public required ChartResult ReturnChart { get; init; }

    /// <summary>The target year for this solar return.</summary>
    public required int Year { get; init; }

    /// <summary>The natal Sun ecliptic longitude that the transiting Sun returns to.</summary>
    public required double NatalSunLongitude { get; init; }

    /// <summary>
    /// The transiting Sun longitude at the exact return moment.
    /// Should match <see cref="NatalSunLongitude"/> to within ~1 arc-second.
    /// </summary>
    public required double ReturnSunLongitude { get; init; }

    /// <summary>
    /// Location where the solar return chart was cast.
    /// This affects house cusps and angular planets.
    /// </summary>
    public required string ReturnLocationName { get; init; }

    /// <summary>Latitude of the return location.</summary>
    public required double ReturnLatitude { get; init; }

    /// <summary>Longitude of the return location.</summary>
    public required double ReturnLongitude { get; init; }

    /// <summary>
    /// Precision of the return time in arc-seconds of ecliptic longitude
    /// (difference between natal Sun longitude and return Sun longitude).
    /// </summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
