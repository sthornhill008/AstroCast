using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Computes the full cycle timeline between two consecutive planetary returns,
/// identifying the exact dates of the four critical phase points (return,
/// opening square, opposition, closing square) and the next return.
/// </summary>
/// <remarks>
/// <para>
/// A cycle runs from return #N's first pass to #N+1's (<see cref="IPlanetaryReturnService"/>,
/// ruling T-D6); the two return rows carry the passage's pass count and all its passes. Each
/// intermediate phase point (90°, 180°, 270°) is found with <see cref="ReturnChartFinder"/> in a
/// window around its expected place in the cycle and takes its first pass, every pass listed. By
/// date, the cycle is the one that holds the date: return #N where N is the number of passages
/// whose first pass is at or before the date (branch F, 1.3.4). Until 1.3.4 the return rows always
/// read one pass (T5-3), Mars, Jupiter and Saturn phase points took their last pass (T2-4), and
/// the cycle for a date went through the return nearest the date's year, which failed for most
/// Jupiter and Saturn requests (T2-3).
/// </para>
/// </remarks>
public sealed class PlanetaryCycleTimelineService : IPlanetaryCycleTimelineService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IPlanetaryReturnService _returnService;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>Phase definitions: name and angular offset from natal longitude.</summary>
    private static readonly (string Name, int Angle)[] Phases =
    [
        ("Return", 0),
        ("Opening Square", 90),
        ("Opposition", 180),
        ("Closing Square", 270),
        ("Next Return", 360),
    ];

    /// <summary>
    /// Approximate orbital periods in days. Read nowhere: the cycle runs between the passages
    /// <see cref="ReturnPassageFinder"/> finds, and each phase point's scan window is estimated
    /// from that cycle's length.
    /// </summary>
    private static readonly Dictionary<CelestialBodyId, double> OrbitalPeriods = new()
    {
        [CelestialBodyId.Mercury] = 87.97,
        [CelestialBodyId.Venus] = 224.70,
        [CelestialBodyId.Mars] = 686.97,
        [CelestialBodyId.Jupiter] = 4332.59,
        [CelestialBodyId.Saturn] = 10759.22,
    };

    /// <summary>The set of planets for which cycle timelines are supported.</summary>
    private static readonly HashSet<CelestialBodyId> SupportedPlanets =
    [
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
    ];

    /// <summary>Bodies included in phase point snapshots.</summary>
    private static readonly CelestialBodyId[] SnapshotBodies =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Moon,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
        CelestialBodyId.TrueNode,
        CelestialBodyId.Chiron,
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="PlanetaryCycleTimelineService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position lookups and JD conversions.</param>
    /// <param name="returnService">Planetary return service for finding return dates.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public PlanetaryCycleTimelineService(
        IEphemerisService ephemeris,
        IPlanetaryReturnService returnService,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(returnService);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _returnService = returnService;
        _logger = logger.ForContext<PlanetaryCycleTimelineService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public PlanetaryCycleTimeline Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ValidatePlanet(planet);

        if (returnNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(returnNumber),
                "Return number must be >= 1.");

        string planetName = planet.ToString();

        _logger.Debug(
            "Computing {Planet} cycle timeline starting at return #{N}",
            planetName, returnNumber);

        // Step 1: Find the starting return
        cancellationToken.ThrowIfCancellationRequested();
        PlanetaryReturnResult startReturn = _returnService.Calculate(
            natalChart, planet, returnNumber,
            natalChart.Input.Latitude, natalChart.Input.Longitude,
            natalChart.Input.LocationName, natalChart.Input.TimeZone,
            cancellationToken);

        // Step 2: Find the next return
        cancellationToken.ThrowIfCancellationRequested();
        PlanetaryReturnResult endReturn = _returnService.Calculate(
            natalChart, planet, returnNumber + 1,
            natalChart.Input.Latitude, natalChart.Input.Longitude,
            natalChart.Input.LocationName, natalChart.Input.TimeZone,
            cancellationToken);

        double natalLon = startReturn.NatalLongitude;
        // The return charts' own Julian Days (RT3): their UtcDateTime read back through the date's
        // calendar switch was a Julian date before 1582-10-15, which started the cycle 7 to 10 days late.
        double jdStart = startReturn.ReturnChart.JulianDay;
        double jdEnd = endReturn.ReturnChart.JulianDay;
        double cycleDays = jdEnd - jdStart;

        _logger.Debug(
            "{Planet} cycle #{N}: {Start:yyyy-MM-dd} to {End:yyyy-MM-dd} ({Days:F0} days)",
            planetName, returnNumber, startReturn.ReturnChart.UtcDateTime,
            endReturn.ReturnChart.UtcDateTime, cycleDays);

        // Step 3: Build phase points
        List<CyclePhasePoint> phasePoints = [];

        foreach ((string phaseName, int phaseAngle) in Phases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CyclePhasePoint point;

            if (phaseAngle == 0)
            {
                // Return — the passage's first pass, with all its passes (T5-3)
                point = BuildPhasePoint(phaseName, phaseAngle, natalLon,
                    startReturn.ReturnChart.UtcDateTime, jdStart, jdStart, cycleDays,
                    passCount: startReturn.Passes.Count, allPasses: startReturn.Passes,
                    includeSnapshots ? GetSnapshot(jdStart) : null);
            }
            else if (phaseAngle == 360)
            {
                // Next Return — the next passage's first pass, with all its passes
                point = BuildPhasePoint(phaseName, phaseAngle, natalLon,
                    endReturn.ReturnChart.UtcDateTime, jdEnd, jdStart, cycleDays,
                    passCount: endReturn.Passes.Count, allPasses: endReturn.Passes,
                    includeSnapshots ? GetSnapshot(jdEnd) : null);
            }
            else
            {
                // Intermediate phase — find via ReturnChartFinder
                point = FindPhasePoint(planet, phaseName, phaseAngle, natalLon,
                    jdStart, jdEnd, cycleDays, includeSnapshots, cancellationToken);
            }

            phasePoints.Add(point);
        }

        string engineVersion = BuildVersionString();

        PlanetaryCycleTimeline timeline = new()
        {
            Planet = planet,
            PlanetName = planetName,
            NatalLongitude = natalLon,
            NatalPositionFormatted = PositionFormatter.FormatLongitude(natalLon),
            NativeName = natalChart.Input.Name,
            ReturnNumber = returnNumber,
            PhasePoints = phasePoints,
            CycleDurationDays = cycleDays,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };

        // Information-level stays PII-free (M-6); the subject name is Debug-only.
        _logger.Information(
            "{Planet} cycle timeline #{N}: {Days:F0} days, {Count} phase points",
            planetName, returnNumber, cycleDays, phasePoints.Count);
        _logger.Debug(
            "{Planet} cycle timeline #{N} for '{Name}' computed",
            planetName, returnNumber, natalChart.Input.Name);

        return timeline;
    }

    /// <inheritdoc />
    public PlanetaryCycleTimeline CalculateForDate(
        ChartResult natalChart,
        CelestialBodyId planet,
        DateTime date,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ValidatePlanet(planet);

        // The cycle that holds the date: return #N, N the number of passages whose first pass is
        // at or before the date (R7); the date is read as UT
        double dateJd = _ephemeris.DateToJulianDay(date.Year, date.Month, date.Day, date.TimeOfDay.TotalHours);
        int returnNumber = 0;
        double? firstReturnJd = null;
        foreach (ReturnPassage passage in ReturnPassageFinder.EnumeratePassages(
            _ephemeris, natalChart, planet, cancellationToken))
        {
            firstReturnJd ??= passage.FirstPass;
            if (passage.FirstPass > dateJd)
                break;
            returnNumber = passage.Number;
        }

        if (returnNumber == 0)
        {
            string firstReturn = firstReturnJd is double jd
                ? string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"the first return is {CalendarDateFormatter.DateTime(JulianDayUtility.ToDateTimeUtc(_ephemeris, jd))} UTC")
                : "no return occurs before the ephemeris files end";
            throw new InvalidOperationException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"No {planet} cycle holds {date:yyyy-MM-dd}: {firstReturn}."));
        }

        return Calculate(natalChart, planet, returnNumber, includeSnapshots, cancellationToken);
    }

    /// <summary>
    /// Find an intermediate phase point (90°, 180°, 270°) using ReturnChartFinder: the first pass of
    /// the planet over the phase's longitude in a window around its expected place in the cycle, with
    /// every pass listed.
    /// </summary>
    private CyclePhasePoint FindPhasePoint(
        CelestialBodyId planet,
        string phaseName,
        int phaseAngle,
        double natalLon,
        double jdCycleStart,
        double jdCycleEnd,
        double cycleDays,
        bool includeSnapshots,
        CancellationToken ct)
    {
        double targetLon = (natalLon + phaseAngle) % 360.0;

        // Estimate where in the cycle this phase should occur
        double fractionEstimate = phaseAngle / 360.0;
        double estimatedJd = jdCycleStart + cycleDays * fractionEstimate;

        // Scan window: 30% margin around the estimate, clamped to cycle boundaries
        double margin = cycleDays * 0.3;
        double scanStart = Math.Max(estimatedJd - margin, jdCycleStart);
        double scanEnd = Math.Min(estimatedJd + margin, jdCycleEnd + cycleDays * 0.05);

        List<double> passes = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, planet, targetLon,
            scanStart, scanEnd,
            filterConjunctionOnly: true, ct);

        if (passes.Count == 0)
        {
            _logger.Warning(
                "{Planet} {Phase} ({Angle}°) not found in scan window. " +
                "Using estimate at {Est:F2} JD",
                planet, phaseName, phaseAngle, estimatedJd);

            // Fallback: use the estimated position
            DateTime estUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, estimatedJd);
            return BuildPhasePoint(phaseName, phaseAngle, natalLon,
                estUtc, estimatedJd, jdCycleStart, cycleDays,
                passCount: 0, allPasses: [],
                includeSnapshots ? GetSnapshot(estimatedJd) : null);
        }

        // Convert all passes to DateTimes
        List<DateTime> allPassDates = passes.Select(jd => JulianDayUtility.ToDateTimeUtc(_ephemeris, jd)).ToList();

        // The phase point is its first pass, for every planet (ruling T-D6)
        double jdExact = passes[0];

        DateTime exactUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdExact);

        _logger.Debug(
            "{Planet} {Phase} ({Angle}°): {Date:yyyy-MM-dd}, {Passes} pass(es)",
            planet, phaseName, phaseAngle, exactUtc, passes.Count);

        return BuildPhasePoint(phaseName, phaseAngle, natalLon,
            exactUtc, jdExact, jdCycleStart, cycleDays,
            passCount: passes.Count, allPasses: allPassDates,
            includeSnapshots ? GetSnapshot(jdExact) : null);
    }

    /// <summary>
    /// Build a <see cref="CyclePhasePoint"/> from computed data.
    /// </summary>
    private static CyclePhasePoint BuildPhasePoint(
        string phaseName,
        int phaseAngle,
        double natalLon,
        DateTime exactUtc,
        double jdExact,
        double jdCycleStart,
        double cycleDays,
        int passCount,
        IReadOnlyList<DateTime> allPasses,
        IReadOnlyList<PlanetPosition>? snapshot)
    {
        double targetLon = (natalLon + phaseAngle) % 360.0;
        double daysFromStart = jdExact - jdCycleStart;
        double cyclePercentage = cycleDays > 0 ? (daysFromStart / cycleDays) * 100.0 : 0.0;

        // Get zodiac sign from target longitude
        int signIndex = (int)(targetLon / 30.0);
        ZodiacSign sign = (ZodiacSign)(signIndex + 1);

        return new CyclePhasePoint
        {
            PhaseName = phaseName,
            PhaseAngle = phaseAngle,
            TargetLongitude = targetLon,
            TargetPositionFormatted = PositionFormatter.FormatLongitude(targetLon),
            ExactUtc = exactUtc,
            Sign = sign.ToString(),
            DaysFromStart = Math.Round(daysFromStart, 2),
            CyclePercentage = Math.Round(cyclePercentage, 2),
            PassCount = passCount,
            AllPasses = allPasses,
            Snapshot = snapshot,
        };
    }

    /// <summary>
    /// Get a snapshot of all traditional planet positions at a given Julian Day.
    /// </summary>
    private IReadOnlyList<PlanetPosition> GetSnapshot(double julianDay)
    {
        List<PlanetPosition> positions = [];
        foreach (CelestialBodyId body in SnapshotBodies)
        {
            PlanetPosition pos = _ephemeris.CalculatePositionLongitudeOnly(julianDay, body);
            positions.Add(pos);
        }
        return positions;
    }

    /// <summary>
    /// Validates that the planet is one of the supported traditional planets.
    /// </summary>
    private static void ValidatePlanet(CelestialBodyId planet)
    {
        if (!SupportedPlanets.Contains(planet))
            throw new ArgumentException(
                $"Cycle timelines are only supported for Mercury, Venus, Mars, Jupiter, " +
                $"and Saturn. Got: {planet}",
                nameof(planet));
    }

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(PlanetaryCycleTimelineService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;
        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }
}
