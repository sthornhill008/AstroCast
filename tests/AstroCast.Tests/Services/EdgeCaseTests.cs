using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Edge-case integration tests verifying the chart engine handles unusual but
/// valid inputs correctly: circumpolar latitudes, southern hemisphere,
/// midnight births, DST transitions, boundary coordinates, and historical dates.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EdgeCaseTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the chart engine.
    /// </summary>
    public EdgeCaseTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Circumpolar Latitude Tests (1–2)

    /// <summary>
    /// Test 1: High latitude — Reykjavik, Iceland (64.13°N) in June.
    /// Near the Placidus failure boundary (~66°), the engine should still
    /// produce a valid chart with all 12 bodies and house cusps.
    /// </summary>
    [Fact]
    public void Calculate_Should_ProduceValidChart_When_HighLatitudeSummer()
    {
        // Arrange — Reykjavik, Iceland, near summer solstice
        ChartInput input = new()
        {
            Name = "High Latitude Summer",
            LocalDateTime = new DateTime(2026, 6, 21, 12, 0, 0),
            TimeZone = "Atlantic/Reykjavik",
            Latitude = 64.13,
            Longitude = -21.90,
            LocationName = "Reykjavik, Iceland",
            EventType = "Event",
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert — 12 positions, 12 house cusps, ASC/MC within range
        result.Positions.Should().HaveCount(12, "all 12 bodies should be calculated");
        result.Houses.Cusps.Should().HaveCount(12, "all 12 house cusps should be present");
        result.Houses.Ascendant.Should().BeInRange(0.0, 360.0);
        result.Houses.Midheaven.Should().BeInRange(0.0, 360.0);
    }

    /// <summary>
    /// Test 2: Circumpolar latitude — Tromso, Norway (69.65°N).
    /// Placidus houses cannot be calculated above approximately 66° latitude.
    /// The engine should throw <see cref="AstroCast.Core.Exceptions.EphemerisCalculationException"/>.
    /// </summary>
    [Fact]
    public void Calculate_Should_ThrowEphemerisException_When_CircumpolarLatitude()
    {
        // Arrange — Tromso, Norway, above Placidus failure threshold
        ChartInput input = new()
        {
            Name = "Circumpolar",
            LocalDateTime = new DateTime(2025, 12, 21, 12, 0, 0),
            TimeZone = "Europe/Oslo",
            Latitude = 69.65,
            Longitude = 18.96,
            LocationName = "Tromso, Norway",
            EventType = "Event",
        };

        // Act
        Action act = () => _engine.Calculate(input);

        // Assert — known Placidus limitation above ~66°
        act.Should().Throw<AstroCast.Core.Exceptions.EphemerisCalculationException>(
            "Placidus houses cannot be computed at extreme latitudes");
    }

    #endregion

    #region Southern Hemisphere Test (3)

    /// <summary>
    /// Test 3: Southern hemisphere — Sydney, Australia (-33.87°S).
    /// House cusps should be valid and all positions calculated.
    /// </summary>
    [Fact]
    public void Calculate_Should_ProduceValidChart_When_SouthernHemisphere()
    {
        // Arrange — Sydney, Australia
        ChartInput input = new()
        {
            Name = "Southern Hemisphere",
            LocalDateTime = new DateTime(2026, 3, 15, 14, 30, 0),
            TimeZone = "Australia/Sydney",
            Latitude = -33.87,
            Longitude = 151.21,
            LocationName = "Sydney, Australia",
            EventType = "Birth",
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Positions.Should().HaveCount(12);
        result.Houses.Cusps.Should().HaveCount(12);
        result.Houses.Ascendant.Should().BeInRange(0.0, 360.0);
        result.Houses.Midheaven.Should().BeInRange(0.0, 360.0);
        result.Aspects.Should().NotBeEmpty("a typical chart should have at least one aspect");
    }

    #endregion

    #region Midnight Birth Test (4)

    /// <summary>
    /// Test 4: Midnight birth — exactly 00:00:00 local time.
    /// The engine should handle the day boundary correctly.
    /// </summary>
    [Fact]
    public void Calculate_Should_ProduceValidChart_When_MidnightBirth()
    {
        // Arrange — London, exactly midnight
        ChartInput input = new()
        {
            Name = "Midnight Birth",
            LocalDateTime = new DateTime(2026, 7, 1, 0, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5,
            Longitude = -0.12,
            LocationName = "London, UK",
            EventType = "Birth",
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Positions.Should().HaveCount(12);
        result.UtcDateTime.Hour.Should().BeLessThanOrEqualTo(23,
            "UTC hour should be valid after midnight conversion");
    }

    #endregion

    #region DST Transition Tests (5–6)

    /// <summary>
    /// Test 5: DST spring-forward gap — US Eastern, March 8 2026 at 2:30 AM.
    /// This local time does not exist (clocks jump from 2:00 AM to 3:00 AM).
    /// The engine should adjust to the end of the gap and still produce a chart.
    /// </summary>
    [Fact]
    public void Calculate_Should_AdjustTime_When_DstSpringForwardGap()
    {
        // Arrange — a time that does not exist due to DST spring-forward
        ChartInput input = new()
        {
            Name = "DST Spring Forward",
            LocalDateTime = new DateTime(2026, 3, 8, 2, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.71,
            Longitude = -74.01,
            LocationName = "New York, NY",
            EventType = "Event",
        };

        // Act — should not throw; engine adjusts invalid local time
        ChartResult result = _engine.Calculate(input);

        // Assert — chart is valid and UTC time was resolved
        result.Positions.Should().HaveCount(12);
        result.UtcDateTime.Should().NotBe(default, "UTC time should be resolved");
    }

    /// <summary>
    /// Test 6: DST fall-back ambiguity — US Eastern, November 1 2026 at 1:30 AM.
    /// This local time occurs twice (before and after clocks fall back).
    /// The engine should use the standard (non-DST) offset and produce a chart.
    /// </summary>
    [Fact]
    public void Calculate_Should_UseStandardOffset_When_DstFallBackAmbiguity()
    {
        // Arrange — an ambiguous time during DST fall-back
        ChartInput input = new()
        {
            Name = "DST Fall Back",
            LocalDateTime = new DateTime(2026, 11, 1, 1, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.71,
            Longitude = -74.01,
            LocationName = "New York, NY",
            EventType = "Event",
        };

        // Act — should not throw; engine resolves ambiguity
        ChartResult result = _engine.Calculate(input);

        // Assert — chart is valid and UTC time was resolved using standard offset
        result.Positions.Should().HaveCount(12);
        // EST (standard) is UTC-5, so 1:30 AM EST = 6:30 AM UTC
        result.UtcDateTime.Hour.Should().Be(6,
            "ambiguous time should resolve to standard (non-DST) offset");
        result.UtcDateTime.Minute.Should().Be(30);
    }

    #endregion

    #region Boundary Coordinate Test (7)

    /// <summary>
    /// Test 7: Boundary longitude — exactly 180.0° (International Date Line).
    /// The engine should accept this valid boundary value without error.
    /// </summary>
    [Fact]
    public void Calculate_Should_AcceptBoundaryLongitude_When_ExactlyDateLine()
    {
        // Arrange — exactly on the International Date Line
        ChartInput input = new()
        {
            Name = "Date Line",
            LocalDateTime = new DateTime(2026, 6, 1, 12, 0, 0),
            TimeZone = "Pacific/Fiji",
            Latitude = -17.77,
            Longitude = 180.0,
            LocationName = "International Date Line",
            EventType = "Event",
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Positions.Should().HaveCount(12);
        result.Houses.Cusps.Should().HaveCount(12);
    }

    #endregion

    #region Historical Date Test (8)

    /// <summary>
    /// Test 8: Pre-1582 date — before the Gregorian calendar adoption.
    /// The engine reads the date on the Julian calendar itself and takes the Julian Day by arithmetic.
    /// The engine should produce a valid chart for historical dates.
    /// </summary>
    [Fact]
    public void Calculate_Should_ProduceValidChart_When_PreGregorianDate()
    {
        // Arrange — a date before the Gregorian calendar (1582-10-15)
        // Using explicit offset since IANA timezone doesn't cover 1200 AD
        ChartInput input = new()
        {
            Name = "Medieval Chart",
            LocalDateTime = new DateTime(1200, 3, 21, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = -0.12,
            LocationName = "London, England",
            EventType = "Event",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert — the Swiss Ephemeris supports dates well before 1582
        result.Positions.Should().HaveCount(12);
        result.Houses.Cusps.Should().HaveCount(12);
        result.JulianDay.Should().BeGreaterThan(0.0, "Julian Day should be positive for 1200 AD");
    }

    #endregion
}
