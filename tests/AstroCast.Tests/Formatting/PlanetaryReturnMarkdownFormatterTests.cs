using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="PlanetaryReturnMarkdownFormatter"/> verifying document structure,
/// section headings, and the natal-keyed time-lord context block emitted at the
/// planetary-return ingress instant. Mirrors the shape of
/// <see cref="SolarReturnMarkdownFormatterTests"/> and
/// <see cref="LunarReturnMarkdownFormatterTests"/>.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PlanetaryReturnMarkdownFormatterTests
{
    private readonly ChartEngine _engine;
    private readonly PlanetaryReturnService _planetaryReturnService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and
    /// PlanetaryReturnService.
    /// </summary>
    public PlanetaryReturnMarkdownFormatterTests(EphemerisFixture fixture)
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

        _planetaryReturnService = new PlanetaryReturnService(
            fixture.EphemerisService, _engine, logger);
    }

    /// <summary>
    /// Create a PlanetaryReturnResult (Saturn return #1) for testing.
    /// </summary>
    private PlanetaryReturnResult CreateTestPlanetaryReturnResult()
    {
        ChartResult natal = _engine.Calculate(new ChartInput
        {
            Name = "Alice",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });

        return _planetaryReturnService.Calculate(
            natal,
            planet: CelestialBodyId.Saturn,
            returnNumber: 1,
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City",
            timeZone: "America/New_York");
    }

    [Fact]
    public void Markdown_ContainsProfectionAtReturnIngressHeading()
    {
        // Arrange
        PlanetaryReturnResult result = CreateTestPlanetaryReturnResult();

        // Act
        string markdown = PlanetaryReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Annual Profection (at Return Ingress)",
            "document should contain the natal-keyed profection section evaluated at the PR ingress instant");
    }

    [Fact]
    public void Markdown_ContainsFirdariaAtReturnIngressHeading()
    {
        // Arrange
        PlanetaryReturnResult result = CreateTestPlanetaryReturnResult();

        // Act
        string markdown = PlanetaryReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Firdaria (at Return Ingress)",
            "document should contain the natal-keyed firdaria section evaluated at the PR ingress instant");
    }

    [Fact]
    public void Markdown_ContainsZodiacalReleasingAtReturnIngressHeading()
    {
        // Arrange
        PlanetaryReturnResult result = CreateTestPlanetaryReturnResult();

        // Act
        string markdown = PlanetaryReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Zodiacal Releasing (Spirit) (at Return Ingress)",
            "document should contain the natal-keyed zodiacal releasing section evaluated at the PR ingress instant");
    }

    [Fact]
    public void Markdown_TimeLordSectionsAppearBeforeReturnChartHeading()
    {
        // Arrange
        PlanetaryReturnResult result = CreateTestPlanetaryReturnResult();

        // Act
        string markdown = PlanetaryReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        int profectionIndex = markdown.IndexOf("## Annual Profection (at Return Ingress)", StringComparison.Ordinal);
        int returnChartIndex = markdown.IndexOf("## Return Chart", StringComparison.Ordinal);
        profectionIndex.Should().BeGreaterThan(-1, "the profection section must be present");
        returnChartIndex.Should().BeGreaterThan(profectionIndex,
            "the time-lord block must appear before the Return Chart body so context precedes detail");
    }

    /// <summary>
    /// A planetary return's profection is the year running at the return, by completed anniversaries of the
    /// birth day (<see cref="ProfectionAnalyzer.Analyze"/>): Saturn return #1 of J2000 NYC, 2028-07-21 08:08 UT, is age 28, where the rounding gave 29. Only the solar return rounds to its own year
    /// (<see cref="ProfectionAnalyzer.AnalyzeForReturn"/>). Until branch H this export rounded too.
    /// </summary>
    [Fact]
    public void Markdown_ProfectionAgeIsTheYearRunningAtTheReturn()
    {
        // Arrange
        PlanetaryReturnResult result = CreateTestPlanetaryReturnResult();
        ProfectionYearResult expected = ProfectionAnalyzer.Analyze(
            result.NatalChart, result.ReturnChart.UtcDateTime);
        expected.Age.Should().Be(28);

        // Act
        string markdown = PlanetaryReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain($"**Age:** {expected.Age} (as of",
            "a planetary return's profection is the year running at the return, by completed anniversaries of the birth day; only the solar return rounds to its own year");
    }
}
