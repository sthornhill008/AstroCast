using System.Globalization;
using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Orchestrates a complete chart calculation by combining the ephemeris,
/// aspect, and midpoint services into a single <see cref="Calculate"/> call.
/// This is the primary entry point for all chart calculations.
/// </summary>
/// <remarks>
/// The engine converts local time + timezone to UTC, computes the Julian Day,
/// then calls each service in sequence: positions, houses, aspects, midpoints.
/// The result is a <see cref="ChartResult"/> containing all computed data.
/// </remarks>
public sealed class ChartEngine : IChartEngine
{
    private readonly IEphemerisService _ephemeris;
    private readonly IAspectService _aspects;
    private readonly IMidpointService _midpoints;
    private readonly IDignityService _dignity;
    private readonly IMoonConditionService _moonCondition;
    private readonly IChartContextService _chartContext;
    private readonly IFixedStarService _fixedStars;
    private readonly IDeclinationAspectService _declinationAspects;
    private readonly IPlanetConditionService _planetCondition;
    private readonly IMoonMansionService _moonMansion;
    private readonly PlanetaryHourCalculator _planetaryHour;
    private readonly IArabicPartService _arabicParts;
    private readonly IAntisciaService _antiscia;
    private readonly IStationService _stations;
    private readonly IParanService _parans;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="ChartEngine"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for planetary positions and houses.</param>
    /// <param name="aspects">Aspect service for detecting aspects between chart points.</param>
    /// <param name="midpoints">Midpoint service for Ebertin 45° dial calculations.</param>
    /// <param name="dignity">Dignity service for essential dignity lookups.</param>
    /// <param name="moonCondition">Moon condition service for phase, VoC, and Via Combusta analysis.</param>
    /// <param name="chartContext">Chart context service for ruler, sect, angular planets, and mutual receptions.</param>
    /// <param name="fixedStars">Fixed star service for conjunction detection.</param>
    /// <param name="declinationAspects">Declination aspect service for parallels and contraparallels.</param>
    /// <param name="planetCondition">Planet condition service for accidental conditions (combustion, besiegement, influence).</param>
    /// <param name="moonMansion">Moon mansion service for lunar mansion calculation.</param>
    /// <param name="planetaryHour">Planetary hour calculator for hour ruler and radicality.</param>
    /// <param name="arabicParts">Arabic Part service for lot calculations.</param>
    /// <param name="antiscia">Antiscia service for mirror-point and hidden connection detection.</param>
    /// <param name="stations">Station service for detecting stationing planets.</param>
    /// <param name="parans">Paran (paranatellonta) service for angular crossing detection.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules a chart's wall clock is converted with. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public ChartEngine(
        IEphemerisService ephemeris,
        IAspectService aspects,
        IMidpointService midpoints,
        IDignityService dignity,
        IMoonConditionService moonCondition,
        IChartContextService chartContext,
        IFixedStarService fixedStars,
        IDeclinationAspectService declinationAspects,
        IPlanetConditionService planetCondition,
        IMoonMansionService moonMansion,
        PlanetaryHourCalculator planetaryHour,
        IArabicPartService arabicParts,
        IAntisciaService antiscia,
        IStationService stations,
        IParanService parans,
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
        ArgumentNullException.ThrowIfNull(declinationAspects);
        ArgumentNullException.ThrowIfNull(planetCondition);
        ArgumentNullException.ThrowIfNull(moonMansion);
        ArgumentNullException.ThrowIfNull(planetaryHour);
        ArgumentNullException.ThrowIfNull(arabicParts);
        ArgumentNullException.ThrowIfNull(antiscia);
        ArgumentNullException.ThrowIfNull(stations);
        ArgumentNullException.ThrowIfNull(parans);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _aspects = aspects;
        _midpoints = midpoints;
        _dignity = dignity;
        _moonCondition = moonCondition;
        _chartContext = chartContext;
        _fixedStars = fixedStars;
        _declinationAspects = declinationAspects;
        _planetCondition = planetCondition;
        _moonMansion = moonMansion;
        _planetaryHour = planetaryHour;
        _arabicParts = arabicParts;
        _antiscia = antiscia;
        _stations = stations;
        _parans = parans;
        _logger = logger.ForContext<ChartEngine>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside -90 to 90, longitude is outside -180 to 180,
    /// or the instant is outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT); when the
    /// local date is one of 1582-10-05 to 1582-10-14, which never occurred; or when the
    /// instant falls on 29 February UT of 1300, 1400 or 1500 on the Julian calendar, a date
    /// <see cref="DateTime"/> cannot hold.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question
    /// category and <see cref="HoraryPerfectionAnalyzer.Analyze"/> reports an
    /// internal invariant breach. A valid chart never produces it; the exception
    /// reports a defect, not a condition to handle.
    /// </exception>
    public ChartResult Calculate(ChartInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);

        (DateTime utcDateTime, double julianDay, double resolvedUtcOffset, ZoneInstant? zoneInstant, string? timeNote) = ConvertInputToJulianDay(input);
        ValidateInstant(input, julianDay, utcDateTime);

        return Cast(input, utcDateTime, julianDay, resolvedUtcOffset, zoneInstant, timeNote, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="input"/> is not at an explicit offset.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside -90 to 90, longitude is outside -180 to 180, or the
    /// instant is outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT); a Julian
    /// Day no instant can hold, or not a number, is refused the same way, naming the Julian Day.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question category and
    /// <see cref="HoraryPerfectionAnalyzer.Analyze"/> reports an internal invariant breach.
    /// </exception>
    public ChartResult CalculateAtInstant(ChartInput input, double julianDayUt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);

        if (!(input.UseExplicitOffset && input.UtcOffsetHours.HasValue))
        {
            throw new ArgumentException(
                "A chart cast at a Julian Day is labelled at an explicit offset, not read on a zone's clock.",
                nameof(input));
        }

        // A Julian Day no instant can hold, or not a number, is outside the files too.
        if (!(julianDayUt >= EphemerisSpan.FirstJulianDay - 1.0 && julianDayUt <= EphemerisSpan.LastJulianDay + 1.0))
            throw OutsideTheFiles(input, string.Create(CultureInfo.InvariantCulture, $"Julian Day {julianDayUt:R}"));

        // The instant to the whole second, and its Julian Day, by arithmetic (D4): the reading is
        // only the label the chart prints. Read back as a reading, a label before 1582-10-15 was a
        // Julian date, and the chart landed 7 to 10 days after its event.
        DateTime utcDateTime = JulianDayUtility.JulianDayToInstant(julianDayUt);
        double julianDay = JulianDayUtility.InstantToJulianDay(utcDateTime);
        ValidateInstant(input, julianDay, utcDateTime);

        long offsetSeconds = (long)Math.Round(input.UtcOffsetHours.Value * 3600.0, MidpointRounding.AwayFromZero);
        return Cast(input, utcDateTime, julianDay, offsetSeconds / 3600.0, null, null, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the target's latitude is outside -90 to 90 or its longitude outside -180 to 180, or
    /// when the natal input's reading is refused as <see cref="Calculate"/> refuses it.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the natal chart is a horary chart with a question category and
    /// <see cref="HoraryPerfectionAnalyzer.Analyze"/> reports an internal invariant breach at the place.
    /// </exception>
    public ChartResult CalculateRelocated(ChartResult natalChart, RelocationInput target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(target);

        // The natal input with the place changed, every other member copied (S-D6, branch K2).
        ChartInput natal = natalChart.Input;
        ChartInput relocatedInput = new()
        {
            Name = $"{natal.Name} \u2014 Relocated to {target.LocationName}",
            LocalDateTime = natal.LocalDateTime,
            TimeZone = natal.TimeZone,
            UseExplicitOffset = natal.UseExplicitOffset,
            UtcOffsetHours = natal.UtcOffsetHours,
            Latitude = target.Latitude,
            Longitude = target.Longitude,
            LocationName = target.LocationName,
            EventType = natal.EventType,
            EventDescription = natal.EventDescription,
            Gender = natal.Gender,
            SourceNotes = natal.SourceNotes,
            Notes = natal.Notes,
            Keywords = natal.Keywords,
            HouseSystem = natal.HouseSystem,
            IncludeMinorPoints = natal.IncludeMinorPoints,
            HoraryQuestion = natal.HoraryQuestion,
            HoraryCategory = natal.HoraryCategory,
        };
        ValidateInput(relocatedInput);

        // Calculate's own pipeline: the reading resolves to the natal instant, the same Julian Day bit for
        // bit, with the natal offset label, mapping and Time Note.
        (DateTime utcDateTime, double julianDay, double resolvedUtcOffset, ZoneInstant? zoneInstant, string? timeNote) = ConvertInputToJulianDay(relocatedInput);
        ValidateInstant(relocatedInput, julianDay, utcDateTime);

        return Cast(relocatedInput, utcDateTime, julianDay, resolvedUtcOffset, zoneInstant, timeNote, cancellationToken, keepFrom: natalChart);
    }

    /// <summary>
    /// Casts the complete chart at an instant: positions, houses, aspects, midpoints, fixed stars,
    /// declination aspects, and everything <see cref="BuildResult"/> assembles from them. The one
    /// pipeline <see cref="Calculate"/>, <see cref="CalculateAtInstant"/> and <see cref="CalculateRelocated"/>
    /// share.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="utcDateTime">The instant the chart is cast at.</param>
    /// <param name="julianDay">The instant's Julian Day (UT).</param>
    /// <param name="resolvedUtcOffset">The offset applied, in hours.</param>
    /// <param name="zoneInstant">How the reading mapped through its zone, or <see langword="null"/> at an explicit offset.</param>
    /// <param name="timeNote">The Time Note, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <param name="keepFrom">
    /// For a relocation, the natal chart whose sect, dignities and planetary hour the chart keeps (S-D6);
    /// <see langword="null"/> for any other chart.
    /// </param>
    /// <returns>The complete chart.</returns>
    private ChartResult Cast(
        ChartInput input,
        DateTime utcDateTime,
        double julianDay,
        double resolvedUtcOffset,
        ZoneInstant? zoneInstant,
        string? timeNote,
        CancellationToken cancellationToken,
        ChartResult? keepFrom = null)
    {
        // 1. Calculate all planetary positions (12 bodies)
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(julianDay);

        // 2. Calculate houses and angles
        char hsys = (char)input.HouseSystem;
        HouseData houses = _ephemeris.CalculateHouses(julianDay, input.Latitude, input.Longitude, hsys);

        // 3. Assign houses to planets
        double obliquity = _ephemeris.GetObliquity(julianDay);
        HousePlacement.Assign(positions, houses);
        SetAltitudes(positions, houses, input.Latitude);

        // 3b. The minor points, Ceres, Pallas, Juno, Vesta and Lilith, behind the switch and never on a
        //     horary chart (branch Asteroids, 1.6.0): a list of their own with their houses by longitude,
        //     then their aspects at 2° to the Sun to Pluto and the angles. Nothing after this step sees
        //     them: the midpoints, stars, declination aspects and everything BuildResult derives read the
        //     twelve positions alone.
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PlanetPosition> minorPoints = CarriesMinorPoints(input)
            ? CalculateMinorPoints(julianDay, houses)
            : [];
        IReadOnlyList<AspectResult> minorPointAspects = minorPoints.Count > 0
            ? _aspects.CalculateMinorPointAspects(minorPoints, positions, houses)
            : [];

        // 4. Calculate all aspects (planet-to-planet + planet-to-angle)
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AspectResult> aspects = _aspects.CalculateAllAspects(
            positions, houses, julianDay);

        // 5. Build point list (12 planets + ASC + MC = 14 points)
        IReadOnlyList<(string Name, double Longitude)> chartPoints =
            MidpointService.BuildPointList(positions, houses);

        // 6. Calculate midpoints and activations
        cancellationToken.ThrowIfCancellationRequested();
        (IReadOnlyList<Midpoint> midpoints, IReadOnlyList<MidpointActivation> activations) =
            _midpoints.CalculateAll(chartPoints);

        // 7. Calculate fixed star conjunctions
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FixedStarConjunction> fixedStarConjunctions =
            _fixedStars.FindConjunctions(positions, houses, julianDay);

        // 8. Calculate declination aspects (parallels and contraparallels)
        IReadOnlyList<DeclinationAspect> declinationAspects =
            _declinationAspects.Calculate(positions);

        _logger.Debug(
            "Calculated chart for '{Name}' at {JD:F6} JD (houses: {HouseSystem}; minor points: {MinorPoints})",
            input.Name, julianDay, input.HouseSystem, minorPoints.Count);

        return BuildResult(input, utcDateTime, julianDay, obliquity, positions, houses, aspects, midpoints, activations, fixedStarConjunctions, declinationAspects, minorPoints, minorPointAspects, resolvedUtcOffset, zoneInstant, timeNote, keepFrom);
    }

    /// <summary>The five minor points, in the order every list and export holds them (R1, branch Asteroids, 1.6.0).</summary>
    private static readonly CelestialBodyId[] MinorPointBodies =
    [
        CelestialBodyId.Ceres,
        CelestialBodyId.Pallas,
        CelestialBodyId.Juno,
        CelestialBodyId.Vesta,
        CelestialBodyId.MeanApogee,
    ];

    /// <summary>
    /// Whether a chart carries the minor points: <see cref="ChartInput.IncludeMinorPoints"/> is on and the chart
    /// is not horary, its <see cref="ChartInput.EventType"/> compared with "Horary" ignoring case, as the chart
    /// export recognises a horary chart (scoping decision 2: the engine computes nothing for one, so every
    /// export agrees).
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <returns><see langword="true"/> when the chart carries the five.</returns>
    private static bool CarriesMinorPoints(ChartInput input) =>
        input.IncludeMinorPoints
        && !string.Equals(input.EventType, "Horary", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The five minor points at an instant, each by <see cref="IEphemerisService.CalculatePosition"/>, the
    /// ecliptic and equatorial places as the twelve bodies take them, with their houses by longitude between the
    /// cusps (<see cref="HousePlacement.Assign"/>). Their altitude stays null: hayz is not theirs.
    /// </summary>
    /// <param name="julianDay">The chart's Julian Day (UT).</param>
    /// <param name="houses">The chart's houses.</param>
    /// <returns>Ceres, Pallas, Juno, Vesta and Lilith, in that order.</returns>
    private IReadOnlyList<PlanetPosition> CalculateMinorPoints(double julianDay, HouseData houses)
    {
        List<PlanetPosition> points = new(MinorPointBodies.Length);
        foreach (CelestialBodyId body in MinorPointBodies)
        {
            points.Add(_ephemeris.CalculatePosition(julianDay, body));
        }

        HousePlacement.Assign(points, houses);
        return points.AsReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside -90 to 90, longitude is outside -180 to 180,
    /// or the instant is outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT); when the
    /// local date is one of 1582-10-05 to 1582-10-14, which never occurred; or when the
    /// instant falls on 29 February UT of 1300, 1400 or 1500 on the Julian calendar, a date
    /// <see cref="DateTime"/> cannot hold.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question
    /// category and <see cref="HoraryPerfectionAnalyzer.Analyze"/> reports an
    /// internal invariant breach. Electional candidates are event charts and never
    /// reach the analysis; a valid horary chart never produces it either.
    /// </exception>
    public ChartResult CalculateForElectional(ChartInput input, CancellationToken cancellationToken = default) =>
        CalculateForElectional(input, (DateTime?)null, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside -90 to 90, longitude is outside -180 to 180,
    /// or the instant is outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT).
    /// </exception>
    public ChartResult CalculateForElectional(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default) =>
        CalculateForElectional(input, (DateTime?)utcDateTime, cancellationToken);

    /// <summary>
    /// Calculates an electional chart at the instant its wall clock denotes, or at a given
    /// instant that wall clock reads.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="instant">The instant to cast at, or <see langword="null"/> for the wall clock's.</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// The chart, with its midpoints and their activations, computed as the full chart computes
    /// them (until 1.3.3 they were skipped, E2-3).
    /// </returns>
    private ChartResult CalculateForElectional(ChartInput input, DateTime? instant, CancellationToken cancellationToken)
    {
        (DateTime utcDateTime, double julianDay, double resolvedUtcOffset, ZoneInstant? zoneInstant, string? timeNote) =
            ResolveElectionalInstant(input, instant, cancellationToken);

        // 1–3. Planetary positions (12 bodies), houses and angles, and the planets' houses
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, double obliquity) = CalculatePositionsAndHouses(input, julianDay);

        // 4. Calculate aspects
        IReadOnlyList<AspectResult> aspects = _aspects.CalculateAllAspects(
            positions, houses, julianDay);

        // 4b. Midpoints and activations, as the full chart computes them (12 planets + ASC + MC
        //     = 14 points): the candidate's Midpoint Activations table reads them (E2-3). The
        //     search scores from CalculateElectionalScoringInputs, so no score reads them.
        IReadOnlyList<(string Name, double Longitude)> chartPoints =
            MidpointService.BuildPointList(positions, houses);
        cancellationToken.ThrowIfCancellationRequested();
        (IReadOnlyList<Midpoint> midpoints, IReadOnlyList<MidpointActivation> activations) =
            _midpoints.CalculateAll(chartPoints);

        // 5. Calculate fixed star conjunctions
        IReadOnlyList<FixedStarConjunction> fixedStarConjunctions =
            _fixedStars.FindConjunctions(positions, houses, julianDay);

        // 6. Calculate declination aspects
        IReadOnlyList<DeclinationAspect> declinationAspects =
            _declinationAspects.Calculate(positions);

        _logger.Debug(
            "Calculated electional chart for '{Name}' at {JD:F6} JD",
            input.Name, julianDay);

        // An electional chart never carries the minor points, whatever the switch (R8, branch
        // Asteroids, 1.6.0): both lists empty.
        return BuildResult(
            input, utcDateTime, julianDay, obliquity, positions, houses, aspects,
            midpoints, activations, fixedStarConjunctions, declinationAspects, [], [], resolvedUtcOffset, zoneInstant, timeNote);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside -90 to 90, longitude is outside -180 to 180,
    /// or the instant is outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT).
    /// </exception>
    public ChartResult CalculateElectionalScoringInputs(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default)
    {
        (DateTime castUtcDateTime, double julianDay, double resolvedUtcOffset, ZoneInstant? zoneInstant, string? timeNote) =
            ResolveElectionalInstant(input, utcDateTime, cancellationToken);

        // The calls the full chart makes for these members, in its order: the positions,
        // houses and their assignment, then the dignities and the context BuildResult derives.
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, double obliquity) = CalculatePositionsAndHouses(input, julianDay);
        IReadOnlyList<DignityInfo> dignities = CalculateDignities(positions, houses);
        ChartContextInfo? chartContext = _chartContext.Analyze(positions, houses, dignities, input.EventType);

        return new ChartResult
        {
            Input = input,
            JulianDay = julianDay,
            Obliquity = obliquity,
            UtcDateTime = castUtcDateTime,
            Positions = positions,
            Houses = houses,
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = dignities,
            MoonCondition = null,
            ChartContext = chartContext,
            FixedStarConjunctions = [],
            DeclinationAspects = [],
            PlanetaryHour = null,
            ResolvedUtcOffsetHours = resolvedUtcOffset,
            ResolvedOffsetLabel = FormatOffsetLabel(resolvedUtcOffset, zoneInstant),
            LocalTimeMapping = ResolveLocalTimeMapping(zoneInstant),
            ResolvedLocalDateTime = ResolveLocalDateTime(input, zoneInstant),
            EarlierUtcDateTime = zoneInstant?.EarlierUtcDateTime,
            TimeNote = timeNote,
            TimeZoneRules = _timeZoneRules,
            HouseSystemName = input.HouseSystem.DisplayName,
            EngineVersion = BuildEngineVersion(),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Validates an electional chart's input and finds the instant it is cast at, as both the
    /// full electional chart and its scoring inputs find it.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="instant">The instant to cast at, or <see langword="null"/> for the wall clock's.</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// Tuple of UTC DateTime, Julian Day number, the offset applied, how the reading mapped
    /// through its zone, and the Time Note.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instant"/> is given and <paramref name="input"/> is cast at an
    /// explicit offset, or its wall clock is not the reading of that instant on its zone's clock.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude, longitude or the instant is outside the supported ranges.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    private (DateTime UtcDateTime, double JulianDay, double ResolvedUtcOffsetHours, ZoneInstant? Zone, string? TimeNote) ResolveElectionalInstant(
        ChartInput input, DateTime? instant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        cancellationToken.ThrowIfCancellationRequested();

        (DateTime UtcDateTime, double JulianDay, double ResolvedUtcOffsetHours, ZoneInstant? Zone, string? TimeNote) resolved = instant is DateTime at
            ? ConvertInstantToJulianDay(input, at)
            : ConvertInputToJulianDay(input);
        ValidateInstant(input, resolved.JulianDay, resolved.UtcDateTime);

        return resolved;
    }

    /// <summary>
    /// Calculates the planetary positions (12 bodies), the houses and angles, and each planet's
    /// house, as both the full electional chart and its scoring inputs calculate them.
    /// </summary>
    /// <param name="input">Chart input parameters: the place and the house system.</param>
    /// <param name="julianDay">The chart's Julian Day (UT).</param>
    /// <returns>
    /// The positions, with their houses assigned, the houses, and the true obliquity of the ecliptic
    /// at <paramref name="julianDay"/> that assigned them.
    /// </returns>
    private (IReadOnlyList<PlanetPosition> Positions, HouseData Houses, double Obliquity) CalculatePositionsAndHouses(ChartInput input, double julianDay)
    {
        // 1. Calculate all planetary positions (12 bodies)
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(julianDay);

        // 2. Calculate houses and angles
        char hsys = (char)input.HouseSystem;
        HouseData houses = _ephemeris.CalculateHouses(julianDay, input.Latitude, input.Longitude, hsys);

        // 3. Assign houses to planets
        double obliquity = _ephemeris.GetObliquity(julianDay);
        HousePlacement.Assign(positions, houses);
        SetAltitudes(positions, houses, input.Latitude);

        return (positions, houses, obliquity);
    }

    /// <summary>
    /// Sets each position's geometric altitude, from its right ascension and declination, the chart's ARMC and the
    /// place's latitude (<see cref="ChartSect.AltitudeOf"/>). A position with no right ascension or declination keeps
    /// a null altitude.
    /// </summary>
    /// <param name="positions">The chart's positions.</param>
    /// <param name="houses">The chart's houses, for the ARMC.</param>
    /// <param name="latitude">The place's geographic latitude.</param>
    private static void SetAltitudes(IReadOnlyList<PlanetPosition> positions, HouseData houses, double latitude)
    {
        foreach (PlanetPosition position in positions)
        {
            if (position.RightAscension is double rightAscension && position.Declination is double declination)
            {
                position.Altitude = ChartSect.AltitudeOf(rightAscension, declination, houses.Armc, latitude);
            }
        }
    }

    /// <summary>The first year the ephemeris files cover, as the refusal names it.</summary>
    private const int FirstYearInFiles = 1200;

    /// <summary>The last year the ephemeris files cover, as the refusal names it.</summary>
    private const int LastYearInFiles = 2399;

    /// <summary>
    /// Validates geographic coordinates and date range of a <see cref="ChartInput"/>.
    /// </summary>
    /// <remarks>
    /// A local date more than a year outside the files' span is refused here, before the
    /// conversion; the instant itself is held to the span by <see cref="ValidateInstant"/>
    /// once it is known, since a zone's offset can carry a reading across a year's end.
    /// </remarks>
    /// <param name="input">Chart input to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude, longitude, or date year is outside supported ranges.
    /// </exception>
    private static void ValidateInput(ChartInput input)
    {
        if (input.Latitude < -90.0 || input.Latitude > 90.0)
            throw new ArgumentOutOfRangeException(nameof(input),
                $"Latitude {input.Latitude} is outside the valid range of -90 to 90 degrees.");

        if (input.Longitude < -180.0 || input.Longitude > 180.0)
            throw new ArgumentOutOfRangeException(nameof(input),
                $"Longitude {input.Longitude} is outside the valid range of -180 to 180 degrees.");

        if (input.LocalDateTime.Year < FirstYearInFiles - 1 || input.LocalDateTime.Year > LastYearInFiles + 1)
            throw OutsideTheFiles(input, string.Create(CultureInfo.InvariantCulture, $"{input.LocalDateTime:yyyy-MM-dd}"));
    }

    /// <summary>
    /// Refuses an instant outside the shipped ephemeris files' span (F0-6): from
    /// 1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT, to the whole
    /// second. Outside it the chart failed on Chiron with a file message, or took its planets
    /// silently from the Moshier model, or both by what had been cast before.
    /// </summary>
    /// <param name="input">Chart input, named in the refusal.</param>
    /// <param name="julianDay">The chart's Julian Day (UT).</param>
    /// <param name="utcDateTime">The chart's instant, named in the refusal.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the instant is outside the files' span.
    /// </exception>
    private static void ValidateInstant(ChartInput input, double julianDay, DateTime utcDateTime)
    {
        if (julianDay < EphemerisSpan.FirstJulianDay || julianDay > EphemerisSpan.LastJulianDay)
            throw OutsideTheFiles(input, CalendarDateFormatter.DateTime(utcDateTime) + " UT");
    }

    /// <summary>
    /// The refusal of an instant outside the ephemeris files' span, naming the span in years.
    /// </summary>
    /// <param name="input">Chart input.</param>
    /// <param name="named">The date or instant refused, as the message names it.</param>
    /// <returns>The exception.</returns>
    private static ArgumentOutOfRangeException OutsideTheFiles(ChartInput input, string named) =>
        new(nameof(input), string.Create(CultureInfo.InvariantCulture,
            $"{named} is outside the years the ephemeris files cover, {FirstYearInFiles} to {LastYearInFiles}: a chart can be cast from 1200-01-01 04:54:46 UT (Julian calendar) to 2399-12-31 23:51:20 UT."));

    /// <summary>
    /// Converts a <see cref="ChartInput"/> to UTC DateTime and Julian Day,
    /// handling explicit UTC offsets and DST edge cases.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <returns>
    /// Tuple of UTC DateTime, Julian Day number, the offset applied, and, for a chart
    /// cast through its zone, how its reading mapped to that instant and the Time Note
    /// that says so when it was repeated, skipped or on local mean time.
    /// </returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the chart is cast through a zone the time-zone rules do not hold.
    /// </exception>
    private (DateTime UtcDateTime, double JulianDay, double ResolvedUtcOffsetHours, ZoneInstant? Zone, string? TimeNote) ConvertInputToJulianDay(ChartInput input)
    {
        (DateTime reading, DateTime utcDateTime, double julianDay, double resolvedOffset, ZoneInstant? zone) =
            ResolveInstant(input, _ephemeris, _timeZoneRules);
        string? timeNote = null;
        if (zone is null)
        {
            _logger.Debug(
                "Using explicit UTC offset: {Offset}h for '{Name}'",
                input.UtcOffsetHours, input.Name);
        }
        else
        {
            if (zone.Mapping != LocalTimeMapping.Unique)
            {
                _logger.Warning(
                    "{Mapping} local time at {LocalTime} in {TimeZone} for '{Name}': cast at {UtcDateTime:o} ({ResolvedLocalTime} {Abbreviation})",
                    zone.Mapping, reading, input.TimeZone, input.Name,
                    zone.UtcDateTime, zone.ResolvedLocalDateTime, zone.Abbreviation);
            }

            timeNote = BuildTimeNote(reading, input, zone, utcDateTime);
        }

        return (utcDateTime, julianDay, resolvedOffset, zone, timeNote);
    }

    /// <summary>
    /// The instant a chart's time input denotes, to the whole second, and its Julian Day:
    /// the one conversion the engine casts with and <see cref="Data.ChartMapper.ToEntity"/>
    /// saves with, so a saved chart stores the instant it was calculated at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>To the whole second (F0-4).</b> The reading is taken to the nearest second, and an
    /// explicit offset entered as decimal hours to the nearest second (−5.8433333 is
    /// −5:50:36), so the instant is a whole second and <c>UtcDateTime</c> and the Julian Day
    /// are the same instant. Adding a decimal-hours offset left floating-point error that
    /// cast −5:50:36 a second early, and a reading's fraction of a second was kept in the
    /// instant but dropped from the Julian Day.
    /// </para>
    /// <para>
    /// <b>Through a zone</b> the app's own IANA data converts under D17: a reading that
    /// occurs twice (clocks going back) is the later instant, and one that never occurs
    /// (clocks going forward) the first instant after the gap.
    /// </para>
    /// <para>
    /// <b>The calendar is decided on the local date (D21).</b> A local date on or after
    /// 1582-10-15 is Gregorian, one on or before 1582-10-04 Julian, and the ten dates between
    /// never occurred and are refused. The reading is read on its own calendar first and the
    /// offset applied after, so 1582-10-15 00:30 at UTC+1 is 1582-10-04 23:30 UT (Julian),
    /// JD 2299160.479; deciding the calendar on the UT date read 14 October as Julian and cast
    /// it ten days late. A zone's offset before the switch is its local mean time, taken from
    /// the time-zone data for the same instant. The UTC instant is proleptic ticks on every
    /// calendar (R-D4, branch R2, 1.3.4), and its Julian Day comes from it by arithmetic
    /// (<see cref="JulianDayUtility.InstantToJulianDay"/>); a relocated chart is cast at that
    /// Julian Day, and the instant prints on the calendar in force through
    /// <see cref="CalendarDateFormatter"/>. Until 1.3.4 it was held as the Julian label before
    /// 1582-10-15 (F5's decision 3), so that a chart recast from it as a reading at offset zero
    /// landed on the same Julian Day.
    /// </para>
    /// </remarks>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="ephemeris">
    /// Ephemeris service; kept for the callers' signatures, the Julian Day no longer reads a date
    /// through it (branch R2, 1.3.4).
    /// </param>
    /// <param name="timeZoneRules">The time-zone rules a zone's reading is converted with.</param>
    /// <returns>
    /// Tuple of the reading to the whole second, the UTC instant (always
    /// <see cref="DateTimeKind.Utc"/>), its Julian Day, the offset applied in hours (the
    /// zone's at the instant, or the explicit offset to the second), and for a chart cast
    /// through its zone how its reading mapped.
    /// </returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the chart is cast through a zone the time-zone rules do not hold.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the local date is one of 1582-10-05 to 1582-10-14, which never occurred,
    /// or the instant falls on 29 February UT of a year that is a leap year on the Julian
    /// calendar but not the Gregorian (1300, 1400, 1500), a date <see cref="DateTime"/>
    /// cannot hold.
    /// </exception>
    internal static (DateTime Reading, DateTime UtcDateTime, double JulianDay, double ResolvedUtcOffsetHours, ZoneInstant? Zone) ResolveInstant(
        ChartInput input, IEphemerisService ephemeris, ITimeZoneRules timeZoneRules)
    {
        DateTime reading = ToWholeSecond(input.LocalDateTime);

        // D21: the calendar is the local date's own. A Julian reading is turned into the
        // instant of its own clock it names, which .NET (and the time-zone data) count on the
        // proleptic Gregorian calendar.
        bool julianReading = IsJulianDate(input, reading);
        DateTime clock = julianReading
            ? DateTime.SpecifyKind(
                new JulianCalendar().ToDateTime(reading.Year, reading.Month, reading.Day, reading.Hour, reading.Minute, reading.Second, 0),
                reading.Kind)
            : reading;

        DateTime utcClock;
        ZoneInstant? zone = null;
        double resolvedOffset;
        if (input.UseExplicitOffset && input.UtcOffsetHours.HasValue)
        {
            // User specified an explicit UTC offset — use it directly, to the second.
            // This bypasses IANA timezone lookup, which is essential for
            // historical dates where DST rules may be incorrect.
            long offsetSeconds = (long)Math.Round(input.UtcOffsetHours.Value * 3600.0, MidpointRounding.AwayFromZero);
            utcClock = clock.AddSeconds(-offsetSeconds);
            resolvedOffset = offsetSeconds / 3600.0;
        }
        else
        {
            // The zone's offset at the instant, to the second (IANA's offsets are whole
            // seconds, so a whole-second reading gives a whole-second instant). It differs
            // from the reading less the instant only for a skipped reading, whose own clock
            // reading is not the instant's. Before the switch it is the zone's local mean
            // time, for the same instant.
            zone = timeZoneRules.ToUniversal(clock, input.TimeZone);
            utcClock = zone.UtcDateTime;
            resolvedOffset = zone.Offset.TotalHours;

            if (julianReading)
                zone = zone with { ResolvedLocalDateTime = DateTime.SpecifyKind(reading, DateTimeKind.Unspecified) };
        }

        // The instant, as proleptic ticks on every calendar (R-D4, branch R2, 1.3.4), and
        // always UTC whatever the input's kind (F0-7): an explicit offset's instant kept the
        // reading's kind, so it was saved, and written to the JSON, with no "Z" or with the
        // saving machine's offset. Until 1.3.4 an instant before 1582-10-15 was held as its
        // Julian label, 7 to 10 days from the instant as ticks; CalendarDateFormatter prints
        // the label.
        DateTime utcDateTime = DateTime.SpecifyKind(utcClock, DateTimeKind.Utc);
        if (utcClock < GregorianCalendarStart)
        {
            // A typed reading whose UT instant falls on a leap day of the Julian calendar the
            // Gregorian does not have is still refused (R-D5): the form holds a date.
            JulianCalendar julian = new();
            int year = julian.GetYear(utcClock);
            int month = julian.GetMonth(utcClock);
            int day = julian.GetDayOfMonth(utcClock);
            if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            {
                throw new ArgumentOutOfRangeException(nameof(input),
                    string.Create(CultureInfo.InvariantCulture,
                        $"The chart's moment falls on {year:D4}-02-29 UT, a leap day of the Julian calendar that the app cannot hold as a date."));
            }
        }

        // The Julian Day by arithmetic from the instant, never through a calendar.
        double julianDay = JulianDayUtility.InstantToJulianDay(utcDateTime);

        return (reading, utcDateTime, julianDay, resolvedOffset, zone);
    }

    /// <summary>The first day of the Gregorian calendar, 1582-10-15; the day before it was 1582-10-04 on the Julian.</summary>
    private static readonly DateTime GregorianCalendarStart = new(1582, 10, 15);

    /// <summary>
    /// Whether a local date is on the Julian calendar (D21): on or before 1582-10-04. A date
    /// on or after 1582-10-15 is Gregorian.
    /// </summary>
    /// <param name="input">The chart input, named in the refusal.</param>
    /// <param name="reading">The local reading.</param>
    /// <returns><see langword="true"/> for a Julian date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the date is one of 1582-10-05 to 1582-10-14, which never occurred.
    /// </exception>
    private static bool IsJulianDate(ChartInput input, DateTime reading)
    {
        if (reading.Date >= GregorianCalendarStart)
            return false;

        if (reading.Date <= GregorianCalendarStart.AddDays(-11))
            return true;

        throw new ArgumentOutOfRangeException(nameof(input),
            string.Create(CultureInfo.InvariantCulture,
                $"{reading:yyyy-MM-dd} does not exist: the Gregorian calendar began on 1582-10-15, the day after 1582-10-04 on the Julian calendar, so 1582-10-05 to 1582-10-14 never occurred."));
    }

    /// <summary>
    /// A date and time to the nearest whole second, half a second rounding up, keeping its
    /// <see cref="DateTime.Kind"/>.
    /// </summary>
    /// <param name="value">The date and time.</param>
    /// <returns>The value with no fraction of a second.</returns>
    private static DateTime ToWholeSecond(DateTime value)
    {
        long fraction = value.Ticks % TimeSpan.TicksPerSecond;
        long ticks = value.Ticks - fraction + (fraction * 2 >= TimeSpan.TicksPerSecond ? TimeSpan.TicksPerSecond : 0);
        return new DateTime(ticks, value.Kind);
    }

    /// <summary>
    /// Converts an instant a chart is to be cast at into its Julian Day, with the offset and
    /// name in force then on the chart's zone. The chart's wall clock must read that instant.
    /// </summary>
    /// <param name="input">Chart input parameters, cast through their zone.</param>
    /// <param name="instant">The instant, read as UTC whatever its kind.</param>
    /// <returns>
    /// Tuple of UTC DateTime, Julian Day number, the zone's offset then, how the wall
    /// clock maps (in a repeated hour it names this instant and one other), and the Time
    /// Note, which in a repeated hour says which of the two the chart is cast for.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="input"/> is cast at an explicit offset, or its wall clock
    /// is not the reading of <paramref name="instant"/> on its zone's clock.
    /// </exception>
    private (DateTime UtcDateTime, double JulianDay, double ResolvedUtcOffsetHours, ZoneInstant? Zone, string? TimeNote) ConvertInstantToJulianDay(
        ChartInput input, DateTime instant)
    {
        if (input.UseExplicitOffset && input.UtcOffsetHours.HasValue)
        {
            throw new ArgumentException(
                "A chart cast at a given instant is read on its zone's clock, not at an explicit offset.",
                nameof(input));
        }

        DateTime utcDateTime = DateTime.SpecifyKind(instant, DateTimeKind.Utc);
        DateTime wallClock = DateTime.SpecifyKind(input.LocalDateTime, DateTimeKind.Unspecified);
        ZoneReading reading = _timeZoneRules.ToLocal(utcDateTime, input.TimeZone);

        if (reading.LocalDateTime != wallClock)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture,
                    $"The chart's wall clock {wallClock:yyyy-MM-dd HH:mm:ss} is not {CalendarDateFormatter.DateTime(utcDateTime)} UT on the clock of {input.TimeZone}, which reads {CalendarDateFormatter.DateTime(reading.LocalDateTime)}."),
                nameof(input));
        }

        ZoneInstant d17 = _timeZoneRules.ToUniversal(wallClock, input.TimeZone);
        ZoneInstant zone = new(
            utcDateTime, reading.Offset, reading.Abbreviation, d17.Mapping, reading.LocalDateTime,
            d17.EarlierUtcDateTime, d17.EarlierOffset, d17.EarlierAbbreviation);

        // The instant's Julian Day by arithmetic, never through a calendar (RT2): before
        // 1582-10-15 the date's calendar switch read the instant's proleptic Gregorian date as a
        // Julian one, and the candidate was cast 7 to 10 days after the moment it was scored for.
        double julianDay = JulianDayUtility.InstantToJulianDay(utcDateTime);

        return (utcDateTime, julianDay, reading.Offset.TotalHours, zone, BuildTimeNote(wallClock, input, d17, utcDateTime));
    }

    /// <summary>
    /// Builds the Time Note of a chart cast through its zone, naming the city whose local
    /// mean time the zone's data gives by the zone's canonical identifier.
    /// </summary>
    /// <param name="reading">The chart's wall clock, to the whole second.</param>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="zone">How the chart's wall clock maps under D17.</param>
    /// <param name="castUtcDateTime">The instant the chart is cast at.</param>
    /// <returns>The Note, or <see langword="null"/> when the reading needs none.</returns>
    private string? BuildTimeNote(DateTime reading, ChartInput input, ZoneInstant zone, DateTime castUtcDateTime) =>
        TimeNoteFormatter.Format(
            reading,
            input.TimeZone,
            _timeZoneRules.Canonicalize(input.TimeZone) ?? input.TimeZone,
            zone,
            castUtcDateTime);

    /// <summary>
    /// Assembles a <see cref="ChartResult"/> with version information.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="utcDateTime">The instant the chart is cast at.</param>
    /// <param name="julianDay">The instant's Julian Day (UT).</param>
    /// <param name="obliquity">The true obliquity of the ecliptic at <paramref name="julianDay"/>, in degrees.</param>
    /// <param name="positions">The planetary positions, with their houses assigned.</param>
    /// <param name="houses">The houses and angles.</param>
    /// <param name="aspects">The aspects.</param>
    /// <param name="midpoints">The midpoints.</param>
    /// <param name="activations">The midpoint activations.</param>
    /// <param name="fixedStarConjunctions">The fixed-star conjunctions.</param>
    /// <param name="declinationAspects">The declination aspects.</param>
    /// <param name="minorPoints">
    /// The minor points, Ceres, Pallas, Juno, Vesta and Lilith, with their houses, or empty where the chart
    /// carries none (branch Asteroids, 1.6.0).
    /// </param>
    /// <param name="minorPointAspects">The minor points' aspects, or empty where the chart carries no minor points.</param>
    /// <param name="resolvedUtcOffset">The offset applied, in hours.</param>
    /// <param name="zoneInstant">How the reading mapped through its zone, or <see langword="null"/> at an explicit offset.</param>
    /// <param name="timeNote">The Time Note, or <see langword="null"/>.</param>
    /// <param name="keepFrom">
    /// For a relocation, the natal chart whose sect the dignities and chart context take and whose planetary
    /// hour the chart keeps (S-D6); <see langword="null"/> for any other chart.
    /// </param>
    /// <returns>The chart.</returns>
    private ChartResult BuildResult(
        ChartInput input,
        DateTime utcDateTime,
        double julianDay,
        double obliquity,
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        IReadOnlyList<AspectResult> aspects,
        IReadOnlyList<Midpoint> midpoints,
        IReadOnlyList<MidpointActivation> activations,
        IReadOnlyList<FixedStarConjunction> fixedStarConjunctions,
        IReadOnlyList<DeclinationAspect> declinationAspects,
        IReadOnlyList<PlanetPosition> minorPoints,
        IReadOnlyList<AspectResult> minorPointAspects,
        double resolvedUtcOffset,
        ZoneInstant? zoneInstant,
        string? timeNote,
        ChartResult? keepFrom = null)
    {
        string engineVersion = BuildEngineVersion();

        IReadOnlyList<DignityInfo> dignities = CalculateDignities(positions, houses, keepFrom);
        MoonConditionInfo? moonCondition = _moonCondition.GetMoonCondition(positions, aspects, _ephemeris, julianDay);
        ChartContextInfo? chartContext = _chartContext.Analyze(positions, houses, dignities, input.EventType, keepFrom?.ChartContext);
        IReadOnlyList<PlanetCondition> planetConditions = _planetCondition.Analyze(positions, aspects);

        // Moon mansion
        PlanetPosition? moonForMansion = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Moon", StringComparison.OrdinalIgnoreCase));
        MoonMansionInfo? moonMansionInfo = moonForMansion is not null
            ? _moonMansion.GetMansion(moonForMansion.Longitude)
            : null;

        // Planetary hour. A relocation keeps the birth's (S-D6): the natal chart's own, none
        // computed at the place, and null stays null. For any other chart: where the Sun does not
        // rise or set (the polar day or night) there is none, and where the sunrise and sunset
        // found cannot bracket the instant the calculator declines: either way the chart is
        // returned without it, and the omission logged. Any other ephemeris failure propagates.
        PlanetaryHourInfo? planetaryHourInfo = null;
        if (keepFrom is not null)
        {
            planetaryHourInfo = keepFrom.PlanetaryHour;
        }
        else
        {
            try
            {
                // A chart cast at an explicit offset reads its hour on that offset's clock, the
                // clock its own time is given on; any other chart, on its zone's.
                PlanetaryHour hour = input.UseExplicitOffset && input.UtcOffsetHours.HasValue
                    ? _planetaryHour.CalculateAtFixedOffset(
                        julianDay, input.Latitude, input.Longitude, input.UtcOffsetHours.Value)
                    : _planetaryHour.Calculate(
                        julianDay, input.Latitude, input.Longitude, input.TimeZone);

                planetaryHourInfo = BuildPlanetaryHourInfo(hour, chartContext, input.EventType);
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or AstroCast.Core.Exceptions.SunDoesNotRiseOrSetException)
            {
                _logger.Warning(
                    "Could not calculate planetary hour for '{Name}': {Message}",
                    input.Name, ex.Message);
            }
        }

        // Arabic Parts / Lots
        bool isDayChartForParts = chartContext?.IsDayChart ?? true;
        IReadOnlyList<ArabicPart> arabicParts = _arabicParts.CalculateAll(
            positions, houses, isDayChartForParts);

        // Antiscia / Contra-antiscia
        (IReadOnlyList<AntisciaPoint> antisciaPoints,
         IReadOnlyList<AntisciaConnection> antisciaConnections) = _antiscia.Calculate(positions);

        // Stations — the finder needs the instant and the ephemeris to locate
        // the nearest station rather than guess its type from the speed's sign.
        IReadOnlyList<StationInfo> stations = _stations.Evaluate(positions, julianDay, _ephemeris);

        // Parans (may fail at extreme latitudes — catch and log)
        ParanResult? paranResult = null;
        try
        {
            paranResult = _parans.Calculate(julianDay, input.Latitude, input.Longitude);
        }
        catch (Exception ex)
        {
            _logger.Warning(
                "Could not calculate parans for '{Name}': {Message}",
                input.Name, ex.Message);
        }

        ChartResult result = new()
        {
            Input = input,
            JulianDay = julianDay,
            Obliquity = obliquity,
            UtcDateTime = utcDateTime,
            Positions = positions,
            Houses = houses,
            Aspects = aspects,
            MinorPoints = minorPoints,
            MinorPointAspects = minorPointAspects,
            Midpoints = midpoints,
            MidpointActivations = activations,
            Dignities = dignities,
            MoonCondition = moonCondition,
            ChartContext = chartContext,
            FixedStarConjunctions = fixedStarConjunctions,
            DeclinationAspects = declinationAspects,
            PlanetConditions = planetConditions,
            MoonMansion = moonMansionInfo,
            PlanetaryHour = planetaryHourInfo,
            ArabicParts = arabicParts,
            AntisciaPoints = antisciaPoints,
            AntisciaConnections = antisciaConnections,
            Stations = stations,
            Parans = paranResult,
            ResolvedUtcOffsetHours = resolvedUtcOffset,
            ResolvedOffsetLabel = FormatOffsetLabel(resolvedUtcOffset, zoneInstant),
            LocalTimeMapping = ResolveLocalTimeMapping(zoneInstant),
            ResolvedLocalDateTime = ResolveLocalDateTime(input, zoneInstant),
            EarlierUtcDateTime = zoneInstant?.EarlierUtcDateTime,
            TimeNote = timeNote,
            TimeZoneRules = _timeZoneRules,
            HouseSystemName = input.HouseSystem.DisplayName,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };

        // Horary perfection — consumes the assembled result and needs the
        // ephemeris to time perfections and stations, so it runs here rather
        // than in the formatter. Only horary charts with a category carry it;
        // electional candidates are "Event" charts and never reach this.
        if (string.Equals(input.EventType, "Horary", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(input.HoraryCategory))
        {
            result.HoraryPerfection = HoraryPerfectionAnalyzer.Analyze(
                result, input.HoraryCategory, julianDay, _ephemeris);
        }

        return result;
    }

    /// <summary>
    /// Generates a human-readable UTC offset label: the zone record's name when it is a
    /// word, then the offset. Examples: "EST (UTC−5)", "LMT (UTC−4:56:02)", "UTC+6",
    /// "UTC−5 (explicit)".
    /// </summary>
    /// <param name="offsetHours">The offset applied, in hours.</param>
    /// <param name="zoneInstant">
    /// The zone conversion the chart was cast through, or <see langword="null"/> for a
    /// chart cast at an explicit offset.
    /// </param>
    /// <returns>The label, by <see cref="OffsetLabelFormatter"/>.</returns>
    private static string FormatOffsetLabel(double offsetHours, ZoneInstant? zoneInstant) =>
        OffsetLabelFormatter.Format(
            zoneInstant?.Offset ?? TimeSpan.FromHours(offsetHours),
            zoneInstant?.Abbreviation,
            isExplicit: zoneInstant is null);

    /// <summary>
    /// How a chart's reading mapped to its instant: the zone conversion's mapping, or
    /// <see cref="LocalTimeMapping.Unique"/> for a chart cast at an explicit offset.
    /// </summary>
    /// <param name="zoneInstant">
    /// The zone conversion the chart was cast through, or <see langword="null"/> for a
    /// chart cast at an explicit offset.
    /// </param>
    /// <returns>The mapping.</returns>
    private static LocalTimeMapping ResolveLocalTimeMapping(ZoneInstant? zoneInstant) =>
        zoneInstant?.Mapping ?? LocalTimeMapping.Unique;

    /// <summary>
    /// The wall clock a chart is cast for: the zone conversion's resolved reading (03:00 for
    /// a skipped 02:30), or the input's own reading, to the whole second, at an explicit offset.
    /// </summary>
    /// <param name="input">Chart input parameters.</param>
    /// <param name="zoneInstant">
    /// The zone conversion the chart was cast through, or <see langword="null"/> for a
    /// chart cast at an explicit offset.
    /// </param>
    /// <returns>The reading, <see cref="DateTimeKind.Unspecified"/>.</returns>
    private static DateTime ResolveLocalDateTime(ChartInput input, ZoneInstant? zoneInstant) =>
        zoneInstant?.ResolvedLocalDateTime
            ?? DateTime.SpecifyKind(ToWholeSecond(input.LocalDateTime), DateTimeKind.Unspecified);

    /// <summary>
    /// The extended dignities of a chart's planets, by its sect: a day chart when the Sun's
    /// longitude lies on the half of the ecliptic above the horizon (<see cref="ChartSect.IsDayChart"/>),
    /// or, for a relocation, the birth chart's sect (S-D6).
    /// </summary>
    /// <param name="positions">The chart's positions.</param>
    /// <param name="houses">The chart's houses.</param>
    /// <param name="keepFrom">
    /// For a relocation, the natal chart whose sect (<see cref="ChartContextInfo.IsDayChart"/>) is read in
    /// place of the Sun's altitude; <see langword="null"/> for any other chart.
    /// </param>
    /// <returns>The dignities.</returns>
    private IReadOnlyList<DignityInfo> CalculateDignities(IReadOnlyList<PlanetPosition> positions, HouseData houses, ChartResult? keepFrom = null)
    {
        bool isDayChartForDignity;
        if (keepFrom?.ChartContext is { } keptContext)
        {
            isDayChartForDignity = keptContext.IsDayChart;
        }
        else
        {
            // Determine chart sect for extended dignity calculation
            PlanetPosition? sunForSect = positions.FirstOrDefault(
                p => string.Equals(p.Name, "Sun", StringComparison.OrdinalIgnoreCase));
            isDayChartForDignity = sunForSect is not null && ChartSect.IsDayChart(sunForSect.Longitude, houses);
        }

        return _dignity.GetDignitiesExtended(positions, isDayChartForDignity);
    }

    /// <summary>
    /// The engine version every chart carries: "AstroCast 1.3.3 (SwissEph 2.10.03, tzdb 2026e)".
    /// </summary>
    /// <returns>The version string.</returns>
    private string BuildEngineVersion()
    {
        string asmVersion = typeof(ChartEngine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        // Trim the git commit hash suffix (e.g., "1.0.0+abc123" -> "1.0.0")
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;

        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }

    /// <summary>
    /// Builds a <see cref="PlanetaryHourInfo"/> with radicality analysis
    /// for horary charts. For non-horary charts, the radicality fields
    /// are left null.
    /// </summary>
    /// <param name="hour">The computed planetary hour.</param>
    /// <param name="chartContext">Chart context (provides chart ruler).</param>
    /// <param name="eventType">Event type from the chart input.</param>
    /// <returns>Planetary hour info with optional radicality analysis.</returns>
    private PlanetaryHourInfo BuildPlanetaryHourInfo(
        PlanetaryHour hour,
        ChartContextInfo? chartContext,
        string? eventType)
    {
        // Radicality check only applies to Horary charts
        bool isHorary = string.Equals(eventType, "Horary", StringComparison.OrdinalIgnoreCase);

        if (!isHorary || chartContext is null)
        {
            return new PlanetaryHourInfo
            {
                Hour = hour,
                IsRadical = null,
                RadicalityLevel = null,
                RadicalityNote = null,
            };
        }

        string hourRuler = hour.RulingPlanet;
        string chartRuler = chartContext.ChartRuler;

        // Level 1: Direct match — hour ruler IS the chart ruler
        if (string.Equals(hourRuler, chartRuler, StringComparison.OrdinalIgnoreCase))
        {
            return new PlanetaryHourInfo
            {
                Hour = hour,
                IsRadical = true,
                RadicalityLevel = "Radical",
                RadicalityNote = $"Hour ruler {hourRuler} matches chart ruler {chartRuler} — chart is radical.",
            };
        }

        // Level 2: Same triplicity element
        // Both planets belong to the triplicity rulers of the same element
        string ascElement = _dignity.GetElement(chartContext.AscendantSign);
        TriplicityRulerInfo triplicity = _dignity.GetTriplicityRulers(
            chartContext.AscendantSign, chartContext.IsDayChart);
        bool hourRulerInTriplicity =
            string.Equals(hourRuler, triplicity.DayRuler, StringComparison.OrdinalIgnoreCase)
            || string.Equals(hourRuler, triplicity.NightRuler, StringComparison.OrdinalIgnoreCase)
            || string.Equals(hourRuler, triplicity.ParticipatingRuler, StringComparison.OrdinalIgnoreCase);

        if (hourRulerInTriplicity)
        {
            return new PlanetaryHourInfo
            {
                Hour = hour,
                IsRadical = true,
                RadicalityLevel = "Supportive",
                RadicalityNote = $"Hour ruler {hourRuler} shares {ascElement} triplicity with chart ruler {chartRuler} — supportive radicality.",
            };
        }

        // Level 3: Dispositor match — hour ruler is the dispositor of the chart ruler
        DignityInfo chartRulerDignity = chartContext.ChartRulerDignity;
        if (string.Equals(hourRuler, chartRulerDignity.Dispositor, StringComparison.OrdinalIgnoreCase))
        {
            return new PlanetaryHourInfo
            {
                Hour = hour,
                IsRadical = true,
                RadicalityLevel = "Weak",
                RadicalityNote = $"Hour ruler {hourRuler} is the dispositor of chart ruler {chartRuler} — weak radicality.",
            };
        }

        // No match
        return new PlanetaryHourInfo
        {
            Hour = hour,
            IsRadical = false,
            RadicalityLevel = "Not Radical",
            RadicalityNote = $"Hour ruler {hourRuler} has no connection to chart ruler {chartRuler} — chart is not radical.",
        };
    }
}
