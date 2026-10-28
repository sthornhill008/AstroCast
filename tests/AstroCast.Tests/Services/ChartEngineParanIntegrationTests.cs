using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests verifying that paran calculation is automatically
/// included in <see cref="ChartEngine.Calculate"/> and
/// <see cref="ChartEngine.CalculateForElectional"/> results.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineParanIntegrationTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all required services.
    /// </summary>
    public ChartEngineParanIntegrationTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()),
            new AntisciaService(), new StationService(),
            new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Standard natal chart at non-polar latitude should include a non-null
    /// ParanResult with angular crossings.
    /// </summary>
    [Fact]
    public void Calculate_IncludesParanResult()
    {
        // Arrange — J2000.0 epoch, New York City
        ChartInput input = new()
        {
            Name = "Paran Integration Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            Gender = Gender.Unspecified,
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Parans.Should().NotBeNull("parans should be computed for non-polar latitudes");
        result.Parans!.Crossings.Should().NotBeEmpty("angular crossings should exist for New York");
    }

    /// <summary>
    /// The ParanResult coordinates should match the chart input coordinates.
    /// </summary>
    [Fact]
    public void Calculate_ParanResult_HasCorrectCoordinates()
    {
        // Arrange
        ChartInput input = new()
        {
            Name = "Coordinate Check",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            Gender = Gender.Unspecified,
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Parans.Should().NotBeNull();
        result.Parans!.Latitude.Should().BeApproximately(40.7128, 0.001);
        result.Parans!.Longitude.Should().BeApproximately(-74.006, 0.001);
    }

    /// <summary>
    /// Electional charts (which share BuildResult) should also include parans.
    /// </summary>
    [Fact]
    public void CalculateForElectional_IncludesParanResult()
    {
        // Arrange — London, 2026-06-15 noon
        ChartInput input = new()
        {
            Name = "Electional Paran Test",
            LocalDateTime = new DateTime(2026, 6, 15, 12, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
            EventType = "Event",
            Gender = Gender.Unspecified,
        };

        // Act
        ChartResult result = _engine.CalculateForElectional(input);

        // Assert
        result.Parans.Should().NotBeNull("electional charts should also include parans");
        result.Parans!.Crossings.Should().NotBeEmpty();
    }
}
