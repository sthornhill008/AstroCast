using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Error-path tests verifying that the engine and utilities correctly reject
/// invalid inputs with appropriate exceptions. These tests ensure defensive
/// validation catches problems before reaching the Swiss Ephemeris layer.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ErrorPathTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the chart engine.
    /// </summary>
    public ErrorPathTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Null Input Test (1)

    /// <summary>
    /// Test 1: Null input — ChartEngine.Calculate should throw ArgumentNullException.
    /// </summary>
    [Fact]
    public void Calculate_Should_ThrowArgumentNull_When_InputIsNull()
    {
        // Act
        Action act = () => _engine.Calculate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("input");
    }

    #endregion

    #region Invalid Timezone Test (2)

    /// <summary>
    /// Test 2: Invalid timezone — should throw TimeZoneNotFoundException.
    /// </summary>
    [Fact]
    public void Calculate_Should_ThrowTimeZoneNotFound_When_TimezoneIsInvalid()
    {
        // Arrange
        ChartInput input = new()
        {
            Name = "Invalid TZ",
            LocalDateTime = new DateTime(2026, 1, 1, 12, 0, 0),
            TimeZone = "Not/A/Timezone",
            Latitude = 51.5,
            Longitude = -0.12,
            LocationName = "London",
            EventType = "Event",
        };

        // Act
        Action act = () => _engine.Calculate(input);

        // Assert
        act.Should().Throw<TimeZoneNotFoundException>();
    }

    #endregion

    #region Out-of-Range Latitude Test (3)

    /// <summary>
    /// Test 3: Latitude out of range — should throw ArgumentOutOfRangeException.
    /// </summary>
    [Theory]
    [InlineData(91.0)]
    [InlineData(-91.0)]
    public void Calculate_Should_ThrowArgumentOutOfRange_When_LatitudeInvalid(double latitude)
    {
        // Arrange
        ChartInput input = new()
        {
            Name = "Bad Latitude",
            LocalDateTime = new DateTime(2026, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = latitude,
            Longitude = 0.0,
            LocationName = "Nowhere",
            EventType = "Event",
        };

        // Act
        Action act = () => _engine.Calculate(input);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Out-of-Range Longitude Test (4)

    /// <summary>
    /// Test 4: Longitude out of range — should throw ArgumentOutOfRangeException.
    /// </summary>
    [Theory]
    [InlineData(181.0)]
    [InlineData(-181.0)]
    public void Calculate_Should_ThrowArgumentOutOfRange_When_LongitudeInvalid(double longitude)
    {
        // Arrange
        ChartInput input = new()
        {
            Name = "Bad Longitude",
            LocalDateTime = new DateTime(2026, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 0.0,
            Longitude = longitude,
            LocationName = "Nowhere",
            EventType = "Event",
        };

        // Act
        Action act = () => _engine.Calculate(input);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Year Beyond Range Test (5)

    /// <summary>
    /// Test 5: Year beyond Swiss Ephemeris range — should throw ArgumentOutOfRangeException.
    /// The engine rejects instants outside the years the ephemeris files cover, 1200 to 2399.
    /// </summary>
    [Fact]
    public void Calculate_Should_ThrowArgumentOutOfRange_When_YearBeyondRange()
    {
        // Arrange — year 6000 is beyond the years the ephemeris files cover
        ChartInput input = new()
        {
            Name = "Far Future",
            LocalDateTime = new DateTime(6000, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 0.0,
            Longitude = 0.0,
            LocationName = "Nowhere",
            EventType = "Event",
        };

        // Act
        Action act = () => _engine.Calculate(input);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FileNameSanitiser Tests (6–7)

    /// <summary>
    /// Test 6: Empty name for sanitiser — should throw ArgumentException.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Sanitise_Should_ThrowArgumentException_When_NameIsEmpty(string? fileName)
    {
        // Act
        Action act = () => FileNameSanitiser.Sanitise(fileName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("fileName");
    }

    /// <summary>
    /// Test 7: Name with only invalid characters — should throw ArgumentException.
    /// </summary>
    [Fact]
    public void Sanitise_Should_ThrowArgumentException_When_NameIsAllInvalidChars()
    {
        // Arrange — a string composed entirely of path separators and traversal sequences
        string fileName = "../../../";

        // Act
        Action act = () => FileNameSanitiser.Sanitise(fileName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("fileName");
    }

    #endregion
}
