using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Integration tests that verify the complete output pipeline: ChartEngine calculation
/// followed by formatting, asserting that Phase 1-3 data (dignities, Moon conditions,
/// chart context) appears in each output format.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class OutputIntegrationTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Creates a ChartEngine using the shared ephemeris fixture with all services wired.
    /// </summary>
    public OutputIntegrationTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignityService = new();
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            dignityService, new MoonConditionService(),
            new ChartContextService(dignityService, new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Creates a standard test chart result for a known date and location.
    /// </summary>
    private ChartResult CreateTestResult(string eventType = "Event") =>
        _engine.Calculate(new ChartInput
        {
            Name = "Output Integration Test",
            LocalDateTime = new DateTime(2000, 6, 15, 14, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = eventType,
        });

    #region Markdown Output Tests

    [Fact]
    public void ChartMarkdown_Should_IncludeDignities_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Essential Dignities",
            "Markdown output should include the Essential Dignities section");
        markdown.Should().Contain("| Planet | Sign | Dignity | Score | Dispositor |",
            "Markdown should include the dignities table header");
    }

    [Fact]
    public void ChartMarkdown_Should_IncludeMoonConditions_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Moon Conditions",
            "Markdown output should include the Moon Conditions section");
        markdown.Should().Contain("**Phase:**",
            "Moon Conditions should include the Moon phase");
        markdown.Should().Contain("-phase)",
            "Moon phase should include the phase position qualifier (early/mid/late-phase)");
        markdown.Should().Contain("**Void of Course:**",
            "Moon Conditions should include the void-of-course status");
    }

    [Fact]
    public void ChartMarkdown_Should_IncludeChartContext_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Chart Context",
            "Markdown output should include the Chart Context section");
        markdown.Should().Contain("| Chart Ruler |",
            "Chart Context should include the chart ruler");
        markdown.Should().Contain("| Sect |",
            "Chart Context should include the sect determination");
    }

    [Fact]
    public void ChartMarkdown_Should_IncludeEventType_When_EventTypeProvided()
    {
        ChartResult result = CreateTestResult("BusinessLaunch");

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Type:** BusinessLaunch",
            "Markdown header should display the event type");
        markdown.Should().Contain("| Event Significator |",
            "Chart Context should include the event significator for non-birth charts");
    }

    [Fact]
    public void ChartMarkdown_Should_IncludeSignificator_When_WeddingEventType()
    {
        ChartResult result = CreateTestResult("Wedding");

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("| Event Significator | Venus",
            "Wedding event type should map to Venus significator");
        markdown.Should().Contain("| Key Houses | 1, 7, 5 |",
            "Wedding event type should map to houses 1, 7, 5");
    }

    #endregion

    #region JSON Output Tests

    [Fact]
    public void ChartJson_Should_IncludeDignitiesArray_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string json = ChartJsonFormatter.ToJson(result);

        json.Should().Contain("\"dignities\":",
            "JSON output should include the dignities array");
        json.Should().Contain("\"isDomicile\":",
            "Each dignity entry should include the isDomicile flag");
    }

    [Fact]
    public void ChartJson_Should_IncludeMoonConditionObject_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string json = ChartJsonFormatter.ToJson(result);

        json.Should().Contain("\"moonCondition\":",
            "JSON output should include the moonCondition object");
        json.Should().Contain("\"phaseLabel\":",
            "Moon condition should include the phase label");
        json.Should().Contain("\"isVoidOfCourse\":",
            "Moon condition should include the VoC status");
    }

    [Fact]
    public void ChartJson_Should_IncludeChartContextObject_When_ChartCalculated()
    {
        ChartResult result = CreateTestResult();

        string json = ChartJsonFormatter.ToJson(result);

        json.Should().Contain("\"chartContext\":",
            "JSON output should include the chartContext object");
        json.Should().Contain("\"chartRuler\":",
            "Chart context should include the chart ruler");
        json.Should().Contain("\"isDayChart\":",
            "Chart context should include the day/night sect flag");
    }

    [Fact]
    public void ChartJson_Should_IncludeEventType_When_EventTypeProvided()
    {
        ChartResult result = CreateTestResult("Investment");

        string json = ChartJsonFormatter.ToJson(result);

        json.Should().Contain("\"eventType\": \"Investment\"",
            "JSON chart metadata should include the event type string");
        json.Should().Contain("\"eventSignificator\": \"Jupiter\"",
            "Investment event type should map to Jupiter significator in JSON");
    }

    #endregion

    #region EventType Helper Tests

    [Fact]
    public void EventTypeHelper_Should_ReturnAllEventTypes_When_GetDisplayItemsCalled()
    {
        IReadOnlyList<EventTypeDisplayItem> items = EventTypeHelper.GetDisplayItems();

        items.Count.Should().Be(Enum.GetValues<EventType>().Length,
            "Display items should include all EventType enum values");
    }

    [Fact]
    public void EventTypeHelper_Should_ReturnDisplayNames_When_DescriptionAttributePresent()
    {
        IReadOnlyList<EventTypeDisplayItem> items = EventTypeHelper.GetDisplayItems();

        EventTypeDisplayItem? wedding = items.FirstOrDefault(i => i.Value == "Wedding");
        wedding.Should().NotBeNull();
        wedding!.DisplayName.Should().Be("Wedding / Marriage",
            "Wedding enum should display as 'Wedding / Marriage'");

        EventTypeDisplayItem? jobStart = items.FirstOrDefault(i => i.Value == "JobStart");
        jobStart.Should().NotBeNull();
        jobStart!.DisplayName.Should().Be("Job / Career Start",
            "JobStart enum should display as 'Job / Career Start'");
    }

    [Fact]
    public void EventTypeHelper_Should_ParseEnumNames_When_TryParseCalled()
    {
        bool parsed = EventTypeHelper.TryParse("BusinessLaunch", out EventType result);

        parsed.Should().BeTrue();
        result.Should().Be(EventType.BusinessLaunch);
    }

    [Fact]
    public void EventTypeHelper_Should_ParseCaseInsensitive_When_TryParseCalled()
    {
        bool parsed = EventTypeHelper.TryParse("wedding", out EventType result);

        parsed.Should().BeTrue();
        result.Should().Be(EventType.Wedding);
    }

    [Fact]
    public void EventTypeHelper_Should_ReturnFalse_When_UnknownValueProvided()
    {
        bool parsed = EventTypeHelper.TryParse("Unknown", out EventType result);

        parsed.Should().BeFalse();
        result.Should().Be(EventType.Event, "Default fallback should be Event");
    }

    #endregion
}
