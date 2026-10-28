using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;
using AstroCast.Tests.Formatting.Golden;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="CompositeMarkdownFormatter"/> verifying document structure,
/// section headings, and content inclusion.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class CompositeMarkdownFormatterTests
{
    private readonly ChartEngine _engine;
    private readonly CompositeService _compositeService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and CompositeService.
    /// </summary>
    public CompositeMarkdownFormatterTests(EphemerisFixture fixture)
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

        _compositeService = new CompositeService(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, logger);
    }

    /// <summary>
    /// Create a CompositeResult for testing.
    /// </summary>
    private CompositeResult CreateTestCompositeResult()
    {
        ChartResult chartA = _engine.Calculate(new ChartInput
        {
            Name = "Alice",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });

        ChartResult chartB = _engine.Calculate(new ChartInput
        {
            Name = "Bob",
            LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London, UK",
            EventType = "Birth",
        });

        return _compositeService.Calculate(chartA, chartB);
    }

    [Fact]
    public void ToMarkdown_ContainsHeader()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().StartWith("# Composite Chart:",
            "document should start with the composite chart header");
    }

    [Fact]
    public void ToMarkdown_ContainsBothNames()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Alice",
            "document should contain chart A's name");
        markdown.Should().Contain("Bob",
            "document should contain chart B's name");
    }

    [Fact]
    public void ToMarkdown_ContainsReferenceLocation()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Reference Location:**",
            "document should contain the reference location info");
    }

    [Fact]
    public void ToMarkdown_ContainsCompositePlanets()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert — verify all 12 body names appear
        string[] bodies = ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter",
            "Saturn", "Uranus", "Neptune", "Pluto", "True Node", "Chiron"];

        foreach (string body in bodies)
        {
            markdown.Should().Contain(body,
                $"document should contain the body name '{body}'");
        }
    }

    [Fact]
    public void ToMarkdown_ContainsSourceSummaries()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Source Chart A: Alice",
            "document should contain source chart A summary");
        markdown.Should().Contain("Source Chart B: Bob",
            "document should contain source chart B summary");
    }

    /// <summary>
    /// The header line a composite export prints after its Method line (ruling S-D4, branch L2, 1.3.4): what
    /// the export leaves out, and why. Two trailing spaces, as every header line; the dash an em dash.
    /// </summary>
    private const string NotShownLine =
        "**Not shown:** declinations, right ascensions, out-of-bounds, declination aspects, stations, planet " +
        "conditions, applying/separating marks, the Moon's next aspect and void of course \u2014 a composite body " +
        "is a midpoint, not a point in the sky.  ";

    /// <summary>
    /// A composite's aspects print without an applying or separating mark: a midpoint is motionless, so the
    /// mark from the averaged speeds said nothing (Y2-1; S-D4). Until 1.3.4 the table had an Ap/Sp column.
    /// </summary>
    [Fact]
    public void ToMarkdown_Aspects_HaveNoApplyingSeparatingColumn()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Aspects");
        markdown.Should().Contain("| Body 1 | Aspect | Body 2 | Orb |");
        markdown.Should().NotContain("Ap/Sp");
        markdown.Split('\n').Select(l => l.TrimEnd('\r'))
            .Should().NotContain(l => l.EndsWith("| Ap |", StringComparison.Ordinal) || l.EndsWith("| Sp |", StringComparison.Ordinal));
    }

    /// <summary>
    /// The composite's Planetary Positions table has no Dec and no OOB cell; the two source summaries keep
    /// their own table (Y2-5; S-D4).
    /// </summary>
    [Fact]
    public void ToMarkdown_PlanetaryPositions_HaveNoDeclinationOrOutOfBoundsColumns()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        Occurrences(markdown, "| Body | Position | Lat | House | Speed | Dir |").Should().Be(1);
        markdown.Should().NotContain("| Body | Position | Lat | Dec |");
        markdown.Should().NotContain("OOB");
        Occurrences(markdown, "| Body | Position | House | Dir |").Should().Be(2, "the two source summaries keep their table");
    }

    /// <summary>No section built from declinations appears in a composite export (Y2-5, Y2-8; S-D4).</summary>
    [Fact]
    public void ToMarkdown_HasNoEquatorialOutOfBoundsOrDeclinationSections()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("### Equatorial Coordinates");
        markdown.Should().NotContain("### Out-of-Bounds Planets");
        markdown.Should().NotContain("## Declination Aspects");
        markdown.Should().NotContain("### Declination Clusters");
        markdown.Should().NotContain("No declination aspects found");
    }

    /// <summary>
    /// The composite's Moon keeps its phase, sign and via combusta; its next aspect and void of course, found by
    /// a scan over midpoints, are not printed (S-D4).
    /// </summary>
    [Fact]
    public void ToMarkdown_MoonConditions_KeepPhaseSignAndViaCombusta_WithoutVoidOrFirstAspect()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Moon Conditions");
        markdown.Should().Contain("**Phase:**");
        markdown.Should().Contain("**Moon Sign:**");
        markdown.Should().Contain("**Via Combusta:**");
        markdown.Should().NotContain("**Void of Course:**");
        markdown.Should().NotContain("**First Applying Aspect:**");
    }

    /// <summary>The export says once, in its header, what it leaves out: the line after the Method line.</summary>
    [Fact]
    public void ToMarkdown_Header_SaysWhatIsNotShown()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);
        List<string> lines = [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

        Occurrences(markdown, NotShownLine).Should().Be(1);
        int method = lines.FindIndex(l => l.StartsWith("**Method:**", StringComparison.Ordinal));
        method.Should().BeGreaterThanOrEqualTo(0);
        lines[method + 1].Should().Be(NotShownLine);
    }

    /// <summary>A guard: the composite keeps its day or night, the Sect row and the Planetary Sect table.</summary>
    [Fact]
    public void ToMarkdown_KeepsTheCompositeSect()
    {
        CompositeResult result = CreateTestCompositeResult();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("| Sect | ");
        markdown.Should().Contain("### Planetary Sect");
    }

    /// <summary>
    /// The Method line of a composite export, after branch L3 (S-D2, S-D3): the Mercury and Venus rule and the
    /// Midheaven rule, with the house system the composite was cast in.
    /// </summary>
    private const string MethodLine =
        "**Method:** Tropical zodiac, short-arc midpoints (Mercury and Venus nearer the composite Sun); MC the " +
        "midpoint of the two Midheavens, Placidus houses for it at the mean birth latitude  ";

    /// <summary>The Note line the golden pair's export prints for Venus (S-D2), after the Not shown line.</summary>
    private const string VenusNoteLine =
        "**Note:** Venus takes the midpoint nearer the composite Sun, 00\u00b013\u203217\u2033 Aries; the short-arc " +
        "midpoint, 00\u00b013\u203217\u2033 Libra, lies opposite.  ";

    /// <summary>The golden pair: J2000 NYC and Secondary, as the composite golden casts them.</summary>
    private CompositeResult CreateGoldenPairComposite()
    {
        ChartResult j2000 = _engine.Calculate(new ChartInput
        {
            Name = "J2000 NYC Fixture",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
        ChartResult secondary = _engine.Calculate(new ChartInput
        {
            Name = "Secondary Fixture",
            LocalDateTime = new DateTime(1970, 6, 15, 12, 0, 0),
            TimeZone = "America/Los_Angeles",
            Latitude = 34.0522,
            Longitude = -118.2437,
            LocationName = "Los Angeles",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
        return _compositeService.Calculate(j2000, secondary);
    }

    /// <summary>
    /// The header states the method once: short-arc midpoints with Mercury and Venus nearer the composite Sun,
    /// and the MC the midpoint of the two Midheavens with the houses cast for it at the mean birth latitude
    /// (S-D2, S-D3; branch L3, 1.3.4).
    /// </summary>
    [Fact]
    public void ToMarkdown_Header_StatesTheMethod()
    {
        CompositeResult result = CreateGoldenPairComposite();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        Occurrences(markdown, MethodLine).Should().Be(1);
    }

    /// <summary>
    /// The golden pair's Venus takes the midpoint nearer the composite Sun, and the header notes it, once, on the
    /// line after the Not shown line; Mercury keeps its short-arc midpoint and gets no Note (S-D2).
    /// </summary>
    [Fact]
    public void ToMarkdown_Header_NotesVenusTakingTheMidpointNearerTheSun()
    {
        CompositeResult result = CreateGoldenPairComposite();

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);
        List<string> lines = [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

        Occurrences(markdown, VenusNoteLine).Should().Be(1);
        int notShown = lines.FindIndex(l => l.StartsWith("**Not shown:**", StringComparison.Ordinal));
        notShown.Should().BeGreaterThanOrEqualTo(0);
        lines[notShown + 1].Should().Be(VenusNoteLine);
        markdown.Should().NotContain("**Note:** Mercury");
    }

    /// <summary>How many times <paramref name="needle"/> occurs in <paramref name="text"/>, without overlap.</summary>
    private static int Occurrences(string text, string needle)
    {
        int count = 0;
        for (int at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Fact]
    public void ToMarkdown_ContainsFooter()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("*Generated by",
            "document should end with a footer containing version info");
    }

    [Fact]
    public void ToMarkdown_ContainsMethod()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("short-arc midpoints",
            "document should mention the composite method");
    }

    [Fact]
    public void ToMarkdown_ContainsCompositeChartSection()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Composite Chart",
            "document should contain the composite chart section heading");
    }

    [Fact]
    public void ToMarkdown_ContainsHouseCusps()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("House Cusps",
            "document should contain house cusp data");
    }

    [Fact]
    public void ToMarkdown_WithContext_ContainsInterpretationContext()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(
            result, "Long-term romantic partnership, focus on communication patterns");

        // Assert
        markdown.Should().Contain("**Interpretation Context:** Long-term romantic partnership, focus on communication patterns",
            "document should contain the interpretation context when provided");
    }

    [Fact]
    public void ToMarkdown_WithoutContext_OmitsInterpretationContext()
    {
        // Arrange
        CompositeResult result = CreateTestCompositeResult();

        // Act
        string markdown = CompositeMarkdownFormatter.ToMarkdown(result, null);

        // Assert
        markdown.Should().NotContain("**Interpretation Context:**",
            "document should not contain interpretation context when null");
    }

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>
    /// A composite with the five prints the section in its composite form through the embedded chart body
    /// (R6, S-D4): a positions table with no RA, Dec or OOB column and an aspects table with no Ap/Sp column,
    /// between the Planetary Positions and the Essential Dignities. The golden pair: composite Ceres at
    /// 13°44′22″ Cancer in the 8th, direct, and Ceres sextile Saturn at 0°12′ first (Tier B, composite lists).
    /// </summary>
    [Fact]
    public void ToMarkdown_SwitchOn_PrintsTheSectionInItsCompositeForm()
    {
        CompositeResult result = _compositeService.Calculate(
            _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true)),
            _engine.Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints: true)));
        result.CompositeChart.MinorPoints.Should().HaveCount(5);

        string markdown = CompositeMarkdownFormatter.ToMarkdown(result);
        List<string> lines = [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

        int section = lines.IndexOf("## Asteroids and Lilith");
        section.Should().BeGreaterThan(lines.IndexOf("## Planetary Positions"));
        section.Should().BeLessThan(lines.IndexOf("## Essential Dignities"));
        lines[section + 4].Should().Be("### Positions");
        lines[section + 6].Should().Be("| Body | Position | Lat | House | Speed | Dir |");
        lines[section + 7].Should().Be("|------|----------|-----|-------|-------|-----|");
        lines[section + 8].Should().Be("| Ceres | 13°44′22″ Cancer | +01°19′24″ | 8 | 0.265/d | D |");
        lines[section + 9].Should().StartWith("| Pallas | ").And.EndWith("| 7 | -0.061/d | R |");
        lines[section + 13].Should().BeEmpty();
        lines[section + 14].Should().Be("### Aspects (2°00′ orb)");
        lines[section + 16].Should().Be("| Body 1 | Aspect | Body 2 | Orb |");
        lines[section + 17].Should().Be("|--------|--------|--------|-----|");
        lines[section + 18].Should().Be("| Ceres | sxt | Saturn | 0°12′ |");
        lines[section + 22].Should().BeEmpty();
        lines[section + 23].Should().Be("## Essential Dignities");

        Occurrences(markdown, "| Body | Position | Lat | House | Speed | Dir |").Should().Be(2, "the chart's table and the five's");
        Occurrences(markdown, "| Body 1 | Aspect | Body 2 | Orb |").Should().Be(2, "the chart's table and the five's");
        markdown.Should().NotContain("| RA |");
        markdown.Should().NotContain("OOB");
        markdown.Should().NotContain("Ap/Sp");
    }

    #endregion
}
