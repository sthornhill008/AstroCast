using Serilog;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Casts the Davison chart of two charts (branch Davison, 1.7.0; Steve's topics 1, 2 and 6 of 2026-10-21): a real
/// chart for the moment halfway between the two births, the mean of their Julian Days in UT, at the place halfway
/// between their birthplaces, the mean latitude and the mean longitude by the shorter arc, in chart A's house
/// system, through <see cref="IChartEngine.CalculateAtInstant"/> at an explicit offset of zero, the path every
/// event chart takes. The midpoint instant is taken once, from the Julian Days.
/// </summary>
public sealed class DavisonService : IDavisonService
{
    /// <summary>The event type a Davison chart carries, a string the chart report prints as its Type line.</summary>
    public const string EventTypeName = "Davison";

    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="DavisonService"/>.
    /// </summary>
    /// <param name="chartEngine">The chart engine, which casts the chart.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public DavisonService(IChartEngine chartEngine, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(chartEngine);
        ArgumentNullException.ThrowIfNull(logger);

        _chartEngine = chartEngine;
        _logger = logger.ForContext<DavisonService>();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when either chart is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    /// <remarks>
    /// A horary chart as either source is allowed: the Davison chart's type is "Davison", so the engine's horary
    /// branch and the five's horary gate never fire. The five follow <paramref name="includeMinorPoints"/> alone,
    /// as for any cast chart.
    /// </remarks>
    public DavisonResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        bool includeMinorPoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chartA);
        ArgumentNullException.ThrowIfNull(chartB);

        _logger.Debug(
            "Calculating Davison chart: '{ChartA}' & '{ChartB}'",
            chartA.Input.Name, chartB.Input.Name);

        double midpointJd = MidpointJulianDay(chartA.JulianDay, chartB.JulianDay);
        DateTime midpointUtc = JulianDayUtility.JulianDayToInstant(midpointJd);
        double midpointLatitude = (chartA.Input.Latitude + chartB.Input.Latitude) / 2.0;
        double midpointLongitude = MidpointLongitude(chartA.Input.Longitude, chartB.Input.Longitude);
        string description = $"Midpoint of {chartA.Input.LocationName} & {chartB.Input.LocationName}";

        ChartInput input = new()
        {
            Name = $"Davison: {chartA.Input.Name} & {chartB.Input.Name}",
            LocalDateTime = midpointUtc,
            TimeZone = "UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = midpointLatitude,
            Longitude = midpointLongitude,
            LocationName = description,
            EventType = EventTypeName,
            HouseSystem = chartA.Input.HouseSystem,
            IncludeMinorPoints = includeMinorPoints,
        };

        cancellationToken.ThrowIfCancellationRequested();
        ChartResult davisonChart = _chartEngine.CalculateAtInstant(input, midpointJd, cancellationToken);

        _logger.Information(
            "Davison chart complete: '{ChartA}' & '{ChartB}' at {MidpointUtc:yyyy-MM-dd HH:mm:ss} UT (JD {MidpointJd:F6}), " +
            "{Latitude:F4}, {Longitude:F4} ({Place}); {PositionCount} positions, {AspectCount} aspects, {HouseSystem} houses",
            chartA.Input.Name, chartB.Input.Name, midpointUtc, midpointJd,
            midpointLatitude, midpointLongitude, description,
            davisonChart.Positions.Count, davisonChart.Aspects.Count, davisonChart.HouseSystemName);

        return new DavisonResult
        {
            ChartA = chartA,
            ChartB = chartB,
            DavisonChart = davisonChart,
            MidpointUtc = midpointUtc,
            MidpointJulianDay = midpointJd,
            MidpointLatitude = midpointLatitude,
            MidpointLongitude = midpointLongitude,
            MidpointLocationDescription = description,
            EngineVersion = davisonChart.EngineVersion,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// The midpoint moment of two instants as Julian Days (UT): their mean, taken once here. A mean of two Julian
    /// Days near 2.45 million is exact to well under a millisecond; the engine then rounds the instant it casts at
    /// to the whole second.
    /// </summary>
    /// <param name="jdA">The first instant, as a Julian Day (UT).</param>
    /// <param name="jdB">The second instant, as a Julian Day (UT).</param>
    /// <returns>The mean, as a Julian Day (UT).</returns>
    internal static double MidpointJulianDay(double jdA, double jdB) => (jdA + jdB) / 2.0;

    /// <summary>
    /// The midpoint of two longitudes by the shorter arc round the globe: the difference from A to B normalised to
    /// (−180°, 180°], half of it added to A, the result normalised to (−180°, 180°] again. Two places 180° apart
    /// have no shorter arc; the difference normalises to +180°, so the midpoint is the eastward half from A: 170°
    /// and −10° give −100°, −10° and 170° give 80°. New York and Tokyo (−74.006°, 139.6917°) give −147.15715°, in
    /// the Pacific, where the plain mean would give 32.8429°, in the Mediterranean.
    /// </summary>
    /// <param name="lonA">The first longitude, in degrees, east positive.</param>
    /// <param name="lonB">The second longitude, in degrees, east positive.</param>
    /// <returns>The midpoint longitude, in degrees in (−180°, 180°].</returns>
    internal static double MidpointLongitude(double lonA, double lonB)
    {
        double difference = NormalizeSigned(lonB - lonA);
        return NormalizeSigned(lonA + difference / 2.0);
    }

    /// <summary>A longitude normalised to (−180°, 180°]: −180° becomes 180°.</summary>
    /// <param name="degrees">The longitude, in degrees.</param>
    /// <returns>The same direction in (−180°, 180°].</returns>
    private static double NormalizeSigned(double degrees)
    {
        double wrapped = ((degrees + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }
}
