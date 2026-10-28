using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds the exact moments when a celestial body enters a zodiac sign
/// and optionally casts full Event charts at those moments.
/// </summary>
/// <remarks>
/// <para>
/// An ingress occurs when a planet crosses from one zodiac sign into the next.
/// The four cardinal ingresses of the Sun (0° Aries, 0° Cancer, 0° Libra,
/// 0° Capricorn) are the most important in mundane astrology: the Aries ingress
/// is the traditional "world year" chart.
/// </para>
/// <para>
/// For outer planets that retrograde across a sign boundary, the service records
/// ALL crossings. The user identifies the final permanent ingress from the
/// chronological sequence. For <see cref="CalculateIngress"/>, the service
/// defaults to the last crossing for outer planets (the permanent ingress)
/// and the first crossing for inner planets.
/// </para>
/// </remarks>
public sealed class IngressService : IIngressService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Sign entry longitudes: 0° of each sign.
    /// </summary>
    private static readonly (ZodiacSign Sign, double Longitude)[] SignEntries =
    [
        (ZodiacSign.Aries, 0.0),
        (ZodiacSign.Taurus, 30.0),
        (ZodiacSign.Gemini, 60.0),
        (ZodiacSign.Cancer, 90.0),
        (ZodiacSign.Leo, 120.0),
        (ZodiacSign.Virgo, 150.0),
        (ZodiacSign.Libra, 180.0),
        (ZodiacSign.Scorpio, 210.0),
        (ZodiacSign.Sagittarius, 240.0),
        (ZodiacSign.Capricorn, 270.0),
        (ZodiacSign.Aquarius, 300.0),
        (ZodiacSign.Pisces, 330.0),
    ];

    /// <summary>Cardinal sign entry longitudes.</summary>
    private static readonly HashSet<double> CardinalLongitudes = [0.0, 90.0, 180.0, 270.0];

    /// <summary>Season names for cardinal Sun ingresses.</summary>
    private static readonly Dictionary<double, string> SeasonNames = new()
    {
        [0.0] = "Vernal Equinox",
        [90.0] = "Summer Solstice",
        [180.0] = "Autumnal Equinox",
        [270.0] = "Winter Solstice",
    };

    /// <summary>
    /// Outer planet IDs whose ingress should default to the last crossing
    /// (permanent ingress) when retrograde produces multiple sign-boundary crossings.
    /// </summary>
    private static readonly HashSet<CelestialBodyId> OuterPlanets =
    [
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="IngressService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting ingress charts.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public IngressService(
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
        _logger = logger.ForContext<IngressService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public IngressSeries FindIngresses(
        CelestialBodyId planet,
        int startYear,
        int endYear,
        bool cardinalOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentException(
                $"Start year ({startYear}) must be <= end year ({endYear}).",
                nameof(startYear));

        _logger.Debug(
            "Searching for {Planet} ingresses from {Start} to {End} (cardinalOnly={CardinalOnly})",
            planet, startYear, endYear, cardinalOnly);

        // [1 January of startYear, 1 January after endYear) UT: the scan's last daily step ends
        // where the next year's request begins, so nothing on 31 December falls between them.
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);

        // The scan steps whole days from jdStart: it runs on to the first step at or after jdEnd, and
        // anything found from jdEnd on belongs to the next window and is dropped.
        double jdScanEnd = EphemerisSpan.WholeDayScanEnd(jdStart, jdEnd);

        // Determine which sign entries to scan
        IEnumerable<(ZodiacSign Sign, double Longitude)> targets = cardinalOnly
            ? SignEntries.Where(e => CardinalLongitudes.Contains(e.Longitude))
            : SignEntries;

        List<IngressResult> allIngresses = [];

        foreach ((ZodiacSign sign, double longitude) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<double> crossingJds = ReturnChartFinder.FindReturnJulianDays(
                _ephemeris,
                planet,
                longitude,
                jdStart,
                jdScanEnd,
                filterConjunctionOnly: true,
                cancellationToken);
            crossingJds.RemoveAll(jd => jd >= jdEnd);

            foreach (double jd in crossingJds)
            {
                IngressResult result = BuildIngressResult(planet, sign, longitude, jd);
                allIngresses.Add(result);
            }
        }

        // Sort chronologically
        allIngresses.Sort((a, b) => a.ExactUtc.CompareTo(b.ExactUtc));

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.Information(
            "Found {Count} {Planet} ingress(es) from {Start} to {End}",
            allIngresses.Count, planet, startYear, endYear);

        return new IngressSeries
        {
            Planet = planet,
            PlanetName = planet.ToString(),
            Ingresses = allIngresses,
            ScanStart = scanStart,
            ScanEnd = scanEnd,
            Count = allIngresses.Count,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <inheritdoc />
    public IngressResult CalculateIngress(
        CelestialBodyId planet,
        ZodiacSign sign,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        double targetLongitude = ((int)sign - 1) * 30.0;

        _logger.Debug(
            "Calculating {Planet} ingress into {Sign} ({Target}°) for year {Year} at {Location}",
            planet, sign, targetLongitude, year, locationName);

        double jdStart;
        double jdEnd;
        if (planet == CelestialBodyId.Sun)
        {
            // The Sun's ingress of a year (T4-1): its one crossing of the sign's first
            // degree in the UT calendar year, 1 January 00:00 (inclusive) to 1 January
            // 00:00 of the next year (exclusive). From 1200 to 2399 the Sun enters every
            // sign exactly once in each calendar year.
            jdStart = _ephemeris.DateToJulianDay(year, 1, 1, 0.0);
            jdEnd = _ephemeris.DateToJulianDay(year + 1, 1, 1, 0.0);
        }
        else
        {
            // Generous scan window: year ± 6 months for outer planets
            jdStart = _ephemeris.DateToJulianDay(year - 1, 7, 1, 0.0);
            jdEnd = _ephemeris.DateToJulianDay(year + 1, 7, 1, 0.0);
        }

        List<double> crossingJds = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris,
            planet,
            targetLongitude,
            jdStart,
            jdEnd,
            filterConjunctionOnly: true,
            cancellationToken);

        if (crossingJds.Count == 0)
            throw new InvalidOperationException(
                $"No {planet} ingress into {sign} found near year {year}.");

        // For outer planets: take the last crossing (permanent ingress).
        // For inner planets: take the first crossing; the Sun's year holds only one.
        double selectedJd = OuterPlanets.Contains(planet)
            ? crossingJds[^1]
            : crossingJds[0];

        // Cast full chart at the exact ingress moment
        DateTime ingressUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, selectedJd);
        string eventDescription = $"{planet} enters {sign}";

        ChartInput input = new()
        {
            Name = eventDescription,
            LocalDateTime = ingressUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = "Ingress",
        };

        // At the ingress's Julian Day: ingressUtc is only the label the chart prints (D4).
        ChartResult chart = _chartEngine.CalculateAtInstant(input, selectedJd, cancellationToken);

        // Build result with chart
        IngressResult result = BuildIngressResult(planet, sign, targetLongitude, selectedJd);

        // Return a new instance with the chart attached
        return new IngressResult
        {
            Planet = result.Planet,
            PlanetName = result.PlanetName,
            Sign = result.Sign,
            Element = result.Element,
            Modality = result.Modality,
            TargetLongitude = result.TargetLongitude,
            ActualLongitude = result.ActualLongitude,
            PrecisionArcSeconds = result.PrecisionArcSeconds,
            ExactUtc = result.ExactUtc,
            IsCardinalIngress = result.IsCardinalIngress,
            SeasonName = result.SeasonName,
            Chart = chart,
            EngineVersion = result.EngineVersion,
            CalculatedAt = result.CalculatedAt,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<IngressResult> CalculateCardinalIngresses(
        int year,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default)
    {
        _logger.Debug(
            "Calculating four cardinal Sun ingresses for {Year} at {Location}",
            year, locationName);

        ZodiacSign[] cardinalSigns =
        [
            ZodiacSign.Aries,
            ZodiacSign.Cancer,
            ZodiacSign.Libra,
            ZodiacSign.Capricorn,
        ];

        List<IngressResult> results = [];

        foreach (ZodiacSign sign in cardinalSigns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IngressResult result = CalculateIngress(
                CelestialBodyId.Sun, sign, year,
                latitude, longitude, locationName, timeZone,
                cancellationToken);

            results.Add(result);
        }

        _logger.Information(
            "Calculated {Count} cardinal ingress chart(s) for {Year}",
            results.Count, year);

        return results;
    }

    #region Private Helpers

    /// <summary>
    /// Build an <see cref="IngressResult"/> from a found ingress Julian Day (timing only, no chart).
    /// </summary>
    private IngressResult BuildIngressResult(
        CelestialBodyId planet, ZodiacSign sign, double targetLongitude, double jd)
    {
        DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

        // Get actual longitude at the found moment for precision measurement
        PlanetPosition pos = _ephemeris.CalculatePositionLongitudeOnly(jd, planet);
        double actualLongitude = pos.Longitude;

        // Precision: distance from target in arc-seconds
        double delta = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(actualLongitude, targetLongitude));
        double precisionArcSec = delta * 3600.0;

        bool isCardinal = planet == CelestialBodyId.Sun
            && CardinalLongitudes.Contains(targetLongitude);

        string? seasonName = isCardinal && SeasonNames.TryGetValue(targetLongitude, out string? name)
            ? name
            : null;

        // The sign entered: the one that begins at the boundary when the body crosses it moving
        // forward, the one that ends there when it crosses retrograde (T4-2).
        string signName = SignEntered(sign, pos.SpeedInLongitude).ToString();
        string element = GetElement(signName);
        string modality = GetModality(signName);

        return new IngressResult
        {
            Planet = planet,
            PlanetName = planet.ToString(),
            Sign = signName,
            Element = element,
            Modality = modality,
            TargetLongitude = targetLongitude,
            ActualLongitude = actualLongitude,
            PrecisionArcSeconds = precisionArcSec,
            ExactUtc = exactUtc,
            IsCardinalIngress = isCardinal,
            SeasonName = seasonName,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// The sign a body enters when it crosses the boundary where <paramref name="signBeginning"/>
    /// begins: that sign when it moves forward, the sign before it (Pisces before Aries) when it is
    /// retrograde, that is with a negative speed in longitude at the crossing.
    /// </summary>
    /// <param name="signBeginning">The sign that begins at the boundary crossed.</param>
    /// <param name="speedInLongitude">The body's speed in longitude at the crossing (degrees per day).</param>
    /// <returns>The sign entered.</returns>
    private static ZodiacSign SignEntered(ZodiacSign signBeginning, double speedInLongitude)
    {
        if (speedInLongitude >= 0.0)
            return signBeginning;

        return signBeginning == ZodiacSign.Aries ? ZodiacSign.Pisces : signBeginning - 1;
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
        string asmVersion = typeof(IngressService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }

    #endregion
}
