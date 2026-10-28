using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ElectionalScorer"/> verifying weight profiles,
/// individual scoring dimensions, and overall score computation.
/// </summary>
/// <remarks>
/// Tests 1–4 are pure unit tests (no ephemeris required).
/// Tests 5–16 are integration tests using the shared Ephemeris fixture
/// to calculate realistic chart data for scoring.
/// </remarks>
[Collection("Ephemeris")]
public sealed class ElectionalScorerTests
{
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _engine;
    private readonly ElectionalScorer _scorer = new();

    /// <summary>
    /// Receives the shared ephemeris fixture and builds services.
    /// </summary>
    public ElectionalScorerTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(_ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(_ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Weight Tests (1–3)

    [Fact]
    [Trait("Category", "Unit")]
    public void GetWeights_Should_SumToOne_When_AnyElectionType()
    {
        // Arrange — all 9 election types
        ElectionType[] allTypes = Enum.GetValues<ElectionType>();

        foreach (ElectionType type in allTypes)
        {
            // Act
            ScoreWeights weights = ElectionalWeights.GetWeights(type);
            double sum = weights.Moon + weights.Ascendant + weights.Significator
                       + weights.NatalHarmony + weights.MaleficAvoidance
                       + weights.Mercury + weights.PlanetaryHour;

            // Assert
            sum.Should().BeApproximately(1.0, 0.001,
                $"weights for {type} must sum to 1.0");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetWeights_Should_HaveHighestMoon_When_Wedding()
    {
        // Act
        ScoreWeights weights = ElectionalWeights.GetWeights(ElectionType.Wedding);

        // Assert — Moon (0.30) should be the highest weight for weddings
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.Ascendant);
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.Significator);
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.NatalHarmony);
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.MaleficAvoidance);
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.Mercury);
        weights.Moon.Should().BeGreaterThanOrEqualTo(weights.PlanetaryHour);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetWeights_Should_HaveHighestMercury_When_Contract()
    {
        // Act
        ScoreWeights weights = ElectionalWeights.GetWeights(ElectionType.Contract);

        // Assert — Mercury (0.25) should be the highest weight for contracts
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.Moon);
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.Ascendant);
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.Significator);
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.NatalHarmony);
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.MaleficAvoidance);
        weights.Mercury.Should().BeGreaterThanOrEqualTo(weights.PlanetaryHour);
    }

    #endregion

    #region ScoreBreakdown Test (4)

    [Fact]
    [Trait("Category", "Unit")]
    public void OverallScore_Should_EqualWeightedSum_When_ScoresAndWeightsKnown()
    {
        // Arrange — known scores and weights
        ScoreWeights weights = new(0.25, 0.10, 0.20, 0.10, 0.15, 0.10, 0.10);
        ScoreBreakdown breakdown = new()
        {
            MoonScore = 80.0,
            AscendantScore = 60.0,
            SignificatorScore = 70.0,
            NatalHarmonyScore = 50.0,
            MaleficAvoidanceScore = 90.0,
            MercuryScore = 75.0,
            PlanetaryHourScore = 100.0,
            Weights = weights,
            ElectionTypeName = "BusinessLaunch",
        };

        // Act
        double expected = 80.0 * 0.25 + 60.0 * 0.10 + 70.0 * 0.20
                        + 50.0 * 0.10 + 90.0 * 0.15 + 75.0 * 0.10
                        + 100.0 * 0.10;

        // Assert
        breakdown.OverallScore.Should().BeApproximately(expected, 0.001,
            "OverallScore should equal the weighted sum of dimension scores");
    }

    #endregion

    #region Moon Scoring Tests (5–6)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMoon_Should_ScoreHigh_When_WaxingNoBlemishes()
    {
        // Arrange — waxing crescent Moon, not VOC, not Via Combusta, in Cancer (domicile)
        MoonCondition moon = new()
        {
            Longitude = 100.0, // Cancer
            Sign = "Cancer",
            Phase = MoonPhaseType.WaxingCrescent,
            PhasePercent = 20.0,
            PhasePosition = "mid",
            IsVoidOfCourse = false,
            IsInViaCombusta = false,
            NextApplyingAspectDescription = "Moon trine Jupiter (applying, 3\u00b012')",
            DailyMotion = 13.0,
        };

        // Act
        double score = ElectionalScorer.ScoreMoon(moon, ElectionType.BusinessLaunch);

        // Assert — base 50 + 15 (waxing for new beginnings) + 25 (Cancer domicile) + 10 (trine Jupiter)
        score.Should().BeGreaterThanOrEqualTo(80.0,
            "waxing Moon in domicile with benefic aspect should score highly");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMoon_Should_ScoreLow_When_VoidOfCourse()
    {
        // Arrange — VOC Moon
        MoonCondition moon = new()
        {
            Longitude = 45.0, // Taurus
            Sign = "Taurus",
            Phase = MoonPhaseType.WaxingCrescent,
            PhasePercent = 15.0,
            PhasePosition = "early",
            IsVoidOfCourse = true,
            VoidStartLocal = DateTime.Now.AddHours(-1),
            VoidEndLocal = DateTime.Now.AddHours(2),
            IsInViaCombusta = false,
            DailyMotion = 12.5,
        };

        // Act
        double score = ElectionalScorer.ScoreMoon(moon, ElectionType.Wedding);

        // Assert — base 50 + 15 (waxing for wedding) - 40 (VOC) + 20 (Taurus exaltation) = 45
        score.Should().BeLessThanOrEqualTo(50.0,
            "void-of-course Moon should receive a significant penalty");
    }

    #endregion

    #region Ascendant Scoring Tests (7–8)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreAscendant_Should_ScoreHigh_When_PreferredSign()
    {
        // Arrange — Leo rising for a Business Launch (Leo is preferred)
        ChartResult chart = CreateChartWithAscendant(135.0); // Leo = 120–150°

        // Act
        double score = ElectionalScorer.ScoreAscendant(chart, ElectionType.BusinessLaunch);

        // Assert
        score.Should().Be(85.0,
            "Leo rising should be a preferred sign for BusinessLaunch");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreAscendant_Should_ScoreLower_When_NonPreferredSign()
    {
        // Arrange — Pisces rising for a Business Launch (Pisces is not preferred)
        ChartResult chart = CreateChartWithAscendant(345.0); // Pisces = 330–360°

        // Act
        double score = ElectionalScorer.ScoreAscendant(chart, ElectionType.BusinessLaunch);

        // Assert
        score.Should().Be(45.0,
            "Pisces rising should not be a preferred sign for BusinessLaunch");
    }

    #endregion

    #region Significator Scoring Tests (9–10)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreSignificator_Should_ScoreHigh_When_DignifiedDirect()
    {
        // Arrange — Wedding: Venus (significator) in Taurus (domicile), direct, angular
        ChartResult chart = CreateChartWithPlanet("Venus",
            longitude: 40.0, // Taurus = 30–60°
            speed: 1.2,      // direct
            house: 1);       // angular

        // Act
        double score = ElectionalScorer.ScoreSignificator(chart, ElectionType.Wedding);

        // Assert — base 50 + 25 (domicile) + 15 (angular) = 90
        score.Should().BeGreaterThanOrEqualTo(85.0,
            "dignified direct significator in angular house should score highly");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreSignificator_Should_Penalize_When_Retrograde()
    {
        // Arrange — Contract: Mercury (significator) retrograde in Sagittarius (detriment)
        ChartResult chart = CreateChartWithPlanet("Mercury",
            longitude: 250.0, // Sagittarius = 240–270°
            speed: -1.0,      // retrograde
            house: 6);        // cadent

        // Act
        double score = ElectionalScorer.ScoreSignificator(chart, ElectionType.Contract);

        // Assert — base 50 - 20 (detriment) - 30 (retrograde) - 10 (cadent) = 0 (clamped)
        score.Should().BeLessThanOrEqualTo(10.0,
            "retrograde significator in detriment and cadent house should score very low");
    }

    #endregion

    #region Malefic Avoidance Scoring Tests (11–12)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMaleficAvoidance_Should_ScoreHigh_When_NoAngularMalefics()
    {
        // Arrange — Mars at 120° and Saturn at 280° are both ≥ 10° from every
        // angle (ASC 0°, MC 270°, DSC 180°, IC 90°): no angular malefic by orb
        ChartResult chart = CreateChartForMaleficTest(marsHouse: 5, saturnHouse: 9);

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — base 100, no penalties
        score.Should().Be(100.0,
            $"non-angular malefics should not incur penalties, got {score}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMaleficAvoidance_Should_Penalize_When_MarsAngularByOrb()
    {
        // Arrange — Mars at 178° sits 2° from the DSC (180°) on the cadent
        // (6th house) side; night chart (Sun house 5) so Mars is the in-sect
        // malefic; Mars in Virgo (peregrine, dignity 0); Aries rising makes
        // Mars the chart ruler.
        ChartResult chart = CreateChartForAngularityTest(
            ascendant: 0.0,
            midheaven: 270.0,
            sunHouse: 5,
            mars: (Longitude: 178.0, House: 6),
            saturn: (Longitude: 210.0, House: 8));

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — 100 − (30 base + 10 chart ruler) = 60, full penalty at 2° orb
        score.Should().Be(60.0,
            $"angular chart-ruler Mars at 2° orb must score 100 - (30+10) = 60, got {score}");
    }

    #endregion

    #region Malefic Avoidance Live-Run Regression Tests (T1–T4, review 2026-06-10)

    /// <summary>Richmond, VA latitude for the Doctor Visit live-run fixtures.</summary>
    private const double RichmondLatitude = 37.5385;

    /// <summary>Richmond, VA longitude for the Doctor Visit live-run fixtures.</summary>
    private const double RichmondLongitude = -77.4343;

    /// <summary>Richmond, VA IANA timezone.</summary>
    private const string RichmondTimeZone = "America/New_York";

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMaleficAvoidance_DoctorVisitCandidate1_AngularOutOfSectRulerMars_ScoresThirty()
    {
        // Arrange — T1 fixture: 2026-06-12 17:44:59 America/New_York, Richmond VA.
        // Known facts (export Doctor_visit_Electional_20260611_20260810.md):
        // day chart; ASC 18°36′04″ Scorpio; Mars 18°31′07″ Taurus — detriment (−5),
        // chart ruler, out-of-sect malefic — on the DSC at 0°04′ orb.
        ChartResult chart = CalculateRichmondElectionChart(new DateTime(2026, 6, 12, 17, 44, 59));

        // The shared angularity routine must report Mars angular on the DSC
        IReadOnlyList<AngularPlanet> angular =
            AngularityService.FindAngularPlanets(chart.Positions, chart.Houses);
        AngularPlanet? mars = angular.FirstOrDefault(a => a.PlanetName == "Mars");

        mars.Should().NotBeNull(
            "the shared angularity routine must report Mars angular (expected DSC, 0°04′ orb); "
            + $"angular planets found: {string.Join(", ", angular.Select(a => $"{a.PlanetName} {a.Angle} {a.Orb}"))}");
        mars!.Angle.Should().Be("DSC",
            $"Mars must be angular on the DSC, got {mars.Angle} at {mars.Orb}°");
        mars.Orb.Should().BeLessThan(0.2,
            $"Mars DSC orb must be approximately 0°04′ (0.07°), got {mars.Orb}°");

        // Fixture preconditions for the exact model value
        chart.ChartContext.Should().NotBeNull("electional charts carry chart context");
        chart.ChartContext!.IsDayChart.Should().BeTrue(
            "17:44 EDT in June is before sunset: a day chart (Mars = out-of-sect malefic)");
        chart.ChartContext.ChartRuler.Should().Be("Mars",
            $"Scorpio rising makes Mars the chart ruler, got {chart.ChartContext.ChartRuler}");
        chart.Dignities.First(d => d.PlanetName == "Mars").Score.Should().Be(-5,
            "Mars in Taurus is in detriment (-5)");

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — binding acceptance: ≤ 40
        score.Should().BeLessThanOrEqualTo(40.0,
            $"an angular out-of-sect, debilitated, chart-ruler Mars must score at most 40, got {score}");

        // Exact model value when Saturn is not also angular:
        // 100 − (30 base + 20 out-of-sect + 10 detriment + 10 chart ruler) = 30
        bool saturnAngular = angular.Any(a => a.PlanetName == "Saturn");
        if (!saturnAngular)
        {
            score.Should().Be(30.0,
                $"model value: 100 - (30+20+10+10) = 30, got {score}");
        }

        // Overall must be recomputed from the updated Malefic Avoidance value
        MoonConditionEvaluator moonEvaluator = new(_ephemeris);
        MoonCondition moon = moonEvaluator.Evaluate(chart.JulianDay, [], RichmondTimeZone);
        PlanetaryHour hour = new PlanetaryHourCalculator(_ephemeris)
            .Calculate(chart.JulianDay, RichmondLatitude, RichmondLongitude, RichmondTimeZone);

        ScoreBreakdown breakdown = _scorer.Score(chart, moon, hour, ElectionType.Other);
        ScoreWeights w = breakdown.Weights;

        breakdown.MaleficAvoidanceScore.Should().Be(score,
            "the breakdown must carry the same Malefic Avoidance value as the dimension scorer");

        double expectedOverall =
            breakdown.MoonScore * w.Moon
            + breakdown.AscendantScore * w.Ascendant
            + breakdown.SignificatorScore * w.Significator
            + breakdown.NatalHarmonyScore * w.NatalHarmony
            + breakdown.MaleficAvoidanceScore * w.MaleficAvoidance
            + breakdown.MercuryScore * w.Mercury
            + breakdown.PlanetaryHourScore * w.PlanetaryHour;

        breakdown.OverallScore.Should().BeApproximately(expectedOverall, 0.0001,
            $"Overall must be the weighted sum including the corrected Malefic Avoidance value, expected {expectedOverall}, got {breakdown.OverallScore}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMaleficAvoidance_DoctorVisitCandidate2_NoAngularMalefic_ScoresHundred()
    {
        // Arrange — T2 fixture: 2026-06-12 23:44:59 America/New_York, Richmond VA.
        // Known facts: night chart; ASC 14°44′42″ Aquarius; no malefic within 5°
        // of any angle (only Uranus on the IC at 0°23′).
        ChartResult chart = CalculateRichmondElectionChart(new DateTime(2026, 6, 12, 23, 44, 59));

        IReadOnlyList<AngularPlanet> angular =
            AngularityService.FindAngularPlanets(chart.Positions, chart.Houses);

        chart.ChartContext.Should().NotBeNull("electional charts carry chart context");
        chart.ChartContext!.IsDayChart.Should().BeFalse("23:44 EDT is a night chart");
        angular.Where(a => a.PlanetName is "Mars" or "Saturn").Should().BeEmpty(
            "the fixture has no malefic within 5° of any angle; angular planets found: "
            + $"{string.Join(", ", angular.Select(a => $"{a.PlanetName} {a.Angle} {a.Orb}"))}");
        angular.Should().Contain(a => a.PlanetName == "Uranus" && a.Angle == "IC",
            "the live-run export listed Uranus on the IC at 0°23′");

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — binding acceptance: ≥ 85; model value: 100
        score.Should().BeGreaterThanOrEqualTo(85.0,
            $"a chart with no angular malefic must score at least 85, got {score}");
        score.Should().Be(100.0,
            $"model value with no angular malefic: 100, got {score}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ScoreMaleficAvoidance_WellDignifiedAngularMars_HalvesPenalty()
    {
        // Arrange — T3 fixture: Mars in Scorpio (domicile, +5 >= +3) exactly on
        // the ASC at 1° orb in a day chart. Modifiers: 30 base + 20 out-of-sect
        // (day chart) + 10 chart ruler (Scorpio rising) = 60; the
        // well-dignified-malefic exception (A3) halves it to 30.
        ChartResult chart = CreateChartForAngularityTest(
            ascendant: 212.0,           // Scorpio rising
            midheaven: 122.0,
            sunHouse: 10,               // day chart
            mars: (Longitude: 213.0, House: 1),   // Scorpio, 1° from ASC
            saturn: (Longitude: 150.0, House: 11)); // far from all angles

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — 100 − ((30+20+10) / 2) = 70 (supports Mars elections such as surgery)
        score.Should().Be(70.0,
            $"a domiciled angular Mars must have its (30+20+10) penalty halved to 30, got {score}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ScoreMaleficAvoidance_MaleficOnCadentSideOfAngleCusp_ScoredAsAngular()
    {
        // Arrange — T4 fixture: Saturn 0°05′ (0.0833°) before the MC cusp,
        // occupying cadent house 9 by Placidus assignment. The house-based
        // scorer missed this placement entirely; the orb-based scorer must
        // treat it as maximally angular. Night chart (Sun house 3) makes
        // Saturn the out-of-sect malefic; Saturn in Sagittarius is peregrine
        // and Aries rising means Saturn is not the chart ruler.
        ChartResult chart = CreateChartForAngularityTest(
            ascendant: 0.0,
            midheaven: 270.0,
            sunHouse: 3,                // night chart
            mars: (Longitude: 120.0, House: 5),     // far from all angles
            saturn: (Longitude: 269.9167, House: 9)); // 0°05′ cadent side of MC

        PlanetPosition saturn = chart.Positions.First(p => p.Name == "Saturn");
        saturn.House.Should().Be(9, "the fixture places Saturn in cadent house 9");

        IReadOnlyList<AngularPlanet> angular =
            AngularityService.FindAngularPlanets(chart.Positions, chart.Houses);
        angular.Should().Contain(a => a.PlanetName == "Saturn" && a.Angle == "MC",
            "Saturn 0°05′ from the MC must be reported angular by the shared routine "
            + "despite occupying the preceding (cadent) house");

        // Act
        double score = ElectionalScorer.ScoreMaleficAvoidance(chart);

        // Assert — 100 − (30 base + 20 out-of-sect) = 50, full penalty at 0°05′ orb
        score.Should().Be(50.0,
            $"a malefic 0°05′ on the cadent side of an angle cusp must be scored as angular: "
            + $"100 - (30+20) = 50, got {score}");
    }

    /// <summary>
    /// Calculates a full electional chart for the Doctor Visit live-run
    /// fixtures (Richmond, VA) via the production engine path.
    /// </summary>
    /// <param name="localDateTime">Local civil time in America/New_York.</param>
    /// <returns>The calculated chart with dignities and chart context.</returns>
    private ChartResult CalculateRichmondElectionChart(DateTime localDateTime)
    {
        ChartInput input = new()
        {
            Name = $"Doctor Visit {localDateTime:yyyy-MM-dd HH:mm}",
            LocalDateTime = localDateTime,
            TimeZone = RichmondTimeZone,
            Latitude = RichmondLatitude,
            Longitude = RichmondLongitude,
            LocationName = "Richmond, VA",
            EventType = "Event",
        };

        return _engine.CalculateForElectional(input);
    }

    /// <summary>
    /// Creates a synthetic chart for orb-based angularity tests with explicit
    /// angles, sect (via the Sun's house), and malefic placements. All
    /// non-malefic bodies sit well away from the angles used by these tests.
    /// Dignities and chart context are intentionally left empty/null so the
    /// scorer exercises its sign-table and Sun-house fallbacks.
    /// </summary>
    /// <param name="ascendant">Ascendant longitude in degrees.</param>
    /// <param name="midheaven">Midheaven longitude in degrees.</param>
    /// <param name="sunHouse">
    /// House for the Sun (7–12 = day chart, 1–6 = night chart): its longitude is the middle of that
    /// house, counted in 30° steps from <paramref name="ascendant"/>, since day or night follows the
    /// Sun's longitude.
    /// </param>
    /// <param name="mars">Mars longitude and house.</param>
    /// <param name="saturn">Saturn longitude and house.</param>
    /// <returns>A minimal chart result for malefic-angularity scoring.</returns>
    private static ChartResult CreateChartForAngularityTest(
        double ascendant,
        double midheaven,
        int sunHouse,
        (double Longitude, int House) mars,
        (double Longitude, int House) saturn)
    {
        double sunLongitude = (ascendant + (sunHouse - 1) * 30.0 + 15.0) % 360.0;
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun",       CelestialBodyId.Sun,      sunLongitude, 1.0, sunHouse),
            CreatePosition("Moon",      CelestialBodyId.Moon,      75.0, 13.0, 2),
            CreatePosition("Mercury",   CelestialBodyId.Mercury,  250.0, 1.5,  5),
            CreatePosition("Venus",     CelestialBodyId.Venus,    260.0, 1.2,  5),
            CreatePosition("Mars",      CelestialBodyId.Mars,     mars.Longitude, 0.5, mars.House),
            CreatePosition("Jupiter",   CelestialBodyId.Jupiter,   95.0, 0.1,  5),
            CreatePosition("Saturn",    CelestialBodyId.Saturn,   saturn.Longitude, 0.05, saturn.House),
            CreatePosition("Uranus",    CelestialBodyId.Uranus,   240.0, 0.01, 9),
            CreatePosition("Neptune",   CelestialBodyId.Neptune,  333.0, 0.005, 11),
            CreatePosition("Pluto",     CelestialBodyId.Pluto,     70.0, 0.003, 12),
            CreatePosition("True Node", CelestialBodyId.TrueNode, 100.0, -0.05, 12),
            CreatePosition("Chiron",    CelestialBodyId.Chiron,    50.0, 0.02,  2),
        ];

        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = ascendant,
                Midheaven = midheaven,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #endregion

    #region Mercury Scoring Tests (13–14)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMercury_Should_ScoreHigh_When_DirectInDomicile()
    {
        // Arrange — Mercury direct in Gemini (domicile), not combust
        ChartResult chart = CreateChartForMercuryTest(
            mercuryLongitude: 75.0,   // Gemini = 60–90°
            mercurySpeed: 1.5,        // direct
            sunLongitude: 180.0);     // far from Mercury

        // Act
        double score = ElectionalScorer.ScoreMercury(chart);

        // Assert — base 80 + 25 (domicile) = 100 (clamped)
        score.Should().BeGreaterThanOrEqualTo(95.0,
            "direct Mercury in domicile should score at or near maximum");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ScoreMercury_Should_Penalize_When_Retrograde()
    {
        // Arrange — Mercury retrograde in Pisces (detriment), combust
        ChartResult chart = CreateChartForMercuryTest(
            mercuryLongitude: 340.0,  // Pisces = 330–360°
            mercurySpeed: -1.2,       // retrograde
            sunLongitude: 335.0);     // 5° from Mercury = combust

        // Act
        double score = ElectionalScorer.ScoreMercury(chart);

        // Assert — base 80 - 50 (retro) - 15 (combust) - 20 (detriment) = 0 (clamped)
        score.Should().BeLessThanOrEqualTo(10.0,
            "retrograde combust Mercury in detriment should score very low");
    }

    #endregion

    #region Planetary Hour Scoring Test (15)

    [Fact]
    [Trait("Category", "Integration")]
    public void ScorePlanetaryHour_Should_Score100_When_MatchesSignificator()
    {
        // Arrange — Wedding: significator is Venus, hour ruler is Venus
        PlanetaryHour hour = new()
        {
            RulingPlanet = "Venus",
            HourNumber = 3,
            IsDaytime = true,
            HourStart = DateTime.Now,
            HourEnd = DateTime.Now.AddMinutes(60),
        };

        // Act
        double score = ElectionalScorer.ScorePlanetaryHour(hour, ElectionType.Wedding);

        // Assert
        score.Should().Be(100.0,
            "hour ruler matching the significator should score 100");
    }

    #endregion

    #region Integration Test (16)

    [Fact]
    [Trait("Category", "Integration")]
    public void Score_Should_ReturnValidBreakdown_When_FullChartProvided()
    {
        // Arrange — calculate a real chart for 2026-01-04 10:00 UTC, London
        ChartInput input = new()
        {
            Name = "Electional Test",
            LocalDateTime = new DateTime(2026, 1, 4, 10, 0, 0),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = 0.0,
            LocationName = "London",
            EventType = "Event",
        };

        ChartResult chart = _engine.Calculate(input);

        // Evaluate Moon condition
        double jd = chart.JulianDay;
        VoidOfCourseScanner vocScanner = new(_ephemeris);
        IReadOnlyList<VoidOfCoursePeriod> vocPeriods = vocScanner.Scan(
            jd - 1.0, jd + 1.0, "UTC");
        MoonConditionEvaluator moonEval = new(_ephemeris);
        MoonCondition moonCondition = moonEval.Evaluate(jd, vocPeriods, "UTC");

        // Calculate planetary hour
        PlanetaryHourCalculator hourCalc = new(_ephemeris);
        PlanetaryHour hour = hourCalc.Calculate(jd, 51.5, 0.0, "UTC");

        // Act
        ScoreBreakdown breakdown = _scorer.Score(
            chart, moonCondition, hour, ElectionType.BusinessLaunch);

        // Assert — all dimension scores should be 0–100
        breakdown.MoonScore.Should().BeInRange(0.0, 100.0, "Moon score must be 0–100");
        breakdown.AscendantScore.Should().BeInRange(0.0, 100.0, "Ascendant score must be 0–100");
        breakdown.SignificatorScore.Should().BeInRange(0.0, 100.0, "Significator score must be 0–100");
        breakdown.NatalHarmonyScore.Should().Be(50.0, "no natal chart = neutral 50");
        breakdown.MaleficAvoidanceScore.Should().BeInRange(0.0, 100.0, "Malefic avoidance must be 0–100");
        breakdown.MercuryScore.Should().BeInRange(0.0, 100.0, "Mercury score must be 0–100");
        breakdown.PlanetaryHourScore.Should().BeInRange(0.0, 100.0, "Planetary hour must be 0–100");

        // Overall score should be in range and match weights
        breakdown.OverallScore.Should().BeInRange(0.0, 100.0,
            "overall weighted score must be 0–100");
        breakdown.ElectionTypeName.Should().Be("BusinessLaunch");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a minimal ChartResult with a specific Ascendant longitude.
    /// </summary>
    private static ChartResult CreateChartWithAscendant(double ascendantLongitude)
    {
        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = CreateMinimalPositions(),
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = ascendantLongitude,
                Midheaven = (ascendantLongitude + 270.0) % 360.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a ChartResult with a specific planet at given longitude, speed, and house.
    /// Includes a Sun at 0° Aries (far from most test positions) to avoid
    /// unintended combustion in significator tests.
    /// </summary>
    private static ChartResult CreateChartWithPlanet(
        string planetName, double longitude, double speed, int house)
    {
        List<PlanetPosition> positions = new(CreateMinimalPositions());

        // Find and replace the target planet with specified values
        for (int i = 0; i < positions.Count; i++)
        {
            if (string.Equals(positions[i].Name, planetName, StringComparison.OrdinalIgnoreCase))
            {
                positions[i] = CreatePosition(planetName, positions[i].Body, longitude, speed, house);
                break;
            }
        }

        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0,
                Midheaven = 270.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a ChartResult for malefic avoidance tests with Mars and Saturn
    /// in specified houses.
    /// </summary>
    private static ChartResult CreateChartForMaleficTest(int marsHouse, int saturnHouse)
    {
        List<PlanetPosition> positions = new(CreateMinimalPositions());

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].Name == "Mars")
            {
                positions[i] = CreatePosition("Mars", CelestialBodyId.Mars,
                    120.0, 0.5, marsHouse);
            }
            else if (positions[i].Name == "Saturn")
            {
                positions[i] = CreatePosition("Saturn", CelestialBodyId.Saturn,
                    280.0, 0.1, saturnHouse);
            }
        }

        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0,
                Midheaven = 270.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a ChartResult for Mercury-specific tests with Mercury and Sun
    /// at specified positions.
    /// </summary>
    private static ChartResult CreateChartForMercuryTest(
        double mercuryLongitude, double mercurySpeed, double sunLongitude)
    {
        List<PlanetPosition> positions = new(CreateMinimalPositions());

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].Name == "Mercury")
            {
                positions[i] = CreatePosition("Mercury", CelestialBodyId.Mercury,
                    mercuryLongitude, mercurySpeed, 5);
            }
            else if (positions[i].Name == "Sun")
            {
                positions[i] = CreatePosition("Sun", CelestialBodyId.Sun,
                    sunLongitude, 1.0, 10);
            }
        }

        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0,
                Midheaven = 270.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Creates a minimal test ChartInput.</summary>
    private static ChartInput CreateTestInput() => new()
    {
        Name = "Test",
        LocalDateTime = DateTime.UtcNow,
        TimeZone = "UTC",
        Latitude = 51.5,
        Longitude = 0.0,
        LocationName = "London",
    };

    /// <summary>
    /// Creates a minimal set of 12 planetary positions at default locations.
    /// All planets direct, in non-critical signs, assigned to non-angular houses.
    /// Sun at 0° Aries to avoid unintended combustion.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreateMinimalPositions()
    {
        return
        [
            CreatePosition("Sun",       CelestialBodyId.Sun,       0.0,   1.0,  5),
            CreatePosition("Moon",      CelestialBodyId.Moon,      90.0, 13.0,  2),
            CreatePosition("Mercury",   CelestialBodyId.Mercury,  30.0,  1.5,  5),
            CreatePosition("Venus",     CelestialBodyId.Venus,    60.0,  1.2,  5),
            CreatePosition("Mars",      CelestialBodyId.Mars,    120.0,  0.5,  5),
            CreatePosition("Jupiter",   CelestialBodyId.Jupiter, 150.0,  0.1,  5),
            CreatePosition("Saturn",    CelestialBodyId.Saturn,  210.0,  0.05, 8),
            CreatePosition("Uranus",    CelestialBodyId.Uranus,  240.0,  0.01, 9),
            CreatePosition("Neptune",   CelestialBodyId.Neptune, 270.0,  0.005, 11),
            CreatePosition("Pluto",     CelestialBodyId.Pluto,   300.0,  0.003, 12),
            CreatePosition("True Node", CelestialBodyId.TrueNode, 330.0, -0.05, 12),
            CreatePosition("Chiron",    CelestialBodyId.Chiron,   15.0,  0.02,  2),
        ];
    }

    /// <summary>Creates a single PlanetPosition with specified values.</summary>
    private static PlanetPosition CreatePosition(
        string name, CelestialBodyId body,
        double longitude, double speed, int house)
    {
        PlanetPosition position = new()
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
        position.House = house;
        return position;
    }

    #endregion
}
