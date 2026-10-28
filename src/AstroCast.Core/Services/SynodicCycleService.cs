using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates synodic cycles (consecutive conjunctions) for any two-planet pair.
/// Generalises <see cref="GreatConjunctionService"/> (Jupiter–Saturn only) to
/// arbitrary body combinations.
/// </summary>
/// <remarks>
/// Uses <see cref="ReturnChartFinder.FindAspectJulianDays"/> with a 0° target
/// angle to locate every conjunction in the search window. Consecutive conjunctions
/// belong to one cycle until the pair has passed through an opposition, so the passes
/// of a triple conjunction are one cycle; the synodic period runs from one cycle's
/// first pass to the next's, and the summary statistics are taken over first passes.
/// </remarks>
public sealed class SynodicCycleService : ISynodicCycleService
{
    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Average daily motion in ecliptic longitude (degrees per day) for body ordering.
    /// The faster-moving body is always assigned as Body A.
    /// </summary>
    private static readonly Dictionary<CelestialBodyId, double> AverageDailyMotion = new()
    {
        [CelestialBodyId.Moon]     = 13.18,
        [CelestialBodyId.Sun]      = 0.986,
        [CelestialBodyId.Mercury]  = 1.383,
        [CelestialBodyId.Venus]    = 1.200,
        [CelestialBodyId.Mars]     = 0.524,
        [CelestialBodyId.Jupiter]  = 0.083,
        [CelestialBodyId.Saturn]   = 0.034,
        [CelestialBodyId.Uranus]   = 0.012,
        [CelestialBodyId.Neptune]  = 0.006,
        [CelestialBodyId.Pluto]    = 0.004,
    };

    /// <summary>
    /// The bodies no two of which are ever in opposition: Mercury and Venus never stray more than
    /// about 28° and 47° from the Sun. A pair of them has a cycle per conjunction (Steve's ruling on
    /// T5-2), where every other pair's cycle runs until it has passed through an opposition.
    /// </summary>
    private static readonly HashSet<CelestialBodyId> NeverOpposed =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="SynodicCycleService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversion.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public SynodicCycleService(IEphemerisService ephemeris, ILogger logger, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<SynodicCycleService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public SynodicCycleSeries FindConjunctions(
        CelestialBodyId bodyA,
        CelestialBodyId bodyB,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default)
    {
        if (bodyA == bodyB)
            throw new ArgumentException(
                $"Body A and Body B must differ (both are {bodyA}).",
                nameof(bodyB));

        if (startYear > endYear)
            throw new ArgumentException(
                $"Start year ({startYear}) must be <= end year ({endYear}).",
                nameof(startYear));

        // Order bodies so the faster-moving body is always Body A.
        (CelestialBodyId fasterBody, CelestialBodyId slowerBody) = OrderBodies(bodyA, bodyB);

        _logger.Debug(
            "Searching for {BodyA}–{BodyB} conjunctions from {Start} to {End}",
            fasterBody, slowerBody, startYear, endYear);

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
            fasterBody,
            slowerBody,
            0.0, // conjunction = 0° separation
            jdStart,
            jdScanEnd,
            cancellationToken);
        conjunctionJds.RemoveAll(jd => jd >= jdEnd);

        // Group the conjunctions into cycles (T5-2): consecutive conjunctions belong to one cycle
        // until the pair has passed through an opposition, so the passes of a triple conjunction
        // are one cycle, which begins at its first pass, as a return does (T-D6). A pair that can
        // never be in opposition keeps a cycle per conjunction.
        bool canBeOpposed = !(NeverOpposed.Contains(fasterBody) && NeverOpposed.Contains(slowerBody));
        List<int> cycleStarts = [];
        for (int i = 0; i < conjunctionJds.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i == 0 || !canBeOpposed || ReturnChartFinder.FindAspectJulianDays(
                    _ephemeris, fasterBody, slowerBody, 180.0,
                    conjunctionJds[i - 1], conjunctionJds[i], cancellationToken).Count > 0)
            {
                cycleStarts.Add(i);
            }
        }

        List<SynodicConjunction> conjunctions = [];
        for (int cycle = 0; cycle < cycleStarts.Count; cycle++)
        {
            int first = cycleStarts[cycle];
            int passCount = (cycle + 1 < cycleStarts.Count ? cycleStarts[cycle + 1] : conjunctionJds.Count) - first;
            double? previousFirstPassJd = cycle > 0 ? conjunctionJds[cycleStarts[cycle - 1]] : null;

            for (int pass = 1; pass <= passCount; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                conjunctions.Add(BuildConjunction(
                    conjunctionJds[first + pass - 1], fasterBody, slowerBody, conjunctions.Count + 1,
                    pass == 1 ? previousFirstPassJd : null, pass, passCount));
            }
        }

        // Compute statistics from synodic periods, over the cycles' first passes only.
        List<double> periods = [];
        foreach (SynodicConjunction conjunction in conjunctions)
        {
            if (conjunction.PassNumber == 1 && conjunction.SynodicPeriodDays is double days)
                periods.Add(days);
        }

        double? avgDays = periods.Count > 0 ? periods.Average() : null;
        double? minDays = periods.Count > 0 ? periods.Min() : null;
        double? maxDays = periods.Count > 0 ? periods.Max() : null;

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.Information(
            "Found {Count} {BodyA}–{BodyB} conjunction(s) from {Start} to {End}",
            conjunctions.Count, fasterBody, slowerBody, startYear, endYear);

        return new SynodicCycleSeries
        {
            BodyAName = fasterBody.ToString(),
            BodyBName = slowerBody.ToString(),
            Conjunctions = conjunctions,
            StartDate = scanStart,
            EndDate = scanEnd,
            Count = conjunctions.Count,
            AverageSynodicPeriodDays = avgDays.HasValue ? Math.Round(avgDays.Value, 2) : null,
            AverageSynodicPeriodYears = avgDays.HasValue ? Math.Round(avgDays.Value / 365.25, 2) : null,
            MinSynodicPeriodDays = minDays.HasValue ? Math.Round(minDays.Value, 2) : null,
            MaxSynodicPeriodDays = maxDays.HasValue ? Math.Round(maxDays.Value, 2) : null,
            EngineVersion = BuildVersionString(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #region Private Helpers

    /// <summary>
    /// Order two bodies so the faster-moving body is first.
    /// </summary>
    private static (CelestialBodyId Faster, CelestialBodyId Slower) OrderBodies(
        CelestialBodyId a, CelestialBodyId b)
    {
        double motionA = AverageDailyMotion.GetValueOrDefault(a, 0.0);
        double motionB = AverageDailyMotion.GetValueOrDefault(b, 0.0);

        return motionA >= motionB ? (a, b) : (b, a);
    }

    /// <summary>
    /// Build a <see cref="SynodicConjunction"/> from a found conjunction Julian Day.
    /// </summary>
    /// <param name="jd">The conjunction's Julian Day (UT).</param>
    /// <param name="bodyA">The faster body.</param>
    /// <param name="bodyB">The slower body.</param>
    /// <param name="number">The conjunction's number in the series, from 1.</param>
    /// <param name="periodStartJd">
    /// For a cycle's first pass, the previous cycle's first pass, which its period runs from; null for
    /// a later pass and for the first cycle in the series.
    /// </param>
    /// <param name="passNumber">Which pass of its cycle the conjunction is, from 1.</param>
    /// <param name="passCount">How many passes its cycle has in the series.</param>
    private SynodicConjunction BuildConjunction(
        double jd,
        CelestialBodyId bodyA,
        CelestialBodyId bodyB,
        int number,
        double? periodStartJd,
        int passNumber,
        int passCount)
    {
        DateTime dateTimeUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

        PlanetPosition posA = _ephemeris.CalculatePositionLongitudeOnly(jd, bodyA);
        PlanetPosition posB = _ephemeris.CalculatePositionLongitudeOnly(jd, bodyB);

        double lonA = posA.Longitude;
        double lonB = posB.Longitude;
        double midLon = MidpointLongitude(lonA, lonB);

        string sign = GetSignName(midLon);
        string element = GetElement(sign);
        string modality = GetModality(sign);
        string posFormatted = PositionFormatter.FormatLongitude(midLon);

        double separation = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(lonA, lonB));
        double precisionArcSec = separation * 3600.0;

        // Synodic period from previous conjunction.
        double? periodDays = null;
        double? periodYears = null;

        if (periodStartJd is double startJd)
        {
            periodDays = Math.Round((jd - startJd), 2);
            periodYears = Math.Round(periodDays.Value / 365.25, 2);
        }

        _logger.Debug(
            "{BodyA}–{BodyB} conjunction #{Num} at {Date:yyyy-MM-dd}: {Pos} precision {Prec:F2}\"",
            bodyA, bodyB, number, dateTimeUtc, posFormatted, precisionArcSec);

        return new SynodicConjunction
        {
            Number = number,
            DateTimeUtc = dateTimeUtc,
            JulianDay = jd,
            Longitude = midLon,
            Position = posFormatted,
            Sign = sign,
            Element = element,
            Modality = modality,
            BodyALongitude = lonA,
            BodyBLongitude = lonB,
            PrecisionArcSeconds = precisionArcSec,
            SynodicPeriodDays = periodDays,
            SynodicPeriodYears = periodYears,
            PassNumber = passNumber,
            PassCount = passCount,
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
        string asmVersion = typeof(SynodicCycleService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }

    #endregion
}
