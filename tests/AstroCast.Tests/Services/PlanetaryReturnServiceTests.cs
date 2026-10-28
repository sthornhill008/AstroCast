using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="PlanetaryReturnService"/> verifying return calculations
/// for Mercury, Venus, Mars, Jupiter, and Saturn. Uses the shared "Ephemeris"
/// collection fixture for integration testing with real ephemeris data.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PlanetaryReturnServiceTests
{
    private readonly ChartEngine _engine;
    private readonly PlanetaryReturnService _service;
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and PlanetaryReturnService.
    /// </summary>
    public PlanetaryReturnServiceTests(EphemerisFixture fixture)
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
            moonCondition, chartContext, fixedStars, new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger),
            logger);

        _service = new PlanetaryReturnService(
            fixture.EphemerisService, _engine, logger);
        _fixture = fixture;
    }

    /// <summary>
    /// Standard test natal chart: J2000.0 epoch, New York City.
    /// Born Jan 1, 2000. Saturn at ~40° (Taurus ~10°).
    /// </summary>
    private ChartResult CalculateNatalChart(bool includeMinorPoints = false) => _engine.Calculate(new ChartInput
    {
        Name = "Test Person",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
        IncludeMinorPoints = includeMinorPoints,
    });

    #region Saturn Return Tests

    /// <summary>
    /// Saturn return #1 is the first passage of Saturn over its natal longitude after birth, cast
    /// at its first pass, 2028-07-21 (branch F, ruling T-D6): the native is 28. Until branch F the
    /// chart was cast at the passage's last pass, 2029-04-01, and the test accepted 28 to 30.
    /// </summary>
    [Fact]
    public void Calculate_SaturnReturn1_FindsReturnAtAge28AtTheFirstPass()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Should().NotBeNull();
        result.Planet.Should().Be(CelestialBodyId.Saturn);
        result.PlanetName.Should().Be("Saturn");
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().Be(28, "the first pass, 2028-07-21, precedes the 29th birthday");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0, "precision should be sub-arc-second");
        result.ReturnChart.Should().NotBeNull();
        result.ReturnChart.Input.EventType.Should().Be("Saturn Return");
    }

    [Fact]
    public void Calculate_SaturnReturn2_FindsReturnAtApproximatelyAge58()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 2,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnNumber.Should().Be(2);
        result.AgeAtReturn.Should().BeInRange(57, 60, "second Saturn return ~58 years");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region Jupiter Return Tests

    [Fact]
    public void Calculate_JupiterReturn1_FindsReturnAtApproximatelyAge12()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Jupiter, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Planet.Should().Be(CelestialBodyId.Jupiter);
        result.PlanetName.Should().Be("Jupiter");
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().BeInRange(11, 13, "Jupiter return ~12 years");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region Mars Return Tests

    [Fact]
    public void Calculate_MarsReturn1_FindsReturnAtApproximatelyAge2()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Mars, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Planet.Should().Be(CelestialBodyId.Mars);
        result.PlanetName.Should().Be("Mars");
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().BeInRange(1, 3, "Mars return ~2 years");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region Venus Return Tests

    [Fact]
    public void Calculate_VenusReturn1_FindsReturn()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Venus, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Planet.Should().Be(CelestialBodyId.Venus);
        result.PlanetName.Should().Be("Venus");
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().BeInRange(0, 1, "Venus return #1 is its first passage after birth, 2000-10-20");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region Mercury Return Tests

    [Fact]
    public void Calculate_MercuryReturn1_FindsReturn()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Mercury, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Planet.Should().Be(CelestialBodyId.Mercury);
        result.PlanetName.Should().Be("Mercury");
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().BeInRange(0, 1, "Mercury return #1 is its first passage after birth, 2000-12-24");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region CalculateForYear Tests

    /// <summary>
    /// By year, the return is the first passage with a pass whose UT date lies in the year (R3):
    /// Saturn's passage #1 has its third pass on 2029-04-01, so Saturn 2029 is #1, cast at its
    /// first pass, 2028-07-21. Until branch F the search ran from 1 July 2028 to 30 June 2030 and
    /// took the pass nearest 1 July 2029, 2029-04-01; the test accepted 2028 to 2030.
    /// </summary>
    [Fact]
    public void CalculateForYear_SaturnIn2029_IsPassage1AtItsFirstPass()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.CalculateForYear(
            natal, CelestialBodyId.Saturn, 2029,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Should().NotBeNull();
        result.Planet.Should().Be(CelestialBodyId.Saturn);
        result.ReturnNumber.Should().Be(1);
        result.ReturnChart.UtcDateTime.Date.Should().Be(new DateTime(2028, 7, 21));
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    /// <summary>
    /// Mars 2003: passage #2, 2003-06-12. Until branch F this test asked for 2002, which has no
    /// Mars pass (#1 is 2001-12-06, #2 2003-06-12), and the search answered with a pass from
    /// another year (T2-3); by year a year with no pass now throws (F3).
    /// </summary>
    [Fact]
    public void CalculateForYear_MarsIn2003_IsPassage2()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.CalculateForYear(
            natal, CelestialBodyId.Mars, 2003,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.Planet.Should().Be(CelestialBodyId.Mars);
        result.ReturnNumber.Should().Be(2);
        result.ReturnChart.UtcDateTime.Date.Should().Be(new DateTime(2003, 6, 12));
        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void Calculate_AllSupportedPlanets_PrecisionUnderOneArcSecond()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        CelestialBodyId[] planets =
        [
            CelestialBodyId.Mercury,
            CelestialBodyId.Venus,
            CelestialBodyId.Mars,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
        ];

        foreach (CelestialBodyId planet in planets)
        {
            // Act
            PlanetaryReturnResult result = _service.Calculate(
                natal, planet, 1,
                40.7128, -74.006, "New York City", "America/New_York");

            // Assert
            result.PrecisionArcSeconds.Should().BeLessThan(1.0,
                $"{planet} return should be sub-arc-second precision");
        }
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void Calculate_ReturnNumberLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Saturn, 0,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("returnNumber");
    }

    [Theory]
    [InlineData(CelestialBodyId.Sun)]
    [InlineData(CelestialBodyId.Moon)]
    [InlineData(CelestialBodyId.Uranus)]
    [InlineData(CelestialBodyId.Neptune)]
    [InlineData(CelestialBodyId.Pluto)]
    public void Calculate_UnsupportedPlanet_ThrowsArgumentException(CelestialBodyId planet)
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        Action act = () => _service.Calculate(
            natal, planet, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("planet");
    }

    [Fact]
    public void Calculate_NullNatalChart_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _service.Calculate(
            null!, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("natalChart");
    }

    [Fact]
    public void Calculate_NullLocationName_ThrowsArgumentException()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, null!, "America/New_York");

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("locationName");
    }

    [Fact]
    public void Calculate_NullTimeZone_ThrowsArgumentException()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, "New York City", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("timeZone");
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Calculate_ReturnChart_HasCorrectEventType()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.Input.EventType.Should().Be("Saturn Return");
    }

    [Fact]
    public void Calculate_ReturnChart_HasCorrectName()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Mars, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.Input.Name.Should().Contain("Mars Return");
        result.ReturnChart.Input.Name.Should().Contain("Test Person");
    }

    [Fact]
    public void Calculate_EngineVersion_IsNotEmpty()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.EngineVersion.Should().NotBeNullOrWhiteSpace();
        result.EngineVersion.Should().Contain("AstroCast");
    }

    [Fact]
    public void Calculate_CalculatedAt_IsRecentUtc()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime before = DateTime.UtcNow;

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Mercury, 1,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.CalculatedAt.Should().BeOnOrAfter(before);
        result.CalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Calculate_ReturnLocation_MatchesInput()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1,
            51.5074, -0.1278, "London", "Europe/London");

        // Assert
        result.ReturnLocationName.Should().Be("London");
        result.ReturnLatitude.Should().BeApproximately(51.5074, 0.001);
        result.ReturnLongitude_Geo.Should().BeApproximately(-0.1278, 0.001);
    }

    #endregion

    #region CalculateForYear Guard Clause Tests

    [Fact]
    public void CalculateForYear_UnsupportedPlanet_ThrowsArgumentException()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        Action act = () => _service.CalculateForYear(
            natal, CelestialBodyId.Moon, 2025,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("planet");
    }

    [Fact]
    public void CalculateForYear_NullNatalChart_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _service.CalculateForYear(
            null!, CelestialBodyId.Saturn, 2029,
            40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("natalChart");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="PlanetaryReturnService.CalculateForYear"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any return search runs.
    /// </summary>
    [Fact]
    public void CalculateForYear_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ChartResult natal = CalculateNatalChart();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.CalculateForYear(
            natal, CelestialBodyId.Saturn, 2029,
            40.7128, -74.006, "New York City", "America/New_York",
            cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the planetary return search immediately");
    }

    #endregion
    #region Branch F — passages and their passes (F1–F3)

    /// <summary>Tolerance on every Tier B instant.</summary>
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    /// <summary>An instant in UT.</summary>
    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    /// <summary>A return of the J2000 NYC fixture chart, cast at the natal place.</summary>
    private PlanetaryReturnResult J2000Return(CelestialBodyId planet, int returnNumber) =>
        _service.Calculate(
            GoldenFixtures.J2000Nyc(_fixture), planet, returnNumber,
            40.7128, -74.006, "New York City", "America/New_York");

    /// <summary>
    /// F1 (R1, R2, R4). Saturn is retrograde at the J2000 NYC birth, so its crossing of
    /// 2000-01-22 belongs to the birth's own passage; return #1 is the passage of 2028, cast at its
    /// first pass, 2028-07-21 08:08:04 UT, with its retrograde pass 2028-09-24 18:23:07 and its
    /// last pass 2029-04-01 21:20:28 (Tier B); the native is 28. Until branch F the chart was cast
    /// at the last pass and the age read 29.
    /// </summary>
    [Fact]
    public void Calculate_SaturnReturn1_IsThePassageOf2028CastAtItsFirstPass()
    {
        PlanetaryReturnResult result = J2000Return(CelestialBodyId.Saturn, 1);

        DateTime firstPass = Utc(2028, 7, 21, 8, 8, 4);
        result.ReturnNumber.Should().Be(1);
        result.AgeAtReturn.Should().Be(28);
        result.ReturnChart.UtcDateTime.Should().BeCloseTo(firstPass, FiveSeconds);
        result.ReturnChart.JulianDay.Should().BeApproximately(
            JulianDayUtility.InstantToJulianDay(firstPass), 5.0 / 86400.0);

        result.Passes.Should().HaveCount(3);
        result.Passes[0].Should().BeCloseTo(firstPass, FiveSeconds);
        result.Passes[1].Should().BeCloseTo(Utc(2028, 9, 24, 18, 23, 7), FiveSeconds);
        result.Passes[2].Should().BeCloseTo(Utc(2029, 4, 1, 21, 20, 28), FiveSeconds);
    }

    /// <summary>
    /// F2 (R1, R2). Mercury returns about once a year as seen from the Earth: #1 to #6 of the J2000
    /// NYC fixture fall in December or January of 2000 to 2006, and #4 holds a retrograde loop over
    /// the degree, three passes (Tier B). Until branch F the search centred on birth + N × 88 days,
    /// so all six lay within two years of birth, 88 days apart (T2-2).
    /// </summary>
    [Fact]
    public void Calculate_MercuryReturns1To6_AreOneAYearWithNumber4InThreePasses()
    {
        (int Number, DateTime FirstPass, int PassCount)[] expected =
        [
            (1, Utc(2000, 12, 24, 6, 40, 13), 1),
            (2, Utc(2001, 12, 17, 0, 35, 17), 1),
            (3, Utc(2002, 12, 10, 2, 6, 52), 1),
            (4, Utc(2003, 12, 4, 8, 35, 50), 3),
            (5, Utc(2005, 1, 11, 12, 56, 32), 1),
            (6, Utc(2006, 1, 5, 3, 21, 4), 1),
        ];

        foreach ((int number, DateTime firstPass, int passCount) in expected)
        {
            PlanetaryReturnResult result = J2000Return(CelestialBodyId.Mercury, number);

            result.ReturnNumber.Should().Be(number);
            result.ReturnChart.UtcDateTime.Should().BeCloseTo(firstPass, FiveSeconds, $"return #{number}");
            result.Passes.Should().HaveCount(passCount, $"return #{number}");
            result.Passes[0].Should().BeCloseTo(firstPass, FiveSeconds, $"return #{number}");
        }

        PlanetaryReturnResult fourth = J2000Return(CelestialBodyId.Mercury, 4);
        fourth.Passes[1].Should().BeCloseTo(Utc(2003, 12, 29, 5, 58, 15), FiveSeconds);
        fourth.Passes[2].Should().BeCloseTo(Utc(2004, 1, 16, 13, 12, 0), FiveSeconds);
    }

    /// <summary>
    /// F3 (R3). By year, the return is the first passage with a pass whose UT date lies in the
    /// year: Mercury 2004 is #4 (its third pass, 2004-01-16; first pass 2003-12-04 08:35:50),
    /// Saturn 2029 is #1 (its third pass, 2029-04-01; first pass 2028-07-21 08:08:04), and Mars
    /// 2002 has none: #1 is 2001-12-06 and #2 2003-06-12, and the exception names both. Until
    /// branch F the search ran from 1 July before to 30 June after and took the pass nearest 1
    /// July: Saturn 2029 was 2029-04-01 and Mars 2002 answered with a pass from another year.
    /// </summary>
    [Fact]
    public void CalculateForYear_IsTheFirstPassageWithAPassInTheYear()
    {
        ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);

        PlanetaryReturnResult mercury2004 = _service.CalculateForYear(
            natal, CelestialBodyId.Mercury, 2004, 40.7128, -74.006, "New York City", "America/New_York");
        mercury2004.ReturnNumber.Should().Be(4);
        mercury2004.ReturnChart.UtcDateTime.Should().BeCloseTo(Utc(2003, 12, 4, 8, 35, 50), FiveSeconds);

        PlanetaryReturnResult saturn2029 = _service.CalculateForYear(
            natal, CelestialBodyId.Saturn, 2029, 40.7128, -74.006, "New York City", "America/New_York");
        saturn2029.ReturnNumber.Should().Be(1);
        saturn2029.ReturnChart.UtcDateTime.Should().BeCloseTo(Utc(2028, 7, 21, 8, 8, 4), FiveSeconds);

        Action mars2002 = () => _service.CalculateForYear(
            natal, CelestialBodyId.Mars, 2002, 40.7128, -74.006, "New York City", "America/New_York");
        mars2002.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("2001-12-06").And.Contain("2003-06-12");
    }

    #endregion

    #region Minor points (branch Asteroids, 1.6.0)

    /// <summary>
    /// The return chart carries its own five when the natal chart's switch is on (rule 6): the return input copies
    /// <see cref="ChartInput.IncludeMinorPoints"/>, so the five are the return instant's, equal to a chart the
    /// engine casts at the return chart's Julian Day with the switch on, and not the natal chart's.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_ReturnChartCarriesItsOwnFive()
    {
        ChartResult natal = CalculateNatalChart(includeMinorPoints: true);

        PlanetaryReturnResult result = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1, 40.7128, -74.006, "New York City", "America/New_York");

        ChartResult returnChart = result.ReturnChart;
        returnChart.Input.IncludeMinorPoints.Should().BeTrue("the return input copies the natal switch");
        returnChart.MinorPoints.Select(p => p.Name).Should().Equal("Ceres", "Pallas", "Juno", "Vesta", "Lilith");

        ChartResult atTheReturn = _engine.CalculateAtInstant(returnChart.Input, returnChart.JulianDay);
        for (int i = 0; i < 5; i++)
        {
            string because = $"of {returnChart.MinorPoints[i].Name}";
            returnChart.MinorPoints[i].Longitude.Should().BeApproximately(
                atTheReturn.MinorPoints[i].Longitude, 1e-9, because);
            returnChart.MinorPoints[i].Longitude.Should().NotBeApproximately(
                natal.MinorPoints[i].Longitude, 0.5, because + ": the return's own sky, not the natal chart's");
        }
    }

    #endregion
}
