using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitToTransitAspectFinder"/> verifying detection
/// of major aspects between transiting planets with binary search refinement.
/// </summary>
[Collection("Ephemeris")]
public sealed class TransitToTransitAspectFinderTests
{
    #region Unit Tests

    /// <summary>
    /// Verifies the model stores all required fields correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TransitToTransitAspect_StoresAllRequiredFields()
    {
        DateTime exactTime = new(2026, 4, 15, 14, 33, 12, DateTimeKind.Utc);

        TransitToTransitAspect aspect = new()
        {
            Planet1 = TransitBody.Saturn,
            Planet1Name = "Saturn",
            Planet1Longitude = 5.2,
            Planet1Position = "5°12' Aries",
            Planet2 = TransitBody.Pluto,
            Planet2Name = "Pluto",
            Planet2Longitude = 305.2,
            Planet2Position = "5°12' Aquarius",
            AspectName = "Sextile",
            AspectAbbreviation = "sxt",
            AspectAngle = 60.0,
            ExactDateTimeUtc = exactTime,
            ExactOrbDegrees = 0.00015,
        };

        aspect.Planet1.Should().Be(TransitBody.Saturn);
        aspect.Planet1Name.Should().Be("Saturn");
        aspect.Planet2.Should().Be(TransitBody.Pluto);
        aspect.Planet2Name.Should().Be("Pluto");
        aspect.AspectName.Should().Be("Sextile");
        aspect.AspectAbbreviation.Should().Be("sxt");
        aspect.AspectAngle.Should().Be(60.0);
        aspect.ExactDateTimeUtc.Should().Be(exactTime);
        aspect.ExactOrbDegrees.Should().BeLessThan(0.001);
        aspect.Planet1Position.Should().Contain("Aries");
        aspect.Planet2Position.Should().Contain("Aquarius");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Detects at least one transit-to-transit aspect in a 6-month period.
    /// Outer planets move slowly, so a long window is needed to ensure detection.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void FindAspects_SixMonthPeriod_FindsAtLeastOneAspect()
    {
        EphemerisFixture fixture = new();
        TransitEphemerisHelper helper = new(fixture.EphemerisService);
        TransitToTransitAspectFinder finder = new(helper);

        // Scan 6 months for maximum chance of finding an outer planet aspect
        DateOnly start = new(2026, 1, 1);
        DateOnly end = new(2026, 6, 30);

        List<TransitBody> bodies =
        [
            TransitBody.Pluto, TransitBody.Neptune, TransitBody.Uranus,
            TransitBody.Saturn, TransitBody.Jupiter, TransitBody.Chiron,
        ];

        IReadOnlyList<TransitToTransitAspect> aspects = finder.FindAspects(bodies, start, end);

        aspects.Should().NotBeEmpty(
            "outer planets should form at least one major aspect in a 6-month window");

        // Verify residual orb precision
        foreach (TransitToTransitAspect aspect in aspects)
        {
            aspect.ExactOrbDegrees.Should().BeLessThan(0.01,
                $"residual orb for {aspect.Planet1Name} {aspect.AspectAbbreviation} {aspect.Planet2Name} " +
                "should be < 0.01° from binary search refinement");
        }
    }

    /// <summary>
    /// Verifies that transit-to-transit aspects are included in TransitResult
    /// when computed through the full TransitService pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void TransitResult_IncludesTransitToTransitAspects()
    {
        EphemerisFixture fixture = new();
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        TransitService service = new(fixture.EphemerisService, logger);

        // Use a chart engine to produce a natal chart for the transit input
        Core.Services.ChartEngine engine = new(
            fixture.EphemerisService,
            new AspectService(fixture.EphemerisService, logger),
            new MidpointService(logger),
            new DignityService(),
            new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()),
            new AntisciaService(),
            new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);

        Core.Models.ChartResult natal = engine.Calculate(new Core.Models.ChartInput
        {
            Name = "Test",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationName = "London",
            EventType = "Birth",
        });

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 6, 30),
            IncludeMinorAspects = false,
        };

        TransitResult result = service.Calculate(input);

        result.TransitToTransitAspects.Should().NotBeNull();
        // Over 6 months we should find at least one
        result.TransitToTransitAspects.Count.Should().BeGreaterThan(0,
            "6-month transit result should contain transit-to-transit aspects");
    }

    #endregion
}
