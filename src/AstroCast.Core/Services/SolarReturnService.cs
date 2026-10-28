using System.Globalization;
using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates solar return charts by finding the exact UTC moment the transiting
/// Sun returns to its natal ecliptic longitude nearest the native's birthday in a
/// target year, then casting a full chart at that moment for the specified location.
/// </summary>
/// <remarks>
/// <para>
/// "Solar Return Y" (ruling T-D7) is the return nearest the birthday in Y, the
/// birthday being 12:00 UT on the birth's local month and day in Y, read on the
/// calendar in force on that date (29 February in a common year is read as
/// 28 February). A December native therefore gets December of Y, and a native born
/// on 1 January the return of 31 December of Y-1, whichever is nearer the birthday.
/// The birth year gives the birth instant itself.
/// </para>
/// <para>
/// The solar return time is found using a two-phase approach via
/// <see cref="ReturnChartFinder"/>:
/// (1) daily step scan across <see cref="ReturnWindowHalfWidthDays"/> days either
/// side of the birthday to bracket every return there,
/// (2) binary search refinement (20 iterations) for sub-second precision.
/// </para>
/// <para>
/// The Sun moves approximately 1°/day, so daily scanning will never miss
/// a return. The 0°/360° boundary near 0° Aries is handled by a signed
/// longitude delta function that wraps correctly.
/// </para>
/// </remarks>
public sealed class SolarReturnService : ISolarReturnService
{
    /// <summary>
    /// Days searched either side of the birthday. The return nearest the birthday is
    /// never more than half a year (182.6 days) from it, and is in practice within two
    /// weeks of the birthday's date (more for a birth before 1583, whose Julian date drifts
    /// against the Gregorian calendar); 200 days holds it wherever it falls.
    /// </summary>
    private const double ReturnWindowHalfWidthDays = 200.0;

    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="SolarReturnService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for Sun longitude lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting the full return chart.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public SolarReturnService(
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
        _logger = logger.ForContext<SolarReturnService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public SolarReturnResult Calculate(
        ChartResult natalChart,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        if (year < -5400 || year > 5400)
            throw new ArgumentOutOfRangeException(nameof(year),
                $"Year {year} is outside the Swiss Ephemeris supported range of -5400 to 5400.");

        // Step 1: Extract natal Sun longitude
        PlanetPosition? natalSun = null;
        foreach (PlanetPosition p in natalChart.Positions)
        {
            if (p.Body == CelestialBodyId.Sun)
            {
                natalSun = p;
                break;
            }
        }

        if (natalSun is null)
            throw new InvalidOperationException(
                "Natal chart does not contain a Sun position.");

        double natalSunLon = natalSun.Longitude;

        _logger.Debug(
            "Searching for solar return: natal Sun at {NatalSunLon:F4}° in year {Year}",
            natalSunLon, year);

        // Step 2: Find the return nearest the birthday in the target year (T-D7): the
        // birthday is 12:00 UT on the birth's local month and day in that year, read on
        // the calendar in force on that date by DateToJulianDay (Julian before 1582-10-15).
        cancellationToken.ThrowIfCancellationRequested();
        int birthMonth = natalChart.Input.LocalDateTime.Month;
        int birthDay = natalChart.Input.LocalDateTime.Day;
        bool leapYear = year <= 1582 ? year % 4 == 0 : DateTime.IsLeapYear(year);
        if (birthMonth == 2 && birthDay == 29 && !leapYear)
        {
            birthDay = 28;
        }

        double birthdayJd = _ephemeris.DateToJulianDay(year, birthMonth, birthDay, 12.0);
        double jdStart = birthdayJd - ReturnWindowHalfWidthDays;
        double jdEnd = birthdayJd + ReturnWindowHalfWidthDays;

        List<double> returnJds = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Sun, natalSunLon,
            jdStart, jdEnd, filterConjunctionOnly: true, cancellationToken);

        // The birth instant is itself a return: the Sun at its natal longitude. A scan
        // sample that falls exactly on it (a birth at the sample hour) sees no sign
        // change, so whenever the window holds the birth instant it is a candidate, in
        // place of the scan's estimate of the same crossing.
        if (natalChart.JulianDay >= jdStart && natalChart.JulianDay <= jdEnd)
        {
            returnJds.RemoveAll(jd => Math.Abs(jd - natalChart.JulianDay) < 1.0);
            returnJds.Add(natalChart.JulianDay);
        }

        if (returnJds.Count == 0)
            throw new InvalidOperationException(
                $"Solar return not found in year {year}. " +
                "This should not happen — verify the natal Sun longitude.");

        // Take the return nearest the birthday.
        double jdExact = returnJds[0];
        foreach (double jd in returnJds)
        {
            if (Math.Abs(jd - birthdayJd) < Math.Abs(jdExact - birthdayJd))
            {
                jdExact = jd;
            }
        }
        DateTime returnUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdExact);

        _logger.Debug(
            "Solar return found at JD {JdExact:F6} = {ReturnUtc:yyyy-MM-dd HH:mm:ss} UTC",
            jdExact, returnUtc);

        // Step 3: Cast the full chart at the exact return moment
        cancellationToken.ThrowIfCancellationRequested();

        ChartInput returnInput = new()
        {
            Name = string.Create(CultureInfo.InvariantCulture, $"Solar Return {natalChart.Input.Name} {year}"),
            LocalDateTime = returnUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = "Solar Return",
            // The return chart carries its own five when the natal chart does (branch Asteroids, 1.6.0).
            IncludeMinorPoints = natalChart.Input.IncludeMinorPoints,
        };

        // At the return's Julian Day: returnUtc is only the label the chart prints (D4).
        ChartResult returnChart = _chartEngine.CalculateAtInstant(returnInput, jdExact, cancellationToken);

        // Step 4: Verify precision
        PlanetPosition? returnSun = null;
        foreach (PlanetPosition p in returnChart.Positions)
        {
            if (p.Body == CelestialBodyId.Sun)
            {
                returnSun = p;
                break;
            }
        }

        double returnSunLon = returnSun?.Longitude ?? 0.0;
        double precisionDegrees = Math.Abs(
            ReturnChartFinder.SignedLongitudeDelta(returnSunLon, natalSunLon));
        double precisionArcSeconds = precisionDegrees * 3600.0;

        // Information-level stays PII-free (M-6); the subject name is Debug-only.
        _logger.Information(
            "Solar return year {Year}: natal Sun {NatalLon:F4}°, return Sun {ReturnLon:F4}°, " +
            "precision {Precision:F4}\" at {ReturnUtc:yyyy-MM-dd HH:mm:ss} UTC",
            year, natalSunLon, returnSunLon, precisionArcSeconds, returnUtc);
        _logger.Debug(
            "Solar return for '{Name}' year {Year} computed",
            natalChart.Input.Name, year);

        // Step 5: Build result
        string engineVersion = BuildVersionString();

        return new SolarReturnResult
        {
            NatalChart = natalChart,
            ReturnChart = returnChart,
            Year = year,
            NatalSunLongitude = natalSunLon,
            ReturnSunLongitude = returnSunLon,
            ReturnLocationName = locationName,
            ReturnLatitude = latitude,
            ReturnLongitude = longitude,
            PrecisionArcSeconds = precisionArcSeconds,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Computes a signed delta between a current longitude and a target longitude,
    /// handling the 0°/360° wraparound. Delegates to
    /// <see cref="ReturnChartFinder.SignedLongitudeDelta"/>.
    /// </summary>
    /// <param name="current">Current ecliptic longitude (0–360°).</param>
    /// <param name="target">Target ecliptic longitude (0–360°).</param>
    /// <returns>Signed delta in the range (-180°, 180°], crossing zero at exact match.</returns>
    internal static double SignedLongitudeDelta(double current, double target) =>
        ReturnChartFinder.SignedLongitudeDelta(current, target);

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(SolarReturnService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;
        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }
}
