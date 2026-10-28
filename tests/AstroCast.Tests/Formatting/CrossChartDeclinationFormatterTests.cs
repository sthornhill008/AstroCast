using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests verifying that the cross-chart declination aspects section is correctly
/// emitted (or suppressed) by each export formatter: Synastry (covers Event Type B),
/// Solar Return, Lunar Return, and Electional.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class CrossChartDeclinationFormatterTests
{
    private readonly ChartEngine _chartEngine;
    private readonly SynastryService _synastryService;
    private readonly SolarReturnService _solarReturnService;
    private readonly LunarReturnService _lunarReturnService;
    private readonly ElectionalService _electionalService;

    /// <summary>NYC latitude.</summary>
    private const double Latitude = 40.7128;

    /// <summary>NYC longitude.</summary>
    private const double Longitude = -74.006;

    /// <summary>NYC timezone.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    public CrossChartDeclinationFormatterTests(EphemerisFixture fixture)
    {
        EphemerisService ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(ephemeris, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        MoonConditionService moonCondition = new();
        ChartContextService chartContext = new(dignity, new SectService());
        FixedStarService fixedStars = new(ephemeris, logger);
        DeclinationAspectService declinationAspects = new();

        _chartEngine = new ChartEngine(
            ephemeris, aspects, midpoints, dignity, moonCondition,
            chartContext, fixedStars, declinationAspects, new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        _synastryService = new SynastryService(logger);
        _solarReturnService = new SolarReturnService(ephemeris, _chartEngine, logger);
        _lunarReturnService = new LunarReturnService(ephemeris, _chartEngine, logger);
        _electionalService = new ElectionalService(ephemeris, _chartEngine);
    }

    /// <summary>
    /// Creates a natal chart (J2000.0, NYC) for testing.
    /// </summary>
    private ChartResult CreateNatalChart()
    {
        return _chartEngine.Calculate(new ChartInput
        {
            Name = "Alice",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            EventType = "Birth",
        });
    }

    /// <summary>
    /// Creates a second chart (different date) for synastry testing.
    /// </summary>
    private ChartResult CreateSecondChart()
    {
        return _chartEngine.Calculate(new ChartInput
        {
            Name = "Bob",
            LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London, UK",
            EventType = "Birth",
        });
    }

    #region Synastry (covers Event Type B)

    [Fact]
    public void Synastry_ToMarkdown_ContainsInterChartDeclinationSection()
    {
        // Arrange
        ChartResult personA = CreateNatalChart();
        ChartResult personB = CreateSecondChart();
        SynastryResult result = _synastryService.Calculate(personA, personB);

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert — section should be present (real charts will have declination aspects)
        markdown.Should().Contain("Inter-Chart Declination Aspects",
            "synastry output should contain the inter-chart declination section");
    }

    [Fact]
    public void Synastry_ToMarkdown_ContainsPersonAPrefixInDeclinationTable()
    {
        // Arrange
        ChartResult personA = CreateNatalChart();
        ChartResult personB = CreateSecondChart();
        SynastryResult result = _synastryService.Calculate(personA, personB);

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("| Person A |",
            "declination table should use 'Person A' column header");
        markdown.Should().Contain("| Person B |",
            "declination table should use 'Person B' column header");
    }

    [Fact]
    public void Synastry_ToMarkdown_DeclinationTableContainsAspectAbbreviation()
    {
        // Arrange
        ChartResult personA = CreateNatalChart();
        ChartResult personB = CreateSecondChart();
        SynastryResult result = _synastryService.Calculate(personA, personB);

        // Act
        string markdown = SynastryMarkdownFormatter.ToMarkdown(result);

        // Assert — at least one Par or CPar should appear
        bool hasParOrCpar = markdown.Contains("| Par |") || markdown.Contains("| CPar |");
        hasParOrCpar.Should().BeTrue(
            "declination table should contain Par or CPar aspect abbreviations");
    }

    #endregion

    #region Solar Return

    [Fact]
    public void SolarReturn_ToMarkdown_ContainsReturnToNatalDeclinationSection()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Return-to-Natal Declination Aspects",
            "solar return output should contain the return-to-natal declination section");
    }

    [Fact]
    public void SolarReturn_ToMarkdown_UsesSRAndNAPrefixes()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("| Return |",
            "declination table should use 'Return' column header");
        markdown.Should().Contain("| Natal |",
            "declination table should use 'Natal' column header");
    }

    [Fact]
    public void SolarReturn_ToMarkdown_DeclinationSectionBeforeFooter()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        SolarReturnResult result = _solarReturnService.Calculate(
            natal, 2025, Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert — declination section should appear before the footer
        int decSectionIndex = markdown.IndexOf("Return-to-Natal Declination Aspects");
        int footerIndex = markdown.IndexOf("*Generated by");

        decSectionIndex.Should().BeGreaterThan(-1,
            "declination section should be present");
        footerIndex.Should().BeGreaterThan(decSectionIndex,
            "declination section should appear before the footer");
    }

    #endregion

    #region Lunar Return

    [Fact]
    public void LunarReturn_ToMarkdown_ContainsReturnToNatalDeclinationSection()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal,
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = LunarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Return-to-Natal Declination Aspects",
            "lunar return output should contain the return-to-natal declination section");
    }

    [Fact]
    public void LunarReturn_ToMarkdown_UsesLRAndNAPrefixes()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal,
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = LunarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("| Return |",
            "declination table should use 'Return' column header");
        markdown.Should().Contain("| Natal |",
            "declination table should use 'Natal' column header");
    }

    [Fact]
    public void LunarReturn_ToMarkdown_DeclinationSectionBeforeFooter()
    {
        // Arrange
        ChartResult natal = CreateNatalChart();
        LunarReturnResult result = _lunarReturnService.CalculateNext(
            natal,
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Latitude, Longitude, "New York City", TimeZone);

        // Act
        string markdown = LunarReturnMarkdownFormatter.ToMarkdown(result);

        // Assert
        int decSectionIndex = markdown.IndexOf("Return-to-Natal Declination Aspects");
        int footerIndex = markdown.IndexOf("*Generated by");

        decSectionIndex.Should().BeGreaterThan(-1,
            "declination section should be present");
        footerIndex.Should().BeGreaterThan(decSectionIndex,
            "declination section should appear before the footer");
    }

    #endregion

    #region Electional

    [Fact]
    public void Electional_ToMarkdown_ContainsCandidateDeclinationSection()
    {
        // Arrange
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 3),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 2,
            MinimumCandidateSeparationHours = 0.0,
        };

        ElectionalResult result = _electionalService.Calculate(input);

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Candidate-to-Natal Declination Aspects",
            "electional output should contain per-candidate declination sections");
    }

    [Fact]
    public void Electional_ToMarkdown_UsesCAAndNAPrefixes()
    {
        // Arrange
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 3),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 2,
            MinimumCandidateSeparationHours = 0.0,
        };

        ElectionalResult result = _electionalService.Calculate(input);

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("| Candidate |",
            "declination table should use 'Candidate' column header");
        markdown.Should().Contain("| Natal |",
            "declination table should use 'Natal' column header");
    }

    [Fact]
    public void Electional_ToMarkdown_DeclinationSectionAfterNatalHarmony()
    {
        // Arrange
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 3),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 2,
            MinimumCandidateSeparationHours = 0.0,
        };

        ElectionalResult result = _electionalService.Calculate(input);

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert — declination section should appear after natal harmony
        int natalHarmonyIndex = markdown.IndexOf("### Natal Harmony");
        int decSectionIndex = markdown.IndexOf("Candidate-to-Natal Declination Aspects");
        int chartDataIndex = markdown.IndexOf("### Chart Data");

        natalHarmonyIndex.Should().BeGreaterThan(-1,
            "natal harmony section should be present");
        decSectionIndex.Should().BeGreaterThan(natalHarmonyIndex,
            "declination section should appear after natal harmony");
        chartDataIndex.Should().BeGreaterThan(decSectionIndex,
            "chart data should appear after declination section");
    }

    #endregion

    #region Shared Table Format

    [Fact]
    public void AppendCrossChartDeclinationTable_EmptyAspects_SuppressesSection()
    {
        // Arrange
        System.Text.StringBuilder sb = new();
        List<DeclinationAspect> emptyAspects = [];

        // Act
        PositionFormatter.AppendCrossChartDeclinationTable(sb, emptyAspects,
            "## Test Section", "A", "Chart A", "A Dec", "B", "Chart B", "B Dec");

        // Assert
        sb.ToString().Should().BeEmpty(
            "section should be entirely suppressed when no aspects exist");
    }

    [Fact]
    public void AppendCrossChartDeclinationTable_WithAspects_EmitsTableWithHeaders()
    {
        // Arrange
        System.Text.StringBuilder sb = new();
        List<DeclinationAspect> aspects =
        [
            new()
            {
                Body1Name = "Sun",
                Body1Declination = 20.0,
                Body2Name = "Mars",
                Body2Declination = 20.3,
                IsParallel = true,
                Orb = 0.3,
            },
        ];

        // Act
        PositionFormatter.AppendCrossChartDeclinationTable(sb, aspects,
            "## Test Section", "SR", "Return", "SR Dec", "NA", "Natal", "Na Dec");

        // Assert
        string output = sb.ToString();
        output.Should().Contain("## Test Section");
        output.Should().Contain("| Return |");
        output.Should().Contain("| Natal |");
        output.Should().Contain("| SR Sun |");
        output.Should().Contain("| NA Mars |");
        output.Should().Contain("| Par |");
    }

    [Fact]
    public void AppendCrossChartDeclinationTable_OrbFormattedAsDegreeMinute()
    {
        // Arrange
        System.Text.StringBuilder sb = new();
        List<DeclinationAspect> aspects =
        [
            new()
            {
                Body1Name = "Moon",
                Body1Declination = 10.0,
                Body2Name = "Jupiter",
                Body2Declination = -10.5,
                IsParallel = false,
                Orb = 0.5,
            },
        ];

        // Act
        PositionFormatter.AppendCrossChartDeclinationTable(sb, aspects,
            "## Test", "A", "A", "A Dec", "B", "B", "B Dec");

        // Assert — 0.5° = 0°30′ via the shared D°MM′ orb formatter
        // (unified across Electional export sites, 2026-06-10 live-run review)
        string output = sb.ToString();
        output.Should().Contain("0\u00b030\u2032",
            "0.5 deg orb should be formatted as 0°30′ by the shared formatter");
        output.Should().Contain("| CPar |");
    }

    #endregion
}
