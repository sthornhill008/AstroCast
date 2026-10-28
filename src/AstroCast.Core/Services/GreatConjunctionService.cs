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
/// Finds Jupiter-Saturn great conjunctions within a date range using the
/// two-body aspect finder, with element/mutation cycle analysis.
/// </summary>
/// <remarks>
/// <para>
/// Great conjunctions occur approximately every 19.86 years. The conjunction
/// sign shifts through elements in a ~200-year mutation cycle: Fire → Earth →
/// Air → Water. A "mutation" marks the first conjunction in a new element.
/// </para>
/// <para>
/// Historically significant: the 2020 conjunction at 0°29' Aquarius marked
/// the mutation from Earth to Air, ending a ~200-year Earth era (1802–2020).
/// </para>
/// </remarks>
public sealed class GreatConjunctionService : IGreatConjunctionService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="GreatConjunctionService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting conjunction charts.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public GreatConjunctionService(
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
        _logger = logger.ForContext<GreatConjunctionService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public GreatConjunctionSeries FindConjunctions(
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentException(
                $"Start year ({startYear}) must be <= end year ({endYear}).",
                nameof(startYear));

        _logger.Debug(
            "Searching for great conjunctions from {Start} to {End}",
            startYear, endYear);

        // [1 January of startYear, 1 January after endYear) UT: the scan's last daily step ends
        // where the next year's request begins, so nothing on 31 December falls between them.
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);

        // The scan steps whole days from jdStart: it runs on to the first step at or after jdEnd, and
        // anything found from jdEnd on belongs to the next window and is dropped.
        double jdScanEnd = EphemerisSpan.WholeDayScanEnd(jdStart, jdEnd);

        List<double> conjunctionJds = ReturnChartFinder.FindAspectJulianDays(
            _ephemeris,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            0.0, // conjunction = 0° separation
            jdStart,
            jdScanEnd,
            cancellationToken);
        conjunctionJds.RemoveAll(jd => jd >= jdEnd);

        List<GreatConjunctionResult> results = [];
        string? previousElement = null;

        foreach (double jd in conjunctionJds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GreatConjunctionResult result = BuildConjunctionResult(
                jd, previousElement, results.Count > 0 ? results[^1] : null);

            results.Add(result);
            previousElement = result.Element;
        }

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.Information(
            "Found {Count} great conjunction(s) from {Start} to {End}",
            results.Count, startYear, endYear);

        return new GreatConjunctionSeries
        {
            Conjunctions = results,
            ScanStart = scanStart,
            ScanEnd = scanEnd,
            Count = results.Count,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <inheritdoc />
    public GreatConjunctionResult FindNearest(
        int year,
        CancellationToken cancellationToken = default)
    {
        // Scan a 30-year window centered on the year
        GreatConjunctionSeries series = FindConjunctions(
            year - 15, year + 15, cancellationToken);

        if (series.Count == 0)
            throw new InvalidOperationException(
                $"No great conjunction found near year {year}.");

        double midYear = year + 0.5;
        // MinBy is O(n) vs. OrderBy().First() at O(n log n); safe to use here because
        // the non-empty guard above ensures the returned reference is never null.
        GreatConjunctionResult nearest = series.Conjunctions
            .MinBy(c => Math.Abs(c.ExactUtc.Year + c.ExactUtc.DayOfYear / 365.25 - midYear))!;

        return nearest;
    }

    /// <inheritdoc />
    public ChartResult CastConjunctionChart(
        GreatConjunctionResult conjunction,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conjunction);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        ChartInput input = new()
        {
            Name = $"Great Conjunction {CalendarDateFormatter.Date(conjunction.ExactUtc)}",
            LocalDateTime = conjunction.ExactUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = "Great Conjunction",
        };

        // At the conjunction's instant, by arithmetic: ExactUtc is the instant, and the label (D4).
        return _chartEngine.CalculateAtInstant(input, JulianDayUtility.InstantToJulianDay(conjunction.ExactUtc), cancellationToken);
    }

    /// <summary>
    /// Build a <see cref="GreatConjunctionResult"/> from a found conjunction Julian Day.
    /// </summary>
    private GreatConjunctionResult BuildConjunctionResult(
        double jd,
        string? previousElement,
        GreatConjunctionResult? previousConjunction)
    {
        DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

        PlanetPosition jupPos = _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Jupiter);
        PlanetPosition satPos = _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Saturn);

        // Use the midpoint of the two longitudes as the conjunction position
        double jupLon = jupPos.Longitude;
        double satLon = satPos.Longitude;
        double midLon = MidpointLongitude(jupLon, satLon);

        string sign = GetSignName(midLon);
        string element = GetElement(sign);
        string modality = GetModality(sign);
        string posFormatted = PositionFormatter.FormatLongitude(midLon);

        // Precision: separation in arc-seconds
        double separation = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(jupLon, satLon));
        double precisionArcSec = separation * 3600.0;

        // Mutation detection
        bool isMutation = previousElement is not null && element != previousElement;

        // Years since previous
        double yearsSincePrev = 0.0;
        if (previousConjunction is not null)
        {
            yearsSincePrev = (exactUtc - previousConjunction.ExactUtc).TotalDays / 365.25;
        }

        _logger.Debug(
            "Great conjunction at {Date:yyyy-MM-dd}: {Pos} ({Element}) precision {Prec:F2}\"",
            exactUtc, posFormatted, element, precisionArcSec);

        return new GreatConjunctionResult
        {
            ExactUtc = exactUtc,
            Longitude = midLon,
            PositionFormatted = posFormatted,
            Sign = sign,
            Element = element,
            Modality = modality,
            IsMutation = isMutation,
            YearsSincePrevious = Math.Round(yearsSincePrev, 1),
            JupiterLongitude = jupLon,
            SaturnLongitude = satLon,
            PrecisionArcSeconds = precisionArcSec,
        };
    }

    /// <summary>
    /// Compute the midpoint longitude between two ecliptic longitudes,
    /// correctly handling the 0°/360° wraparound.
    /// </summary>
    private static double MidpointLongitude(double lon1, double lon2)
    {
        double delta = ReturnChartFinder.SignedLongitudeDelta(lon2, lon1);
        double mid = lon1 + delta / 2.0;
        return ((mid % 360.0) + 360.0) % 360.0;
    }

    /// <summary>
    /// Get the zodiac sign name from an ecliptic longitude.
    /// </summary>
    private static string GetSignName(double longitude)
    {
        double normalised = ((longitude % 360.0) + 360.0) % 360.0;
        int signIndex = (int)(normalised / 30.0);
        ZodiacSign sign = (ZodiacSign)(signIndex + 1);
        return sign.ToString();
    }

    /// <summary>
    /// Get the element (Fire, Earth, Air, Water) for a zodiac sign.
    /// </summary>
    private static string GetElement(string sign) => sign switch
    {
        "Aries" or "Leo" or "Sagittarius" => "Fire",
        "Taurus" or "Virgo" or "Capricorn" => "Earth",
        "Gemini" or "Libra" or "Aquarius" => "Air",
        "Cancer" or "Scorpio" or "Pisces" => "Water",
        _ => "Unknown",
    };

    /// <summary>
    /// Get the modality (Cardinal, Fixed, Mutable) for a zodiac sign.
    /// </summary>
    private static string GetModality(string sign) => sign switch
    {
        "Aries" or "Cancer" or "Libra" or "Capricorn" => "Cardinal",
        "Taurus" or "Leo" or "Scorpio" or "Aquarius" => "Fixed",
        "Gemini" or "Virgo" or "Sagittarius" or "Pisces" => "Mutable",
        _ => "Unknown",
    };

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(GreatConjunctionService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }
}
