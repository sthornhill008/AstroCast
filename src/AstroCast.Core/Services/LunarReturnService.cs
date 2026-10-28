using System.Globalization;
using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates lunar return charts by finding the exact UTC moments the transiting
/// Moon returns to its natal ecliptic longitude within a date range, then casting
/// a full chart at each moment for the specified location.
/// </summary>
/// <remarks>
/// <para>
/// The Moon completes a full ecliptic cycle every ~27.32 days, producing
/// approximately 13 returns per year. Each return time is found using
/// <see cref="ReturnChartFinder"/>: daily step scan to bracket, then
/// 20-iteration binary search refinement for sub-arc-second precision.
/// </para>
/// <para>
/// The Moon moves approximately 13.2°/day (range 11.8°–14.8°/day), so daily
/// scanning will never miss a return. The Moon never retrogrades in ecliptic
/// longitude, so every sign change is a genuine return (no opposition filtering).
/// </para>
/// </remarks>
public sealed class LunarReturnService : ILunarReturnService
{
    /// <summary>
    /// Number of days to scan when finding the next lunar return.
    /// 35 days guarantees at least one full Moon cycle (~27.32 days).
    /// </summary>
    private const int NextReturnScanDays = 35;

    /// <summary>
    /// Extended scan range in days if no return found in the initial range.
    /// 60 days covers more than two full Moon cycles.
    /// </summary>
    private const int ExtendedScanDays = 60;

    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="LunarReturnService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for Moon longitude lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting the full return chart.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public LunarReturnService(
        IEphemerisService ephemeris,
        IChartEngine chartEngine,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(chartEngine);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _chartEngine = chartEngine;
        _logger = logger.ForContext<LunarReturnService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public IReadOnlyList<LunarReturnResult> Calculate(
        ChartResult natalChart,
        DateTime startDate,
        DateTime endDate,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        if (endDate <= startDate)
            throw new ArgumentOutOfRangeException(nameof(endDate),
                "End date must be after start date.");

        // Step 1: Extract natal Moon longitude
        double natalMoonLon = ExtractNatalMoonLongitude(natalChart);

        _logger.Debug(
            "Searching for lunar returns: natal Moon at {NatalMoonLon:F4}° from {Start} to {End}",
            natalMoonLon, startDate, endDate);

        // Step 2: Find all return moments in the date range. Its bounds are dates picked on a
        // calendar, so they are midnights on the return chart's own clock, not in Greenwich:
        // read as UT, the range began and ended the evening before in the Americas (T0-2). Each
        // is read once to the clock it denotes (D21; branch R3, 1.3.4, R-D7): a bound on or before
        // 1582-10-04 is a Julian date, one in 1582-10-05 to 1582-10-14 is refused.
        double jdStart = JulianDayUtility.LocalDateTimeToJulianDay(
            _ephemeris, CalendarDateFormatter.ProlepticClock(startDate), _timeZoneRules, timeZone);
        double jdEnd = JulianDayUtility.LocalDateTimeToJulianDay(
            _ephemeris, CalendarDateFormatter.ProlepticClock(endDate), _timeZoneRules, timeZone);

        List<double> returnJds = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, natalMoonLon,
            jdStart, jdEnd, filterConjunctionOnly: true, cancellationToken);

        _logger.Debug(
            "Found {Count} lunar return(s) in date range", returnJds.Count);

        // Step 3: Build a result for each return
        string engineVersion = BuildVersionString();
        DateTime calculatedAt = DateTime.UtcNow;
        List<LunarReturnResult> results = new(returnJds.Count);

        for (int i = 0; i < returnJds.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LunarReturnResult result = BuildReturnResult(
                natalChart, natalMoonLon, returnJds[i], i + 1,
                latitude, longitude, locationName, timeZone,
                engineVersion, calculatedAt, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public LunarReturnResult CalculateNext(
        ChartResult natalChart,
        DateTime onOrAfter,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        double natalMoonLon = ExtractNatalMoonLongitude(natalChart);

        _logger.Debug(
            "Searching for next lunar return: natal Moon at {NatalMoonLon:F4}° on or after {Date}",
            natalMoonLon, onOrAfter);

        double jdStart = DateTimeToJulianDay(onOrAfter);

        // Try the standard scan range first (35 days = at least one full cycle)
        double jdEnd = jdStart + NextReturnScanDays;
        List<double> returnJds = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, natalMoonLon,
            jdStart, jdEnd, filterConjunctionOnly: true, cancellationToken);

        // Extend if nothing found (shouldn't happen, but be defensive)
        if (returnJds.Count == 0)
        {
            jdEnd = jdStart + ExtendedScanDays;
            returnJds = ReturnChartFinder.FindReturnJulianDays(
                _ephemeris, CelestialBodyId.Moon, natalMoonLon,
                jdStart, jdEnd, filterConjunctionOnly: true, cancellationToken);
        }

        if (returnJds.Count == 0)
            throw new InvalidOperationException(
                $"No lunar return found within {ExtendedScanDays} days of {onOrAfter:yyyy-MM-dd}. " +
                "This should not happen — verify the natal Moon longitude.");

        string engineVersion = BuildVersionString();

        return BuildReturnResult(
            natalChart, natalMoonLon, returnJds[0], 1,
            latitude, longitude, locationName, timeZone,
            engineVersion, DateTime.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Extracts the natal Moon ecliptic longitude from a chart result.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <returns>Natal Moon longitude in degrees (0–360°).</returns>
    /// <exception cref="InvalidOperationException">If the chart has no Moon position.</exception>
    private static double ExtractNatalMoonLongitude(ChartResult natalChart)
    {
        foreach (PlanetPosition p in natalChart.Positions)
        {
            if (p.Body == CelestialBodyId.Moon)
                return p.Longitude;
        }

        throw new InvalidOperationException(
            "Natal chart does not contain a Moon position.");
    }

    /// <summary>
    /// Builds a single <see cref="LunarReturnResult"/> from a refined Julian Day.
    /// Casts the full chart, verifies precision, and assembles the result.
    /// </summary>
    private LunarReturnResult BuildReturnResult(
        ChartResult natalChart,
        double natalMoonLon,
        double jdExact,
        int sequenceNumber,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        string engineVersion,
        DateTime calculatedAt,
        CancellationToken cancellationToken)
    {
        DateTime returnUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdExact);

        // Cast the full chart at the exact return moment
        ChartInput returnInput = new()
        {
            Name = $"Lunar Return {natalChart.Input.Name} {CalendarDateFormatter.Date(returnUtc)}",
            LocalDateTime = returnUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = "Lunar Return",
            // The return chart carries its own five when the natal chart does (branch Asteroids, 1.6.0).
            IncludeMinorPoints = natalChart.Input.IncludeMinorPoints,
        };

        // At the return's Julian Day: returnUtc is only the label the chart prints (D4).
        ChartResult returnChart = _chartEngine.CalculateAtInstant(returnInput, jdExact, cancellationToken);

        // Verify precision
        double returnMoonLon = 0.0;
        foreach (PlanetPosition p in returnChart.Positions)
        {
            if (p.Body == CelestialBodyId.Moon)
            {
                returnMoonLon = p.Longitude;
                break;
            }
        }

        double precisionDegrees = Math.Abs(
            ReturnChartFinder.SignedLongitudeDelta(returnMoonLon, natalMoonLon));
        double precisionArcSeconds = precisionDegrees * 3600.0;

        // Information-level stays PII-free (M-6); the subject name is Debug-only.
        _logger.Information(
            "Lunar return #{Seq}: natal Moon {NatalLon:F4}°, return Moon {ReturnLon:F4}°, " +
            "precision {Precision:F4}\" at {ReturnUtc:yyyy-MM-dd HH:mm:ss} UTC",
            sequenceNumber, natalMoonLon, returnMoonLon,
            precisionArcSeconds, returnUtc);
        _logger.Debug(
            "Lunar return #{Seq} for '{Name}' computed",
            sequenceNumber, natalChart.Input.Name);

        return new LunarReturnResult
        {
            NatalChart = natalChart,
            ReturnChart = returnChart,
            SequenceNumber = sequenceNumber,
            NatalMoonLongitude = natalMoonLon,
            ReturnMoonLongitude = returnMoonLon,
            ReturnLocationName = locationName,
            ReturnLatitude = latitude,
            ReturnLongitude = longitude,
            PrecisionArcSeconds = precisionArcSeconds,
            EngineVersion = engineVersion,
            CalculatedAt = calculatedAt,
        };
    }

    /// <summary>
    /// Converts a UTC instant to its Julian Day number by arithmetic (branch R3, 1.3.4); until then it
    /// read the instant's components through the calendar switch.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>Its Julian Day (UT).</returns>
    private static double DateTimeToJulianDay(DateTime utc) => JulianDayUtility.InstantToJulianDay(utc);

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(LunarReturnService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;
        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }
}
