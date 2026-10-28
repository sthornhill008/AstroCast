using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="CompositeService"/> verifying short-arc midpoint calculation,
/// composite chart construction, house derivation, and the complete composite pipeline.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class CompositeServiceTests
{
    private readonly ChartEngine _engine;
    private readonly CompositeService _compositeService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and CompositeService.
    /// </summary>
    public CompositeServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        MoonConditionService moonCondition = new();
        ChartContextService chartContext = new(dignity, new SectService());
        FixedStarService fixedStars = new(fixture.EphemerisService, logger);

        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        _compositeService = new CompositeService(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, logger);
    }

    /// <summary>
    /// Standard J2000.0 test input for New York City (Chart A).
    /// </summary>
    private static ChartInput CreateChartAInput() => new()
    {
        Name = "Person A",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    };

    /// <summary>
    /// Second test input for London (Chart B).
    /// </summary>
    private static ChartInput CreateChartBInput() => new()
    {
        Name = "Person B",
        LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
        TimeZone = "Europe/London",
        Latitude = 51.5074,
        Longitude = -0.1278,
        LocationName = "London, UK",
        EventType = "Birth",
    };

    /// <summary>The golden pair's first chart: J2000 NYC, 2000-01-01 07:00 America/New_York, Placidus.</summary>
    private static ChartInput GoldenPairA() => new()
    {
        Name = "J2000 NYC Fixture",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>The golden pair's second chart: Secondary, 1970-06-15 12:00 America/Los_Angeles, Placidus.</summary>
    private static ChartInput GoldenPairB() => new()
    {
        Name = "Secondary Fixture",
        LocalDateTime = new DateTime(1970, 6, 15, 12, 0, 0),
        TimeZone = "America/Los_Angeles",
        Latitude = 34.0522,
        Longitude = -118.2437,
        LocationName = "Los Angeles",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>A copy of an input in another house system.</summary>
    private static ChartInput WithHouseSystem(ChartInput input, HouseSystem houseSystem) => new()
    {
        Name = input.Name,
        LocalDateTime = input.LocalDateTime,
        TimeZone = input.TimeZone,
        Latitude = input.Latitude,
        Longitude = input.Longitude,
        LocationName = input.LocationName,
        EventType = input.EventType,
        HouseSystem = houseSystem,
    };

    /// <summary>
    /// Calculate both charts and return the composite result.
    /// </summary>
    private CompositeResult CalculateTestComposite()
    {
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());
        return _compositeService.Calculate(chartA, chartB);
    }

    #region Short-Arc Midpoint Algorithm Tests

    [Fact]
    public void ShortArcMidpoint_SameSign_ReturnsMidpoint()
    {
        // 10° Ari + 20° Ari → 15° Ari
        double result = CompositeService.ShortArcMidpoint(10.0, 20.0);
        result.Should().BeApproximately(15.0, 0.0001,
            "midpoint of 10° and 20° should be 15°");
    }

    [Fact]
    public void ShortArcMidpoint_CrossZero_ReturnsCorrectMidpoint()
    {
        // 350° + 10° → 0° (not 180°)
        double result = CompositeService.ShortArcMidpoint(350.0, 10.0);
        result.Should().BeApproximately(0.0, 0.0001,
            "midpoint of 350° and 10° should be 0° (crossing 0° Aries)");
    }

    [Fact]
    public void ShortArcMidpoint_Opposite_ReturnsValidMidpoint()
    {
        // 0° + 180° → either 90° or 270° (both arcs are equal length, so both midpoints are valid)
        double result = CompositeService.ShortArcMidpoint(0.0, 180.0);
        bool isValid = Math.Abs(result - 90.0) < 0.0001 || Math.Abs(result - 270.0) < 0.0001;
        isValid.Should().BeTrue(
            "midpoint of 0° and 180° should be 90° or 270° (both arcs are equal length)");
    }

    [Fact]
    public void ShortArcMidpoint_NearOpposite_ReturnsCorrectArc()
    {
        // 1° + 179° → 90°
        double result = CompositeService.ShortArcMidpoint(1.0, 179.0);
        result.Should().BeApproximately(90.0, 0.0001,
            "midpoint of 1° and 179° should be 90°");
    }

    [Fact]
    public void ShortArcMidpoint_Identical_ReturnsSamePosition()
    {
        // 100° + 100° → 100°
        double result = CompositeService.ShortArcMidpoint(100.0, 100.0);
        result.Should().BeApproximately(100.0, 0.0001,
            "midpoint of identical positions should be the same position");
    }

    [Fact]
    public void ShortArcMidpoint_ReversedInputs_ReturnsSameResult()
    {
        // Midpoint should be the same regardless of input order
        double result1 = CompositeService.ShortArcMidpoint(30.0, 60.0);
        double result2 = CompositeService.ShortArcMidpoint(60.0, 30.0);
        result1.Should().BeApproximately(result2, 0.0001,
            "midpoint should be identical regardless of input order");
    }

    [Fact]
    public void ShortArcMidpoint_CrossZeroReversed_ReturnsCorrectMidpoint()
    {
        // 10° + 350° → 0° (same as 350° + 10°)
        double result = CompositeService.ShortArcMidpoint(10.0, 350.0);
        result.Should().BeApproximately(0.0, 0.0001,
            "midpoint of 10° and 350° should be 0°");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Calculate_TwoCharts_ProducesValidResult()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.CompositeChart.Positions.Should().HaveCount(12,
            "composite chart should have 12 planetary positions");
        result.CompositeChart.Houses.Cusps.Should().HaveCount(12,
            "composite chart should have 12 house cusps");
        result.CompositeChart.Aspects.Should().NotBeEmpty(
            "composite chart should have at least one aspect");
        result.CompositeChart.MidpointActivations.Should().NotBeEmpty(
            "composite chart should have midpoint activations");
    }

    [Fact]
    public void Calculate_CompositePositions_AreBetweenSourcePositions()
    {
        // Arrange
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert — each composite position should be a valid longitude
        result.CompositeChart.Positions.Should().AllSatisfy(p =>
        {
            p.Longitude.Should().BeInRange(0.0, 360.0,
                $"composite {p.Name} longitude should be in valid range");
        });
    }

    [Fact]
    public void Calculate_Houses_AreValidLongitudes()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert — cusps should be valid longitudes, ordered in ecliptic sequence
        result.CompositeChart.Houses.Cusps.Should().AllSatisfy(c =>
            c.Should().BeInRange(0.0, 360.0,
                "each house cusp should be a valid longitude"));

        result.CompositeChart.Houses.Ascendant.Should().BeInRange(0.0, 360.0,
            "composite ASC should be a valid longitude");
        result.CompositeChart.Houses.Midheaven.Should().BeInRange(0.0, 360.0,
            "composite MC should be a valid longitude");
    }

    [Fact]
    public void Calculate_NullChartA_ThrowsArgumentNull()
    {
        // Arrange
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        // Act & Assert
        Action act = () => _compositeService.Calculate(null!, chartB);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chartA");
    }

    [Fact]
    public void Calculate_NullChartB_ThrowsArgumentNull()
    {
        // Arrange
        ChartResult chartA = _engine.Calculate(CreateChartAInput());

        // Act & Assert
        Action act = () => _compositeService.Calculate(chartA, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chartB");
    }

    /// <summary>A guard: the reference latitude is the mean of the two birth latitudes, always (S-D3).</summary>
    [Fact]
    public void Calculate_ReferenceLatitude_IsTheMeanBirthLatitude()
    {
        // Arrange
        ChartInput inputA = CreateChartAInput();
        ChartInput inputB = CreateChartBInput();
        ChartResult chartA = _engine.Calculate(inputA);
        ChartResult chartB = _engine.Calculate(inputB);

        double expectedMidLat = (inputA.Latitude + inputB.Latitude) / 2.0;

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert
        result.ReferenceLatitude.Should().BeApproximately(expectedMidLat, 0.0001,
            "should use the midpoint of both charts' latitudes");
    }

    /// <summary>
    /// The composite MC is the short-arc midpoint of the two Midheavens, and the Ascendant and cusps are cast
    /// for it at the mean of the two birth latitudes in the shared house system (ruling S-D3, Q1; branch L3,
    /// 1.3.4). The golden pair, J2000 NYC and Secondary (Tier B, l3_truth.out): Midheavens 28°28′08″ Libra and
    /// 11°54′29″ Gemini, their midpoint 140.188601° = 20°11′18″ Leo, its right ascension 142.594671° at the
    /// midpoint obliquity 23.442991°, and swe_houses_armc at 37.3825° N, Placidus. Until 1.3.4 the houses came
    /// from the midpoint of the two ARMCs, neither published method (the MC up to 4.9° from the midpoint of
    /// the two MCs; here 15°57′55″ Leo), at Person A's house system (Y2-3, Y2-4).
    /// </summary>
    [Fact]
    public void Calculate_Angles_AreTheMidpointMidheavenAtTheMeanLatitude()
    {
        ChartResult j2000 = _engine.Calculate(GoldenPairA());
        ChartResult secondary = _engine.Calculate(GoldenPairB());

        CompositeResult result = _compositeService.Calculate(j2000, secondary);
        HouseData houses = result.CompositeChart.Houses;

        houses.Midheaven.Should().BeApproximately(140.188601, 0.0001);
        houses.Ascendant.Should().BeApproximately(222.685164, 0.0001);
        houses.Armc.Should().BeApproximately(142.594671, 0.0001);
        double[] expectedCusps =
        [
            222.685164, 251.968608, 285.127207, 320.188601, 352.735888, 20.100056,
            42.685164, 71.968608, 105.127207, 140.188601, 172.735888, 200.100056,
        ];
        houses.Cusps.Should().HaveCount(12);
        for (int i = 0; i < 12; i++)
        {
            houses.Cusps[i].Should().BeApproximately(expectedCusps[i], 0.0001, "cusp {0}", i + 1);
        }

        result.ReferenceLatitude.Should().Be(37.3825);
        result.CompositeChart.Input.HouseSystem.Should().Be(HouseSystem.Placidus);
        result.CompositeChart.HouseSystemName.Should().Be("Placidus");
    }

    /// <summary>A guard: A &amp; B and B &amp; A are one composite — the same bodies, angles and cusps (S-D3).</summary>
    [Fact]
    public void Calculate_IsIndependentOfTheOrder()
    {
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        ChartResult ab = _compositeService.Calculate(chartA, chartB).CompositeChart;
        ChartResult ba = _compositeService.Calculate(chartB, chartA).CompositeChart;

        foreach (PlanetPosition body in ab.Positions)
        {
            ba.Positions.Single(p => p.Body == body.Body).Longitude.Should().BeApproximately(body.Longitude, 1e-9, "{0}", body.Name);
        }

        ba.Houses.Ascendant.Should().BeApproximately(ab.Houses.Ascendant, 1e-9);
        ba.Houses.Midheaven.Should().BeApproximately(ab.Houses.Midheaven, 1e-9);
        ba.Houses.Armc.Should().BeApproximately(ab.Houses.Armc, 1e-9);
        for (int i = 0; i < 12; i++)
        {
            ba.Houses.Cusps[i].Should().BeApproximately(ab.Houses.Cusps[i], 1e-9, "cusp {0}", i + 1);
        }

        ba.HouseSystemName.Should().Be(ab.HouseSystemName);
        _compositeService.Calculate(chartB, chartA).ReferenceLatitude.Should().Be(
            _compositeService.Calculate(chartA, chartB).ReferenceLatitude);
    }

    /// <summary>
    /// The composite's houses are in the house system both charts share, else Placidus (Q1). Until 1.3.4 they
    /// were in chart A's, so A &amp; B and B &amp; A could differ in system.
    /// </summary>
    [Fact]
    public void Calculate_HouseSystem_IsTheSharedOne_ElsePlacidus()
    {
        ChartResult kochA = _engine.Calculate(WithHouseSystem(CreateChartAInput(), HouseSystem.Koch));
        ChartResult kochB = _engine.Calculate(WithHouseSystem(CreateChartBInput(), HouseSystem.Koch));
        ChartResult placidusB = _engine.Calculate(CreateChartBInput());

        CompositeResult shared = _compositeService.Calculate(kochA, kochB);
        shared.CompositeChart.Input.HouseSystem.Should().Be(HouseSystem.Koch);
        shared.CompositeChart.HouseSystemName.Should().Be("Koch");

        CompositeResult mixed = _compositeService.Calculate(kochA, placidusB);
        mixed.CompositeChart.Input.HouseSystem.Should().Be(HouseSystem.Placidus);
        mixed.CompositeChart.HouseSystemName.Should().Be("Placidus");
    }

    /// <summary>
    /// Mercury and Venus take the midpoint nearer the composite Sun (ruling S-D2; branch L3, 1.3.4). The golden
    /// pair (l3_truth.out): the composite Sun is 02°18′27″ Aries; Mercury's short-arc midpoint, 17°35′27″ Pisces,
    /// is 14.72° from it and stays; Venus's, 00°13′17″ Libra, is 177.91° from it, and the far-side midpoint,
    /// 00°13′17″ Aries, 2.09°: Venus takes it. Until 1.3.4 every body took its own short-arc midpoint, so the
    /// composite Venus landed opposite the composite Sun in 8 of 78 saved pairs (Y2-2).
    /// </summary>
    [Fact]
    public void Calculate_VenusOppositeTheCompositeSun_TakesTheMidpointNearerTheSun()
    {
        ChartResult j2000 = _engine.Calculate(GoldenPairA());
        ChartResult secondary = _engine.Calculate(GoldenPairB());

        CompositeResult result = _compositeService.Calculate(j2000, secondary);

        result.CompositeChart.Positions.Single(p => p.Name == "Venus").Longitude.Should().BeApproximately(0.221389, 0.0005);
        result.CompositeChart.Positions.Single(p => p.Name == "Mercury").Longitude.Should().BeApproximately(347.590833, 0.0005);
        result.SunSideBodies.Should().Equal("Venus");
    }

    [Fact]
    public void Calculate_Speeds_AreAveraged()
    {
        // Arrange
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert — verify Sun speed is average of both charts' Sun speeds
        PlanetPosition sunA = chartA.Positions.First(p => p.Name == "Sun");
        PlanetPosition sunB = chartB.Positions.First(p => p.Name == "Sun");
        PlanetPosition sunComposite = result.CompositeChart.Positions.First(p => p.Name == "Sun");

        double expectedSpeed = (sunA.SpeedInLongitude + sunB.SpeedInLongitude) / 2.0;
        sunComposite.SpeedInLongitude.Should().BeApproximately(expectedSpeed, 0.0001,
            "composite Sun speed should be the average of both charts' Sun speeds");
    }

    /// <summary>
    /// A composite body is a midpoint, not a point in the sky: it carries no declination and no right
    /// ascension, though both source charts' Suns carry both (ruling S-D4, branch L2, 1.3.4). Until 1.3.4
    /// the composite's were the means of the two charts' values, off by more than 1° for 1,156 of 1,920
    /// bodies, with the out-of-bounds flags and clusters read from them (Y2-5).
    /// </summary>
    [Fact]
    public void Calculate_CompositeBodies_CarryNoDeclinationOrRightAscension()
    {
        // Arrange
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert — the premise: both source Suns carry equatorial coordinates
        PlanetPosition sunA = chartA.Positions.First(p => p.Name == "Sun");
        PlanetPosition sunB = chartB.Positions.First(p => p.Name == "Sun");
        sunA.Declination.Should().NotBeNull();
        sunA.RightAscension.Should().NotBeNull();
        sunB.Declination.Should().NotBeNull();
        sunB.RightAscension.Should().NotBeNull();

        // — and no composite body does
        result.CompositeChart.Positions.Should().HaveCount(12);
        result.CompositeChart.Positions.Should().OnlyContain(
            p => p.Declination == null && p.RightAscension == null,
            "a composite body is a midpoint, not a point in the sky");
    }

    [Fact]
    public void Calculate_FixedStars_CheckedAtMidpointJD()
    {
        // Arrange
        ChartResult chartA = _engine.Calculate(CreateChartAInput());
        ChartResult chartB = _engine.Calculate(CreateChartBInput());

        double expectedMidpointJd = (chartA.JulianDay + chartB.JulianDay) / 2.0;

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert — the composite chart JD should be the midpoint of both charts
        result.CompositeChart.JulianDay.Should().BeApproximately(expectedMidpointJd, 0.0001,
            "composite chart JD should be the midpoint of both charts' JDs");
    }

    [Fact]
    public void Calculate_SetEngineVersion_When_Calculated()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.EngineVersion.Should().NotBeNullOrEmpty(
            "EngineVersion should contain version information");
        result.EngineVersion.Should().Contain("AstroCast",
            "EngineVersion should include the application name");
    }

    [Fact]
    public void Calculate_SetCalculatedAt_When_Calculated()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.CalculatedAt.Should().BeCloseTo(before, TimeSpan.FromMinutes(1),
            "CalculatedAt should be within 1 minute of the current time");
    }

    [Fact]
    public void Calculate_PreservesSourceCharts()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.ChartA.Input.Name.Should().Be("Person A");
        result.ChartB.Input.Name.Should().Be("Person B");
        result.ChartA.Positions.Should().HaveCount(12);
        result.ChartB.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_CompositeInput_HasCompositeEventType()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.CompositeChart.Input.EventType.Should().Be("Composite",
            "composite chart should have EventType 'Composite'");
        result.CompositeChart.Input.Name.Should().Contain("Person A");
        result.CompositeChart.Input.Name.Should().Contain("Person B");
    }

    [Fact]
    public void Calculate_Dignities_ComputedForComposite()
    {
        // Arrange & Act
        CompositeResult result = CalculateTestComposite();

        // Assert
        result.CompositeChart.Dignities.Should().NotBeEmpty(
            "composite chart should have dignity information");
    }

    [Fact]
    public void Calculate_Dignities_LabelSunInItsOwnFaceAsFace()
    {
        // Arrange — persons A and B (invented, branch Fixtures): person A 1947-10-05 09:30 at an
        // explicit UTC−5, Harrisburg, Pennsylvania; person B 1964-12-05 14:30 America/New_York,
        // Albany, New York (fixtures/truth/persons.py).
        ChartResult chartA = _engine.Calculate(new ChartInput
        {
            Name = "Eleanor Hartwell",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0, DateTimeKind.Unspecified),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania, United States",
            EventType = "Birth",
        });
        ChartResult chartB = _engine.Calculate(new ChartInput
        {
            Name = "Margaret Whitfield",
            LocalDateTime = new DateTime(1964, 12, 5, 14, 30, 0, DateTimeKind.Unspecified),
            TimeZone = "America/New_York",
            Latitude = 42.6526,
            Longitude = -73.7562,
            LocationName = "Albany, New York, United States",
            EventType = "Birth",
        });

        // Act
        CompositeResult result = _compositeService.Calculate(chartA, chartB);

        // Assert — the composite Sun is the short-arc midpoint of 191.500148 and 253.714020,
        // 222.6071 (12°36′ Scorpio; fixtures/truth/persons.py). Lilly, Christian Astrology (1647),
        // Book I, Table of the Essential Dignities of the Planets: the faces of Scorpio are Mars,
        // the Sun and Venus, so 10°–20° Scorpio is the Sun's own face. No other dignity or
        // debility applies.
        result.CompositeChart.Positions.Single(p => p.Name == "Sun").Longitude
            .Should().BeApproximately(222.6071, 1e-4);
        DignityInfo sun = result.CompositeChart.Dignities.Single(d => d.PlanetName == "Sun");
        sun.Label.Should().Be("Face");
        sun.Score.Should().Be(1);
    }

    #endregion

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>J2000 NYC with the Secondary fixture, each chart with the switch as given.</summary>
    /// <param name="chartAOn">Whether chart A, J2000 NYC, carries the five.</param>
    /// <param name="chartBOn">Whether chart B, the Secondary fixture, carries the five.</param>
    /// <returns>The composite.</returns>
    private CompositeResult CalculateGoldenPair(bool chartAOn, bool chartBOn) =>
        _compositeService.Calculate(
            _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: chartAOn)),
            _engine.Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints: chartBOn)));

    /// <summary>
    /// The composite five are Tier B's composite points list (ast_truth_export.py): per point in R1's order the
    /// short-arc midpoint of the two longitudes with no Sun-side rule, the latitude and speed the means, the house by
    /// longitude between the composite cusps, retrograde when the mean speed is negative (Pallas); each within 1e-6.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_CompositeMinorPointsMatchTierB()
    {
        List<MinorPointTierB.CompositePointRow> rows = MinorPointTierB.LoadCompositePoints();
        rows.Select(r => r.Body).Should().Equal(MinorPointTierB.Names);

        CompositeResult result = CalculateGoldenPair(chartAOn: true, chartBOn: true);
        ChartResult composite = result.CompositeChart;

        composite.Input.IncludeMinorPoints.Should().BeTrue("the composite input says the chart carries them");
        composite.Positions.Should().HaveCount(12, "the five never join the positions (R3)");
        composite.MinorPoints.Select(p => p.Name).Should().Equal(MinorPointTierB.Names);
        for (int i = 0; i < rows.Count; i++)
        {
            MinorPointTierB.CompositePointRow row = rows[i];
            PlanetPosition point = composite.MinorPoints[i];
            string because = $"of {row.Body}";

            MinorPointTierB.WrappedDifference(point.Longitude, row.Longitude).Should().BeLessThanOrEqualTo(MinorPointTierB.Tolerance, because);
            point.Latitude.Should().BeApproximately(row.Latitude, MinorPointTierB.Tolerance, because);
            point.SpeedInLongitude.Should().BeApproximately(row.Speed, MinorPointTierB.Tolerance, because);
            point.House.Should().Be(row.House, because);
            point.IsRetrograde.Should().Be(row.Retrograde, because);
        }
    }

    /// <summary>
    /// The composite five's aspects are Tier B's composite aspects list row for row: to the composite Sun to Pluto
    /// (Mercury and Venus where the composite put them), the Ascendant and the Midheaven, at 2° inclusive, sorted
    /// by orb; four rows, Ceres sextile Saturn at 0.2132° the tightest, every orb within 1e-6°.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_CompositeMinorPointAspectsMatchTierB()
    {
        List<MinorPointTierB.CompositeAspectRow> rows = MinorPointTierB.LoadCompositeAspects();
        rows.Should().HaveCount(4, "the list holds the four aspects the export summary reports");

        CompositeResult result = CalculateGoldenPair(chartAOn: true, chartBOn: true);

        result.CompositeChart.MinorPointAspects.Should().HaveCount(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            MinorPointTierB.CompositeAspectRow row = rows[i];
            AspectResult aspect = result.CompositeChart.MinorPointAspects[i];
            string because = $"of row {i + 1}, {row.Point} {row.Aspect} {row.Target}";

            aspect.Body1Name.Should().Be(row.Point, because);
            aspect.Aspect.Type.Should().Be(row.Aspect, because);
            aspect.Body2Name.Should().Be(row.Target, because);
            aspect.Orb.Should().BeApproximately(row.Orb, MinorPointTierB.OrbTolerance, because);
        }
    }

    /// <summary>A composite minor point is a midpoint, not a point in the sky (S-D4): no right ascension, no declination.</summary>
    [Fact]
    public void Calculate_SwitchOn_CompositeMinorPointsCarryNoDeclinationOrRightAscension()
    {
        CompositeResult result = CalculateGoldenPair(chartAOn: true, chartBOn: true);

        result.CompositeChart.MinorPoints.Should().HaveCount(5);
        result.CompositeChart.MinorPoints.Should().AllSatisfy(p =>
        {
            p.RightAscension.Should().BeNull();
            p.Declination.Should().BeNull();
            p.Altitude.Should().BeNull();
        });
    }

    /// <summary>
    /// Both lists are empty, and the composite input's switch off, unless both charts carry the five; the twelve
    /// composite bodies are the same whichever charts do (R9).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOffOnEitherChart_BothListsEmpty()
    {
        CompositeResult bothOff = CalculateGoldenPair(chartAOn: false, chartBOn: false);
        CompositeResult onlyAOn = CalculateGoldenPair(chartAOn: true, chartBOn: false);
        CompositeResult onlyBOn = CalculateGoldenPair(chartAOn: false, chartBOn: true);
        CompositeResult bothOn = CalculateGoldenPair(chartAOn: true, chartBOn: true);

        foreach (CompositeResult result in new[] { bothOff, onlyAOn, onlyBOn })
        {
            result.CompositeChart.MinorPoints.Should().BeEmpty();
            result.CompositeChart.MinorPointAspects.Should().BeEmpty();
            result.CompositeChart.Input.IncludeMinorPoints.Should().BeFalse();
        }

        bothOn.CompositeChart.MinorPoints.Should().HaveCount(5);
        foreach (CompositeResult result in new[] { onlyAOn, onlyBOn, bothOn })
        {
            result.CompositeChart.Positions.Select(p => (p.Name, p.Longitude, p.House))
                .Should().Equal(bothOff.CompositeChart.Positions.Select(p => (p.Name, p.Longitude, p.House)));
            result.CompositeChart.Aspects.Should().HaveCount(bothOff.CompositeChart.Aspects.Count);
        }
    }

    #endregion
}
