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
/// General-purpose mundane aspect finder using harmonic modes (45° and 90°).
/// Searches multiple target angles in a single operation and returns a complete
/// chronological cycle timeline with waxing/waning phase labels.
/// </summary>
/// <remarks>
/// <para>
/// The 45° harmonic mode searches 0°, 45°, 90°, 135°, 180° — the two-body finder's
/// bidirectional detection catches both waxing and waning aspects, producing all 8
/// phase points in the Ebertin cosmobiology cycle.
/// </para>
/// <para>
/// The 90° harmonic mode searches 0°, 90°, 180° — producing all 4 major cycle phases
/// (conjunction, waxing/waning square, opposition).
/// </para>
/// <para>
/// This service intentionally does NOT cast charts. It provides precise timing only.
/// The user takes the UTC times from the export and casts Event charts separately.
/// </para>
/// </remarks>
public sealed class MundaneAspectService : IMundaneAspectService
{
    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Harmonic mode definitions. Each mode specifies the target angles to search.
    /// The two-body finder's bidirectional detection catches both waxing and waning
    /// aspects for non-0°/180° targets automatically.
    /// </summary>
    private static readonly Dictionary<string, double[]> HarmonicModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["45° Harmonic"] = [0.0, 45.0, 90.0, 135.0, 180.0],
        ["90° Harmonic"] = [0.0, 90.0, 180.0],
    };

    /// <summary>
    /// The bodies a cycle's phase is measured between, fastest first: by mean sidereal period, the
    /// Moon (27.3 days) to Pluto (248 years). A cycle waxes as the faster body moves ahead of the
    /// slower, so the phase is the faster body's longitude minus the slower's.
    /// </summary>
    private static readonly CelestialBodyId[] BodiesFastestFirst =
    [
        CelestialBodyId.Moon,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Sun,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Chiron,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="MundaneAspectService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public MundaneAspectService(
        IEphemerisService ephemeris,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<MundaneAspectService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public MundaneAspectSeries FindAspects(
        CelestialBodyId body1,
        CelestialBodyId body2,
        string harmonicMode,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default)
    {
        // Guard clauses
        if (body1 == body2)
            throw new ArgumentException(
                "Cannot find aspects between a body and itself.",
                nameof(body2));

        ArgumentException.ThrowIfNullOrWhiteSpace(harmonicMode);

        if (!HarmonicModes.TryGetValue(harmonicMode, out double[]? targetAngles))
            throw new ArgumentException(
                $"Unknown harmonic mode '{harmonicMode}'. Valid modes: {string.Join(", ", HarmonicModes.Keys)}.",
                nameof(harmonicMode));

        if (startYear > endYear)
            throw new ArgumentOutOfRangeException(
                nameof(startYear),
                $"Start year ({startYear}) must be <= end year ({endYear}).");

        string body1Name = body1.ToString();
        string body2Name = body2.ToString();

        _logger.Debug(
            "Searching for {Body1}-{Body2} {Mode} from {Start} to {End}",
            body1Name, body2Name, harmonicMode, startYear, endYear);

        // [1 January of startYear, 1 January after endYear) UT: the scan's last daily step ends
        // where the next year's request begins, so nothing on 31 December falls between them.
        // Clamped to the span the ephemeris files cover, so nothing is asked for outside it.
        double jdStart = Math.Max(_ephemeris.DateToJulianDay(startYear, 1, 1, 0.0), EphemerisSpan.FirstJulianDay);
        double jdEnd = Math.Min(_ephemeris.DateToJulianDay(endYear + 1, 1, 1, 0.0), EphemerisSpan.LastJulianDay);

        // The scan steps whole days from jdStart: it runs on to the first step at or after jdEnd, and
        // anything found from jdEnd on belongs to the next window and is dropped.
        double jdScanEnd = EphemerisSpan.WholeDayScanEnd(jdStart, jdEnd);

        string engineVersion = BuildVersionString();
        DateTime calculatedAt = DateTime.UtcNow;
        List<MundaneAspectResult> allResults = [];

        foreach (double targetAngle in targetAngles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<double> aspectJds = ReturnChartFinder.FindAspectJulianDays(
                _ephemeris,
                body1,
                body2,
                targetAngle,
                jdStart,
                jdScanEnd,
                cancellationToken);
            aspectJds.RemoveAll(jd => jd >= jdEnd);

            foreach (double jd in aspectJds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MundaneAspectResult result = BuildAspectResult(
                    jd, body1, body2, body1Name, body2Name,
                    targetAngle, engineVersion, calculatedAt);

                allResults.Add(result);
            }
        }

        // Sort ALL results chronologically across all target angles
        allResults.Sort((a, b) => a.ExactUtc.CompareTo(b.ExactUtc));

        DateTime scanStart = new(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanEnd = new(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.Information(
            "Found {Count} {Body1}-{Body2} aspects ({Mode}) from {Start} to {End}",
            allResults.Count, body1Name, body2Name, harmonicMode, startYear, endYear);

        return new MundaneAspectSeries
        {
            Body1 = body1,
            Body1Name = body1Name,
            Body2 = body2,
            Body2Name = body2Name,
            HarmonicMode = harmonicMode,
            Aspects = allResults,
            ScanStart = scanStart,
            ScanEnd = scanEnd,
            Count = allResults.Count,
            EngineVersion = engineVersion,
            CalculatedAt = calculatedAt,
        };
    }

    /// <summary>
    /// Build a <see cref="MundaneAspectResult"/> from a found aspect Julian Day.
    /// Computes cycle phase (0°–360°), waxing/waning label, and full aspect name.
    /// </summary>
    private MundaneAspectResult BuildAspectResult(
        double jd,
        CelestialBodyId body1,
        CelestialBodyId body2,
        string body1Name,
        string body2Name,
        double targetAngle,
        string engineVersion,
        DateTime calculatedAt)
    {
        DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);

        PlanetPosition pos1 = _ephemeris.CalculatePositionLongitudeOnly(jd, body1);
        PlanetPosition pos2 = _ephemeris.CalculatePositionLongitudeOnly(jd, body2);

        double lon1 = pos1.Longitude;
        double lon2 = pos2.Longitude;

        // Compute the actual angular separation using the signed delta
        double signedDelta = ReturnChartFinder.SignedLongitudeDelta(lon2, lon1);
        double actualSeparation = Math.Abs(signedDelta);

        // For aspects other than conjunction/opposition, check both the direct
        // separation and 360-separation to find which is closer to the target
        if (targetAngle > 0.0 && targetAngle < 180.0)
        {
            double alt = 360.0 - actualSeparation;
            if (Math.Abs(alt - targetAngle) < Math.Abs(actualSeparation - targetAngle))
                actualSeparation = alt;
        }

        double precisionArcSec = Math.Abs(actualSeparation - targetAngle) * 3600.0;

        // Cycle phase: how far the faster body has moved ahead of the slower (0°-360°), whichever
        // is named first (T5-1). A pair with a body not ranked by period keeps body2 - body1.
        double rawPhase = IsFaster(body1, body2) ? (lon1 - lon2) % 360.0 : (lon2 - lon1) % 360.0;
        if (rawPhase < 0) rawPhase += 360.0;

        // Phase label and full aspect name
        string phaseLabel = rawPhase <= 180.0 ? "Waxing" : "Waning";
        string aspectBaseName = GetAspectBaseName(targetAngle);
        string aspectName = (targetAngle == 0.0 || targetAngle == 180.0)
            ? aspectBaseName
            : $"{phaseLabel} {aspectBaseName}";

        string sign1 = GetSignName(lon1);
        string sign2 = GetSignName(lon2);
        string pos1Formatted = PositionFormatter.FormatLongitude(lon1);
        string pos2Formatted = PositionFormatter.FormatLongitude(lon2);

        _logger.Debug(
            "{Body1}-{Body2} {Aspect} at {Date:yyyy-MM-dd HH:mm:ss}: " +
            "{Pos1} / {Pos2} phase {Phase:F0}° precision {Prec:F2}\"",
            body1Name, body2Name, aspectName, exactUtc,
            pos1Formatted, pos2Formatted, rawPhase, precisionArcSec);

        return new MundaneAspectResult
        {
            Body1 = body1,
            Body1Name = body1Name,
            Body2 = body2,
            Body2Name = body2Name,
            AspectName = aspectName,
            TargetAngle = targetAngle,
            ExactUtc = exactUtc,
            Longitude1 = lon1,
            Longitude2 = lon2,
            Position1Formatted = pos1Formatted,
            Position2Formatted = pos2Formatted,
            Sign1 = sign1,
            Sign2 = sign2,
            ActualSeparation = actualSeparation,
            PrecisionArcSeconds = precisionArcSec,
            CyclePhase = rawPhase,
            PhaseLabel = phaseLabel,
            EngineVersion = engineVersion,
            CalculatedAt = calculatedAt,
        };
    }

    /// <summary>
    /// Whether <paramref name="body"/> is the faster of the pair: the one with the shorter mean
    /// sidereal period, in the order of <see cref="BodiesFastestFirst"/>. False when either body is
    /// not on that list, so such a pair keeps the phase of the second body named from the first.
    /// </summary>
    /// <param name="body">The body asked about.</param>
    /// <param name="other">The other body of the pair.</param>
    /// <returns>True when both are ranked and <paramref name="body"/> comes first.</returns>
    private static bool IsFaster(CelestialBodyId body, CelestialBodyId other)
    {
        int rank = Array.IndexOf(BodiesFastestFirst, body);
        int otherRank = Array.IndexOf(BodiesFastestFirst, other);
        return rank >= 0 && otherRank >= 0 && rank < otherRank;
    }

    /// <summary>
    /// Get the base aspect name from a target angle.
    /// </summary>
    private static string GetAspectBaseName(double targetAngle) => targetAngle switch
    {
        0.0 => "Conjunction",
        45.0 => "Semi-square",
        60.0 => "Sextile",
        90.0 => "Square",
        120.0 => "Trine",
        135.0 => "Sesquiquadrate",
        180.0 => "Opposition",
        _ => string.Create(CultureInfo.InvariantCulture, $"Aspect ({targetAngle:F1}°)"),
    };

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
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(MundaneAspectService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];
        return $"AstroCast {asmVersion} (SwissEph {_ephemeris.Version}, tzdb {_timeZoneRules.DataVersion})";
    }
}
