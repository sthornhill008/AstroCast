using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Unit tests for <see cref="AstrocartographyFormatter"/> verifying the structure
/// of the Markdown output using synthetic ACG data (no ephemeris required).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AstrocartographyFormatterTests
{
    #region Markdown Output Structure

    [Fact]
    public void ToMarkdown_IncludesHeader()
    {
        // Arrange
        AcgResult result = CreateSampleResult();

        // Act
        string markdown = AstrocartographyFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("# Astrocartography Map",
            "output should start with the main heading");
        markdown.Should().Contain("Sample Subject",
            "output should include the chart subject name");
        markdown.Should().Contain("Obliquity:",
            "output should include the obliquity line");
        markdown.Should().Contain("GAST:",
            "output should include the GAST line");
    }

    [Fact]
    public void ToMarkdown_IncludesLineSummaryTable()
    {
        // Arrange
        AcgResult result = CreateSampleResult();

        // Act
        string markdown = AstrocartographyFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Angular Lines Summary",
            "output should contain the summary section heading");
        markdown.Should().Contain("| Body | MC Longitude | IC Longitude | ASC Points | DSC Points |",
            "output should include the summary table header row");
        markdown.Should().Contain("Sun",
            "output should list each body by name");
        markdown.Should().Contain("Moon",
            "output should list each body by name");
    }

    [Fact]
    public void ToMarkdown_IncludesCoordinateTables()
    {
        // Arrange
        AcgResult result = CreateSampleResult();

        // Act
        string markdown = AstrocartographyFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Line Coordinates",
            "output should have a coordinate tables section");
        markdown.Should().Contain("### Sun MC",
            "output should have a subheading for each body+line-type combination");
        markdown.Should().Contain("| Latitude | Longitude |",
            "coordinate tables should share the same header row");
    }

    [Fact]
    public void ToMarkdown_WithCrossingReport_IncludesCrossings()
    {
        // Arrange
        AcgResult result = CreateSampleResult();
        AcgCrossingReport report = new()
        {
            LocationName = "London, UK",
            Latitude = 51.5074,
            Longitude = -0.1278,
            OrbKilometres = 334.0,
            Crossings =
            [
                new AcgCrossing
                {
                    Body = CelestialBodyId.Jupiter,
                    BodyName = "Jupiter",
                    LineType = AcgLineType.MC,
                    DistanceKilometres = 27.8,
                },
                new AcgCrossing
                {
                    Body = CelestialBodyId.Venus,
                    BodyName = "Venus",
                    LineType = AcgLineType.ASC,
                    DistanceKilometres = 200.2,
                },
            ],
        };

        // Act
        string markdown = AstrocartographyFormatter.ToMarkdown(result, report);

        // Assert
        markdown.Should().Contain("## Crossing Report",
            "output should contain a crossing report heading when one is provided");
        markdown.Should().Contain("London, UK",
            "crossing report should include the location name");
        markdown.Should().Contain("Jupiter",
            "crossing report should list bodies with crossings");
        markdown.Should().Contain("| Body | Line | Distance |",
            "crossing report should include its own table header");
    }

    [Fact]
    public void ToMarkdown_WithoutCrossingReport_OmitsCrossings()
    {
        // Arrange
        AcgResult result = CreateSampleResult();

        // Act
        string markdown = AstrocartographyFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("## Crossing Report",
            "crossing report section should not appear when no report is provided");
    }

    [Fact]
    public async Task SaveAsync_WritesFile()
    {
        // Arrange
        AcgResult result = CreateSampleResult();
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"acg-formatter-test-{Guid.NewGuid():N}.md");

        try
        {
            // Act
            await AstrocartographyFormatter.SaveAsync(result, tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue("the file should be created");

            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().Contain("# Astrocartography Map",
                "saved file should contain the full formatted output");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    #endregion

    #region In mundo (branch J)

    /// <summary>The cultures each culture-sensitive test below also runs under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// The footer says what the lines are and how they are computed (Y4-4, Y4-5, E2-10): in
    /// mundo, geocentric with no parallax, the geometric horizon with no refraction, closed form,
    /// and the latitude range.
    /// </summary>
    [Fact]
    public void ToMarkdown_Footer_SaysTheLinesAreInMundo_AndHowTheyAreComputed()
    {
        string footer = DataNote(AstrocartographyFormatter.ToMarkdown(CreateSampleResult()));

        footer.Should().Contain("Lines in mundo")
            .And.Contain("where the body itself, not its zodiac degree, is on the meridian or the horizon")
            .And.Contain("geocentric apparent right ascension (RA) and declination on the true equator of date")
            .And.Contain("Greenwich apparent sidereal time (GAST)")
            .And.Contain("Geocentric, with no parallax (the Moon included)")
            .And.Contain("the geometric horizon, with no refraction")
            .And.Contain("Computed in closed form, with no search")
            .And.Contain("MC and IC lines run from 85\u00b0 S to 85\u00b0 N")
            .And.Contain("a point at every whole degree from 85\u00b0 S to 85\u00b0 N at which the body rises and sets")
            .And.Contain("end where it stops, at latitude \u00b1(90\u00b0 \u2212 |declination|)");
    }

    /// <summary>
    /// The footer no longer claims a binary search of 20 iterations or a latitude limit set by
    /// the Placidus house system (Y4-4, Y4-5): the lines are computed in closed form, and no
    /// house system enters them.
    /// </summary>
    [Fact]
    public void ToMarkdown_Footer_DropsTheSearchAndPlacidusClaims()
    {
        string footer = DataNote(AstrocartographyFormatter.ToMarkdown(CreateSampleResult()));

        footer.Should().NotContain("Placidus")
            .And.NotContain("binary search")
            .And.NotContain("20 iterations")
            .And.NotContain("arc-second")
            .And.NotContain("\u00b166\u00b0");
    }

    /// <summary>The footer names the bodies the lines are drawn for, in the result's order.</summary>
    [Fact]
    public void ToMarkdown_Footer_NamesTheBodiesWithLines()
    {
        string footer = DataNote(AstrocartographyFormatter.ToMarkdown(CreateSampleResult()));

        footer.Should().EndWith("Lines for: Sun, Moon.*");
    }

    /// <summary>
    /// The summary gives each body's ASC/DSC latitude range, from the first and last points of
    /// its lines, so a line that ends short of 85° says where.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void ToMarkdown_Summary_PrintsWhereEachBodysHorizonLinesRun(string culture)
    {
        using CultureScope scope = new(culture);

        string markdown = AstrocartographyFormatter.ToMarkdown(CreateSampleResult());

        markdown.Should().Contain("| Body | MC Longitude | IC Longitude | ASC Points | DSC Points | ASC/DSC Latitudes |");
        markdown.Should().Contain("| Sun | -74.00\u00b0 | 106.00\u00b0 | 10 | 10 | -66.00\u00b0 to 65.99\u00b0 |");
    }

    /// <summary>
    /// Coordinate tables print latitude to two decimals, so a line's end latitude (65.07° for
    /// person A's Moon) reads as it is.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void ToMarkdown_CoordinateTables_PrintLatitudeToTwoDecimals(string culture)
    {
        using CultureScope scope = new(culture);

        string markdown = AstrocartographyFormatter.ToMarkdown(CreateSampleResult());

        markdown.Should().Contain("| -85.00 | -74.00 |");
        markdown.Should().Contain("| -51.33 | 47.50 |");
    }

    /// <summary>
    /// The crossing report prints each line's great-circle distance in whole kilometres and
    /// says what the distance is and the orb it was listed within (S-D8, Q9).
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void ToMarkdown_CrossingReport_PrintsKilometresAndTheOrb(string culture)
    {
        using CultureScope scope = new(culture);

        string markdown = AstrocartographyFormatter.ToMarkdown(CreateSampleResult(), CreateSampleReport());

        markdown.Should().Contain("Lines within 2,000 km of the place, by great-circle distance to the nearest point of the line.");
        markdown.Should().Contain("| Jupiter | MC | 28 km |");
        markdown.Should().Contain("| Venus | ASC | 1,200 km |");
    }

    /// <summary>A crossing report with no line within its orb says so, in kilometres.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void ToMarkdown_EmptyCrossingReport_NamesTheOrbInKilometres(string culture)
    {
        using CultureScope scope = new(culture);
        AcgCrossingReport report = new()
        {
            LocationName = "Nowhere",
            Latitude = 60.0,
            Longitude = 100.0,
            OrbKilometres = 334.0,
            Crossings = [],
        };

        string markdown = AstrocartographyFormatter.ToMarkdown(CreateSampleResult(), report);

        markdown.Should().Contain("*No ACG line passes within 334 km of this location.*");
    }

    /// <summary>
    /// The footer says how a crossing distance is measured: the great-circle distance to the
    /// line's nearest point on a sphere of radius 6,371.0088 km, listed within 334 km.
    /// </summary>
    [Fact]
    public void ToMarkdown_Footer_SaysHowACrossingIsMeasured()
    {
        string footer = DataNote(AstrocartographyFormatter.ToMarkdown(CreateSampleResult()));

        footer.Should().Contain(
            "A crossing, listed on screen for a place clicked on the map, is the great-circle distance "
            + "from the place to the nearest point of the line, on a sphere of radius 6,371.0088 km; "
            + "a line is listed when it is within 334 km.");
    }

    /// <summary>A crossing report with two lines, one beyond 1,000 km, made with a 2,000 km orb.</summary>
    private static AcgCrossingReport CreateSampleReport() => new()
    {
        LocationName = "London, UK",
        Latitude = 51.5074,
        Longitude = -0.1278,
        OrbKilometres = 2000.0,
        Crossings =
        [
            new AcgCrossing { Body = CelestialBodyId.Jupiter, BodyName = "Jupiter", LineType = AcgLineType.MC, DistanceKilometres = 27.8 },
            new AcgCrossing { Body = CelestialBodyId.Venus, BodyName = "Venus", LineType = AcgLineType.ASC, DistanceKilometres = 1200.2 },
        ],
    };

    /// <summary>The footer's data note: the line that follows the closing rule.</summary>
    private static string DataNote(string markdown) =>
        markdown.Split('\n').Single(l => l.StartsWith("*Data from AstroCast", StringComparison.Ordinal)).TrimEnd('\r');

    #endregion

    #region Test Helpers

    /// <summary>
    /// Create a minimal <see cref="AcgResult"/> with two bodies and synthetic
    /// line data — enough to drive the formatter through every code path
    /// without invoking the ephemeris.
    /// </summary>
    private static AcgResult CreateSampleResult()
    {
        PlanetPosition sun = CreatePosition(CelestialBodyId.Sun, "Sun", 15.0);
        PlanetPosition moon = CreatePosition(CelestialBodyId.Moon, "Moon", 105.0);

        ChartResult chart = CreateMinimalChart(sun, moon);

        List<AcgLine> lines =
        [
            CreateMcLine(sun, -74.0),
            CreateIcLine(sun, 106.0),
            CreateAscLine(sun, "Sun", AcgLineType.ASC),
            CreateAscLine(sun, "Sun", AcgLineType.DSC),
            CreateMcLine(moon, 22.5),
            CreateIcLine(moon, -157.5),
            CreateAscLine(moon, "Moon", AcgLineType.ASC),
            CreateAscLine(moon, "Moon", AcgLineType.DSC),
        ];

        return new AcgResult
        {
            NatalChart = chart,
            Lines = lines.AsReadOnly(),
            Obliquity = 23.4393,
            GastDegrees = 280.4606,
            LatitudeMin = -85.0,
            LatitudeMax = 85.0,
            LatitudeStep = 1.0,
        };
    }

    private static PlanetPosition CreatePosition(CelestialBodyId body, string name, double longitude)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
    }

    private static ChartResult CreateMinimalChart(params PlanetPosition[] positions)
    {
        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Sample Subject",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0,
                Midheaven = 270.0,
                Armc = 280.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    private static AcgLine CreateMcLine(PlanetPosition planet, double geoLongitude)
    {
        return new AcgLine
        {
            Body = planet.Body,
            BodyName = planet.Name,
            LineType = AcgLineType.MC,
            Coordinates =
            [
                new AcgCoordinate(-85.0, geoLongitude),
                new AcgCoordinate(85.0, geoLongitude),
            ],
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    private static AcgLine CreateIcLine(PlanetPosition planet, double geoLongitude)
    {
        return new AcgLine
        {
            Body = planet.Body,
            BodyName = planet.Name,
            LineType = AcgLineType.IC,
            Coordinates =
            [
                new AcgCoordinate(-85.0, geoLongitude),
                new AcgCoordinate(85.0, geoLongitude),
            ],
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    private static AcgLine CreateAscLine(PlanetPosition planet, string bodyName, AcgLineType type)
    {
        // Synthetic ASC/DSC curve: 10 points spanning the latitude range.
        List<AcgCoordinate> coords = new(10);
        for (int i = 0; i < 10; i++)
        {
            double lat = -66.0 + i * 14.666;
            double lon = 45.0 + i * 2.5;
            coords.Add(new AcgCoordinate(lat, lon));
        }

        return new AcgLine
        {
            Body = planet.Body,
            BodyName = bodyName,
            LineType = type,
            Coordinates = coords.AsReadOnly(),
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    #endregion
}
