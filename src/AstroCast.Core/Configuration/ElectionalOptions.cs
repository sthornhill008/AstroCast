namespace AstroCast.Core.Configuration;

/// <summary>
/// Configuration options for the electional calculation engine.
/// Controls scan granularity and orb thresholds.
/// </summary>
public sealed class ElectionalOptions
{
    /// <summary>
    /// Scan step for date ranges at or below <see cref="LongRangeDayThreshold"/>
    /// days, expressed as a fraction of a Julian Day. Default: 15 minutes.
    /// </summary>
    public double ShortRangeStep { get; init; } = 15.0 / 1440.0;

    /// <summary>
    /// Scan step for date ranges above <see cref="LongRangeDayThreshold"/>
    /// days, expressed as a fraction of a Julian Day. Default: 30 minutes.
    /// </summary>
    public double LongRangeStep { get; init; } = 30.0 / 1440.0;

    /// <summary>
    /// Day threshold at which the scan switches from fine to coarse step size.
    /// Default: 90 days.
    /// </summary>
    public int LongRangeDayThreshold { get; init; } = 90;

    /// <summary>
    /// Orb threshold (in degrees) below which an aspect is annotated "exact".
    /// Default: 10 arcminutes.
    /// </summary>
    public double ExactOrbThreshold { get; init; } = 10.0 / 60.0;

    /// <summary>
    /// Orb threshold (in degrees) above which an aspect is annotated "wide".
    /// Default: 5.0 degrees.
    /// </summary>
    public double WideOrbThreshold { get; init; } = 5.0;

    /// <summary>
    /// Returns the default instance with standard electional settings.
    /// </summary>
    public static ElectionalOptions Default { get; } = new();
}
