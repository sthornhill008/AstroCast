using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Interop;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds solar and lunar eclipses using the Swiss Ephemeris dedicated eclipse
/// functions, providing definitive type classification and sub-second precision
/// timing for all eclipse phases.
/// </summary>
/// <remarks>
/// <para>
/// Uses <c>swe_sol_eclipse_when_glob</c> for solar eclipses and
/// <c>swe_lun_eclipse_when</c> for lunar eclipses. These functions use analytical
/// methods (not daily scanning) and execute very quickly.
/// </para>
/// <para>
/// This service intentionally does NOT cast charts. It provides precise timing only.
/// The user takes the UTC times from the export and casts Event charts separately.
/// </para>
/// </remarks>
public sealed class EclipseService : IEclipseService
{
    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="EclipseService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for eclipse finding and position lookups.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public EclipseService(IEphemerisService ephemeris, ILogger logger, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<EclipseService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public EclipseSeries FindEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentOutOfRangeException(
                nameof(startYear),
                $"Start year ({startYear}) must be <= end year ({endYear}).");

        _logger.Debug("Searching for all eclipses from {Start} to {End}", startYear, endYear);

        List<EclipseResult> solarResults = FindSolarEclipsesInternal(startYear, endYear, cancellationToken);
        List<EclipseResult> lunarResults = FindLunarEclipsesInternal(startYear, endYear, cancellationToken);

        List<EclipseResult> allResults = [.. solarResults, .. lunarResults];
        allResults.Sort((a, b) => a.MaximumUtc.CompareTo(b.MaximumUtc));

        return BuildSeries(allResults, startYear, endYear);
    }

    /// <inheritdoc />
    public EclipseSeries FindSolarEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentOutOfRangeException(
                nameof(startYear),
                $"Start year ({startYear}) must be <= end year ({endYear}).");

        List<EclipseResult> results = FindSolarEclipsesInternal(startYear, endYear, cancellationToken);
        return BuildSeries(results, startYear, endYear);
    }

    /// <inheritdoc />
    public EclipseSeries FindLunarEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default)
    {
        if (startYear > endYear)
            throw new ArgumentOutOfRangeException(
                nameof(startYear),
                $"Start year ({startYear}) must be <= end year ({endYear}).");

        List<EclipseResult> results = FindLunarEclipsesInternal(startYear, endYear, cancellationToken);
        return BuildSeries(results, startYear, endYear);
    }

    /// <summary>
    /// Find all solar eclipses in the date range using <c>swe_sol_eclipse_when_glob</c>.
    /// </summary>
    private List<EclipseResult> FindSolarEclipsesInternal(
        int startYear, int endYear, CancellationToken ct)
    {
        List<EclipseResult> results = [];
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);
        double jdSearch = jdStart;
        string engineVersion = BuildVersionString();
        DateTime calculatedAt = DateTime.UtcNow;

        while (jdSearch < jdEnd)
        {
            ct.ThrowIfCancellationRequested();

            (int flags, double[] tret) = _ephemeris.FindNextSolarEclipse(jdSearch);

            if (flags < 0) break;
            if (tret[0] >= jdEnd) break;

            double jdMax = tret[0];

            // Get positions at maximum eclipse
            PlanetPosition sunPos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.Sun);
            PlanetPosition moonPos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.Moon);
            PlanetPosition nodePos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.TrueNode);

            string type = GetSolarEclipseType(flags);
            string fullLabel = $"{type} Solar Eclipse";

            // Solar eclipse: sign is the Sun's sign (Sun and Moon are conjunct)
            double eclipseLon = sunPos.Longitude;
            string sign = GetSignName(eclipseLon);
            double degree = eclipseLon % 30.0;
            string posFormatted = PositionFormatter.FormatLongitude(eclipseLon);

            // Node distance
            double nodeDist = ComputeNodeDistance(moonPos.Longitude, nodePos.Longitude);

            // Phase times (0.0 means not applicable)
            DateTime maxUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdMax);
            DateTime? firstContact = JdToNullableDateTime(tret[2]);
            DateTime? lastContact = JdToNullableDateTime(tret[3]);
            DateTime? totalBegin = JdToNullableDateTime(tret[4]);
            DateTime? totalEnd = JdToNullableDateTime(tret[5]);

            double? totalDur = (totalBegin is not null && totalEnd is not null)
                ? (totalEnd.Value - totalBegin.Value).TotalMinutes
                : null;

            _logger.Debug(
                "{Label} at {Date:yyyy-MM-dd HH:mm:ss}: {Pos} node dist {Dist:F1}°",
                fullLabel, maxUtc, posFormatted, nodeDist);

            results.Add(new EclipseResult
            {
                Category = "Solar",
                IsSolar = true,
                Type = type,
                FullLabel = fullLabel,
                MaximumUtc = maxUtc,
                MaximumJd = jdMax,
                FirstContactUtc = firstContact,
                LastContactUtc = lastContact,
                TotalityBeginUtc = totalBegin,
                TotalityEndUtc = totalEnd,
                PenumbralBeginUtc = null,
                PenumbralEndUtc = null,
                TotalityDurationMinutes = totalDur,
                SunLongitude = sunPos.Longitude,
                MoonLongitude = moonPos.Longitude,
                Sign = sign,
                Degree = degree,
                PositionFormatted = posFormatted,
                Element = GetElement(sign),
                Modality = GetModality(sign),
                NodeLongitude = nodePos.Longitude,
                NodeDistance = nodeDist,
                RawFlags = flags,
                EngineVersion = engineVersion,
                CalculatedAt = calculatedAt,
            });

            // Advance past this eclipse (at least 20 days)
            jdSearch = tret[0] + 20.0;
        }

        _logger.Information("Found {Count} solar eclipse(s) from {Start} to {End}",
            results.Count, startYear, endYear);

        return results;
    }

    /// <summary>
    /// Find all lunar eclipses in the date range using <c>swe_lun_eclipse_when</c>.
    /// </summary>
    private List<EclipseResult> FindLunarEclipsesInternal(
        int startYear, int endYear, CancellationToken ct)
    {
        List<EclipseResult> results = [];
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);
        double jdSearch = jdStart;
        string engineVersion = BuildVersionString();
        DateTime calculatedAt = DateTime.UtcNow;

        while (jdSearch < jdEnd)
        {
            ct.ThrowIfCancellationRequested();

            (int flags, double[] tret) = _ephemeris.FindNextLunarEclipse(jdSearch);

            if (flags < 0) break;
            if (tret[0] >= jdEnd) break;

            double jdMax = tret[0];

            // Get positions at maximum eclipse
            PlanetPosition sunPos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.Sun);
            PlanetPosition moonPos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.Moon);
            PlanetPosition nodePos = _ephemeris.CalculatePositionLongitudeOnly(jdMax, CelestialBodyId.TrueNode);

            string type = GetLunarEclipseType(flags);
            string fullLabel = $"{type} Lunar Eclipse";

            // Lunar eclipse: sign is the Moon's sign (Moon is opposite the Sun)
            double eclipseLon = moonPos.Longitude;
            string sign = GetSignName(eclipseLon);
            double degree = eclipseLon % 30.0;
            string posFormatted = PositionFormatter.FormatLongitude(eclipseLon);

            // Node distance
            double nodeDist = ComputeNodeDistance(moonPos.Longitude, nodePos.Longitude);

            // Phase times
            DateTime maxUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdMax);
            // tret[2]/tret[3] = partial begin/end for lunar eclipses
            DateTime? firstContact = JdToNullableDateTime(tret[2]);
            DateTime? lastContact = JdToNullableDateTime(tret[3]);
            DateTime? totalBegin = JdToNullableDateTime(tret[4]);
            DateTime? totalEnd = JdToNullableDateTime(tret[5]);
            DateTime? penumbralBegin = JdToNullableDateTime(tret[6]);
            DateTime? penumbralEnd = JdToNullableDateTime(tret[7]);

            double? totalDur = (totalBegin is not null && totalEnd is not null)
                ? (totalEnd.Value - totalBegin.Value).TotalMinutes
                : null;

            _logger.Debug(
                "{Label} at {Date:yyyy-MM-dd HH:mm:ss}: {Pos} node dist {Dist:F1}°",
                fullLabel, maxUtc, posFormatted, nodeDist);

            results.Add(new EclipseResult
            {
                Category = "Lunar",
                IsSolar = false,
                Type = type,
                FullLabel = fullLabel,
                MaximumUtc = maxUtc,
                MaximumJd = jdMax,
                FirstContactUtc = firstContact,
                LastContactUtc = lastContact,
                TotalityBeginUtc = totalBegin,
                TotalityEndUtc = totalEnd,
                PenumbralBeginUtc = penumbralBegin,
                PenumbralEndUtc = penumbralEnd,
                TotalityDurationMinutes = totalDur,
                SunLongitude = sunPos.Longitude,
                MoonLongitude = moonPos.Longitude,
                Sign = sign,
                Degree = degree,
                PositionFormatted = posFormatted,
                Element = GetElement(sign),
                Modality = GetModality(sign),
                NodeLongitude = nodePos.Longitude,
                NodeDistance = nodeDist,
                RawFlags = flags,
                EngineVersion = engineVersion,
                CalculatedAt = calculatedAt,
            });

            // Advance past this eclipse (at least 20 days)
            jdSearch = tret[0] + 20.0;
        }

        _logger.Information("Found {Count} lunar eclipse(s) from {Start} to {End}",
            results.Count, startYear, endYear);

        return results;
    }

    /// <summary>
    /// Classify a solar eclipse from the Swiss Ephemeris return flags.
    /// </summary>
    private static string GetSolarEclipseType(int flags)
    {
        if ((flags & SwissEphInterop.SE_ECL_TOTAL) != 0) return "Total";
        if ((flags & SwissEphInterop.SE_ECL_ANNULAR_TOTAL) != 0) return "Hybrid";
        if ((flags & SwissEphInterop.SE_ECL_ANNULAR) != 0) return "Annular";
        if ((flags & SwissEphInterop.SE_ECL_PARTIAL) != 0) return "Partial";
        return "Unknown";
    }

    /// <summary>
    /// Classify a lunar eclipse from the Swiss Ephemeris return flags.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Swiss Ephemeris flag <c>SE_ECL_PENUMBRAL</c> (64) shares the same value
    /// as <c>SE_ECL_LUNAR</c> — it is always set for any lunar eclipse regardless of
    /// type. Therefore penumbral is identified by exclusion: if neither Total nor
    /// Partial flags are set, the eclipse is penumbral.
    /// </para>
    /// </remarks>
    private static string GetLunarEclipseType(int flags)
    {
        if ((flags & SwissEphInterop.SE_ECL_TOTAL) != 0) return "Total";
        if ((flags & SwissEphInterop.SE_ECL_PARTIAL) != 0) return "Partial";

        // If swe_lun_eclipse_when returned a valid eclipse (flags > 0) but
        // neither Total nor Partial is set, it must be penumbral.
        return "Penumbral";
    }

    /// <summary>
    /// Compute the distance from the Moon to the nearest node (north or south).
    /// </summary>
    private static double ComputeNodeDistance(double moonLon, double nodeLon)
    {
        double distNorth = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(moonLon, nodeLon));
        double southNodeLon = (nodeLon + 180.0) % 360.0;
        double distSouth = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(moonLon, southNodeLon));
        return Math.Min(distNorth, distSouth);
    }

    /// <summary>
    /// Build an <see cref="EclipseSeries"/> from a list of results.
    /// </summary>
    private EclipseSeries BuildSeries(List<EclipseResult> results, int startYear, int endYear)
    {
        return new EclipseSeries
        {
            Eclipses = results,
            ScanStart = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            Count = results.Count,
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

    /// <summary>Get the element for a zodiac sign.</summary>
    private static string GetElement(string sign) => sign switch
    {
        "Aries" or "Leo" or "Sagittarius" => "Fire",
        "Taurus" or "Virgo" or "Capricorn" => "Earth",
        "Gemini" or "Libra" or "Aquarius" => "Air",
        "Cancer" or "Scorpio" or "Pisces" => "Water",
        _ => "Unknown",
    };

    /// <summary>Get the modality for a zodiac sign.</summary>
    private static string GetModality(string sign) => sign switch
    {
        "Aries" or "Cancer" or "Libra" or "Capricorn" => "Cardinal",
        "Taurus" or "Leo" or "Scorpio" or "Aquarius" => "Fixed",
        "Gemini" or "Virgo" or "Sagittarius" or "Pisces" => "Mutable",
        _ => "Unknown",
    };

    /// <summary>
    /// Convert a Julian Day to UTC DateTime. Returns null if the JD is 0.0
    /// (Swiss Ephemeris sets tret entries to 0.0 when a phase doesn't apply).
    /// </summary>
    private DateTime? JdToNullableDateTime(double jd)
    {
        if (jd == 0.0) return null;
        return JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);
    }

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(EclipseService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }
}
