using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// End-to-end integration test verifying that a real transit calculation produces
/// the enhanced timeline output with Transit Windows, Events table with Position
/// column, and sub-minute UTC timestamps for entry/exit events.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitTimelineIntegrationTests
{
    private readonly TransitService _transitService;
    private readonly ChartResult _natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds a ChartEngine to produce
    /// the natal chart, then constructs the TransitService.
    /// </summary>
    public TransitTimelineIntegrationTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(
            fixture.EphemerisService, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()),
            new AntisciaService(), new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);

        ChartInput input = new()
        {
            Name = "J2000.0 Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

        _natalChart = engine.Calculate(input);
        _transitService = new TransitService(fixture.EphemerisService, logger);
    }

    /// <summary>
    /// Enhanced timeline from real transits contains Transit Windows, Events table
    /// with Position column, and sub-minute UTC timestamps for entry/exit.
    /// </summary>
    [Fact]
    public void EnhancedTimeline_RealTransits_ContainsAllSections()
    {
        TransitInput transitInput = new()
        {
            NatalChart = _natalChart,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 6, 30),
            IncludeMinorAspects = false,
        };

        TransitResult result = _transitService.Calculate(transitInput);

        result.ActiveTransits.Should().NotBeEmpty(
            "6-month transit period should have active outer planet transits");

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // Transit Windows section
        md.Should().Contain("## Transit Windows",
            "enhanced timeline should include Transit Windows section");

        // Events table with Position column
        md.Should().Contain("## Events");
        md.Should().Contain("| Position |",
            "Events table should include Position column");

        // Entry events should have non-noon UTC times
        ActiveTransit firstTransit = result.ActiveTransits[0];
        TransitLifecycleEvent? entry = firstTransit.Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Entry);

        if (entry is not null)
        {
            bool isNoon = entry.Date.Hour == 12 && entry.Date.Minute == 0 && entry.Date.Second == 0;
            isNoon.Should().BeFalse(
                $"entry time {entry.Date:HH:mm:ss} should have sub-minute precision, not noon UTC");
        }

        // Footer with updated precision note
        md.Should().Contain("Entry/exit times mark the orb boundary crossing");
    }
}
