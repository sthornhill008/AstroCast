using AstroCast.Core.Enums;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// Complete result of a transit calculation for a natal chart over a date range.
/// </summary>
public sealed class TransitResult
{
    /// <summary>The natal chart being transited.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>Report start date.</summary>
    public required DateOnly ReportStartDate { get; init; }

    /// <summary>Report end date.</summary>
    public required DateOnly ReportEndDate { get; init; }

    /// <summary>
    /// Report midpoint day (for the house overlay snapshot): the proleptic day
    /// <see cref="TransitInput.MidpointDate"/> derives from the scan (branch R3, 1.3.4), printed through
    /// <see cref="Formatting.CalendarDateFormatter.Format(DateOnly, string)"/>. <see cref="ReportStartDate"/>
    /// and <see cref="ReportEndDate"/> stay the typed bounds, printed as typed. Until 1.4.0 the
    /// progressions and solar arcs were taken here too; since branch Modes they are taken at the start.
    /// </summary>
    public required DateOnly MidpointDate { get; init; }

    /// <summary>Whether minor aspects are included.</summary>
    public required bool IncludeMinorAspects { get; init; }

    /// <summary>
    /// Whether the transit scan ran (<see cref="TransitInput.IncludeTransits"/>; branch Modes, 1.4.0
    /// item 2). When false the transit lists are empty and the export prints no transit section.
    /// </summary>
    public bool IncludesTransits { get; init; } = true;

    /// <summary>
    /// Transiting planet positions at report start date.
    /// Key: TransitBody, Value: position snapshot.
    /// </summary>
    public required IReadOnlyDictionary<TransitBody, TransitPlanetPosition> TransitPositions { get; init; }

    /// <summary>Planetary stations during the report period.</summary>
    public required IReadOnlyList<TransitStation> Stations { get; init; }

    /// <summary>Sign ingresses during the report period.</summary>
    public required IReadOnlyList<TransitIngress> Ingresses { get; init; }

    /// <summary>
    /// Active transit-to-natal aspects, sorted by transiting planet
    /// (outermost first), then by natal target.
    /// </summary>
    public required IReadOnlyList<ActiveTransit> ActiveTransits { get; init; }

    /// <summary>
    /// Transit house overlays at the report midpoint date.
    /// </summary>
    public required IReadOnlyList<TransitHouseOverlay> HouseOverlays { get; init; }

    /// <summary>
    /// Transit midpoint activations on the 45° dial.
    /// </summary>
    public required IReadOnlyList<TransitMidpointActivation> MidpointActivations { get; init; }

    /// <summary>
    /// Whether Mars was included in this transit calculation
    /// (true only if Mars stations or is near station during report period).
    /// </summary>
    public required bool MarsIncluded { get; init; }

    /// <summary>
    /// The transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0; R7): the
    /// bodies that transit, Mars through the same gates as its other transits, each over the five by the five
    /// Ptolemaic aspects at its own major orbs whatever the report's Aspects setting, sorted as
    /// <see cref="ActiveTransits"/> is (outermost body first, then by natal target). Empty unless the natal
    /// chart carries the five (<see cref="ChartResult.MinorPoints"/>) and the transit scan ran. The five take no
    /// house overlay, midpoint activation, declination row or transit-to-transit row (R8).
    /// </summary>
    public IReadOnlyList<ActiveTransit> MinorPointTransits { get; init; } = [];

    /// <summary>
    /// Transit-to-natal declination aspects (parallels and contraparallels)
    /// detected at exact pass times within a ±1° orb. Sorted by date ascending.
    /// Empty list if no transit-to-natal declination aspects found.
    /// </summary>
    public IReadOnlyList<TransitDeclinationAspect> TransitDeclinationAspects { get; init; } = [];

    /// <summary>
    /// Major aspects between transiting planets that reach exact during the
    /// report period. These are mundane events affecting the general sky
    /// configuration. Sorted chronologically by exact date/time.
    /// </summary>
    public IReadOnlyList<TransitToTransitAspect> TransitToTransitAspects { get; init; } = [];

    /// <summary>
    /// Secondary progression data at the report's start (its midpoint until 1.4.0). Null when the
    /// secondary progressions are not requested.
    /// </summary>
    public ProgressionResult? Progressions { get; init; }

    /// <summary>
    /// Solar arc direction data at the report's start (its midpoint until 1.4.0). Null when the solar
    /// arcs are not requested.
    /// </summary>
    public SolarArcResult? SolarArcs { get; init; }

    /// <summary>
    /// The dated, exact solar arc hits in the report period (<see cref="Services.SolarArcHitFinder"/>),
    /// sorted by instant. Empty when the solar arcs are not requested or none falls in the period.
    /// </summary>
    public IReadOnlyList<SolarArcHit> SolarArcHits { get; init; } = [];

    /// <summary>
    /// The dated, exact secondary progression hits in the report period
    /// (<see cref="Services.ProgressedHitFinder"/>), sorted by instant. Empty when the secondary
    /// progressions are not requested or none falls in the period.
    /// </summary>
    public IReadOnlyList<ProgressedHit> ProgressedHits { get; init; } = [];

    /// <summary>
    /// The dated, exact secondary progression hits to the natal Ceres, Pallas, Juno, Vesta and Lilith in the
    /// report period (<see cref="Services.ProgressedHitFinder"/>'s target overload; branch AsteroidsTiming,
    /// 1.6.0, R7), sorted by instant. Empty unless the natal chart carries the five and the secondary
    /// progressions are requested; the five are never progressed.
    /// </summary>
    public IReadOnlyList<ProgressedHit> MinorPointProgressedHits { get; init; } = [];

    /// <summary>
    /// The dated, exact solar arc hits to the natal Ceres, Pallas, Juno, Vesta and Lilith in the report period
    /// (<see cref="Services.SolarArcHitFinder"/>'s target overload, which seeks no midpoint contact; branch
    /// AsteroidsTiming, 1.6.0, R7), sorted by instant. Empty unless the natal chart carries the five and the
    /// solar arcs are requested; the five are never directed.
    /// </summary>
    public IReadOnlyList<SolarArcHit> MinorPointSolarArcHits { get; init; } = [];

    /// <summary>
    /// The chart's primary directions, calculated once for the whole life
    /// (<see cref="Services.PrimaryDirections.PrimaryDirectionService"/>; branch PrimaryExport, 1.5.0): the
    /// settings, the count left out and every hit to the age limit. Null when the primary directions are
    /// not requested.
    /// </summary>
    public PrimaryDirectionResult? PrimaryDirections { get; init; }

    /// <summary>
    /// The primary directions exact in the report period: those of <see cref="PrimaryDirections"/> whose
    /// instant's UT date lies in the period, in time order. Empty when the primary directions are not
    /// requested or none falls in the period.
    /// </summary>
    public IReadOnlyList<PrimaryDirectionHit> PrimaryDirectionHits { get; init; } = [];
}
