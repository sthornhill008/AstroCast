using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates a composite chart from two source charts by computing the
/// short-arc midpoint of each corresponding planet pair (Mercury and Venus nearer
/// the composite Sun) and casting the houses for the midpoint of the two
/// Midheavens at the mean birth latitude, in the shared house system, else Placidus.
/// </summary>
/// <remarks>
/// <para>
/// The composite chart is a relationship chart that represents the combined
/// energy of two individuals. Each composite planet position is the short-arc
/// midpoint of the two charts' corresponding bodies, except that Mercury and
/// Venus take the midpoint nearer the composite Sun, the short-arc one or the
/// one opposite it (ruling S-D2, branch L3, 1.3.4); the export notes each body
/// moved. Until 1.3.4 the composite Venus landed opposite the composite Sun in
/// 8 of 78 saved pairs, Mercury in 2 (Y2-2).
/// </para>
/// <para>
/// The composite MC is the short-arc midpoint of the two Midheavens; its right
/// ascension at the midpoint obliquity (<see cref="RightAscensionOfMidheaven"/>) is
/// the sidereal time <c>swe_houses_armc()</c> casts the Ascendant and cusps for, at
/// the mean of the two birth latitudes, in the house system both charts share,
/// else Placidus (ruling S-D3 with its option (d), Q1; branch L3, 1.3.4). The
/// reference longitude is the mean of the two, a label only. So A &amp; B and
/// B &amp; A are one composite. Until 1.3.4 the houses came from the midpoint of
/// the two ARMCs, neither published method (the MC up to 4.9° from the midpoint
/// of the two MCs, median 1.4°), at a reference place the app set to Person A's
/// birthplace in Person A's house system, so the two orders differed, the
/// Ascendant by up to 26° among the saved pairs (Y2-3, Y2-4).
/// </para>
/// <para>
/// A composite body is a midpoint, not a point in the sky (ruling S-D4, branch L2,
/// 1.3.4): it carries no right ascension and no declination, so the composite has
/// no out-of-bounds flags, declination aspects or clusters. Its aspects still carry
/// the applying flag from the averaged speeds and its Moon condition still comes
/// from the fixed-target scan (phase, sign and via combusta are read from it), but
/// the export prints neither the marks nor the Moon's next aspect and void of
/// course, and says so once in its header. The sect, the Planetary Sect table, the
/// triplicity tier, the midpoints, fixed stars and medical correspondences are kept.
/// </para>
/// <para>
/// When both charts carry Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsExport, 1.6.0; R6), the
/// composite carries their short-arc midpoints too, in a list of their own (<see cref="ChartResult.MinorPoints"/>):
/// no Sun-side rule, the latitude and speeds the means, no right ascension or declination, the house by
/// longitude between the composite cusps, and their aspects at 2° to the composite Sun to Pluto, Ascendant and
/// Midheaven (<see cref="IAspectService.CalculateMinorPointAspects"/>); the composite input's switch says so.
/// </para>
/// </remarks>
public sealed class CompositeService : ICompositeService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IAspectService _aspects;
    private readonly IMidpointService _midpoints;
    private readonly IDignityService _dignity;
    private readonly IMoonConditionService _moonCondition;
    private readonly IChartContextService _chartContext;
    private readonly IFixedStarService _fixedStars;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="CompositeService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for house calculation from ARMC and obliquity.</param>
    /// <param name="aspects">Aspect service for computing aspects on composite positions.</param>
    /// <param name="midpoints">Midpoint service for Ebertin 45° dial calculations.</param>
    /// <param name="dignity">Dignity service for essential dignity lookups.</param>
    /// <param name="moonCondition">Moon condition service for composite Moon analysis.</param>
    /// <param name="chartContext">Chart context service for chart context analysis.</param>
    /// <param name="fixedStars">Fixed star service for conjunction detection.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public CompositeService(
        IEphemerisService ephemeris,
        IAspectService aspects,
        IMidpointService midpoints,
        IDignityService dignity,
        IMoonConditionService moonCondition,
        IChartContextService chartContext,
        IFixedStarService fixedStars,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(aspects);
        ArgumentNullException.ThrowIfNull(midpoints);
        ArgumentNullException.ThrowIfNull(dignity);
        ArgumentNullException.ThrowIfNull(moonCondition);
        ArgumentNullException.ThrowIfNull(chartContext);
        ArgumentNullException.ThrowIfNull(fixedStars);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _aspects = aspects;
        _midpoints = midpoints;
        _dignity = dignity;
        _moonCondition = moonCondition;
        _chartContext = chartContext;
        _fixedStars = fixedStars;
        _logger = logger.ForContext<CompositeService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public CompositeResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chartA);
        ArgumentNullException.ThrowIfNull(chartB);

        _logger.Debug(
            "Calculating composite chart: '{ChartA}' & '{ChartB}'",
            chartA.Input.Name, chartB.Input.Name);

        // Step 1: The reference place: the mean birth latitude, where the houses are cast (S-D3), and the
        // mean birth longitude, a label only.
        double refLat = (chartA.Input.Latitude + chartB.Input.Latitude) / 2.0;
        double refLon = (chartA.Input.Longitude + chartB.Input.Longitude) / 2.0;
        string refLocationDesc = $"Midpoint of {chartA.Input.LocationName} & {chartB.Input.LocationName}";

        // Step 2: Compute composite planetary positions (short-arc midpoints; Mercury and Venus
        // nearer the composite Sun, S-D2)
        cancellationToken.ThrowIfCancellationRequested();
        (IReadOnlyList<PlanetPosition> compositePositions, IReadOnlyList<string> sunSideBodies) =
            ComputeCompositePositions(chartA.Positions, chartB.Positions);

        // Step 3: The composite MC is the short-arc midpoint of the two Midheavens; the Ascendant and
        // cusps are cast for it, through its right ascension at the midpoint obliquity, at the mean birth
        // latitude, in the house system both charts share, else Placidus (S-D3, Q1). Until 1.3.4 the houses
        // came from the midpoint of the two ARMCs, neither published method, in chart A's system (Y2-3, Y2-4).
        double compositeMc = ShortArcMidpoint(chartA.Houses.Midheaven, chartB.Houses.Midheaven);
        double midpointJd = (chartA.JulianDay + chartB.JulianDay) / 2.0;
        double obliquity = _ephemeris.GetObliquity(midpointJd);
        double ramc = RightAscensionOfMidheaven(compositeMc, obliquity);

        cancellationToken.ThrowIfCancellationRequested();
        bool sharedHouseSystem = chartA.Input.HouseSystem == chartB.Input.HouseSystem;
        HouseSystem hsys = sharedHouseSystem ? chartA.Input.HouseSystem : HouseSystem.Placidus;
        HouseData compositeHouses = _ephemeris.CalculateHousesFromArmc(
            ramc, refLat, obliquity, (char)hsys);

        // Step 4: Assign houses to composite positions
        HousePlacement.Assign(compositePositions, compositeHouses);

        // Step 5: Compute aspects on composite positions
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AspectResult> aspects = _aspects.CalculateAllAspects(
            compositePositions, compositeHouses, midpointJd);

        // Step 5b: the five minor points, only when both charts carry them (R6; branch AsteroidsExport, 1.6.0):
        // the short-arc midpoint of each with no Sun-side rule, placed against the composite houses, and their
        // aspects at 2° to the composite Sun to Pluto (Mercury and Venus where step 2 put them) and the angles.
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PlanetPosition> compositeMinorPoints =
            ComputeCompositeMinorPoints(chartA.MinorPoints, chartB.MinorPoints);
        HousePlacement.Assign(compositeMinorPoints, compositeHouses);
        IReadOnlyList<AspectResult> compositeMinorPointAspects = compositeMinorPoints.Count > 0
            ? _aspects.CalculateMinorPointAspects(compositeMinorPoints, compositePositions, compositeHouses)
            : [];

        // Step 6: Build chart points and compute midpoints/activations
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<(string Name, double Longitude)> chartPoints =
            MidpointService.BuildPointList(compositePositions, compositeHouses);
        (IReadOnlyList<Midpoint> midpoints, IReadOnlyList<MidpointActivation> activations) =
            _midpoints.CalculateAll(chartPoints);

        // Step 7: Compute fixed star conjunctions at midpoint JD
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FixedStarConjunction> fixedStarConjunctions =
            _fixedStars.FindConjunctions(compositePositions, compositeHouses, midpointJd);

        // Step 8: Compute dignities, moon condition, chart context
        // Day or night for the triplicity tier: the composite Sun against the composite houses.
        PlanetPosition? sunForSect = compositePositions.FirstOrDefault(
            p => string.Equals(p.Name, "Sun", StringComparison.OrdinalIgnoreCase));
        bool isDayChartForDignity = sunForSect is not null && ChartSect.IsDayChart(sunForSect.Longitude, compositeHouses);
        IReadOnlyList<DignityInfo> dignities = _dignity.GetDignitiesExtended(compositePositions, isDayChartForDignity);
        // Composite midpoints have no motion to follow, so the Moon's targets are held fixed.
        MoonConditionInfo? moonCondition = _moonCondition.GetMoonConditionWithFixedTargets(
            compositePositions, aspects);
        ChartContextInfo? chartContext = _chartContext.Analyze(
            compositePositions, compositeHouses, dignities, "Composite");

        // Step 9: Build composite ChartInput and ChartResult
        DateTime midpointUtc = ComputeMidpointUtc(chartA.UtcDateTime, chartB.UtcDateTime);
        string engineVersion = BuildVersionString();

        ChartInput compositeInput = new()
        {
            Name = $"Composite: {chartA.Input.Name} & {chartB.Input.Name}",
            LocalDateTime = midpointUtc,
            TimeZone = "UTC",
            Latitude = refLat,
            Longitude = refLon,
            LocationName = refLocationDesc,
            EventType = "Composite",
            HouseSystem = hsys,
            IncludeMinorPoints = compositeMinorPoints.Count > 0,
        };

        ChartResult compositeChart = new()
        {
            Input = compositeInput,
            JulianDay = midpointJd,
            Obliquity = obliquity,
            UtcDateTime = midpointUtc,
            Positions = compositePositions,
            Houses = compositeHouses,
            Aspects = aspects,
            MinorPoints = compositeMinorPoints,
            MinorPointAspects = compositeMinorPointAspects,
            Midpoints = midpoints,
            MidpointActivations = activations,
            Dignities = dignities,
            MoonCondition = moonCondition,
            ChartContext = chartContext,
            FixedStarConjunctions = fixedStarConjunctions,
            ResolvedUtcOffsetHours = 0.0,
            ResolvedOffsetLabel = "UTC+0",
            HouseSystemName = sharedHouseSystem ? chartA.HouseSystemName : "Placidus",
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };

        _logger.Information(
            "Composite chart complete: {PositionCount} positions, {AspectCount} aspects, " +
            "{MidpointCount} midpoint activations, {StarCount} fixed star conjunctions, " +
            "{MinorPointCount} minor points with {MinorPointAspectCount} aspects; " +
            "MC {Mc:F4}° at latitude {Lat:F4} in {HouseSystem}; nearer the Sun: {SunSide}",
            compositePositions.Count, aspects.Count, activations.Count,
            fixedStarConjunctions.Count, compositeMinorPoints.Count, compositeMinorPointAspects.Count,
            compositeMc, refLat, hsys,
            sunSideBodies.Count == 0 ? "none" : string.Join(", ", sunSideBodies));

        return new CompositeResult
        {
            ChartA = chartA,
            ChartB = chartB,
            CompositeChart = compositeChart,
            ReferenceLatitude = refLat,
            ReferenceLongitude = refLon,
            ReferenceLocationDescription = refLocationDesc,
            SunSideBodies = sunSideBodies,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// The right ascension of a Midheaven at an obliquity, RAMC = atan2(sin MC · cos ε, cos MC) in
    /// [0°, 360°): the sidereal time the houses are cast for, so that the ephemeris gives back that
    /// Midheaven. The twin of <c>ProgressionService.AscendantForMidheaven</c>'s formula, which casts the
    /// Ascendant for a progressed Midheaven the same way (S-D3, branch L3, 1.3.4).
    /// </summary>
    /// <param name="midheaven">The Midheaven's ecliptic longitude, in degrees.</param>
    /// <param name="obliquity">The true obliquity of the ecliptic, in degrees.</param>
    /// <returns>The Midheaven's right ascension, in degrees, 0 to 360.</returns>
    private static double RightAscensionOfMidheaven(double midheaven, double obliquity)
    {
        double mcRadians = midheaven * Math.PI / 180.0;
        double obliquityRadians = obliquity * Math.PI / 180.0;
        double ramc = Math.Atan2(Math.Sin(mcRadians) * Math.Cos(obliquityRadians), Math.Cos(mcRadians)) * 180.0 / Math.PI;
        return ((ramc % 360.0) + 360.0) % 360.0;
    }

    /// <summary>
    /// Compute the short-arc midpoint of two ecliptic longitudes.
    /// Always returns the midpoint of the shorter of the two possible arcs.
    /// </summary>
    /// <param name="lon1">First longitude (0–360°).</param>
    /// <param name="lon2">Second longitude (0–360°).</param>
    /// <returns>Short-arc midpoint longitude (0–360°).</returns>
    internal static double ShortArcMidpoint(double lon1, double lon2)
    {
        double diff = lon2 - lon1;

        // Normalise difference to [-180, 180)
        while (diff < -180.0) diff += 360.0;
        while (diff >= 180.0) diff -= 360.0;

        double mid = lon1 + diff / 2.0;

        // Normalise result to [0, 360)
        while (mid < 0.0) mid += 360.0;
        while (mid >= 360.0) mid -= 360.0;

        return mid;
    }

    /// <summary>
    /// Compute composite planetary positions by taking the short-arc midpoint
    /// of each corresponding planet pair from the two source charts; then Mercury
    /// and Venus each take the midpoint nearer the composite Sun — the short-arc
    /// one, or the far-side one (the short-arc one + 180°) when that is nearer, a
    /// tie keeping the short-arc one (ruling S-D2, branch L3, 1.3.4). Latitude,
    /// distance and the speeds stay the means. A composite body carries no right
    /// ascension and no declination: it is a midpoint, not a point in the sky
    /// (ruling S-D4, branch L2, 1.3.4). Until 1.3.4 every body took its own short-arc
    /// midpoint, so when the two Suns were far apart and the two Venuses fell on the
    /// other side the composite Venus landed opposite the composite Sun (8 of 78
    /// saved pairs; Mercury 2; Y2-2), and the equatorial coordinates were the means
    /// of the two charts' values (Y2-5).
    /// </summary>
    /// <param name="positionsA">Positions from chart A (12 bodies).</param>
    /// <param name="positionsB">Positions from chart B (12 bodies).</param>
    /// <returns>
    /// The composite positions (12 bodies) and the names, in position order, of the
    /// bodies that took the midpoint nearer the composite Sun.
    /// </returns>
    private (IReadOnlyList<PlanetPosition> Positions, IReadOnlyList<string> SunSideBodies) ComputeCompositePositions(
        IReadOnlyList<PlanetPosition> positionsA,
        IReadOnlyList<PlanetPosition> positionsB)
    {
        List<PlanetPosition> compositePositions = new(positionsA.Count);

        foreach (PlanetPosition posA in positionsA)
        {
            PlanetPosition? posB = FindMatchingPosition(positionsB, posA.Body);
            if (posB is null)
            {
                _logger.Warning(
                    "No matching position for {Body} in chart B — using chart A position",
                    posA.Name);
                compositePositions.Add(posA);
                continue;
            }

            double compositeLon = ShortArcMidpoint(posA.Longitude, posB.Longitude);
            double compositeLat = (posA.Latitude + posB.Latitude) / 2.0;
            double compositeDist = (posA.Distance + posB.Distance) / 2.0;
            double compositeSpeedLon = (posA.SpeedInLongitude + posB.SpeedInLongitude) / 2.0;
            double compositeSpeedLat = (posA.SpeedInLatitude + posB.SpeedInLatitude) / 2.0;
            double compositeSpeedDist = (posA.SpeedInDistance + posB.SpeedInDistance) / 2.0;

            // No right ascension and no declination: a midpoint is not a point in the sky (S-D4).
            compositePositions.Add(new PlanetPosition
            {
                Body = posA.Body,
                Name = posA.Name,
                Longitude = compositeLon,
                Latitude = compositeLat,
                Distance = compositeDist,
                SpeedInLongitude = compositeSpeedLon,
                SpeedInLatitude = compositeSpeedLat,
                SpeedInDistance = compositeSpeedDist,
            });

            _logger.Debug(
                "Composite {Body}: A={LonA:F4}° + B={LonB:F4}° → {CompLon:F4}°",
                posA.Name, posA.Longitude, posB.Longitude, compositeLon);
        }

        // Mercury and Venus take the midpoint nearer the composite Sun (S-D2).
        List<string> sunSideBodies = [];
        PlanetPosition? compositeSun = compositePositions.FirstOrDefault(p => p.Body == CelestialBodyId.Sun);
        if (compositeSun is not null)
        {
            for (int i = 0; i < compositePositions.Count; i++)
            {
                PlanetPosition inner = compositePositions[i];
                if (inner.Body is not (CelestialBodyId.Mercury or CelestialBodyId.Venus))
                    continue;

                double farSide = (inner.Longitude + 180.0) % 360.0;
                double shortArcSeparation = AspectService.AngularSeparation(inner.Longitude, compositeSun.Longitude);
                double farSideSeparation = AspectService.AngularSeparation(farSide, compositeSun.Longitude);
                if (farSideSeparation >= shortArcSeparation)
                    continue;

                compositePositions[i] = new PlanetPosition
                {
                    Body = inner.Body,
                    Name = inner.Name,
                    Longitude = farSide,
                    Latitude = inner.Latitude,
                    Distance = inner.Distance,
                    SpeedInLongitude = inner.SpeedInLongitude,
                    SpeedInLatitude = inner.SpeedInLatitude,
                    SpeedInDistance = inner.SpeedInDistance,
                };
                sunSideBodies.Add(inner.Name);

                _logger.Debug(
                    "Composite {Body} takes the midpoint nearer the composite Sun: {FarSide:F4}° ({FarSep:F2}° from it) for the short-arc {ShortArc:F4}° ({ShortSep:F2}°)",
                    inner.Name, farSide, farSideSeparation, inner.Longitude, shortArcSeparation);
            }
        }

        return (compositePositions.AsReadOnly(), sunSideBodies.AsReadOnly());
    }

    /// <summary>
    /// Compute the composite minor points (R6; branch AsteroidsExport, 1.6.0): empty unless both charts carry
    /// the five; else, per point in chart A's order (R1's: Ceres, Pallas, Juno, Vesta, Lilith), matched by body
    /// in chart B, the short-arc midpoint of the two longitudes (<see cref="ShortArcMidpoint"/>; no Sun-side
    /// rule, which is Mercury's and Venus's, S-D2), the latitude, distance and the three speeds the means, and
    /// no right ascension or declination: a midpoint is not a point in the sky (S-D4). The caller places them
    /// against the composite houses and finds their aspects.
    /// </summary>
    /// <param name="pointsA">Chart A's minor points, the five or none.</param>
    /// <param name="pointsB">Chart B's minor points, the five or none.</param>
    /// <returns>The composite minor points, or an empty list.</returns>
    private IReadOnlyList<PlanetPosition> ComputeCompositeMinorPoints(
        IReadOnlyList<PlanetPosition> pointsA,
        IReadOnlyList<PlanetPosition> pointsB)
    {
        if (pointsA.Count == 0 || pointsB.Count == 0)
            return [];

        List<PlanetPosition> composite = new(pointsA.Count);
        foreach (PlanetPosition posA in pointsA)
        {
            PlanetPosition? posB = FindMatchingPosition(pointsB, posA.Body);
            if (posB is null)
            {
                _logger.Warning(
                    "No matching minor point for {Body} in chart B — left out of the composite", posA.Name);
                continue;
            }

            composite.Add(new PlanetPosition
            {
                Body = posA.Body,
                Name = posA.Name,
                Longitude = ShortArcMidpoint(posA.Longitude, posB.Longitude),
                Latitude = (posA.Latitude + posB.Latitude) / 2.0,
                Distance = (posA.Distance + posB.Distance) / 2.0,
                SpeedInLongitude = (posA.SpeedInLongitude + posB.SpeedInLongitude) / 2.0,
                SpeedInLatitude = (posA.SpeedInLatitude + posB.SpeedInLatitude) / 2.0,
                SpeedInDistance = (posA.SpeedInDistance + posB.SpeedInDistance) / 2.0,
            });
        }

        return composite.AsReadOnly();
    }

    /// <summary>
    /// Find a position matching the given body in a list of positions.
    /// </summary>
    /// <param name="positions">List of positions to search.</param>
    /// <param name="body">The celestial body to find.</param>
    /// <returns>The matching position, or null if not found.</returns>
    private static PlanetPosition? FindMatchingPosition(
        IReadOnlyList<PlanetPosition> positions, CelestialBodyId body)
    {
        foreach (PlanetPosition p in positions)
        {
            if (p.Body == body) return p;
        }

        return null;
    }

    /// <summary>
    /// Compute the midpoint of two UTC DateTimes, used as the composite chart timestamp.
    /// </summary>
    /// <param name="utcA">UTC DateTime from chart A.</param>
    /// <param name="utcB">UTC DateTime from chart B.</param>
    /// <returns>The DateTime halfway between the two.</returns>
    private static DateTime ComputeMidpointUtc(DateTime utcA, DateTime utcB)
    {
        long ticksA = utcA.Ticks;
        long ticksB = utcB.Ticks;
        long midTicks = ticksA + (ticksB - ticksA) / 2;
        return new DateTime(midTicks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    /// <returns>Version string (e.g., "AstroCast 1.0.39 (SwissEph 2.10.03)").</returns>
    private string BuildVersionString()
    {
        string asmVersion = typeof(CompositeService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        // Trim the git commit hash suffix (e.g., "1.0.0+abc123" -> "1.0.0")
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;
        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }
}
