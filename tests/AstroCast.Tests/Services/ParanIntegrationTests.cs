using System.Text;

using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// End-to-end integration tests for the paran calculation pipeline:
/// calculation through Markdown export, astronomical correctness validation,
/// and location-dependent paran variation.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ParanIntegrationTests
{
    private readonly ParanService _paranService;
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all required services.
    /// </summary>
    public ParanIntegrationTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        _paranService = new ParanService(_ephemeris, logger);

        AspectService aspectService = new(_ephemeris, logger);
        MidpointService midpointService = new(logger);
        DignityService dignityService = new();
        MoonConditionService moonConditionService = new();
        ChartContextService chartContextService = new(dignityService, new SectService());
        FixedStarService fixedStarService = new(_ephemeris, logger);
        DeclinationAspectService declinationAspectService = new();

        _chartEngine = new ChartEngine(
            _ephemeris, aspectService, midpointService, dignityService,
            moonConditionService, chartContextService, fixedStarService,
            declinationAspectService, new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Full Pipeline

    [Fact]
    public void FullPipeline_Should_ProduceValidMarkdown_When_CalculatingAndFormatting()
    {
        // Arrange — London, 2026-06-15 noon
        double jd = _ephemeris.DateToJulianDay(2026, 6, 15, 12.0);
        const double latitude = 51.5074;
        const double longitude = -0.1278;

        // Act — calculate then format
        ParanResult result = _paranService.Calculate(jd, latitude, longitude);
        string markdown = ParanMarkdownFormatter.Format(result, "Test Chart");

        // Assert
        markdown.Should().NotBeEmpty("formatted Markdown should have content");
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "Markdown should contain the main heading");
        markdown.Should().Contain("### Angular Crossings",
            "Markdown should contain the crossings section");
        result.Crossings.Should().NotBeEmpty(
            "angular crossings should be detected for 12 bodies");
    }

    #endregion

    #region Astronomical Validation — Sun Rise

    [Fact]
    public void SunRise_Should_OccurNearPublishedTime_When_SummerSolsticeLondon()
    {
        // Arrange — 2026-06-21 (summer solstice), London
        // Published sunrise for London on summer solstice is approximately 04:43 UTC
        double jd = _ephemeris.DateToJulianDay(2026, 6, 21, 0.0);
        const double latitude = 51.5074;
        const double longitude = -0.1278;

        // Act
        ParanResult result = _paranService.Calculate(jd, latitude, longitude,
            new ParanOptions { Bodies = [CelestialBodyId.Sun] });

        // Assert — find the Sun's Rising crossing
        AngularCrossing? sunRise = result.Crossings
            .FirstOrDefault(c => c.Body == CelestialBodyId.Sun && c.Angle == AngleType.Rising);

        sunRise.Should().NotBeNull("Sun should rise on the summer solstice at London");

        // Convert JD to fractional hour to verify approximate time
        (int _, int _, int _, double hour) = _ephemeris.JulianDayToDate(sunRise!.JulianDayUt);

        // London sunrise on summer solstice is approximately 03:45 UTC
        // (04:45 BST local). Allow +-1 hour tolerance for atmospheric model variations.
        hour.Should().BeInRange(3.0, 5.0,
            "Sun should rise between 03:00 and 05:00 UTC on summer solstice at London");
    }

    #endregion

    #region Astronomical Validation — MC Crossing

    [Fact]
    public void SunMC_Should_OccurNearLocalNoon_When_SummerSolsticeLondon()
    {
        // Arrange — 2026-06-21, London
        double jd = _ephemeris.DateToJulianDay(2026, 6, 21, 0.0);
        const double latitude = 51.5074;
        const double longitude = -0.1278;

        // Act
        ParanResult result = _paranService.Calculate(jd, latitude, longitude,
            new ParanOptions { Bodies = [CelestialBodyId.Sun] });

        // Assert — Sun's MC (culmination) should be around local noon
        AngularCrossing? sunMC = result.Crossings
            .FirstOrDefault(c => c.Body == CelestialBodyId.Sun && c.Angle == AngleType.Culminating);

        sunMC.Should().NotBeNull("Sun should culminate (reach MC) at London");

        (int _, int _, int _, double hour) = _ephemeris.JulianDayToDate(sunMC!.JulianDayUt);

        // London is close to Greenwich, so solar noon should be ~12:00 UTC
        // Allow +-30 minutes tolerance
        hour.Should().BeInRange(11.5, 13.0,
            "Sun should culminate between 11:30 and 13:00 UTC at London (near local noon)");
    }

    #endregion

    #region Location-Dependent Variation

    [Fact]
    public void Calculate_Should_ProduceDifferentParans_When_DifferentLocations()
    {
        // Arrange — same date, different locations
        double jd = _ephemeris.DateToJulianDay(2026, 6, 15, 12.0);

        const double londonLat = 51.5074;
        const double londonLon = -0.1278;

        const double sydneyLat = -33.8688;
        const double sydneyLon = 151.2093;

        // Act
        ParanResult londonResult = _paranService.Calculate(jd, londonLat, londonLon);
        ParanResult sydneyResult = _paranService.Calculate(jd, sydneyLat, sydneyLon);

        // Assert — crossing times should differ significantly between hemispheres
        londonResult.Crossings.Should().NotBeEmpty("London should have crossings");
        sydneyResult.Crossings.Should().NotBeEmpty("Sydney should have crossings");

        // Find Sun rising time in both locations — they should be very different
        AngularCrossing? londonSunRise = londonResult.Crossings
            .FirstOrDefault(c => c.Body == CelestialBodyId.Sun && c.Angle == AngleType.Rising);
        AngularCrossing? sydneySunRise = sydneyResult.Crossings
            .FirstOrDefault(c => c.Body == CelestialBodyId.Sun && c.Angle == AngleType.Rising);

        if (londonSunRise is not null && sydneySunRise is not null)
        {
            double timeDiffHours = Math.Abs(londonSunRise.JulianDayUt - sydneySunRise.JulianDayUt) * 24.0;
            timeDiffHours.Should().BeGreaterThan(1.0,
                "sunrise in London and Sydney should differ by more than 1 hour");
        }
    }

    #endregion

    #region Markdown File Write

    [Fact]
    public async Task MarkdownFileWrite_Should_CreateNonEmptyFile_When_Exported()
    {
        // Arrange
        double jd = _ephemeris.DateToJulianDay(2026, 6, 15, 12.0);
        ParanResult result = _paranService.Calculate(jd, 51.5074, -0.1278);
        string tempPath = Path.Combine(Path.GetTempPath(), $"paran_test_{Guid.NewGuid()}.md");

        try
        {
            // Act
            await ParanMarkdownFormatter.SaveAsync(result, "Test Export", tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue("the Markdown file should exist after export");
            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().NotBeEmpty("the Markdown file should have content");
            content.Should().Contain("Test Export", "the file should contain the chart name");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    #endregion
}
