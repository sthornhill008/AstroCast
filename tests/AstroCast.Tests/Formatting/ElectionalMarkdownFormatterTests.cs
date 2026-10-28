using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ElectionalMarkdownFormatter"/> verifying document structure,
/// section headings, content inclusion, and file output.
/// Uses a real electional calculation (3-day range, NYC) for end-to-end coverage.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalMarkdownFormatterTests
{
    private readonly ElectionalService _service;
    private readonly ChartEngine _chartEngine;

    /// <summary>NYC latitude.</summary>
    private const double Latitude = 40.7128;

    /// <summary>NYC longitude.</summary>
    private const double Longitude = -74.006;

    /// <summary>NYC timezone.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    public ElectionalMarkdownFormatterTests(EphemerisFixture fixture)
    {
        EphemerisService ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _service = new ElectionalService(ephemeris, _chartEngine);
    }

    /// <summary>
    /// Creates a natal chart for testing (J2000.0, NYC).
    /// </summary>
    private ChartResult CreateNatalChart()
    {
        ChartInput input = new()
        {
            Name = "Test Client",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            EventType = "Birth",
        };

        return _chartEngine.Calculate(input);
    }

    /// <summary>
    /// Creates a standard electional result for formatter testing.
    /// Uses a 3-day range for fast execution.
    /// </summary>
    private ElectionalResult CreateTestResult()
    {
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
            CandidateCount = 3,
            MinimumCandidateSeparationHours = 0.0,
        };

        return _service.Calculate(input);
    }

    #region Test 1: Report Header

    [Fact]
    public void ToMarkdown_Should_ContainReportHeader()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("# Electional Report");
    }

    #endregion

    #region Test 2: Natal Chart Summary

    [Fact]
    public void ToMarkdown_Should_ContainNatalChartSummary()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Natal Chart Summary");
    }

    #endregion

    #region Test 3: All Candidate Sections

    [Fact]
    public void ToMarkdown_Should_ContainAllCandidateSections()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Candidate instant 1");
        markdown.Should().Contain("## Candidate instant 2");
        markdown.Should().Contain("## Candidate instant 3");
    }

    #endregion

    #region Test 4: Score Breakdown Table

    [Fact]
    public void ToMarkdown_Should_ContainScoreBreakdownTable()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("### Score Breakdown");
        markdown.Should().Contain("| Dimension |");
    }

    #endregion

    #region Test 5: Disqualified Periods Section

    [Fact]
    public void ToMarkdown_Should_ContainDisqualifiedPeriodsSection()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Disqualified Periods");
    }

    /// <summary>
    /// After the Disqualified Periods heading and its blank line, one line says which rules disqualify (E2-3): the
    /// Moon void of course always, and Mercury retrograde only when the search avoids it.
    /// </summary>
    /// <param name="avoidMercuryRetrograde">Whether the search avoids Mercury retrograde.</param>
    /// <param name="rules">The expected rules line.</param>
    [Theory]
    [InlineData(true, "Disqualifying rules: the Moon void of course and Mercury retrograde.")]
    [InlineData(false, "Disqualifying rules: the Moon void of course (Mercury retrograde not avoided).")]
    public void Electional_DisqualifyingRules_Line(bool avoidMercuryRetrograde, string rules)
    {
        ElectionalResult result = _service.Calculate(new ElectionalInput
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 2),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 1,
            AvoidMercuryRetrograde = avoidMercuryRetrograde,
        });

        List<string> lines = [.. ElectionalMarkdownFormatter.ToMarkdown(result).Split('\n').Select(l => l.TrimEnd('\r'))];

        int heading = lines.IndexOf("## Disqualified Periods");
        heading.Should().BeGreaterThan(0);
        lines[heading + 1].Should().BeEmpty();
        lines[heading + 2].Should().Be(rules);
        lines[heading + 3].Should().BeEmpty();
    }

    #endregion

    #region Test 6: VOC Calendar

    [Fact]
    public void ToMarkdown_Should_ContainVocCalendar()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Moon Void-of-Course Calendar");
    }

    #endregion

    #region Test 7: Strengths and Compromises

    [Fact]
    public void ToMarkdown_Should_ContainStrengthsAndCompromises()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("### Strengths");
        markdown.Should().Contain("### Compromises");
    }

    #endregion

    #region Test 8: Planetary Hour Info

    [Fact]
    public void ToMarkdown_Should_ContainPlanetaryHourInfo()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Planetary Hour:**");
    }

    #endregion

    #region Test 9: SaveAsync

    [Fact]
    public async Task SaveAsync_Should_CreateFile()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();
        string tempFile = Path.Combine(Path.GetTempPath(), $"electional_test_{Guid.NewGuid()}.md");

        try
        {
            // Act
            await ElectionalMarkdownFormatter.SaveAsync(result, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue("the file should be created");
            long fileSize = new FileInfo(tempFile).Length;
            fileSize.Should().BeGreaterThan(0, "the file should have content");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Test 10: Election Type and Goal in Header

    [Fact]
    public void ToMarkdown_ElectionTypeAndGoal_Should_AppearInHeader()
    {
        // Arrange
        ElectionalResult result = CreateTestResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("BusinessLaunch");
        markdown.Should().Contain("Launch new product");
    }

    #endregion
}
