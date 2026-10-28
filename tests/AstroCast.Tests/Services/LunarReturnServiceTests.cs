using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="LunarReturnService"/> verifying the binary search algorithm,
/// return precision, multiple-returns-per-year behavior, and the complete lunar
/// return pipeline. Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class LunarReturnServiceTests
{
    private readonly ChartEngine _engine;
    private readonly LunarReturnService _lunarReturnService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and LunarReturnService.
    /// </summary>
    public LunarReturnServiceTests(EphemerisFixture fixture)
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

        _lunarReturnService = new LunarReturnService(
            fixture.EphemerisService, _engine, logger);
    }

    /// <summary>
    /// Standard test natal chart: J2000.0 epoch, New York City.
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

    /// <summary>
    /// Natal chart with Moon near 0° Aries (boundary test).
    /// </summary>
    private ChartResult CalculateNatalChartMoonNear0Aries() => _engine.Calculate(new ChartInput
    {
        Name = "Aries Moon",
        LocalDateTime = new DateTime(2000, 4, 4, 12, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>
    /// Natal chart with Moon near 29° Pisces (~359°, near boundary from other side).
    /// </summary>
    private ChartResult CalculateNatalChartMoonNear29Pisces() => _engine.Calculate(new ChartInput
    {
        Name = "Pisces Moon",
        LocalDateTime = new DateTime(2000, 4, 2, 6, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    #region Core Calculation Tests

    [Fact]
    public void Calculate_RangeStartInTheTenDays_IsRefused()
    {
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(1582, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(1582, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        Action act = () => _lunarReturnService.Calculate(natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // A lunar-return range's typed bounds are read once on the calendar in force (branch R3,
        // 1.3.4; R-D7): a bound in 1582-10-05 to 1582-10-14 never occurred and the search is refused.
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*1582-10-08 does not exist*");
    }

    [Fact]
    public void Calculate_OneMonth_FindsOneReturn()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().HaveCount(1,
            "a ~30-day window should contain exactly 1 lunar return");
    }

    [Fact]
    public void Calculate_FullYear_FindsApprox13Returns()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().HaveCountGreaterThanOrEqualTo(12)
            .And.HaveCountLessThanOrEqualTo(14,
                "a full year should contain approximately 13 lunar returns");
    }

    [Fact]
    public void Calculate_ReturnMoonLongitude_MatchesNatal()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        foreach (LunarReturnResult result in results)
        {
            double diffArcSec = Math.Abs(
                ReturnChartFinder.SignedLongitudeDelta(
                    result.ReturnMoonLongitude, result.NatalMoonLongitude)) * 3600.0;

            diffArcSec.Should().BeLessThan(1.0,
                "return Moon longitude should match natal Moon to within 1 arc-second");
        }
    }

    [Fact]
    public void Calculate_Precision_SubArcSecond()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        foreach (LunarReturnResult result in results)
        {
            result.PrecisionArcSeconds.Should().BeLessThan(1.0,
                "precision should be sub-arc-second");
        }
    }

    [Fact]
    public void Calculate_Returns_AreChronological()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        for (int i = 1; i < results.Count; i++)
        {
            results[i].ReturnChart.UtcDateTime.Should().BeAfter(
                results[i - 1].ReturnChart.UtcDateTime,
                "results should be sorted chronologically");
        }
    }

    [Fact]
    public void Calculate_SequenceNumbers_AreSequential()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        for (int i = 0; i < results.Count; i++)
        {
            results[i].SequenceNumber.Should().Be(i + 1,
                "sequence numbers should be 1-based and sequential");
        }
    }

    [Fact]
    public void Calculate_ReturnSpacing_ApproximatelyMoonCycle()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert — spacing should be ~27.3 ± 2 days
        for (int i = 1; i < results.Count; i++)
        {
            double daysBetween = (results[i].ReturnChart.UtcDateTime -
                results[i - 1].ReturnChart.UtcDateTime).TotalDays;
            daysBetween.Should().BeInRange(25.0, 30.0,
                "adjacent lunar returns should be approximately 27.3 days apart");
        }
    }

    [Fact]
    public void Calculate_AllReturns_HaveAllBodies()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        foreach (LunarReturnResult result in results)
        {
            result.ReturnChart.Positions.Should().HaveCount(12,
                "each return chart should have 12 planetary positions");
        }
    }

    [Fact]
    public void Calculate_AllReturns_HaveValidHouses()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        foreach (LunarReturnResult result in results)
        {
            result.ReturnChart.Houses.Cusps.Should().HaveCount(12,
                "each return chart should have 12 house cusps");
            result.ReturnChart.Houses.Cusps.Should().AllSatisfy(c =>
                c.Should().BeInRange(0.0, 360.0,
                    "each house cusp should be a valid longitude"));
        }
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void Calculate_NatalMoonNear0Aries_HandlesWraparound()
    {
        // Arrange — use CalculateNext to get a single return for boundary testing
        ChartResult natal = CalculateNatalChartMoonNear0Aries();
        DateTime onOrAfter = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal, onOrAfter, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.PrecisionArcSeconds.Should().BeLessThan(1.0,
            "precision should be sub-arc-second even near 0° Aries boundary");
        result.ReturnChart.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_NatalMoonNear29Pisces_HandlesWraparound()
    {
        // Arrange — use CalculateNext to get a single return for boundary testing
        ChartResult natal = CalculateNatalChartMoonNear29Pisces();
        DateTime onOrAfter = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal, onOrAfter, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.PrecisionArcSeconds.Should().BeLessThan(1.0,
            "precision should be sub-arc-second even near 29° Pisces boundary");
        result.ReturnChart.Positions.Should().HaveCount(12);
    }

    #endregion

    #region Location Tests

    [Fact]
    public void Calculate_ReturnLocation_AffectsHouses()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act — same natal chart, two different return locations
        IReadOnlyList<LunarReturnResult> nyResults = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");
        IReadOnlyList<LunarReturnResult> londonResults = _lunarReturnService.Calculate(
            natal, start, end, 51.5074, -0.1278, "London, UK", "Europe/London");

        // Assert — different locations should produce different house cusps
        nyResults.Should().NotBeEmpty();
        londonResults.Should().NotBeEmpty();
        nyResults[0].ReturnChart.Houses.Ascendant.Should().NotBeApproximately(
            londonResults[0].ReturnChart.Houses.Ascendant, 0.01,
            "different locations should produce different Ascendants");
    }

    [Fact]
    public void Calculate_ReturnLocation_DoesNotAffectReturnTime()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act — same natal chart, two different return locations
        IReadOnlyList<LunarReturnResult> nyResults = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");
        IReadOnlyList<LunarReturnResult> londonResults = _lunarReturnService.Calculate(
            natal, start, end, 51.5074, -0.1278, "London, UK", "Europe/London");

        // Assert — return time should be the same (Moon return is location-independent)
        nyResults.Should().NotBeEmpty();
        londonResults.Should().NotBeEmpty();
        nyResults[0].ReturnChart.UtcDateTime.Should().BeCloseTo(
            londonResults[0].ReturnChart.UtcDateTime, TimeSpan.FromSeconds(2),
            "return UTC time should be the same regardless of location");
    }

    #endregion

    #region CalculateNext Tests

    [Fact]
    public void CalculateNext_FindsFirstReturnOnOrAfter()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime onOrAfter = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal, onOrAfter, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.UtcDateTime.Should().BeOnOrAfter(onOrAfter,
            "next return should be on or after the start date");
        result.PrecisionArcSeconds.Should().BeLessThan(1.0,
            "precision should be sub-arc-second");
        result.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void CalculateNext_WithinCycle_FindsCorrectReturn()
    {
        // Arrange — start mid-cycle (March 15)
        ChartResult natal = CalculateNatalChart();
        DateTime onOrAfter = new(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal, onOrAfter, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert — should be within ~28 days of start date
        double daysUntilReturn = (result.ReturnChart.UtcDateTime - onOrAfter).TotalDays;
        daysUntilReturn.Should().BeInRange(0, 28,
            "next lunar return should be within one Moon cycle");
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void Calculate_NullNatalChart_ThrowsArgumentNull()
    {
        // Act & Assert
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);
        Action act = () => _lunarReturnService.Calculate(
            null!, start, end, 40.7128, -74.006, "NYC", "America/New_York");
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("natalChart");
    }

    [Fact]
    public void Calculate_EndBeforeStart_ThrowsArgumentOutOfRange()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 30, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Action act = () => _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "NYC", "America/New_York");
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("endDate");
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Calculate_ChartInput_HasCorrectEventType()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        results[0].ReturnChart.Input.EventType.Should().Be("Lunar Return",
            "return chart should have EventType 'Lunar Return'");
        results[0].ReturnChart.Input.Name.Should().Contain("Lunar Return");
    }

    [Fact]
    public void Calculate_PreservesNatalChart()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        results[0].NatalChart.Input.Name.Should().Be("Test Person");
        results[0].NatalChart.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_SetEngineVersion()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        results[0].EngineVersion.Should().Contain("AstroCast");
    }

    [Fact]
    public void Calculate_SetCalculatedAt()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime before = DateTime.UtcNow;
        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        // Act
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        results.Should().NotBeEmpty();
        results[0].CalculatedAt.Should().BeCloseTo(before, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="LunarReturnService.Calculate"/> honours the
    /// supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any return search runs.
    /// </summary>
    [Fact]
    public void Calculate_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ChartResult natal = CalculateNatalChart();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York",
            cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the lunar return scan immediately");
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

        DateTime start = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2025, 3, 30, 23, 59, 59, DateTimeKind.Utc);
        IReadOnlyList<LunarReturnResult> results = _lunarReturnService.Calculate(
            natal, start, end, 40.7128, -74.006, "New York City", "America/New_York");

        ChartResult returnChart = results.Should().ContainSingle("a 30-day window holds one lunar return").Which.ReturnChart;
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
