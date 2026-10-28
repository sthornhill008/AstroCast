using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// End-to-end integration tests for the Daily Horoscope export pipeline.
/// Uses a real natal chart (Bill Clinton: 1946-08-19, 08:51, Hope, Arkansas)
/// and a 7-day transit range to verify the complete pipeline: calculate transits,
/// build daily data, render Markdown.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DailyHoroscopeIntegrationTests
{
    private readonly EphemerisService _ephemeris;
    private readonly TransitService _transitService;
    private readonly ChartResult _natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds Bill Clinton's natal chart,
    /// and constructs the TransitService for integration testing.
    /// </summary>
    public DailyHoroscopeIntegrationTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(
            _ephemeris, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_ephemeris, logger),
            new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        // Bill Clinton: 1946-08-19, 08:51, Hope, Arkansas
        ChartInput input = new()
        {
            Name = "Bill Clinton",
            LocalDateTime = new DateTime(1946, 8, 19, 8, 51, 0),
            TimeZone = "America/Chicago",
            Latitude = 33.6673,
            Longitude = -93.5916,
            LocationName = "Hope, Arkansas",
            EventType = "Birth",
        };

        _natalChart = engine.Calculate(input);
        _transitService = new TransitService(_ephemeris, logger);
    }

    /// <summary>Calculates a 7-day transit result for March 13–19, 2026.</summary>
    private TransitResult CalculateWeekTransits()
    {
        TransitInput input = new()
        {
            NatalChart = _natalChart,
            ReportStartDate = new DateOnly(2026, 3, 13),
            ReportEndDate = new DateOnly(2026, 3, 19),
            IncludeMinorAspects = false,
        };

        return _transitService.Calculate(input);
    }

    /// <summary>Builds daily data from the 7-day transit result.</summary>
    private (TransitResult Result, IReadOnlyList<DailyHoroscopeDay> Days) BuildFullPipeline()
    {
        TransitResult result = CalculateWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);
        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/Chicago");
        return (result, days);
    }

    [Fact]
    public void FullPipeline_ProducesValidMarkdown()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(
            result, days,
            birthTimeSource: "Birth certificate",
            timeZoneId: "America/Chicago");

        // Document header
        md.Should().Contain("# Daily Horoscope Data");
        md.Should().Contain("Bill Clinton");

        // Natal chart summary
        md.Should().Contain("## Natal Chart Summary");

        // Slow-moving backdrop
        md.Should().Contain("## Slow-Moving Transit Backdrop");

        // Exactly 7 daily snapshot headers
        int headerCount = md.Split("### ").Length - 1;
        headerCount.Should().Be(7, "a 7-day range should produce 7 daily snapshot sections");

        // Each day should have a Moon line
        md.Split("**Moon:**").Length.Should().Be(8, "7 days + 1 (before first occurrence) = 8 splits");

        // Valid text
        md.Should().NotContain("\0", "output should not contain null characters");
        md.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildDailyData_RealChart_MoonSignsVary()
    {
        (_, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        // Over 7 days the Moon moves through ~2-3 signs
        int distinctSigns = days.Select(d => d.MoonSign).Distinct().Count();
        distinctSigns.Should().BeGreaterThanOrEqualTo(2,
            "over a 7-day period the Moon should pass through at least 2 different signs");
    }

    [Fact]
    public void FullPipeline_ActiveTransitsPresent()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        // At least one day should have an Active (in orb) table
        md.Should().Contain("**Active (in orb):**",
            "outer planet transits should create active transit entries");
    }

    [Fact]
    public async Task FullPipeline_SaveAsync_WritesFile()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string tempPath = Path.Combine(Path.GetTempPath(), $"daily_horoscope_test_{Guid.NewGuid()}.md");
        try
        {
            await DailyHoroscopeMarkdownFormatter.SaveAsync(
                result, days, tempPath,
                birthTimeSource: "Birth certificate",
                timeZoneId: "America/Chicago");

            File.Exists(tempPath).Should().BeTrue("SaveAsync should create the file");

            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().NotBeEmpty("the file should contain Markdown content");
            content.Should().Contain("# Daily Horoscope Data");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FullPipeline_DayOfWeekFormatting()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        // March 13, 2026 is a Friday
        md.Should().Contain("### Friday, March 13, 2026");
    }

    [Fact]
    public void FullPipeline_NoDuplicateTransitsInSingleDay()
    {
        (_, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        foreach (DailyHoroscopeDay day in days)
        {
            // Check for duplicates: same transit planet + aspect + natal target
            List<string> keys = day.ActiveTransits
                .Select(t => $"{t.TransitPlanet}|{t.AspectAbbrev}|{t.NatalTarget}")
                .ToList();

            keys.Should().OnlyHaveUniqueItems(
                $"day {day.Date} should not have duplicate transit entries");
        }
    }

    [Fact]
    public void FullPipeline_OuterPlanetsInBackdrop()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        // The backdrop section should contain at least some outer planet names
        string backdropSection = ExtractSection(md, "## Slow-Moving Transit Backdrop", "## Daily Snapshots");

        // Saturn and Pluto should almost certainly have transits to Clinton's chart
        backdropSection.Should().NotBeEmpty("the backdrop section should contain transit data");
        backdropSection.Should().Contain("TR ",
            "outer planet transits should appear with TR prefix");
    }

    [Fact]
    public async Task FullPipeline_GenerateSampleOutput()
    {
        (TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) = BuildFullPipeline();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(
            result, days,
            birthTimeSource: "Birth certificate",
            interpretationContext: "Focus on career and public image themes.",
            timeZoneId: "America/Chicago");

        string samplePath = Path.Combine(Path.GetTempPath(), "daily_horoscope_sample.md");
        await File.WriteAllTextAsync(samplePath, md);

        // This test just generates a sample for manual review
        md.Should().NotBeEmpty();
    }

    /// <summary>
    /// Extracts a section of Markdown between two headers.
    /// </summary>
    private static string ExtractSection(string md, string startHeader, string endHeader)
    {
        int startIdx = md.IndexOf(startHeader, StringComparison.Ordinal);
        if (startIdx < 0) return string.Empty;

        int endIdx = md.IndexOf(endHeader, startIdx + startHeader.Length, StringComparison.Ordinal);
        if (endIdx < 0) endIdx = md.Length;

        return md[startIdx..endIdx];
    }

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="FullPipeline_DayOfWeekFormatting"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void FullPipeline_DayOfWeekFormatting_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        FullPipeline_DayOfWeekFormatting();
    }
}
