using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Orchestrates a complete transit calculation by coordinating all transit
/// sub-services: ephemeris helper, station detector, ingress detector,
/// Mars evaluator, aspect finder, midpoint scanner, and house overlay calculator.
/// </summary>
/// <remarks>
/// This is the composition root for the transit engine. It constructs all
/// sub-services internally from a single <see cref="IEphemerisService"/>
/// dependency, following the same pattern as <see cref="ChartEngine"/>.
/// The <see cref="Calculate"/> method produces a complete <see cref="TransitResult"/>
/// in a single call.
/// </remarks>
public sealed class TransitService : ITransitService
{
    #region Sub-Services

    private readonly ILogger _logger;
    private readonly IEphemerisService _rawEphemeris;
    private readonly TransitEphemerisHelper _ephemeris;
    private readonly TransitStationDetector _stationDetector;
    private readonly TransitIngressDetector _ingressDetector;
    private readonly TransitMarsEvaluator _marsEvaluator;
    private readonly TransitAspectFinder _aspectFinder;
    private readonly TransitMidpointScanner _midpointScanner;
    private readonly TransitToTransitAspectFinder _transitToTransitFinder;
    private readonly TransitHouseOverlayCalculator _houseOverlayCalculator;

    #endregion

    #region Constants

    /// <summary>
    /// Standard transit bodies always included in a transit report.
    /// These are the outer planets, Chiron, and North Node.
    /// </summary>
    private static readonly TransitBody[] StandardTransitBodies =
    [
        TransitBody.Pluto,
        TransitBody.Neptune,
        TransitBody.Uranus,
        TransitBody.Saturn,
        TransitBody.Jupiter,
        TransitBody.Chiron,
        TransitBody.NorthNode,
    ];

    /// <summary>
    /// Every body scanned for stations, always, in the order the Stations table lists them: the
    /// standard transit bodies, Mars whether or not it is an active transit body, then Mercury and
    /// Venus, which station but are never active transit bodies (card O-D6). The Sun and Moon never
    /// station, and the true node's reversals of motion, about fifty a year, are oscillation, not
    /// stations: it has no station rows (ruling T-D4, branch D, 1.3.4).
    /// </summary>
    private static readonly TransitBody[] StationBodies =
    [
        TransitBody.Pluto,
        TransitBody.Neptune,
        TransitBody.Uranus,
        TransitBody.Saturn,
        TransitBody.Jupiter,
        TransitBody.Chiron,
        TransitBody.Mars,
        TransitBody.Mercury,
        TransitBody.Venus,
    ];

    /// <summary>
    /// Name-to-symbol mapping for natal chart bodies and angles.
    /// </summary>
    private static readonly Dictionary<string, string> NatalSymbolMap = new(StringComparer.Ordinal)
    {
        ["Sun"] = "SU",
        ["Moon"] = "MO",
        ["Mercury"] = "ME",
        ["Venus"] = "VE",
        ["Mars"] = "MA",
        ["Jupiter"] = "JU",
        ["Saturn"] = "SA",
        ["Uranus"] = "UR",
        ["Neptune"] = "NE",
        ["Pluto"] = "PL",
        ["True Node"] = "NN",
        ["Chiron"] = "CH",
        ["ASC"] = "ASC",
        ["MC"] = "MC",
    };

    /// <summary>
    /// The transit symbols of the natal Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0;
    /// scoping decision 7), explicit: the two-letter fallback <see cref="BuildNatalTargets"/> keeps for an
    /// unmapped name would give Juno and Vesta Jupiter's and Venus's symbols.
    /// </summary>
    private static readonly Dictionary<string, string> MinorPointSymbolMap = new(StringComparer.Ordinal)
    {
        ["Ceres"] = "CER",
        ["Pallas"] = "PAL",
        ["Juno"] = "JUN",
        ["Vesta"] = "VES",
        ["Lilith"] = "LIL",
    };

    /// <summary>
    /// Personal point names that trigger priority flagging for midpoints.
    /// </summary>
    private static readonly HashSet<string> PriorityBodies =
        new(StringComparer.Ordinal) { "Sun", "Moon", "ASC", "MC" };

    #endregion

    /// <summary>
    /// Initialises a new instance of <see cref="TransitService"/>.
    /// Constructs all sub-services from the provided ephemeris service.
    /// </summary>
    /// <param name="ephemerisService">The shared ephemeris service for Swiss Ephemeris calls.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <remarks>
    /// <para>
    /// Sub-services (<see cref="TransitEphemerisHelper"/>, <see cref="TransitStationDetector"/>,
    /// <see cref="TransitIngressDetector"/>, <see cref="TransitMarsEvaluator"/>,
    /// <see cref="TransitAspectFinder"/>, <see cref="TransitMidpointScanner"/>,
    /// <see cref="TransitHouseOverlayCalculator"/>) are intentionally constructed
    /// here with <c>new</c> rather than injected via DI. These are internal
    /// implementation details of the transit engine — they share the same
    /// <see cref="IEphemerisService"/> instance and have no independent lifecycle.
    /// Injecting them would expose an implementation detail and provide no testability
    /// benefit, since the transit sub-services are always tested through
    /// <see cref="ITransitService.Calculate"/>.
    /// </para>
    /// <para>
    /// <see cref="ProgressionService"/>, <see cref="SolarArcService"/>, <see cref="SolarArcHitFinder"/>
    /// and <see cref="ProgressedHitFinder"/> are also created on-demand inside <see cref="Calculate"/>
    /// when their techniques are requested. They follow the same composition pattern.
    /// </para>
    /// </remarks>
    public TransitService(IEphemerisService ephemerisService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemerisService);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger.ForContext<TransitService>();
        _rawEphemeris = ephemerisService;
        _ephemeris = new TransitEphemerisHelper(ephemerisService);
        _stationDetector = new TransitStationDetector(_ephemeris);
        _ingressDetector = new TransitIngressDetector(_ephemeris);
        _marsEvaluator = new TransitMarsEvaluator(_ephemeris, _stationDetector);
        _aspectFinder = new TransitAspectFinder(_ephemeris, _stationDetector);
        _midpointScanner = new TransitMidpointScanner(_ephemeris);
        _transitToTransitFinder = new TransitToTransitAspectFinder(_ephemeris);
        _houseOverlayCalculator = new TransitHouseOverlayCalculator(_ephemeris);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when none of <see cref="TransitInput.IncludeTransits"/>, <see cref="TransitInput.IncludeSolarArcs"/>,
    /// <see cref="TransitInput.IncludeSecondaryProgressions"/> and <see cref="TransitInput.IncludePrimaryDirections"/>
    /// is set.
    /// </exception>
    public TransitResult Calculate(TransitInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.IncludeTransits && !input.IncludeSolarArcs && !input.IncludeSecondaryProgressions && !input.IncludePrimaryDirections)
        {
            throw new ArgumentException(
                "At least one technique must be requested: IncludeTransits, IncludeSolarArcs, IncludeSecondaryProgressions or IncludePrimaryDirections.",
                nameof(input));
        }

        // The typed bounds, read once to the days they denote (D21; branch R3, 1.3.4, R-D7): a bound
        // on or before 1582-10-04 is a Julian date, one in 1582-10-05 to 1582-10-14 is refused. Every
        // scan from here steps proleptic days and converts them by arithmetic; the result keeps the
        // typed bounds, printed as typed. Until 1.3.4 the scans stepped the typed date and read each
        // day back through the calendar switch, so across October 1582 ten days were scanned twice.
        DateOnly scanStart = CalendarDateFormatter.ProlepticDay(input.ReportStartDate);
        DateOnly scanEnd = CalendarDateFormatter.ProlepticDay(input.ReportEndDate);

        // The progressions and solar arcs (branch Modes, 1.4.0 item 2): the snapshots at the report's
        // start, not its midpoint, and the dated hits over the period; the solar arcs are directed by
        // the progressions' arc, so the progressions are computed for either technique
        ProgressionResult? progressions = null;
        SolarArcResult? solarArcs = null;
        IReadOnlyList<SolarArcHit> solarArcHits = [];
        IReadOnlyList<ProgressedHit> progressedHits = [];

        // The natal Ceres, Pallas, Juno, Vesta and Lilith as targets (R7, branch AsteroidsTiming, 1.6.0): when the
        // natal chart carries them, the dated hits to them by the finders' target overloads, in lists of their
        // own; the five are never progressed or directed
        IReadOnlyList<(string Name, double Longitude)> minorPointLongitudes = MinorPointLongitudes(input.NatalChart);
        IReadOnlyList<ProgressedHit> minorPointProgressedHits = [];
        IReadOnlyList<SolarArcHit> minorPointSolarArcHits = [];

        if (input.IncludeSolarArcs || input.IncludeSecondaryProgressions)
        {
            IProgressionService progressionService = new ProgressionService(_rawEphemeris);
            ProgressionResult computed = progressionService.Calculate(input.NatalChart, scanStart, cancellationToken);

            if (input.IncludeSecondaryProgressions)
            {
                progressions = computed;
                ProgressedHitFinder progressedHitFinder = new(_rawEphemeris);
                progressedHits = progressedHitFinder.FindHits(input.NatalChart, scanStart, scanEnd);
                if (minorPointLongitudes.Count > 0)
                    minorPointProgressedHits = progressedHitFinder.FindHits(input.NatalChart, scanStart, scanEnd, minorPointLongitudes);
            }

            if (input.IncludeSolarArcs)
            {
                ISolarArcService solarArcService = new SolarArcService();
                solarArcs = solarArcService.Calculate(input.NatalChart, computed.SolarArcValue);
                SolarArcHitFinder solarArcHitFinder = new(_rawEphemeris);
                solarArcHits = solarArcHitFinder.FindHits(input.NatalChart, scanStart, scanEnd);
                if (minorPointLongitudes.Count > 0)
                    minorPointSolarArcHits = solarArcHitFinder.FindHits(input.NatalChart, scanStart, scanEnd, minorPointLongitudes);
            }
        }

        // The primary directions (branch PrimaryExport, 1.5.0): the whole life calculated once, the
        // period's hits taken by FindHits' window, the UT date in [start, end], without a second calculation
        PrimaryDirectionResult? primaryDirections = null;
        IReadOnlyList<PrimaryDirectionHit> primaryDirectionHits = [];

        if (input.IncludePrimaryDirections)
        {
            primaryDirections = new PrimaryDirectionService(_rawEphemeris)
                .Calculate(input.NatalChart, input.PrimaryDirectionSettings, cancellationToken);
            primaryDirectionHits = primaryDirections.Hits
                .Where(hit =>
                {
                    DateOnly date = DateOnly.FromDateTime(hit.InstantUtc);
                    return date >= scanStart && date <= scanEnd;
                })
                .ToList()
                .AsReadOnly();
        }

        // Without the transits the whole scan is skipped: the result holds the natal chart, the
        // techniques asked for and empty transit lists
        if (!input.IncludeTransits)
        {
            return new TransitResult
            {
                NatalChart = input.NatalChart,
                ReportStartDate = input.ReportStartDate,
                ReportEndDate = input.ReportEndDate,
                MidpointDate = input.MidpointDate,
                IncludeMinorAspects = input.IncludeMinorAspects,
                IncludesTransits = false,
                TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
                Stations = [],
                Ingresses = [],
                ActiveTransits = [],
                HouseOverlays = [],
                MidpointActivations = [],
                MarsIncluded = false,
                Progressions = progressions,
                SolarArcs = solarArcs,
                SolarArcHits = solarArcHits,
                ProgressedHits = progressedHits,
                MinorPointProgressedHits = minorPointProgressedHits,
                MinorPointSolarArcHits = minorPointSolarArcHits,
                PrimaryDirections = primaryDirections,
                PrimaryDirectionHits = primaryDirectionHits,
            };
        }

        // 1. Extract natal data from the chart result
        IReadOnlyList<NatalTarget> natalTargets = BuildNatalTargets(input.NatalChart);
        IReadOnlyList<NatalMidpointTarget> natalMidpoints = BuildNatalMidpointTargets(input.NatalChart);
        IReadOnlyList<double> houseCusps = ExtractHouseCusps(input.NatalChart);

        // 2. Determine if Mars should be included
        IReadOnlyList<double> natalTargetLongitudes = natalTargets
            .Select(t => t.Longitude).ToList();

        bool marsIncluded = _marsEvaluator.ShouldIncludeMars(
            scanStart, scanEnd, natalTargetLongitudes);

        // 3. Build the list of active transit bodies
        List<TransitBody> activeBodies = [.. StandardTransitBodies];
        if (marsIncluded)
            activeBodies.Add(TransitBody.Mars);

        // 4. Get transit positions at report start date
        Dictionary<TransitBody, TransitPlanetPosition> transitPositions = [];
        double startJd = _ephemeris.DateToJulianDay(scanStart);
        foreach (TransitBody body in activeBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            transitPositions[body] = _ephemeris.GetPosition(body, startJd);
        }

        // 5. Stations of every body that stations, in the Stations table's order (card O-D6; the
        //    true node has none, T-D4), and ingresses of every active transit body. The aspect
        //    finder finds each transit's own stations over the transit's dates (R3).
        List<TransitStation> allStations = [];
        foreach (TransitBody body in StationBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allStations.AddRange(
                _stationDetector.FindStations(body, scanStart, scanEnd));
        }

        List<TransitIngress> allIngresses = [];
        foreach (TransitBody body in activeBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allIngresses.AddRange(
                _ingressDetector.FindIngresses(body, scanStart, scanEnd));
        }

        // 6. Find active transit-to-natal aspects for each transit body
        List<ActiveTransit> allActiveTransits = [];
        foreach (TransitBody body in activeBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ActiveTransit> transits = _aspectFinder.FindActiveTransits(
                body, natalTargets, scanStart, scanEnd,
                input.IncludeMinorAspects, houseCusps, cancellationToken);
            allActiveTransits.AddRange(transits);
        }

        // A Mars transit is listed only where Mars is slow and in orb on a report day (T-D5)
        allActiveTransits = KeepMarsTransitsWhereSlowInOrb(
            allActiveTransits, scanStart, scanEnd);

        // Sort active transits: outermost planet first, then by natal target order. The sort is
        // stable, so the transits of one body and target keep the finder's order: aspects in table
        // order, and one aspect's transits by entry.
        allActiveTransits = SortByBodyThenTarget(allActiveTransits);

        // 6b. The transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (R7, branch AsteroidsTiming,
        //     1.6.0), when the natal chart carries them: the same active bodies (Mars's presence decided
        //     from the main natal targets alone) over the five's targets by the five Ptolemaic aspects at
        //     each body's major orbs whatever the report's Aspects setting, a Mars transit kept by the same
        //     rule (T-D5), sorted as the chart's own transits are. The five take no house overlay,
        //     midpoint activation, declination row or transit-to-transit row (R8).
        List<ActiveTransit> minorPointTransits = [];
        IReadOnlyList<NatalTarget> minorPointTargets = BuildMinorPointTargets(input.NatalChart);
        if (minorPointTargets.Count > 0)
        {
            foreach (TransitBody body in activeBodies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                minorPointTransits.AddRange(_aspectFinder.FindActiveTransits(
                    body, minorPointTargets, scanStart, scanEnd,
                    includeMinorAspects: false, houseCusps, cancellationToken));
            }

            minorPointTransits = SortByBodyThenTarget(
                KeepMarsTransitsWhereSlowInOrb(minorPointTransits, scanStart, scanEnd));
        }

        // 7. Calculate house overlays at report midpoint date
        IReadOnlyList<TransitHouseOverlay> houseOverlays =
            _houseOverlayCalculator.Calculate(activeBodies, input.MidpointDate, houseCusps);

        // 8. Find midpoint activations for each transit body
        List<TransitMidpointActivation> allMidpointActivations = [];
        foreach (TransitBody body in activeBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TransitMidpointActivation> activations =
                _midpointScanner.FindActivations(
                    body, natalMidpoints, scanStart, scanEnd);
            allMidpointActivations.AddRange(activations);
        }

        // A Mars midpoint activation is listed only where Mars is slow and in orb on a report day (T-D5)
        allMidpointActivations = KeepMarsActivationsWhereSlowInOrb(
            allMidpointActivations, scanStart, scanEnd);

        // Sort midpoint activations: outermost planet first
        allMidpointActivations.Sort((a, b) =>
        {
            int sortA = TransitBodySortOrder(a.TransitPlanet);
            int sortB = TransitBodySortOrder(b.TransitPlanet);
            return sortA.CompareTo(sortB);
        });

        // 9. Transit-to-transit aspects (exclude North Node due to oscillation)
        List<TransitBody> transitToTransitBodies = activeBodies
            .Where(b => b != TransitBody.NorthNode)
            .ToList();
        IReadOnlyList<TransitToTransitAspect> transitToTransitAspects =
            _transitToTransitFinder.FindAspects(
                transitToTransitBodies, scanStart, scanEnd);

        // 10. Transit-to-natal declination aspects at exact pass times
        IReadOnlyList<TransitDeclinationAspect> transitDeclinationAspects =
            FindTransitDeclinationAspects(allActiveTransits, input.NatalChart.Positions);

        // 11. Assemble the complete transit result, the progressions and solar arcs computed above
        return new TransitResult
        {
            NatalChart = input.NatalChart,
            ReportStartDate = input.ReportStartDate,
            ReportEndDate = input.ReportEndDate,
            MidpointDate = input.MidpointDate,
            IncludeMinorAspects = input.IncludeMinorAspects,
            IncludesTransits = true,
            TransitPositions = transitPositions,
            Stations = allStations,
            Ingresses = allIngresses,
            ActiveTransits = allActiveTransits,
            HouseOverlays = houseOverlays,
            MidpointActivations = allMidpointActivations,
            MarsIncluded = marsIncluded,
            MinorPointTransits = minorPointTransits,
            TransitDeclinationAspects = transitDeclinationAspects,
            TransitToTransitAspects = transitToTransitAspects,
            Progressions = progressions,
            SolarArcs = solarArcs,
            SolarArcHits = solarArcHits,
            ProgressedHits = progressedHits,
            MinorPointProgressedHits = minorPointProgressedHits,
            MinorPointSolarArcHits = minorPointSolarArcHits,
            PrimaryDirections = primaryDirections,
            PrimaryDirectionHits = primaryDirectionHits,
        };
    }

    #region Natal Data Extraction

    /// <summary>
    /// Builds natal target list from the chart result: 12 planets + ASC + MC = 14 targets.
    /// Each target includes name, symbol, ecliptic longitude, and natal house number.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <returns>14 natal targets for aspect scanning.</returns>
    private IReadOnlyList<NatalTarget> BuildNatalTargets(ChartResult chart)
    {
        List<NatalTarget> targets = new(chart.Positions.Count + 2);

        // 12 planetary bodies
        foreach (PlanetPosition position in chart.Positions)
        {
            string symbol = NatalSymbolMap.TryGetValue(position.Name, out string? sym)
                ? sym
                : position.Name[..2].ToUpperInvariant();

            if (position.House is null)
            {
                _logger.Warning(
                    "House not assigned for {Body} — defaulting to house 1",
                    position.Name);
            }

            targets.Add(new NatalTarget
            {
                Name = position.Name,
                Symbol = symbol,
                Longitude = position.Longitude,
                HouseNumber = position.House ?? 1,
            });
        }

        // Ascendant
        targets.Add(new NatalTarget
        {
            Name = "ASC",
            Symbol = "ASC",
            Longitude = chart.Houses.Ascendant,
            HouseNumber = 1,
        });

        // Midheaven
        targets.Add(new NatalTarget
        {
            Name = "MC",
            Symbol = "MC",
            Longitude = chart.Houses.Midheaven,
            HouseNumber = 10,
        });

        return targets;
    }

    /// <summary>
    /// Builds the natal targets of the minor points (R7, branch AsteroidsTiming, 1.6.0): one target per entry of
    /// <see cref="ChartResult.MinorPoints"/>, in its order (Ceres, Pallas, Juno, Vesta, Lilith), each with the
    /// symbol <see cref="MinorPointSymbolMap"/> gives it, its longitude and its natal house. Empty when the chart
    /// does not carry the five.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <returns>The five's targets, or none.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a minor point has no symbol in <see cref="MinorPointSymbolMap"/>: the fallback the planets'
    /// map keeps is never taken for the five.
    /// </exception>
    private IReadOnlyList<NatalTarget> BuildMinorPointTargets(ChartResult chart)
    {
        List<NatalTarget> targets = new(chart.MinorPoints.Count);

        foreach (PlanetPosition position in chart.MinorPoints)
        {
            if (!MinorPointSymbolMap.TryGetValue(position.Name, out string? symbol))
            {
                throw new InvalidOperationException(
                    $"The minor point '{position.Name}' has no transit symbol.");
            }

            if (position.House is null)
            {
                _logger.Warning(
                    "House not assigned for {Body} — defaulting to house 1",
                    position.Name);
            }

            targets.Add(new NatalTarget
            {
                Name = position.Name,
                Symbol = symbol,
                Longitude = position.Longitude,
                HouseNumber = position.House ?? 1,
            });
        }

        return targets;
    }

    /// <summary>
    /// The minor points as the hit finders' targets (R7, branch AsteroidsTiming, 1.6.0): each point's name and
    /// longitude, in <see cref="ChartResult.MinorPoints"/>' order. Empty when the chart does not carry the five.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <returns>The five's names and longitudes, or none.</returns>
    private static IReadOnlyList<(string Name, double Longitude)> MinorPointLongitudes(ChartResult chart) =>
        chart.MinorPoints.Select(p => (p.Name, p.Longitude)).ToList().AsReadOnly();

    /// <summary>
    /// Builds natal midpoint targets from the chart result, reduced to the 45° dial.
    /// Priority midpoints are those involving personal points (Sun, Moon, ASC, MC).
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <returns>Natal midpoint targets for transit midpoint scanning.</returns>
    private static IReadOnlyList<NatalMidpointTarget> BuildNatalMidpointTargets(ChartResult chart)
    {
        List<NatalMidpointTarget> targets = new(chart.Midpoints.Count);

        foreach (Midpoint midpoint in chart.Midpoints)
        {
            double dial45Position = TransitMidpointScanner.ReduceToDial45(midpoint.Longitude);
            bool isPriority = PriorityBodies.Contains(midpoint.Body1Name)
                           || PriorityBodies.Contains(midpoint.Body2Name);

            targets.Add(new NatalMidpointTarget
            {
                Body1 = midpoint.Body1Name,
                Body2 = midpoint.Body2Name,
                Dial45Position = dial45Position,
                IsPriority = isPriority,
                PriorityReason = isPriority ? midpoint.Notation : null,
            });
        }

        return targets;
    }

    /// <summary>
    /// Extracts the 12 house cusp longitudes from the chart result.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <returns>12 house cusp longitudes (index 0 = house 1 cusp).</returns>
    private static IReadOnlyList<double> ExtractHouseCusps(ChartResult chart)
    {
        return chart.Houses.Cusps;
    }

    #endregion

    #region Mars (T-D5)

    /// <summary>Mars's longitude and daily motion at 12:00 UT on a report date.</summary>
    /// <param name="Date">The report date.</param>
    /// <param name="Longitude">Mars's longitude at 12:00 UT.</param>
    /// <param name="DailyMotion">Mars's daily motion in longitude at 12:00 UT.</param>
    private sealed record MarsSample(DateOnly Date, double Longitude, double DailyMotion);

    /// <summary>Mars's longitude and daily motion at 12:00 UT on every report date.</summary>
    /// <param name="reportStart">The report's first date.</param>
    /// <param name="reportEnd">The report's last date.</param>
    /// <returns>The samples, in date order.</returns>
    private List<MarsSample> ReadMarsReportSamples(DateOnly reportStart, DateOnly reportEnd)
    {
        List<MarsSample> samples = [];
        for (DateOnly date = reportStart; date <= reportEnd; date = date.AddDays(1))
        {
            double jd = _ephemeris.DateToJulianDay(date);
            samples.Add(new MarsSample(
                date,
                _ephemeris.GetLongitude(TransitBody.Mars, jd),
                _ephemeris.GetDailyMotion(TransitBody.Mars, jd)));
        }

        return samples;
    }

    /// <summary>
    /// Whether on at least one report date between the lifecycle's entry and exit Mars is moving
    /// below <see cref="TransitOrbs.MarsSlowMotionThreshold"/> at 12:00 UT and its longitude then
    /// satisfies <paramref name="inOrb"/>.
    /// </summary>
    /// <param name="lifecycle">A Mars transit's or activation's lifecycle.</param>
    /// <param name="samples">Mars's report samples.</param>
    /// <param name="inOrb">Whether a longitude is in orb of the transit or activation.</param>
    /// <returns>Whether such a date exists.</returns>
    private static bool MarsIsSlowInOrbOnAReportDay(
        IReadOnlyList<TransitLifecycleEvent> lifecycle, List<MarsSample> samples, Func<double, bool> inOrb)
    {
        DateOnly entry = lifecycle.First(e => e.EventType == TransitLifecycleEventType.Entry).DatePart;
        DateOnly exit = lifecycle.First(e => e.EventType == TransitLifecycleEventType.Exit).DatePart;

        foreach (MarsSample sample in samples)
        {
            if (sample.Date >= entry && sample.Date <= exit
                && Math.Abs(sample.DailyMotion) < TransitOrbs.MarsSlowMotionThreshold
                && inOrb(sample.Longitude))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps a Mars transit only if on at least one report date between its entry and exit Mars is
    /// in orb of its aspect at 12:00 UT and moving below <see cref="TransitOrbs.MarsSlowMotionThreshold"/>
    /// (ruling T-D5, option c, branch D, 1.3.4): a transit Mars makes at speed, over in two or three
    /// days, is not listed; one it stations in, or crawls through near a station, is. Every other
    /// body's transit passes through. <see cref="TransitMarsEvaluator.ShouldIncludeMars"/> stays
    /// the gate for Mars's presence in the report; until 1.3.4 a report that passed it listed every
    /// Mars transit of its period, and a two-year report always did.
    /// </summary>
    /// <param name="transits">The active transits of every body.</param>
    /// <param name="reportStart">The report's first date.</param>
    /// <param name="reportEnd">The report's last date.</param>
    /// <returns>The transits without the Mars transits that fail the rule.</returns>
    private List<ActiveTransit> KeepMarsTransitsWhereSlowInOrb(
        List<ActiveTransit> transits, DateOnly reportStart, DateOnly reportEnd)
    {
        if (!transits.Any(t => t.TransitPlanet == TransitBody.Mars))
            return transits;

        List<MarsSample> samples = ReadMarsReportSamples(reportStart, reportEnd);

        bool Keep(ActiveTransit transit)
        {
            if (transit.TransitPlanet != TransitBody.Mars)
                return true;

            if (!TransitAspectFinder.TryGetAspect(transit.AspectName, out double angle, out bool isMinor))
                return true;

            double orb = TransitOrbs.GetOrb(TransitBody.Mars, angle, isMinor);
            return MarsIsSlowInOrbOnAReportDay(
                transit.Lifecycle, samples,
                longitude => TransitAspectFinder.IsWithinOrb(longitude, transit.NatalLongitude, angle, orb));
        }

        return transits.Where(Keep).ToList();
    }

    /// <summary>
    /// Keeps a Mars midpoint activation only if on at least one report date between its entry and
    /// exit Mars is within <see cref="TransitOrbs.MidpointOrb"/> of the midpoint on the 45° dial at
    /// 12:00 UT and moving below <see cref="TransitOrbs.MarsSlowMotionThreshold"/> (ruling T-D5,
    /// option c). Every other body's activation passes through.
    /// </summary>
    /// <param name="activations">The midpoint activations of every body.</param>
    /// <param name="reportStart">The report's first date.</param>
    /// <param name="reportEnd">The report's last date.</param>
    /// <returns>The activations without the Mars activations that fail the rule.</returns>
    private List<TransitMidpointActivation> KeepMarsActivationsWhereSlowInOrb(
        List<TransitMidpointActivation> activations, DateOnly reportStart, DateOnly reportEnd)
    {
        if (!activations.Any(a => a.TransitPlanet == TransitBody.Mars))
            return activations;

        List<MarsSample> samples = ReadMarsReportSamples(reportStart, reportEnd);

        bool Keep(TransitMidpointActivation activation) =>
            activation.TransitPlanet != TransitBody.Mars
            || MarsIsSlowInOrbOnAReportDay(
                activation.Lifecycle, samples,
                longitude => TransitMidpointScanner.Dial45Separation(
                    TransitMidpointScanner.ReduceToDial45(longitude), activation.NatalMidpointDial45Position)
                    <= TransitOrbs.MidpointOrb);

        return activations.Where(Keep).ToList();
    }

    #endregion

    #region Transit-to-Natal Declination Aspects

    /// <summary>
    /// Finds transit-to-natal declination aspects (parallels and contraparallels)
    /// by computing the transiting planet's declination at each exact pass time
    /// and comparing it to natal planet declinations within a ±1° orb: one aspect per pair, a
    /// parallel when both lie on the same side of the equator (0° counting as north) and a
    /// contraparallel when on opposite sides, as <see cref="DeclinationAspectService"/> judges a
    /// chart's own pairs (ruling D13). Until 1.3.4 the two tests were independent, so a pair within
    /// a degree of the equator was listed as both.
    /// </summary>
    /// <param name="activeTransits">All active transit-to-natal aspects.</param>
    /// <param name="natalPositions">All natal planet positions with declination data.</param>
    /// <returns>
    /// Transit-to-natal declination aspects sorted chronologically by date.
    /// Empty list if none found.
    /// </returns>
    private IReadOnlyList<TransitDeclinationAspect> FindTransitDeclinationAspects(
        IReadOnlyList<ActiveTransit> activeTransits,
        IReadOnlyList<PlanetPosition> natalPositions)
    {
        // Build a dictionary of natal declinations for fast lookup
        List<(string Name, double Declination)> natalDecs = [];
        foreach (PlanetPosition p in natalPositions)
        {
            if (p.Declination.HasValue)
                natalDecs.Add((p.Name, p.Declination.Value));
        }

        if (natalDecs.Count == 0)
            return [];

        // Collect all unique (transit body, exact pass date) pairs to avoid
        // computing the same declination multiple times for the same body+date
        Dictionary<(TransitBody Body, DateTime Date), double?> transitDecCache = [];

        List<TransitDeclinationAspect> results = [];

        foreach (ActiveTransit transit in activeTransits)
        {
            foreach (TransitLifecycleEvent evt in transit.Lifecycle)
            {
                if (evt.EventType != TransitLifecycleEventType.Exact)
                    continue;

                // Get transiting planet declination at exact pass time
                (TransitBody Body, DateTime Date) cacheKey = (transit.TransitPlanet, evt.Date);
                if (!transitDecCache.TryGetValue(cacheKey, out double? transitDec))
                {
                    // An exact pass's Date is the instant of its Julian Day, to the whole second;
                    // converted by arithmetic (RT6), not read back through the date's calendar switch,
                    // which before 1582-10-15 took the declination 7 to 10 days late.
                    double jd = JulianDayUtility.InstantToJulianDay(evt.Date);
                    transitDec = _ephemeris.GetDeclination(transit.TransitPlanet, jd);
                    transitDecCache[cacheKey] = transitDec;
                }

                if (!transitDec.HasValue)
                    continue;

                double trDec = transitDec.Value;

                // Check against each natal planet: one aspect per pair, by side of the equator (D13)
                foreach ((string natalName, double natalDec) in natalDecs)
                {
                    if (DeclinationAspectService.FindDeclinationAspect(trDec, natalDec)
                        is not (bool isParallel, double orb))
                    {
                        continue;
                    }

                    results.Add(new TransitDeclinationAspect
                    {
                        TransitPlanetName = FormatTransitBodyName(transit.TransitPlanet),
                        TransitDeclination = trDec,
                        IsParallel = isParallel,
                        NatalPlanetName = natalName,
                        NatalDeclination = natalDec,
                        Orb = orb,
                        Date = evt.DatePart,
                    });
                }
            }
        }

        // Remove duplicates: same transit planet + natal planet + aspect type + date
        List<TransitDeclinationAspect> deduplicated = results
            .GroupBy(a => (a.TransitPlanetName, a.NatalPlanetName, a.IsParallel, a.Date))
            .Select(g => g.OrderBy(a => a.Orb).First())
            .ToList();

        // Sort chronologically
        deduplicated.Sort((a, b) => a.Date.CompareTo(b.Date));
        return deduplicated;
    }

    /// <summary>
    /// Formats a <see cref="TransitBody"/> to a display name for transit declination aspects.
    /// </summary>
    /// <param name="body">The transit body.</param>
    /// <returns>Display name (e.g., "Jupiter", "North Node").</returns>
    private static string FormatTransitBodyName(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "Jupiter",
        TransitBody.Saturn => "Saturn",
        TransitBody.Uranus => "Uranus",
        TransitBody.Neptune => "Neptune",
        TransitBody.Pluto => "Pluto",
        TransitBody.Chiron => "Chiron",
        TransitBody.NorthNode => "North Node",
        TransitBody.Mars => "Mars",
        TransitBody.Mercury => "Mercury",
        TransitBody.Venus => "Venus",
        _ => body.ToString(),
    };

    #endregion

    #region Transit Body Ordering

    /// <summary>
    /// Sorts active transits outermost planet first, then by natal target (ordinal). The sort is stable, so
    /// the transits of one body and target keep the finder's order: aspects in table order, and one aspect's
    /// transits by entry. The chart's own transits and the transits to the five (branch AsteroidsTiming, 1.6.0)
    /// are sorted by it alike.
    /// </summary>
    /// <param name="transits">The transits, in the finder's order.</param>
    /// <returns>The transits, sorted.</returns>
    private static List<ActiveTransit> SortByBodyThenTarget(List<ActiveTransit> transits) =>
        transits
            .OrderBy(t => TransitBodySortOrder(t.TransitPlanet))
            .ThenBy(t => t.NatalTarget, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns a sort key for transit bodies: outermost first.
    /// </summary>
    /// <param name="body">The transit body.</param>
    /// <returns>Sort order (lower = sorted first).</returns>
    private static int TransitBodySortOrder(TransitBody body) => body switch
    {
        TransitBody.Pluto => 0,
        TransitBody.Neptune => 1,
        TransitBody.Uranus => 2,
        TransitBody.Saturn => 3,
        TransitBody.Jupiter => 4,
        TransitBody.Chiron => 5,
        TransitBody.NorthNode => 6,
        TransitBody.Mars => 7,
        TransitBody.Mercury => 8,
        TransitBody.Venus => 9,
        _ => 99,
    };

    #endregion
}
