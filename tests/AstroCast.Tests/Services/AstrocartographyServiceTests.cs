using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="AstrocartographyService"/> — verifies
/// ACG line computation (MC/IC vertical meridians and ASC/DSC curves) for the
/// J2000.0 test chart (2000-01-01 12:00 UTC in New York). Uses the shared
/// <see cref="EphemerisFixture"/> so the Swiss Ephemeris is initialised once.
/// </summary>
[Collection("Ephemeris")]
public sealed class AstrocartographyServiceTests
{
    /// <summary>Birth longitude in decimal degrees (New York City).</summary>
    private const double NycLongitude = -74.006;

    /// <summary>Birth latitude in decimal degrees (New York City).</summary>
    private const double NycLatitude = 40.7128;

    private readonly IChartEngine _engine;
    private readonly AstrocartographyService _service;

    public AstrocartographyServiceTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        DignityService dignityService = new();
        SectService sectService = new();
        AspectService aspectService = new(fixture.EphemerisService, logger);

        _engine = new ChartEngine(
            fixture.EphemerisService,
            aspectService,
            new MidpointService(logger),
            dignityService,
            new MoonConditionService(),
            new ChartContextService(dignityService, sectService),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(dignityService),
            new AntisciaService(),
            new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);

        _service = new AstrocartographyService(fixture.EphemerisService, logger);
    }

    #region Line Count

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_ReturnsCorrectLineCount()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);

        // Assert - 12 bodies x 4 line types = 48 lines (some ASC/DSC may partially fail)
        result.Lines.Count.Should().BeGreaterThanOrEqualTo(44,
            "at least 11 of 12 bodies should produce complete four-line sets");
        result.Lines.Count.Should().BeLessThanOrEqualTo(48,
            "maximum is 12 bodies x 4 line types");
    }

    #endregion

    #region MC / IC Line Geometry

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_McLinesAreVertical()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);

        // Assert - every MC line has all coordinates at the same longitude
        foreach (AcgLine line in result.Lines.Where(l => l.LineType == AcgLineType.MC))
        {
            double firstLon = line.Coordinates[0].Longitude;
            foreach (AcgCoordinate coord in line.Coordinates)
            {
                coord.Longitude.Should().BeApproximately(firstLon, 0.0001,
                    $"{line.BodyName} MC line must be vertical (constant longitude)");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_IcLinesAre180FromMc()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);

        // Assert - for each body, IC longitude is MC longitude + 180 (mod 360, normalised)
        foreach (PlanetPosition planet in natal.Positions)
        {
            AcgLine mc = result.Lines.Single(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.MC);
            AcgLine ic = result.Lines.Single(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.IC);

            double mcLon = mc.Coordinates[0].Longitude;
            double icLon = ic.Coordinates[0].Longitude;

            double diff = ((mcLon - icLon + 540.0) % 360.0) - 180.0;
            Math.Abs(diff).Should().BeApproximately(180.0, 0.0001,
                $"{planet.Name} IC must be 180\u00b0 from MC in geographic longitude");
        }
    }

    #endregion

    #region ASC / DSC Line Shape

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_AscLinesHaveMultiplePoints()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);

        // Assert - every ASC line should have >100 points (up to 171 at 1-degree steps)
        foreach (AcgLine line in result.Lines.Where(l => l.LineType == AcgLineType.ASC))
        {
            line.Coordinates.Count.Should().BeGreaterThan(100,
                $"{line.BodyName} ASC line should have a point at every whole degree from -85\u00b0 to +85\u00b0 where the body rises and sets");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_SunMcLine_PassesNearGreenwich()
    {
        // Arrange - At J2000 (2000-Jan-1 12:00 UTC), the Sun is culminating
        // over Greenwich (GAST and Sun's RA happen to be very close).
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);

        // Assert - Sun MC line at J2000 passes within a few degrees of Greenwich
        AcgLine sunMc = result.Lines.Single(l =>
            l.Body == CelestialBodyId.Sun && l.LineType == AcgLineType.MC);

        double sunMcLon = sunMc.Coordinates[0].Longitude;

        Math.Abs(sunMcLon).Should().BeLessThan(5.0,
            "at J2000 noon UTC the Sun is on the meridian at roughly Greenwich longitude");
    }

    #endregion

    #region Crossings

    [Fact]
    [Trait("Category", "Integration")]
    public void GetCrossings_BirthLocation_FindsNearbyLines()
    {
        // Arrange - at NY at 12:00 UTC on 2000-01-01, the Sun is rising,
        // so the Sun's ASC line should pass near NY.
        ChartResult natal = CalculateTestChart();
        AcgResult result = _service.Calculate(natal);

        // Act
        AcgCrossingReport report = _service.GetCrossings(
            result, NycLatitude, NycLongitude, "New York City", orbKilometres: 556.0);

        // Assert
        report.Crossings.Should().NotBeEmpty(
            "at the birth location some body should have an angular line passing close to it");

        // Crossings should be sorted by distance ascending
        for (int i = 1; i < report.Crossings.Count; i++)
        {
            report.Crossings[i].DistanceKilometres.Should().BeGreaterThanOrEqualTo(
                report.Crossings[i - 1].DistanceKilometres,
                "crossings must be sorted by distance ascending");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GetCrossings_EmptyOrb_ReturnsFewOrNoCrossings()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();
        AcgResult result = _service.Calculate(natal);

        // Act - tight orb at an arbitrary mid-Pacific point
        AcgCrossingReport report = _service.GetCrossings(
            result, 0.0, 175.0, "Test Point", orbKilometres: 0.1);

        // Assert - at a 100-metre orb it is extraordinarily unlikely that any line crosses
        report.Crossings.Count.Should().BeLessThanOrEqualTo(1,
            "a 100-metre orb should catch at most one coincidental crossing");
    }

    #endregion

    #region End-to-end integration with formatter

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_AndFormat_ProducesValidMarkdown()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();

        // Act
        AcgResult result = _service.Calculate(natal);
        AcgCrossingReport report = _service.GetCrossings(
            result, NycLatitude, NycLongitude, "New York City", orbKilometres: 556.0);
        string markdown = AstrocartographyFormatter.ToMarkdown(result, report);

        // Assert - the full round trip from ephemeris to formatter yields a
        // Markdown document with all major sections populated.
        markdown.Should().NotBeNullOrEmpty("formatter output must be non-empty");
        markdown.Should().Contain("# Astrocartography Map");
        markdown.Should().Contain("## Angular Lines Summary");
        markdown.Should().Contain("## Crossing Report");
        markdown.Should().Contain("## Line Coordinates");
        markdown.Should().Contain("New York City");
    }

    #endregion

    #region Cancellation

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ObservesCancellationToken()
    {
        // Arrange
        ChartResult natal = CalculateTestChart();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act / Assert
        Action act = () => _service.Calculate(natal, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "the service must honour a pre-cancelled token");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Calculates the standard test natal chart: 2000-01-01 07:00 EST
    /// (= 2000-01-01 12:00 UTC = JD 2451545.0 = J2000 epoch), New York City.
    /// </summary>
    private ChartResult CalculateTestChart()
    {
        ChartInput input = new()
        {
            Name = "Astrocartography Test Subject",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            EventType = "Birth",
        };

        return _engine.Calculate(input);
    }

    #endregion
}
