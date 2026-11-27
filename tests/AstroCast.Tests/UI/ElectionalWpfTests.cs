using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.UI;

/// <summary>
/// Tests for the electional WPF workflow verifying data flow through
/// ElectionalInput, ElectionalService, and ElectionalMarkdownFormatter.
/// These tests validate the electional-specific properties and integration
/// across the calculate → export lifecycle.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalWpfTests
{
    private readonly EphemerisService _ephemerisService;
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ElectionalWpfTests(EphemerisFixture fixture)
    {
        _ephemerisService = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Helpers

    /// <summary>Create a natal chart input for testing.</summary>
    private static ChartInput CreateNatalInput() =>
        new()
        {
            Name = "Electional Test Subject",
            LocalDateTime = new DateTime(1990, 3, 15, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            EventType = "Birth",
        };

    /// <summary>Create an electional input bound to a natal chart result.</summary>
    private ElectionalInput CreateElectionalInput(
        ChartResult natalChart,
        ElectionType type = ElectionType.BusinessLaunch,
        string goal = "Launch new product line",
        int days = 3)
    {
        return new ElectionalInput
        {
            NatalChart = natalChart,
            ElectionType = type,
            ElectionGoal = goal,
            RangeStart = new DateOnly(2025, 7, 1),
            RangeEnd = new DateOnly(2025, 7, 1).AddDays(days),
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            TimeZone = "America/New_York",
            CandidateCount = 3,
            AvoidMercuryRetrograde = true,
        };
    }

    #endregion

    #region ElectionType Enum Tests

    [Fact]
    public void ElectionType_Should_Contain_AllExpectedValues()
    {
        // Act
        ElectionType[] values = Enum.GetValues<ElectionType>();

        // Assert
        values.Should().HaveCount(9, "there are 9 defined election types");
        values.Should().Contain(ElectionType.BusinessLaunch);
        values.Should().Contain(ElectionType.Wedding);
        values.Should().Contain(ElectionType.Contract);
        values.Should().Contain(ElectionType.Surgery);
        values.Should().Contain(ElectionType.Other);
    }

    #endregion

    #region TimeConstraint Tests

    [Fact]
    public void TimeConstraint_Should_StoreEarliestAndLatest()
    {
        // Arrange & Act
        TimeConstraint constraint = new()
        {
            Earliest = new TimeOnly(9, 0),
            Latest = new TimeOnly(17, 0),
        };

        // Assert
        constraint.Earliest.Should().Be(new TimeOnly(9, 0),
            "Earliest should store 09:00");
        constraint.Latest.Should().Be(new TimeOnly(17, 0),
            "Latest should store 17:00");
    }

    [Fact]
    public void ElectionalInput_WithTimeConstraint_Should_Persist()
    {
        // Arrange
        ChartInput natalInput = CreateNatalInput();
        ChartResult natalChart = _engine.Calculate(natalInput);

        TimeConstraint constraint = new()
        {
            Earliest = new TimeOnly(10, 0),
            Latest = new TimeOnly(14, 0),
        };

        // Act
        ElectionalInput input = new()
        {
            NatalChart = natalChart,
            ElectionType = ElectionType.Wedding,
            ElectionGoal = "Wedding ceremony",
            RangeStart = new DateOnly(2025, 9, 1),
            RangeEnd = new DateOnly(2025, 9, 30),
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            TimeZone = "America/New_York",
            TimeConstraint = constraint,
        };

        // Assert
        input.TimeConstraint.Should().NotBeNull();
        input.TimeConstraint!.Earliest.Should().Be(new TimeOnly(10, 0));
        input.TimeConstraint.Latest.Should().Be(new TimeOnly(14, 0));
        input.ElectionType.Should().Be(ElectionType.Wedding);
    }

    #endregion

    #region ElectionalService Integration Tests

    [Fact]
    public void ElectionalService_Calculate_Should_ReturnResult()
    {
        // Arrange — use a very short date range to keep the test fast
        ChartInput natalInput = CreateNatalInput();
        ChartResult natalChart = _engine.Calculate(natalInput);
        ElectionalInput electInput = CreateElectionalInput(natalChart, days: 2);

        ElectionalService service = new(_ephemerisService, _engine);

        // Act
        ElectionalResult result = service.Calculate(electInput);

        // Assert
        result.Should().NotBeNull();
        result.Input.Should().BeSameAs(electInput);
        result.NatalChart.Should().BeSameAs(natalChart);
        result.Candidates.Should().NotBeNull();
        result.CalculatedAt.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public void ElectionalService_Calculate_Should_RankCandidates()
    {
        // Arrange
        ChartInput natalInput = CreateNatalInput();
        ChartResult natalChart = _engine.Calculate(natalInput);
        ElectionalInput electInput = CreateElectionalInput(natalChart, days: 2);

        ElectionalService service = new(_ephemerisService, _engine);

        // Act
        ElectionalResult result = service.Calculate(electInput);

        // Assert — candidates should be ranked in descending score order
        if (result.Candidates.Count > 1)
        {
            for (int i = 1; i < result.Candidates.Count; i++)
            {
                result.Candidates[i].Score.Should().BeLessThanOrEqualTo(
                    result.Candidates[i - 1].Score,
                    "candidates should be ranked by descending score");
            }
        }

        // Rank numbers should be sequential starting from 1
        for (int i = 0; i < result.Candidates.Count; i++)
        {
            result.Candidates[i].Rank.Should().Be(i + 1,
                "rank should be sequential starting from 1");
        }
    }

    #endregion

    #region ElectionalMarkdownFormatter Tests

    [Fact]
    public void ElectionalMarkdownFormatter_Should_ContainReportHeader()
    {
        // Arrange
        ChartInput natalInput = CreateNatalInput();
        ChartResult natalChart = _engine.Calculate(natalInput);
        ElectionalInput electInput = CreateElectionalInput(natalChart, days: 2);

        ElectionalService service = new(_ephemerisService, _engine);
        ElectionalResult result = service.Calculate(electInput);

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("# Electional Report",
            "markdown should contain the H1 title");
        // the type's display name since branch ExportText (1.7.2; finding N11), until then "BusinessLaunch"
        markdown.Should().Contain("**Election Type:** Business Launch",
            "markdown should contain the election type");
        markdown.Should().Contain("**Election Goal:** Launch new product line",
            "markdown should contain the election goal");
        markdown.Should().Contain("Electional Test Subject",
            "markdown should contain the natal chart subject name");
    }

    [Fact]
    public void ElectionalMarkdownFormatter_Should_ContainDateRange()
    {
        // Arrange
        ChartInput natalInput = CreateNatalInput();
        ChartResult natalChart = _engine.Calculate(natalInput);
        ElectionalInput electInput = CreateElectionalInput(natalChart, days: 2);

        ElectionalService service = new(_ephemerisService, _engine);
        ElectionalResult result = service.Calculate(electInput);

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Date Range:** 2025-07-01 to 2025-07-03",
            "markdown should contain the date range");
        markdown.Should().Contain("Lynchburg, VA",
            "markdown should contain the event location name");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ElectionalMarkdownFormatter_Should_ContainDateRange"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ElectionalMarkdownFormatter_Should_ContainDateRange_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ElectionalMarkdownFormatter_Should_ContainDateRange();
    }

    #endregion
}
