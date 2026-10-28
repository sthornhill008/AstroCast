using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="SolarReturnService"/> verifying the binary search algorithm,
/// return precision, edge cases, and the complete solar return pipeline.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SolarReturnServiceTests
{
    private readonly ChartEngine _engine;
    private readonly SolarReturnService _solarReturnService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and SolarReturnService.
    /// </summary>
    public SolarReturnServiceTests(EphemerisFixture fixture)
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

        _solarReturnService = new SolarReturnService(
            fixture.EphemerisService, _engine, logger);
    }

    /// <summary>
    /// Standard test natal chart: J2000.0 epoch, New York City.
    /// Sun at ~280.6° (Capricorn 10°).
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
    /// Natal chart with Sun near 0° Aries (boundary test).
    /// March 21, 2000 sunrise → Sun near 0° Aries.
    /// </summary>
    private ChartResult CalculateNatalChartAries0() => _engine.Calculate(new ChartInput
    {
        Name = "Aries Native",
        LocalDateTime = new DateTime(2000, 3, 21, 6, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>
    /// Natal chart with Sun near 29° Pisces (~359°, near boundary from other side).
    /// March 19, 2000 → Sun near end of Pisces.
    /// </summary>
    private ChartResult CalculateNatalChartPisces29() => _engine.Calculate(new ChartInput
    {
        Name = "Pisces Native",
        LocalDateTime = new DateTime(2000, 3, 19, 6, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    #region Core Calculation Tests

    [Fact]
    public void Calculate_KnownChart_ReturnsWithinYear()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert — the return may fall on Dec 31 of the prior year (UTC) for
        // early-January birthdays, which is astronomically correct.
        result.ReturnChart.UtcDateTime.Year.Should().BeOneOf(new[] { 2024, 2025 },
            "solar return should occur near the target year boundary");
    }

    [Fact]
    public void Calculate_ReturnSunLongitude_MatchesNatal()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert — return Sun should match natal Sun to within 1 arc-second
        double diffArcSec = Math.Abs(
            SolarReturnService.SignedLongitudeDelta(
                result.ReturnSunLongitude, result.NatalSunLongitude)) * 3600.0;

        diffArcSec.Should().BeLessThan(1.0,
            "return Sun longitude should match natal Sun to within 1 arc-second");
    }

    [Fact]
    public void Calculate_Precision_SubArcSecond()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.PrecisionArcSeconds.Should().BeLessThan(1.0,
            "precision should be sub-arc-second");
    }

    [Fact]
    public void Calculate_Positions_HasAllBodies()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.Positions.Should().HaveCount(12,
            "return chart should have 12 planetary positions");
    }

    [Fact]
    public void Calculate_Houses_ValidPlacidus()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.Houses.Cusps.Should().HaveCount(12,
            "return chart should have 12 house cusps");
        result.ReturnChart.Houses.Cusps.Should().AllSatisfy(c =>
            c.Should().BeInRange(0.0, 360.0,
                "each house cusp should be a valid longitude"));
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void Calculate_NatalSunNear0Aries_HandlesWraparound()
    {
        // Arrange — Sun near 0° Aries (0°/360° boundary)
        ChartResult natal = CalculateNatalChartAries0();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.PrecisionArcSeconds.Should().BeLessThan(1.0,
            "precision should be sub-arc-second even near 0° Aries boundary");
        result.ReturnChart.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_NatalSunNear29Pisces_HandlesWraparound()
    {
        // Arrange — Sun near 359° (end of Pisces, approaching 0°/360° boundary)
        ChartResult natal = CalculateNatalChartPisces29();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

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

        // Act — same natal chart, two different return locations
        SolarReturnResult nyResult = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");
        SolarReturnResult londonResult = _solarReturnService.Calculate(
            natal, 2025, 51.5074, -0.1278, "London, UK", "Europe/London");

        // Assert — different locations should produce different house cusps
        nyResult.ReturnChart.Houses.Ascendant.Should().NotBeApproximately(
            londonResult.ReturnChart.Houses.Ascendant, 0.01,
            "different locations should produce different Ascendants");
    }

    [Fact]
    public void Calculate_ReturnLocation_DoesNotAffectReturnTime()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act — same natal chart, two different return locations
        SolarReturnResult nyResult = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");
        SolarReturnResult londonResult = _solarReturnService.Calculate(
            natal, 2025, 51.5074, -0.1278, "London, UK", "Europe/London");

        // Assert — return time should be the same (Sun return is location-independent)
        nyResult.ReturnChart.UtcDateTime.Should().BeCloseTo(
            londonResult.ReturnChart.UtcDateTime, TimeSpan.FromSeconds(2),
            "return UTC time should be the same regardless of location");
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void Calculate_NullNatalChart_ThrowsArgumentNull()
    {
        // Act & Assert
        Action act = () => _solarReturnService.Calculate(
            null!, 2025, 40.7128, -74.006, "NYC", "America/New_York");
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("natalChart");
    }

    [Fact]
    public void Calculate_YearTooEarly_ThrowsArgumentOutOfRange()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act & Assert
        Action act = () => _solarReturnService.Calculate(
            natal, -6000, 40.7128, -74.006, "NYC", "America/New_York");
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("year");
    }

    #endregion

    #region Multi-Year and Metadata Tests

    [Fact]
    public void Calculate_MultipleConsecutiveYears_EachReturnsValid()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act & Assert — 3 consecutive years
        for (int year = 2024; year <= 2026; year++)
        {
            SolarReturnResult result = _solarReturnService.Calculate(
                natal, year, 40.7128, -74.006, "New York City", "America/New_York");

            // The return may fall on Dec 31 of the prior year (UTC) for
            // early-January birthdays, which is astronomically correct.
            result.ReturnChart.UtcDateTime.Year.Should().BeOneOf(new[] { year - 1, year },
                $"solar return for {year} should occur near that year");
            result.PrecisionArcSeconds.Should().BeLessThan(1.0,
                $"precision should be sub-arc-second for year {year}");
        }
    }

    [Fact]
    public void Calculate_ReturnDateTime_HasUtcKind()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.UtcDateTime.Kind.Should().Be(DateTimeKind.Utc,
            "return DateTime should be UTC");
    }

    [Fact]
    public void Calculate_ChartInput_HasCorrectEventType()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.ReturnChart.Input.EventType.Should().Be("Solar Return",
            "return chart should have EventType 'Solar Return'");
        result.ReturnChart.Input.Name.Should().Contain("Solar Return");
        result.ReturnChart.Input.Name.Should().Contain("2025");
    }

    [Fact]
    public void Calculate_PreservesNatalChart()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.NatalChart.Input.Name.Should().Be("Test Person");
        result.NatalChart.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_SetEngineVersion()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.EngineVersion.Should().Contain("AstroCast");
    }

    [Fact]
    public void Calculate_SetCalculatedAt()
    {
        // Arrange
        ChartResult natal = CalculateNatalChart();
        DateTime before = DateTime.UtcNow;

        // Act
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

        // Assert
        result.CalculatedAt.Should().BeCloseTo(before, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region SignedLongitudeDelta Unit Tests

    [Fact]
    public void SignedLongitudeDelta_SamePosition_ReturnsZero()
    {
        double delta = SolarReturnService.SignedLongitudeDelta(100.0, 100.0);
        delta.Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void SignedLongitudeDelta_SmallPositive_ReturnsPositive()
    {
        double delta = SolarReturnService.SignedLongitudeDelta(105.0, 100.0);
        delta.Should().BeApproximately(5.0, 0.0001);
    }

    [Fact]
    public void SignedLongitudeDelta_SmallNegative_ReturnsNegative()
    {
        double delta = SolarReturnService.SignedLongitudeDelta(95.0, 100.0);
        delta.Should().BeApproximately(-5.0, 0.0001);
    }

    [Fact]
    public void SignedLongitudeDelta_CrossZero_ReturnsCorrectSign()
    {
        // 5° current, 355° target → current is 10° ahead (crossing 0°)
        double delta = SolarReturnService.SignedLongitudeDelta(5.0, 355.0);
        delta.Should().BeApproximately(10.0, 0.0001,
            "5° is 10° ahead of 355° crossing 0°");
    }

    [Fact]
    public void SignedLongitudeDelta_CrossZeroNegative_ReturnsCorrectSign()
    {
        // 355° current, 5° target → current is 10° behind (crossing 0°)
        double delta = SolarReturnService.SignedLongitudeDelta(355.0, 5.0);
        delta.Should().BeApproximately(-10.0, 0.0001,
            "355° is 10° behind 5° crossing 0°");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="SolarReturnService.Calculate"/> honours the
    /// supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any return search runs.
    /// </summary>
    [Fact]
    public void Calculate_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ChartResult natal = CalculateNatalChart();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York",
            cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the solar return calculation immediately");
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

        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, 40.7128, -74.006, "New York City", "America/New_York");

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
