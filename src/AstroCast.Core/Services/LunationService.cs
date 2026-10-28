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
/// Finds the exact moments of New Moons (Sun-Moon conjunction) and Full Moons
/// (Sun-Moon opposition) with sub-arc-second precision, including eclipse proximity
/// detection based on lunar node distance.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="ReturnChartFinder.FindAspectJulianDays"/> with target angle 0°
/// for New Moons and 180° for Full Moons. The Moon moves ~13°/day, giving a
/// Sun-Moon separation change of ~12°/day — safe for daily-step scanning.
/// </para>
/// <para>
/// Eclipse proximity is an approximation: New Moon within 18° of a node flags
/// a possible solar eclipse; Full Moon within 12° flags a possible lunar eclipse.
/// Precise eclipse calculation requires additional factors (node latitude, parallax).
/// </para>
/// </remarks>
public sealed class LunationService : ILunationService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Angular distance threshold from the True Node for a New Moon to be a
    /// potential solar eclipse (degrees).
    /// </summary>
    private const double SolarEclipseNodeOrb = 18.0;

    /// <summary>
    /// Angular distance threshold from the True Node for a Full Moon to be a
    /// potential lunar eclipse (degrees).
    /// </summary>
    private const double LunarEclipseNodeOrb = 12.0;

    /// <summary>
    /// Initialises a new instance of <see cref="LunationService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting lunation charts.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public LunationService(
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
        _logger = logger.ForContext<LunationService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public LunationSeries FindLunations(
        int startYear, int endYear,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentException(
                $"Start year ({startYear}) must be <= end year ({endYear}).",
                nameof(startYear));

        _logger.Debug(
            "Searching for lunations from {Start} to {End}",
            startYear, endYear);

        // [1 January of startYear, 1 January after endYear) UT: the scan's last daily step ends
        // where the next year's request begins, so nothing on 31 December falls between them.
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);

        // The scan steps whole days from jdStart: it runs on to the first step at or after jdEnd, and
        // anything found from jdEnd on belongs to the next window and is dropped.
        double jdScanEnd = EphemerisSpan.WholeDayScanEnd(jdStart, jdEnd);

        // Find New Moons (Sun-Moon conjunction = 0° separation)
        List<double> newMoonJds = ReturnChartFinder.FindAspectJulianDays(
            _ephemeris,
            CelestialBodyId.Sun,
            CelestialBodyId.Moon,
            0.0,
            jdStart, jdScanEnd,
            cancellationToken);
        newMoonJds.RemoveAll(jd => jd >= jdEnd);

        // Find Full Moons (Sun-Moon opposition = 180° separation)
        List<double> fullMoonJds = ReturnChartFinder.FindAspectJulianDays(
            _ephemeris,
            CelestialBodyId.Sun,
            CelestialBodyId.Moon,
            180.0,
            jdStart, jdScanEnd,
            cancellationToken);
        fullMoonJds.RemoveAll(jd => jd >= jdEnd);

        List<LunationResult> allLunations = [];

        foreach (double jd in newMoonJds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allLunations.Add(BuildLunationResult(jd, isNewMoon: true));
        }

        foreach (double jd in fullMoonJds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allLunations.Add(BuildLunationResult(jd, isNewMoon: false));
        }

        // Sort chronologically
        allLunations.Sort((a, b) => a.ExactUtc.CompareTo(b.ExactUtc));

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.Information(
            "Found {Count} lunation(s) from {Start} to {End} ({NewMoons} New, {FullMoons} Full)",
            allLunations.Count, startYear, endYear,
            allLunations.Count(l => l.IsNewMoon),
            allLunations.Count(l => !l.IsNewMoon));

        return new LunationSeries
        {
            Lunations = allLunations,
            ScanStart = scanStart,
            ScanEnd = scanEnd,
            Count = allLunations.Count,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <inheritdoc />
    public LunationResult CalculateLunation(
        DateTime nearDate,
        bool newMoon,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        // Scan ±30 days around the target date
        double jdMid = _ephemeris.DateToJulianDay(
            nearDate.Year, nearDate.Month, nearDate.Day,
            nearDate.Hour + nearDate.Minute / 60.0);
        double jdStart = jdMid - 30.0;
        double jdEnd = jdMid + 30.0;

        double targetAngle = newMoon ? 0.0 : 180.0;

        List<double> jds = ReturnChartFinder.FindAspectJulianDays(
            _ephemeris,
            CelestialBodyId.Sun,
            CelestialBodyId.Moon,
            targetAngle,
            jdStart, jdEnd,
            cancellationToken);

        if (jds.Count == 0)
        {
            string type = newMoon ? "New Moon" : "Full Moon";
            throw new InvalidOperationException(
                $"No {type} found near {nearDate:yyyy-MM-dd}.");
        }

        // Take the lunation closest to the target date.
        // MinBy is O(n) vs. OrderBy().First() at O(n log n); safe to use here because
        // the non-empty guard above ensures MinBy cannot return the default(double) = 0.
        double selectedJd = jds.MinBy(jd => Math.Abs(jd - jdMid));

        // Build the timing result
        LunationResult timing = BuildLunationResult(selectedJd, newMoon);

        // Cast full chart
        ChartInput input = new()
        {
            Name = $"{timing.Type} in {timing.Sign}",
            LocalDateTime = timing.ExactUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = "Lunation",
        };

        // At the lunation's Julian Day: ExactUtc is only the label the chart prints (D4).
        ChartResult chart = _chartEngine.CalculateAtInstant(input, selectedJd, cancellationToken);

        // Return with chart attached
        return new LunationResult
        {
            Type = timing.Type,
            IsNewMoon = timing.IsNewMoon,
            ExactUtc = timing.ExactUtc,
            SunLongitude = timing.SunLongitude,
            MoonLongitude = timing.MoonLongitude,
            Sign = timing.Sign,
            Degree = timing.Degree,
            PositionFormatted = timing.PositionFormatted,
            Element = timing.Element,
            Modality = timing.Modality,
            PrecisionArcSeconds = timing.PrecisionArcSeconds,
            IsNearNode = timing.IsNearNode,
            NodeDistance = timing.NodeDistance,
            EclipseIndicator = timing.EclipseIndicator,
            Chart = chart,
            EngineVersion = timing.EngineVersion,
            CalculatedAt = timing.CalculatedAt,
        };
    }

    /// <inheritdoc />
    public LunationSeries CalculateLunationsForYear(
        int year,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        _logger.Debug(
            "Calculating lunation charts for {Year} at {Location}",
            year, locationName);

        // Find all lunations for the year (timing only)
        LunationSeries timingSeries = FindLunations(year, year, cancellationToken);

        // Cast a full chart at each lunation
        List<LunationResult> charted = [];

        foreach (LunationResult timing in timingSeries.Lunations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ChartInput input = new()
            {
                Name = $"{timing.Type} in {timing.Sign}",
                LocalDateTime = timing.ExactUtc,
                TimeZone = timeZone,
                UseExplicitOffset = true,
                UtcOffsetHours = 0.0,
                Latitude = latitude,
                Longitude = longitude,
                LocationName = locationName,
                EventType = "Lunation",
            };

            // At the lunation's instant, by arithmetic: ExactUtc is the instant, and the label (D4).
            ChartResult chart = _chartEngine.CalculateAtInstant(
                input, JulianDayUtility.InstantToJulianDay(timing.ExactUtc), cancellationToken);

            charted.Add(new LunationResult
            {
                Type = timing.Type,
                IsNewMoon = timing.IsNewMoon,
                ExactUtc = timing.ExactUtc,
                SunLongitude = timing.SunLongitude,
                MoonLongitude = timing.MoonLongitude,
                Sign = timing.Sign,
                Degree = timing.Degree,
                PositionFormatted = timing.PositionFormatted,
                Element = timing.Element,
                Modality = timing.Modality,
                PrecisionArcSeconds = timing.PrecisionArcSeconds,
                IsNearNode = timing.IsNearNode,
                NodeDistance = timing.NodeDistance,
                EclipseIndicator = timing.EclipseIndicator,
                Chart = chart,
                EngineVersion = timing.EngineVersion,
                CalculatedAt = timing.CalculatedAt,
            });
        }

        _logger.Information(
            "Calculated {Count} lunation chart(s) for {Year} at {Location}",
            charted.Count, year, locationName);

        return new LunationSeries
        {
            Lunations = charted,
            ScanStart = timingSeries.ScanStart,
            ScanEnd = timingSeries.ScanEnd,
            Count = charted.Count,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #region Private Helpers

    /// <summary>
    /// Build a <see cref="LunationResult"/> from a found lunation Julian Day (timing only, no chart).
    /// </summary>
    private LunationResult BuildLunationResult(double jd, bool isNewMoon)
    {
        DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

        PlanetPosition sunPos = _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Sun);
        PlanetPosition moonPos = _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Moon);

        double sunLon = sunPos.Longitude;
        double moonLon = moonPos.Longitude;

        // Precision: actual Sun-Moon separation vs target (0° or 180°)
        double separation = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(sunLon, moonLon));
        double targetSep = isNewMoon ? 0.0 : 180.0;
        double precisionArcSec = Math.Abs(separation - targetSep) * 3600.0;

        // For New Moon: sign is the Moon's sign (same as Sun's since they're conjunct)
        // For Full Moon: sign is the Moon's sign (conventional: "Full Moon in Libra")
        double lunationLon = moonLon;
        string sign = GetSignName(lunationLon);
        double degree = lunationLon % 30.0;
        string element = GetElement(sign);
        string modality = GetModality(sign);
        string posFormatted = PositionFormatter.FormatLongitude(lunationLon);

        // Eclipse proximity check: distance from the True Node
        PlanetPosition nodePos = _ephemeris.CalculatePositionLongitudeOnly(
            jd, CelestialBodyId.TrueNode);
        double nodeLon = nodePos.Longitude;

        // Distance to the node (or its opposite point)
        double distToNode = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(moonLon, nodeLon));
        double distToSouthNode = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(
            moonLon, (nodeLon + 180.0) % 360.0));
        double minNodeDist = Math.Min(distToNode, distToSouthNode);

        double eclipseOrb = isNewMoon ? SolarEclipseNodeOrb : LunarEclipseNodeOrb;
        bool isNearNode = minNodeDist <= eclipseOrb;

        string? eclipseIndicator = null;
        if (isNearNode)
        {
            // The flag says what it measures, the distance to the node, not whether an eclipse
            // occurs (T4-3, Q8).
            eclipseIndicator = string.Create(
                CultureInfo.InvariantCulture, $"Near a node (within {eclipseOrb:0}\u00b0)");
        }

        string type = isNewMoon ? "New Moon" : "Full Moon";

        return new LunationResult
        {
            Type = type,
            IsNewMoon = isNewMoon,
            ExactUtc = exactUtc,
            SunLongitude = sunLon,
            MoonLongitude = moonLon,
            Sign = sign,
            Degree = degree,
            PositionFormatted = posFormatted,
            Element = element,
            Modality = modality,
            PrecisionArcSeconds = precisionArcSec,
            IsNearNode = isNearNode,
            NodeDistance = minNodeDist,
            EclipseIndicator = eclipseIndicator,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
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
        string asmVersion = typeof(LunationService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }

    #endregion
}
