using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="SynastryMarkdownFormatter"/> verifying document structure,
/// section headings, and content inclusion.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SynastryMarkdownFormatterTests
{
    private readonly ChartEngine _engine;
    private readonly SynastryService _synastryService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and SynastryService.
    /// </summary>
    public SynastryMarkdownFormatterTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _synastryService = new SynastryService(logger);
    }

    /// <summary>
    /// Create a SynastryResult for testing.
    /// </summary>
    private SynastryResult CreateTestSynastryResult()
    {
        ChartResult personA = _engine.Calculate(new ChartInput
        {
            Name = "Alice",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });

        ChartResult personB = _engine.Calculate(new ChartInput
        {
            Name = "Bob",
            LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London, UK",
            EventType = "Birth",
        });

        return _synastryService.Calculate(personA, personB);
    }

    /// <summary>
    /// The inter-chart aspect and cross-chart declination orbs print as truncated D°MM′ through one
    /// function (branch O-D3, 1.3.4): J2000 NYC against the secondary natal, A Neptune trine B Mercury
    /// 0°05′ and A Neptune parallel B Neptune 0°59′ (59′58″). Until 1.3.4 the aspect printed "0°05'"
    /// with an apostrophe and the parallel rounded to "1°00′".
    /// </summary>
    [Fact]
    public void ToMarkdown_J2000AndSecondary_PrintsOrbsAsTruncatedDegreesAndMinutes()
    {
        SynastryResult result = _synastryService.Calculate(
            _engine.Calculate(GoldenFixtures.J2000NycInput()),
            _engine.Calculate(GoldenFixtures.SecondaryNatalInput()));

        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("| A Neptune | tri | B Mercury | 0\u00b005\u2032 | exact |");
        markdown.Should().Contain(
            "| A Neptune | -19\u00b012\u203247\u2033 | Par | B Neptune | -18\u00b012\u203249\u2033 | 0\u00b059\u2032 |");
    }

    [Fact]
    public void ToMarkdown_Should_ContainSynastryHeader_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().StartWith("# Synastry:",
            "document should start with the synastry header");
    }

    [Fact]
    public void ToMarkdown_Should_ContainBothNames_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Person A: Alice",
            "document should contain Person A's name");
        markdown.Should().Contain("Person B: Bob",
            "document should contain Person B's name");
    }

    [Fact]
    public void ToMarkdown_Should_ContainInterChartAspects_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Inter-Chart Aspects",
            "document should contain the inter-chart aspects section");
        markdown.Should().Contain("| A Body | Aspect | B Body | Orb | Notes |",
            "document should contain the aspects table header");
    }

    [Fact]
    public void ToMarkdown_Should_ContainHouseOverlays_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain(
            "House Overlays: Alice's Planets in Bob's Houses",
            "document should contain A-in-B house overlays section");
        markdown.Should().Contain(
            "House Overlays: Bob's Planets in Alice's Houses",
            "document should contain B-in-A house overlays section");
    }

    [Fact]
    public void ToMarkdown_Should_ContainNatalPositions_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Planetary Positions",
            "document should include natal positions from both charts");
        markdown.Should().Contain("## House Cusps",
            "document should include house cusps from both charts");
    }

    [Fact]
    public void ToMarkdown_Should_ContainFooter_When_Generated()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("*Generated by",
            "document should end with a footer containing version info");
    }

    [Fact]
    public void ToMarkdown_Should_ContainRelationshipType_When_Specified()
    {
        // Arrange
        SynastryResult result = CreateTestSynastryResultWithRelationship(
            RelationshipType.Marriage, "married 15 years");

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Relationship:** Marriage",
            "document should contain the relationship type when specified");
        markdown.Should().Contain("**Relationship Notes:** married 15 years",
            "document should contain the relationship notes when provided");
    }

    [Fact]
    public void ToMarkdown_Should_OmitRelationshipType_When_Unspecified()
    {
        // Arrange — default CreateTestSynastryResult uses Unspecified
        SynastryResult result = CreateTestSynastryResult();

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("**Relationship:**",
            "document should not contain relationship line when Unspecified");
    }

    /// <summary>
    /// Create a SynastryResult with relationship type and notes for testing.
    /// </summary>
    private SynastryResult CreateTestSynastryResultWithRelationship(
        RelationshipType relationshipType,
        string? relationshipNotes = null)
    {
        ChartResult personA = _engine.Calculate(new ChartInput
        {
            Name = "Alice",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });

        ChartResult personB = _engine.Calculate(new ChartInput
        {
            Name = "Bob",
            LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London, UK",
            EventType = "Birth",
        });

        return _synastryService.Calculate(personA, personB, relationshipType, relationshipNotes);
    }

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>
    /// With both charts carrying the five, the export prints <c>## Inter-Chart Aspects: Asteroids and Lilith</c>
    /// directly after the Inter-Chart Aspects section and its rule, with the same spacing and its own rule after,
    /// before the first House Overlays heading (R6, scoping decision 8): the opening line, the table with the
    /// inter-chart columns, and the ten rows of Tier B's synastry list, A Ceres conjunct B Uranus at 0°12′ first.
    /// </summary>
    [Fact]
    public void ToMarkdown_SwitchOn_PrintsTheMinorPointSection_AfterInterChartAspects_BeforeTheOverlays()
    {
        SynastryResult result = _synastryService.Calculate(
            _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true)),
            _engine.Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints: true)));
        result.MinorPointAspects.Should().HaveCount(10);

        List<string> lines = [.. SynastryMarkdownFormatter.ToMarkdown(result).Split('\n').Select(l => l.TrimEnd('\r'))];

        int aspects = lines.IndexOf("## Inter-Chart Aspects");
        int section = lines.IndexOf("## Inter-Chart Aspects: Asteroids and Lilith");
        int overlays = lines.FindIndex(l => l.StartsWith("## House Overlays:", StringComparison.Ordinal));
        aspects.Should().BeGreaterThan(0);
        section.Should().BeGreaterThan(aspects);
        overlays.Should().BeGreaterThan(section);
        lines.FindIndex(aspects + 1, l => l.StartsWith("## ", StringComparison.Ordinal)).Should().Be(section, "the section follows the Inter-Chart Aspects section directly");
        lines.FindIndex(section + 1, l => l.StartsWith("## ", StringComparison.Ordinal)).Should().Be(overlays, "the first House Overlays heading follows it");

        lines[section - 2].Should().Be("---");
        lines[section - 1].Should().BeEmpty();
        lines[section + 1].Should().BeEmpty();
        lines[section + 2].Should().Be(
            "Each chart's Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee) to the other chart's " +
            "Sun to Pluto, Ascendant and Midheaven: the five Ptolemaic aspects at a 2°00′ orb.");
        lines[section + 3].Should().BeEmpty();
        lines[section + 4].Should().Be("| A Body | Aspect | B Body | Orb | Notes |");
        lines[section + 5].Should().Be("|--------|--------|--------|-----|-------|");
        lines[section + 6].Should().Be("| A Ceres | cnj | B Uranus | 0°12′ |  |");
        lines[section + 7].Should().Be("| B Pallas | tri | A Moon | 0°44′ |  |");
        lines[section + 15].Should().Be("| B Vesta | sqr | A Venus | 1°58′ |  |");
        lines[section + 16].Should().BeEmpty();
        lines[section + 17].Should().Be("---");
        lines[section + 18].Should().BeEmpty();
        (section + 19).Should().Be(overlays);
    }

    #endregion
}
