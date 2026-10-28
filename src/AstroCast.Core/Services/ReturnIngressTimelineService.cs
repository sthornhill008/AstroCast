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
/// Produces a combined chronological timeline of all planetary returns
/// and sign ingresses within a date range — a master timing calendar.
/// </summary>
/// <remarks>
/// <para>
/// For returns, lists one entry per passage of each return planet over its natal longitude
/// whose first pass lies in the window, at the first pass, labelled "Return #N" with N the
/// passage's number from birth, and "Return #N (first of 3 passes)" when a retrograde loop lies
/// over the degree (<see cref="ReturnPassageFinder"/>; ruling T-D6, branch F, 1.3.4). Until 1.3.4
/// the crossings of the window were grouped when less than 120 days apart, one kept per group
/// (the last for Mars, Jupiter and Saturn), and the number was elapsed time over the sidereal
/// period, printed "~#" (T2-2, T2-5).
/// </para>
/// <para>
/// For ingresses, scans all 12 sign-entry longitudes (0° of each sign) for
/// each ingress planet. Outer planets that retrograde across a sign boundary
/// produce multiple entries — all crossings are recorded.
/// </para>
/// </remarks>
public sealed class ReturnIngressTimelineService : IReturnIngressTimelineService
{
    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Default return planets: Mercury through Saturn.
    /// </summary>
    private static readonly CelestialBodyId[] DefaultReturnPlanets =
    [
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
    ];

    /// <summary>
    /// Default ingress planets: outer planets + Chiron (sign changes are rare and significant).
    /// </summary>
    private static readonly CelestialBodyId[] DefaultIngressPlanets =
    [
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
        CelestialBodyId.Chiron,
    ];

    /// <summary>
    /// Sign entry longitudes: 0° of each zodiac sign, paired with sign name.
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

    /// <summary>
    /// Season names for cardinal Sun ingresses.
    /// </summary>
    private static readonly Dictionary<double, string> SeasonNames = new()
    {
        [0.0] = "Vernal Equinox",
        [90.0] = "Summer Solstice",
        [180.0] = "Autumnal Equinox",
        [270.0] = "Winter Solstice",
    };

    /// <summary>
    /// Initialises a new instance of <see cref="ReturnIngressTimelineService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public ReturnIngressTimelineService(
        IEphemerisService ephemeris,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<ReturnIngressTimelineService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public ReturnIngressTimeline Calculate(
        ChartResult? natalChart,
        int startYear,
        int endYear,
        bool includeReturns = true,
        bool includeIngresses = true,
        CelestialBodyId[]? returnPlanets = null,
        CelestialBodyId[]? ingressPlanets = null,
        CancellationToken cancellationToken = default)
    {
        if (includeReturns && natalChart is null)
            throw new ArgumentNullException(
                nameof(natalChart),
                "A natal chart is required when includeReturns is true.");

        if (startYear > endYear)
            throw new ArgumentException(
                $"Start year ({startYear}) must be <= end year ({endYear}).",
                nameof(startYear));

        CelestialBodyId[] returnPlanetList = returnPlanets ?? DefaultReturnPlanets;
        CelestialBodyId[] ingressPlanetList = ingressPlanets ?? DefaultIngressPlanets;

        _logger.Debug(
            "Calculating Return & Ingress Timeline from {Start} to {End} (returns={IncReturns}, ingresses={IncIngresses})",
            startYear, endYear, includeReturns, includeIngresses);

        // [1 January of startYear, 1 January after endYear) UT: the scan's last daily step ends
        // where the next year's request begins, so nothing on 31 December falls between them.
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);

        // The scan steps whole days from jdStart: it runs on to the first step at or after jdEnd, and
        // anything found from jdEnd on belongs to the next window and is dropped.
        double jdScanEnd = EphemerisSpan.WholeDayScanEnd(jdStart, jdEnd);

        List<ReturnIngressTimelineEntry> allEntries = [];

        // Part A: Returns
        if (includeReturns && natalChart is not null)
        {
            List<ReturnIngressTimelineEntry> returnEntries = FindReturns(
                natalChart, returnPlanetList, jdStart, jdEnd, cancellationToken);
            allEntries.AddRange(returnEntries);
        }

        // Part B: Ingresses
        if (includeIngresses)
        {
            List<ReturnIngressTimelineEntry> ingressEntries = FindIngresses(
                ingressPlanetList, jdStart, jdScanEnd, jdEnd, cancellationToken);
            allEntries.AddRange(ingressEntries);
        }

        // Part C: Sort chronologically
        allEntries.Sort((a, b) => a.ExactUtc.CompareTo(b.ExactUtc));

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        int returnCount = allEntries.Count(e => e.EventType == "Return");
        int ingressCount = allEntries.Count(e => e.EventType == "Ingress");

        _logger.Information(
            "Return & Ingress Timeline: {Total} events ({Returns} returns, {Ingresses} ingresses) from {Start} to {End}",
            allEntries.Count, returnCount, ingressCount, startYear, endYear);

        return new ReturnIngressTimeline
        {
            NativeName = natalChart?.Input.Name ?? "Unknown",
            Entries = allEntries,
            ScanStart = scanStart,
            ScanEnd = scanEnd,
            Count = allEntries.Count,
            // A search that did not run names no planets (E2-3): the export says "not searched".
            ReturnPlanets = includeReturns ? returnPlanetList.Select(p => p.ToString()).ToList() : [],
            IngressPlanets = includeIngresses ? ingressPlanetList.Select(p => p.ToString()).ToList() : [],
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #region Private Helpers — Returns

    /// <summary>
    /// Find the planetary returns of the specified planets whose first pass lies in the window:
    /// one entry per passage, at the first pass, labelled by the passage's number from birth (R6).
    /// </summary>
    /// <param name="natalChart">The natal chart whose points return.</param>
    /// <param name="planets">The returning planets.</param>
    /// <param name="jdStart">The window's first instant (Julian Day, UT).</param>
    /// <param name="jdEnd">The window's end: a return from it on belongs to the next window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private List<ReturnIngressTimelineEntry> FindReturns(
        ChartResult natalChart,
        CelestialBodyId[] planets,
        double jdStart,
        double jdEnd,
        CancellationToken cancellationToken)
    {
        List<ReturnIngressTimelineEntry> entries = [];

        foreach (CelestialBodyId planet in planets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find the natal longitude for this planet
            PlanetPosition? natalPos = natalChart.Positions
                .FirstOrDefault(p => p.Body == planet);

            if (natalPos is null)
            {
                _logger.Debug("Skipping {Planet} return — not found in natal chart", planet);
                continue;
            }

            double natalLongitude = natalPos.Longitude;

            _logger.Debug(
                "Searching for {Planet} returns to {Longitude:F4}° with a first pass from JD {Start:F2} to {End:F2}",
                planet, natalLongitude, jdStart, jdEnd);

            int found = 0;
            foreach (ReturnPassage passage in ReturnPassageFinder.EnumeratePassages(
                _ephemeris, natalChart, planet, cancellationToken))
            {
                if (passage.FirstPass >= jdEnd)
                    break;
                if (passage.FirstPass < jdStart)
                    continue;

                double jd = passage.FirstPass;
                DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

                // Measure precision
                PlanetPosition pos = _ephemeris.CalculatePositionLongitudeOnly(jd, planet);
                double delta = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(
                    pos.Longitude, natalLongitude));
                double precisionArcSec = delta * 3600.0;

                string posFormatted = PositionFormatter.FormatLongitude(natalLongitude);
                string sign = GetSignFromLongitude(natalLongitude);
                int passCount = passage.PassJulianDays.Count;
                string label = passCount > 1
                    ? string.Create(CultureInfo.InvariantCulture, $"Return #{passage.Number} (first of {passCount} passes)")
                    : string.Create(CultureInfo.InvariantCulture, $"Return #{passage.Number}");

                entries.Add(new ReturnIngressTimelineEntry
                {
                    EventType = "Return",
                    Planet = planet,
                    PlanetName = planet.ToString(),
                    ExactUtc = exactUtc,
                    Longitude = natalLongitude,
                    PositionFormatted = posFormatted,
                    Sign = sign,
                    Label = label,
                    PrecisionArcSeconds = precisionArcSec,
                });
                found++;
            }

            _logger.Debug(
                "Found {Count} {Planet} return(s) in scan range",
                found, planet);
        }

        return entries;
    }

    #endregion

    #region Private Helpers — Ingresses

    /// <summary>
    /// Find all sign ingresses for the specified planets within the scan range.
    /// </summary>
    /// <param name="planets">The planets whose sign entries are found.</param>
    /// <param name="jdStart">The scan's first instant (Julian Day, UT).</param>
    /// <param name="jdScanEnd">The scan's last instant, a whole number of days from the first.</param>
    /// <param name="jdEnd">The window's end: an ingress found from it on belongs to the next window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private List<ReturnIngressTimelineEntry> FindIngresses(
        CelestialBodyId[] planets,
        double jdStart,
        double jdScanEnd,
        double jdEnd,
        CancellationToken cancellationToken)
    {
        List<ReturnIngressTimelineEntry> entries = [];

        foreach (CelestialBodyId planet in planets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach ((ZodiacSign sign, double longitude) in SignEntries)
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
                    DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

                    // Measure precision
                    PlanetPosition pos = _ephemeris.CalculatePositionLongitudeOnly(jd, planet);
                    double delta = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(
                        pos.Longitude, longitude));
                    double precisionArcSec = delta * 3600.0;

                    // Determine label. The sign entered is the one that begins at the boundary
                    // when the body crosses it moving forward, the one that ends there when it
                    // crosses retrograde (T2-6).
                    string label = GetIngressLabel(planet, longitude);
                    string signName = SignEntered(sign, pos.SpeedInLongitude).ToString();
                    string posFormatted = PositionFormatter.FormatLongitude(longitude);

                    entries.Add(new ReturnIngressTimelineEntry
                    {
                        EventType = "Ingress",
                        Planet = planet,
                        PlanetName = planet.ToString(),
                        ExactUtc = exactUtc,
                        Longitude = longitude,
                        PositionFormatted = posFormatted,
                        Sign = signName,
                        Label = label,
                        PrecisionArcSeconds = precisionArcSec,
                    });
                }
            }

            int planetCount = entries.Count(e => e.PlanetName == planet.ToString());
            _logger.Debug("Found {Count} {Planet} ingress(es) in scan range", planetCount, planet);
        }

        return entries;
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
    /// Get the ingress label. For Sun at cardinal points, use season names;
    /// otherwise, use "Sign Entry".
    /// </summary>
    private static string GetIngressLabel(CelestialBodyId planet, double longitude)
    {
        if (planet == CelestialBodyId.Sun && SeasonNames.TryGetValue(longitude, out string? seasonName))
            return seasonName;

        return "Sign Entry";
    }

    #endregion

    #region Private Helpers — Common

    /// <summary>
    /// Get the zodiac sign name from a longitude value.
    /// </summary>
    private static string GetSignFromLongitude(double longitude)
    {
        double normalised = longitude % 360.0;
        int signIndex = (int)(normalised / 30.0);
        ZodiacSign sign = (ZodiacSign)(signIndex + 1);
        return sign.ToString();
    }

    /// <summary>
    /// Builds the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(ReturnIngressTimelineService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }

    #endregion
}
