using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ChartMarkdownFormatter"/> verifying the paran
/// (paranatellonta) section: presence when ChartResult.Parans is set,
/// absence when null, empty parans message, section placement, and
/// body/angle rendering.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartMarkdownParanTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ChartMarkdownParanTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Creates a standard test chart result (New York, J2000.0).
    /// The result includes parans computed by the engine.
    /// </summary>
    private ChartResult CreateTestResult() =>
        _engine.Calculate(new ChartInput
        {
            Name = "Paran Test Chart",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        });

    /// <summary>
    /// Creates a copy of a ChartResult with a custom ParanResult.
    /// </summary>
    private static ChartResult WithParans(ChartResult source, ParanResult? parans) => new()
    {
        Input = source.Input,
        JulianDay = source.JulianDay,
        Obliquity = source.Obliquity,
        UtcDateTime = source.UtcDateTime,
        Positions = source.Positions,
        Houses = source.Houses,
        Aspects = source.Aspects,
        Midpoints = source.Midpoints,
        MidpointActivations = source.MidpointActivations,
        Dignities = source.Dignities,
        MoonCondition = source.MoonCondition,
        ChartContext = source.ChartContext,
        FixedStarConjunctions = source.FixedStarConjunctions,
        DeclinationAspects = source.DeclinationAspects,
        PlanetConditions = source.PlanetConditions,
        MoonMansion = source.MoonMansion,
        PlanetaryHour = source.PlanetaryHour,
        ArabicParts = source.ArabicParts,
        AntisciaPoints = source.AntisciaPoints,
        AntisciaConnections = source.AntisciaConnections,
        Stations = source.Stations,
        Parans = parans,
        ResolvedUtcOffsetHours = -5.0,
        ResolvedOffsetLabel = "EST (UTC-5)",
        EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
        CalculatedAt = source.CalculatedAt,
    };

    /// <summary>
    /// Creates a ParanResult with sample paran events for testing.
    /// </summary>
    private static ParanResult CreateSampleParanResult() => new()
    {
        Latitude = 40.7128,
        Longitude = -74.006,
        StartJulianDay = 2451545.0,
        EndJulianDay = 2451546.0,
        OrbMinutes = 4.0,
        BodyCount = 12,
        CalculatedAt = DateTime.UtcNow,
        Crossings =
        [
            new AngularCrossing
            {
                Body = CelestialBodyId.Sun,
                BodyName = "Sun",
                Angle = AngleType.Rising,
                JulianDayUt = 2451545.25,
                Longitude = 280.5,
            },
            new AngularCrossing
            {
                Body = CelestialBodyId.Moon,
                BodyName = "Moon",
                Angle = AngleType.Culminating,
                JulianDayUt = 2451545.252,
                Longitude = 105.3,
            },
        ],
        Parans =
        [
            new ParanEvent
            {
                BodyA = CelestialBodyId.Sun,
                BodyAName = "Sun",
                AngleA = AngleType.Rising,
                JulianDayA = 2451545.25,
                LongitudeA = 280.5,
                BodyB = CelestialBodyId.Moon,
                BodyBName = "Moon",
                AngleB = AngleType.Culminating,
                JulianDayB = 2451545.252,
                LongitudeB = 105.3,
                OrbMinutes = 2.88,
            },
        ],
    };

    /// <summary>
    /// Creates a ParanResult with no paran events (empty lists).
    /// </summary>
    private static ParanResult CreateEmptyParanResult() => new()
    {
        Latitude = 40.7128,
        Longitude = -74.006,
        StartJulianDay = 2451545.0,
        EndJulianDay = 2451546.0,
        OrbMinutes = 4.0,
        BodyCount = 12,
        CalculatedAt = DateTime.UtcNow,
        Crossings = [],
        Parans = [],
    };

    #region Paran Section Presence

    /// <summary>
    /// Test 1: Paran section should be present when ChartResult.Parans is set.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_ContainParanSection_When_ParanResultPresent()
    {
        // Arrange
        ChartResult chartResult = WithParans(CreateTestResult(), CreateSampleParanResult());

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "Markdown should contain the paran section heading");
        markdown.Should().Contain("| Body A | Angle A | Position A |",
            "Markdown should contain the paran table headers");
    }

    #endregion

    #region No Paran Section When Null

    /// <summary>
    /// Test 2: Paran section should NOT be present when ChartResult.Parans is null.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_NotContainParanSection_When_ParansIsNull()
    {
        // Arrange
        ChartResult chartResult = WithParans(CreateTestResult(), null);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        markdown.Should().NotContain("## Parans",
            "Markdown should not contain the paran section when Parans is null");
    }

    #endregion

    #region Engine-Calculated Parans

    /// <summary>
    /// Test 3: When parans are engine-calculated (standard chart at non-polar
    /// latitude), the section should appear automatically.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_ContainParanSection_When_EngineCalculated()
    {
        // Arrange — engine automatically calculates parans
        ChartResult chartResult = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        chartResult.Parans.Should().NotBeNull("engine should compute parans for NYC");
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "Engine-calculated parans should appear in Markdown output");
    }

    #endregion

    #region Empty Parans Message

    /// <summary>
    /// Test 4: When ChartResult.Parans has an empty Parans list, the output should
    /// contain the "No paran events detected" message.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_ShowEmptyMessage_When_ParansListEmpty()
    {
        // Arrange
        ChartResult chartResult = WithParans(CreateTestResult(), CreateEmptyParanResult());

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        markdown.Should().Contain("No paran events detected within the configured orb.",
            "Empty parans should produce the no-events message");
    }

    #endregion

    #region Section Placement

    /// <summary>
    /// Test 5: The paran section should appear AFTER Fixed Stars and BEFORE
    /// the footer line.
    /// </summary>
    [Fact]
    public void ToMarkdown_ParanSection_Should_AppearAfterFixedStars_AndBeforeFooter()
    {
        // Arrange
        ChartResult chartResult = WithParans(CreateTestResult(), CreateSampleParanResult());

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        int fixedStarsIndex = markdown.IndexOf("## Fixed Stars", StringComparison.Ordinal);
        int paranIndex = markdown.IndexOf("## Parans (Paranatellonta)", StringComparison.Ordinal);
        int footerIndex = markdown.IndexOf("*Generated by", StringComparison.Ordinal);

        paranIndex.Should().BeGreaterThan(fixedStarsIndex,
            "Paran section should appear after Fixed Stars");
        paranIndex.Should().BeLessThan(footerIndex,
            "Paran section should appear before the footer");
    }

    #endregion

    #region Body Names and Angles in Table

    /// <summary>
    /// Test 6: Body names and angle strings should appear in the paran table output.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_ContainBodyNamesAndAngles_When_ParanEventsPresent()
    {
        // Arrange
        ChartResult chartResult = WithParans(CreateTestResult(), CreateSampleParanResult());

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert
        markdown.Should().Contain("Sun",
            "Paran table should contain Body A name");
        markdown.Should().Contain("Rising",
            "Paran table should contain Angle A string");
        markdown.Should().Contain("Moon",
            "Paran table should contain Body B name");
        markdown.Should().Contain("Culminating",
            "Paran table should contain Angle B string");
        markdown.Should().Contain("2.88",
            "Paran table should contain the orb value");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_Should_ContainBodyNamesAndAngles_When_ParanEventsPresent"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_Should_ContainBodyNamesAndAngles_When_ParanEventsPresent_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_Should_ContainBodyNamesAndAngles_When_ParanEventsPresent();
    }

    #endregion
}
