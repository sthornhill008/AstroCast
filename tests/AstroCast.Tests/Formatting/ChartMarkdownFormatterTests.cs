using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ChartMarkdownFormatter"/> verifying document structure,
/// section headings, and conditional content rendering.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartMarkdownFormatterTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ChartMarkdownFormatterTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Create a ChartResult with optional gender and notes settings.
    /// </summary>
    private ChartResult CreateTestResult(
        Gender gender = Gender.Unspecified,
        string? notes = null,
        string? eventDescription = null) =>
        _engine.Calculate(new ChartInput
        {
            Name = "Markdown Test Chart",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            Gender = gender,
            Notes = notes,
            EventDescription = eventDescription,
        });

    /// <summary>
    /// A chart at an explicit offset prints its Date/Time line from its instant on the calendar in force
    /// (R4, branch R2, 1.3.4): an event chart cast by CalculateAtInstant at offset 0, whose input holds the
    /// proleptic instant as its reading, as the services set it (ToDateTimeUtc of the Julian Day). N1's
    /// 1301 solar return, JD 2196322.2379282, reads 1301-03-23T17:42:37 and prints Julian 1301-03-15
    /// 17:42:37; N3's leap-day return of 1300, JD 2195941.9668981, reads 1300-03-08T11:12:20 and prints
    /// 1300-02-29 11:12:20, a date no reading can hold. Until R4 the line printed the reading, 1301-03-23
    /// and 1300-03-08, beside a Return Date/Time line and a Calendar line on the Julian calendar.
    /// </summary>
    /// <param name="julianDay">The event's Julian Day.</param>
    /// <param name="reading">The input's reading, the proleptic instant.</param>
    /// <param name="line">The Date/Time line.</param>
    [Theory]
    [InlineData(2196322.2379282, "1301-03-23T17:42:37", "**Date/Time:** 1301-03-15 17:42:37 UTC+0 (explicit)  ")]
    [InlineData(2195941.9668981, "1300-03-08T11:12:20", "**Date/Time:** 1300-02-29 11:12:20 UTC+0 (explicit)  ")]
    public void ToMarkdown_ExplicitOffsetEventChart_PrintsItsInstantOnTheCalendarInForce(double julianDay, string reading, string line)
    {
        ChartResult chart = _engine.CalculateAtInstant(new ChartInput
        {
            Name = "Event",
            LocalDateTime = DateTime.Parse(reading, System.Globalization.CultureInfo.InvariantCulture),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Solar Return",
        }, julianDay);

        ChartMarkdownFormatter.ToMarkdown(chart).Split('\n').Select(l => l.TrimEnd('\r')).Should().Contain(line);
    }

    /// <summary>
    /// Guard for R4: a chart read at an explicit offset prints the line it always did, the reading and
    /// the offset's label, since the reading is its instant plus the same whole seconds.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExplicitOffsetReading_PrintsTheReadingAsBefore()
    {
        ChartResult chart = _engine.Calculate(new ChartInput
        {
            Name = "Explicit",
            LocalDateTime = new DateTime(2026, 4, 15, 7, 0, 0),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        });

        ChartMarkdownFormatter.ToMarkdown(chart).Split('\n').Select(l => l.TrimEnd('\r'))
            .Should().Contain($"**Date/Time:** 2026-04-15 07:00:00 {chart.ResolvedOffsetLabel}  ");
        chart.ResolvedOffsetLabel.Should().Be("UTC\u22125 (explicit)");
    }

    [Fact]
    public void ToMarkdown_Should_ContainHeader_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("# Markdown Test Chart",
            "Markdown should start with the chart name as an H1 heading");
    }

    [Fact]
    public void ToMarkdown_Should_ContainAllSections_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Planetary Positions",
            "Markdown should contain the positions section");
        markdown.Should().Contain("## House Cusps",
            "Markdown should contain the houses section");
        markdown.Should().Contain("## Aspects",
            "Markdown should contain the aspects section");
        markdown.Should().Contain("## Midpoint Activations",
            "Markdown should contain the midpoint activations section");
        markdown.Should().Contain("## Fixed Stars",
            "Markdown should contain the fixed stars section");
    }

    [Fact]
    public void ToMarkdown_Should_ContainGender_When_Specified()
    {
        // Arrange
        ChartResult result = CreateTestResult(Gender.Female);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Gender:** Female",
            "Markdown should display the gender when it is not Unspecified");
    }

    [Fact]
    public void ToMarkdown_Should_OmitGender_When_Unspecified()
    {
        // Arrange
        ChartResult result = CreateTestResult(Gender.Unspecified);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("**Gender:**",
            "Markdown should not display the gender line when it is Unspecified");
    }

    [Fact]
    public void ToMarkdown_Should_ContainNotes_When_Provided()
    {
        // Arrange
        ChartResult result = CreateTestResult(notes: "Twin birth, first born");

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Notes:** Twin birth, first born",
            "Markdown should display the notes when provided");
    }

    [Fact]
    public void ToMarkdown_Should_OmitNotes_When_Empty()
    {
        // Arrange
        ChartResult result = CreateTestResult(notes: null);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("**Notes:**",
            "Markdown should not display the notes line when it is null");
    }

    [Fact]
    public void ToMarkdown_Should_ContainDescription_When_Provided()
    {
        // Arrange
        ChartResult result = CreateTestResult(eventDescription: "Solar eclipse chart");

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("**Description:** Solar eclipse chart",
            "Markdown should display the event description when provided");
    }

    [Fact]
    public void ToMarkdown_Should_OmitDescription_When_Empty()
    {
        // Arrange
        ChartResult result = CreateTestResult(eventDescription: null);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("**Description:**",
            "Markdown should not display the description line when it is null");
    }

    [Fact]
    public void ToMarkdown_Should_ContainFixedStarsSectionHeader_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Fixed Stars (1\u00b000\u2032 orb, conjunction only)",
            "Markdown should contain the fixed stars section header with orb specification");
    }

    /// <summary>
    /// Every orb in the chart export prints as truncated D°MM′ through one function (branch O-D3,
    /// 1.3.4): the aspect table, the midpoint table, the declination table, the Moon's first applying
    /// aspect and the Fixed Stars heading, on J2000 NYC. Until 1.3.4 the aspect table printed "129'",
    /// the midpoint table "003'", the declination table "0°03'", the Moon line "(orb 1.49°)" and the
    /// heading "1°00'".
    /// </summary>
    [Fact]
    public void ToMarkdown_J2000Nyc_PrintsEveryOrbAsTruncatedDegreesAndMinutes()
    {
        string markdown = ChartMarkdownFormatter.ToMarkdown(_engine.Calculate(GoldenFixtures.J2000NycInput()));

        markdown.Should().Contain("| Moon | sqr | Uranus | 1\u00b029\u2032 | Ap |");
        markdown.Should().Contain("| Mars/Saturn | ASC | 90 | 0\u00b003\u2032 |");
        markdown.Should().Contain(
            "| Neptune | -19\u00b012\u203247\u2033 | CPar | True Node | +19\u00b015\u203252\u2033 | 0\u00b003\u2032 |");
        markdown.Should().Contain("**First Applying Aspect:** Moon Square Uranus (orb 1\u00b029\u2032)");
        markdown.Should().Contain("## Fixed Stars (1\u00b000\u2032 orb, conjunction only)");
    }

    /// <summary>
    /// The Lord's Natal Aspects list prints its orbs as truncated D°MM′ through one function (branch
    /// O-D3, 1.3.4): on J2000 NYC at the golden clock, Jupiter sesquiquadrate Pluto is 1°12′, the aspect
    /// table's row. Until 1.3.4 the line printed the orb to one decimal, "(orb 1.2°, Ap)".
    /// </summary>
    [Fact]
    public void ToMarkdown_J2000Nyc_LordsNatalAspects_PrintOrbsAsTruncatedDegreesAndMinutes()
    {
        string markdown = ChartMarkdownFormatter.ToMarkdown(
            _engine.Calculate(GoldenFixtures.J2000NycInput()), timeProvider: GoldenFixtures.GoldenClock());

        markdown.Should().Contain("- Jupiter Sesquiquadrate Pluto (orb 1\u00b012\u2032, Ap)");
    }

    [Fact]
    public void ToMarkdown_Should_ContainFixedStarTableHeaders_When_ConjunctionsExist()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert — J2000.0 NYC chart should have some fixed star conjunctions
        if (result.FixedStarConjunctions.Count > 0)
        {
            markdown.Should().Contain("| Star | Mag | Position | Conjunct | Orb | Nature | Keywords |",
                "Markdown should contain the fixed star table headers");
        }
    }

    [Fact]
    public void ToMarkdown_Should_ShowStarName_When_ConjunctionsExist()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert — verify at least one fixed star name appears in the output
        if (result.FixedStarConjunctions.Count > 0)
        {
            FixedStarConjunction first = result.FixedStarConjunctions[0];
            markdown.Should().Contain(first.StarName,
                "Markdown should contain the star name from the first conjunction");
        }
    }

    [Fact]
    public void ToMarkdown_Should_PlaceFixedStarsAfterMidpoints_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert — fixed stars section should appear after midpoints and before footer
        int midpointsIndex = markdown.IndexOf("## Midpoint Activations", StringComparison.Ordinal);
        int fixedStarsIndex = markdown.IndexOf("## Fixed Stars", StringComparison.Ordinal);
        int footerIndex = markdown.IndexOf("*Generated by", StringComparison.Ordinal);

        fixedStarsIndex.Should().BeGreaterThan(midpointsIndex,
            "Fixed Stars section should appear after Midpoint Activations");
        fixedStarsIndex.Should().BeLessThan(footerIndex,
            "Fixed Stars section should appear before the footer");
    }

    #region Coordinate Extension Tests

    [Fact]
    public void ToMarkdown_PositionsTable_ContainsLatDecOobColumns()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("| Body | Position | Lat | Dec | House | Speed | Dir | OOB |",
            "positions table should include Lat, Dec, and OOB columns");
    }

    [Fact]
    public void ToMarkdown_PositionsTable_ContainsLatitudeValues()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert — Sun latitude is ~0°, so should contain "+00°" or similar
        markdown.Should().Contain("+00\u00b000",
            "Sun ecliptic latitude near 0° should appear in the positions table");
    }

    [Fact]
    public void ToMarkdown_ContainsEquatorialCoordinatesSection()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("### Equatorial Coordinates",
            "Markdown should contain the Equatorial Coordinates subsection");
        markdown.Should().Contain("| Body | Right Ascension | Declination |",
            "Equatorial Coordinates should have RA and Dec table headers");
    }

    [Fact]
    public void ToMarkdown_ContainsOutOfBoundsPlanetsSection()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("### Out-of-Bounds Planets",
            "Markdown should contain the Out-of-Bounds Planets subsection");
    }

    [Fact]
    public void ToMarkdown_ContainsDeclinationAspectsSection()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Declination Aspects",
            "Markdown should contain the Declination Aspects section");
    }

    [Fact]
    public void ToMarkdown_DeclinationAspects_PlacedAfterLongitudeAspects()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        int aspectsIndex = markdown.IndexOf("## Aspects", StringComparison.Ordinal);
        int decAspectsIndex = markdown.IndexOf("## Declination Aspects", StringComparison.Ordinal);
        int midpointsIndex = markdown.IndexOf("## Midpoint Activations", StringComparison.Ordinal);

        decAspectsIndex.Should().BeGreaterThan(aspectsIndex,
            "Declination Aspects should appear after longitude Aspects");
        decAspectsIndex.Should().BeLessThan(midpointsIndex,
            "Declination Aspects should appear before Midpoint Activations");
    }

    #endregion

    #region Stationing Planets

    [Fact]
    public void ToMarkdown_StationingPlanets_VenusStationHorary_ShouldRenderNearestStationEvent()
    {
        // An invented horary at the Venus-station instant (2026-10-01 06:56:30 EDT,
        // Columbus, Ohio; the station data do not depend on the place). Venus is
        // direct at +0.0705°/day and decelerating toward its station retrograde of
        // 2026-10-03 07:15:54 UT (03:15 EDT) at 08°29′28″ Scorpio, 1.85 d ahead.
        // The 1.3.0 export labelled this "Station Direct" and printed 58% (truncated).
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Parcel enquiry 1001",
            LocalDateTime = new DateTime(2026, 10, 1, 6, 56, 30),
            TimeZone = "America/New_York",
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio, United States",
            EventType = "Horary",
            HoraryQuestion = "Will the parcel arrive by Friday?",
            HoraryCategory = "Travel",
            HouseSystem = HouseSystem.Regiomontanus,
        });

        string markdown = ChartMarkdownFormatter.ToMarkdown(result).Replace("\r\n", "\n");

        const string expectedSection =
            "## Stationing Planets\n" +
            "\n" +
            "| Planet | Station | Phase | Station Time | Station Position | Speed | Proximity |\n" +
            "|--------|---------|-------|--------------|------------------|-------|-----------|\n" +
            "| Venus | Station Retrograde | Approaching (in 1.85 d) | 2026-10-03 03:15 (America/New_York) | 08°29′28″ Scorpio | 0.0705°/day | 59% of threshold |\n" +
            "\n";

        markdown.Should().Contain(expectedSection);
    }

    [Fact]
    public void ToMarkdown_StationingPlanets_ShouldPrintSignedSpeedForRetrogradePlanet()
    {
        // 1 d after the 2026-10-03 station retrograde: Venus retrograde at
        // −0.0390°/day and accelerating away from the station it just made.
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Venus separating",
            LocalDateTime = new DateTime(2026, 10, 4, 7, 15, 54),
            TimeZone = "Etc/UTC",
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio",
            EventType = "Event",
        });

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain(
            "| Venus | Station Retrograde | Separating (1.00 d ago) | 2026-10-03 07:15 (Etc/UTC) | 08°29′28″ Scorpio | -0.0390°/day | 32% of threshold |");
    }

    [Fact]
    public void ToMarkdown_StationingPlanets_ExplicitOffsetChart_ShouldKeepTheChartsFixedOffset()
    {
        // The same instant entered with an explicit UTC−5 (the user
        // overriding America/New_York's DST rule): 05:56:30 at −5 is the same
        // 10:56:30 UT. The station must be shown on that same fixed clock,
        // 02:15 at −5, not converted through the zone rules to 03:15 EDT.
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Venus station, explicit offset",
            LocalDateTime = new DateTime(2026, 10, 1, 5, 56, 30),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio",
            EventType = "Event",
        });

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**UTC Offset:** UTC−5 (explicit)");
        markdown.Should().Contain(
            "| Venus | Station Retrograde | Approaching (in 1.85 d) | 2026-10-03 02:15 UTC−5 (explicit) | 08°29′28″ Scorpio | 0.0705°/day | 59% of threshold |");
    }

    [Fact]
    public void ToMarkdown_StationingPlanets_StationAcrossDstChange_ShouldUseTheStationsOwnOffset()
    {
        // Europe/London leaves BST at 01:00 UT on 2026-10-25. A chart at 03:00
        // GMT that morning has Mercury flagged at −0.112°/day, 0.8 d past its
        // station retrograde of 2026-10-24 07:12 UT — which fell under BST and
        // must therefore print as 08:12, not as 07:12 on the chart's GMT offset.
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Mercury across the clock change",
            LocalDateTime = new DateTime(2026, 10, 25, 3, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
            EventType = "Event",
        });

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Date/Time:** 2026-10-25 03:00:00 (Europe/London)");
        markdown.Should().Contain("**UTC Offset:** GMT (UTC+0)");
        string mercuryRow = markdown.Split('\n').Single(l => l.StartsWith("| Mercury | Station", StringComparison.Ordinal));
        mercuryRow.Should().StartWith("| Mercury | Station Retrograde | Separating (0.8");
        mercuryRow.Should().Contain("| 2026-10-24 08:12 (Europe/London) |",
            "the station fell under BST (+1) even though the chart itself is on GMT (+0)");
    }

    #endregion

    #region Date/Time header

    [Fact]
    public void ToMarkdown_DateTimeHeader_ExplicitOffsetChart_ShouldLabelTheOffsetNotTheZone()
    {
        // A return chart is cast at a UTC wall clock with an explicit 0 offset while its
        // zone stays the natal one. 14:18:44 is a UTC time, so the header names that
        // offset, as the UTC Offset line and the Stationing rows do; labelled
        // America/New_York it would read as EDT, four hours from the true instant.
        // A chart computed through its zone keeps the zone.
        ChartResult explicitOffset = _engine.Calculate(new ChartInput
        {
            Name = "Lunar return instant, UTC wall clock",
            LocalDateTime = new DateTime(2026, 4, 4, 14, 18, 44),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        });

        string markdown = ChartMarkdownFormatter.ToMarkdown(explicitOffset);

        markdown.Should().Contain("**Date/Time:** 2026-04-04 14:18:44 UTC+0 (explicit)  ");
        markdown.Should().Contain("**UTC Offset:** UTC+0 (explicit)  ");
        markdown.Should().NotContain("**Date/Time:** 2026-04-04 14:18:44 (America/New_York)");
        ChartMarkdownFormatter.ToMarkdown(CreateTestResult())
            .Should().Contain("**Date/Time:** 2000-01-01 07:00:00 (America/New_York)  ");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_StationingPlanets_VenusStationHorary_ShouldRenderNearestStationEvent"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_StationingPlanets_VenusStationHorary_ShouldRenderNearestStationEvent_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_StationingPlanets_VenusStationHorary_ShouldRenderNearestStationEvent();
    }

    /// <summary>
    /// <see cref="ToMarkdown_StationingPlanets_ShouldPrintSignedSpeedForRetrogradePlanet"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_StationingPlanets_ShouldPrintSignedSpeedForRetrogradePlanet_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_StationingPlanets_ShouldPrintSignedSpeedForRetrogradePlanet();
    }

    /// <summary>
    /// <see cref="ToMarkdown_StationingPlanets_ExplicitOffsetChart_ShouldKeepTheChartsFixedOffset"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_StationingPlanets_ExplicitOffsetChart_ShouldKeepTheChartsFixedOffset_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_StationingPlanets_ExplicitOffsetChart_ShouldKeepTheChartsFixedOffset();
    }

    /// <summary>
    /// <see cref="ToMarkdown_StationingPlanets_StationAcrossDstChange_ShouldUseTheStationsOwnOffset"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_StationingPlanets_StationAcrossDstChange_ShouldUseTheStationsOwnOffset_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_StationingPlanets_StationAcrossDstChange_ShouldUseTheStationsOwnOffset();
    }

    /// <summary>
    /// <see cref="ToMarkdown_DateTimeHeader_ExplicitOffsetChart_ShouldLabelTheOffsetNotTheZone"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_DateTimeHeader_ExplicitOffsetChart_ShouldLabelTheOffsetNotTheZone_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_DateTimeHeader_ExplicitOffsetChart_ShouldLabelTheOffsetNotTheZone();
    }

    #endregion

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>The heading of the minor points' section, as the export prints it (R6, scoping decision 3).</summary>
    private const string MinorPointsHeading = "## Asteroids and Lilith";

    /// <summary>The section's aspects sub-heading: the five Ptolemaic aspects at 2°00′.</summary>
    private const string MinorPointAspectsHeading = "### Aspects (2°00′ orb)";

    /// <summary>The lines of a Markdown export, line endings removed.</summary>
    /// <param name="markdown">The export.</param>
    /// <returns>Its lines.</returns>
    private static List<string> Lines(string markdown) =>
        [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

    /// <summary>
    /// A copy of a cast chart with other minor points and minor-point aspects, every other member kept, so a test
    /// can hand the formatter a list the engine would not produce (no aspects; an aspect whose orb is not the
    /// definition's).
    /// </summary>
    /// <param name="chart">The cast chart.</param>
    /// <param name="minorPoints">The five.</param>
    /// <param name="minorPointAspects">Their aspects.</param>
    /// <returns>The copy.</returns>
    private static ChartResult WithMinorPoints(
        ChartResult chart, IReadOnlyList<PlanetPosition> minorPoints, IReadOnlyList<AspectResult> minorPointAspects) => new()
    {
        Input = chart.Input,
        JulianDay = chart.JulianDay,
        Obliquity = chart.Obliquity,
        UtcDateTime = chart.UtcDateTime,
        Positions = chart.Positions,
        Houses = chart.Houses,
        Aspects = chart.Aspects,
        MinorPoints = minorPoints,
        MinorPointAspects = minorPointAspects,
        Midpoints = chart.Midpoints,
        MidpointActivations = chart.MidpointActivations,
        Dignities = chart.Dignities,
        MoonCondition = chart.MoonCondition,
        ChartContext = chart.ChartContext,
        FixedStarConjunctions = chart.FixedStarConjunctions,
        DeclinationAspects = chart.DeclinationAspects,
        PlanetConditions = chart.PlanetConditions,
        MoonMansion = chart.MoonMansion,
        PlanetaryHour = chart.PlanetaryHour,
        ArabicParts = chart.ArabicParts,
        AntisciaPoints = chart.AntisciaPoints,
        AntisciaConnections = chart.AntisciaConnections,
        Stations = chart.Stations,
        HoraryPerfection = chart.HoraryPerfection,
        Parans = chart.Parans,
        ResolvedUtcOffsetHours = chart.ResolvedUtcOffsetHours,
        ResolvedOffsetLabel = chart.ResolvedOffsetLabel,
        LocalTimeMapping = chart.LocalTimeMapping,
        ResolvedLocalDateTime = chart.ResolvedLocalDateTime,
        EarlierUtcDateTime = chart.EarlierUtcDateTime,
        TimeNote = chart.TimeNote,
        TimeZoneRules = chart.TimeZoneRules,
        HouseSystemName = chart.HouseSystemName,
        EngineVersion = chart.EngineVersion,
        CalculatedAt = chart.CalculatedAt,
    };

    /// <summary>
    /// With the switch on the section sits directly after the Out-of-Bounds Planets sub-section and before the
    /// Essential Dignities (R6, scoping decision 3): its heading, the opening line, the Positions table with the
    /// nine columns and the Aspects table at 2°00′. J2000 NYC: Pallas retrograde and out of bounds, and the first
    /// aspect Ceres square the Ascendant at 0°12′, separating (Tier B, j2000nyc).
    /// </summary>
    [Fact]
    public void ToMarkdown_SwitchOn_PrintsTheSection_AfterOutOfBounds_BeforeDignities()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        List<string> lines = Lines(ChartMarkdownFormatter.ToMarkdown(result));

        int section = lines.IndexOf(MinorPointsHeading);
        section.Should().BeGreaterThan(0, "the section prints with the switch on");
        lines.FindLastIndex(section, l => l.StartsWith("### ", StringComparison.Ordinal))
            .Should().Be(lines.IndexOf("### Out-of-Bounds Planets"), "the sub-heading before the section is Out-of-Bounds Planets");
        lines.FindIndex(section + 1, l => l.StartsWith("## ", StringComparison.Ordinal))
            .Should().Be(lines.IndexOf("## Essential Dignities"), "the heading after the section is Essential Dignities");
        lines[section - 1].Should().BeEmpty();
        lines[section + 1].Should().BeEmpty();
        lines[section + 2].Should().Be(
            "Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee): minor modern points, read as Chiron is. " +
            "Their aspects are the five Ptolemaic aspects at a 2°00′ orb, to the Sun to Pluto, the Ascendant and the Midheaven only.");
        lines[section + 3].Should().BeEmpty();
        lines[section + 4].Should().Be("### Positions");
        lines[section + 5].Should().BeEmpty();
        lines[section + 6].Should().Be("| Body | Position | Lat | RA | Dec | House | Speed | Dir | OOB |");
        lines[section + 7].Should().Be("|------|----------|-----|----|-----|-------|-------|-----|-----|");
        lines.Skip(section + 8).Take(5).Select(l => l.Split(" | ")[0]).Should().Equal("| Ceres", "| Pallas", "| Juno", "| Vesta", "| Lilith");
        lines[section + 9].Should().Be("| Pallas | 14°02′34″ Leo | -48°21′03″ | 08h 08m 32s | -29°42′26″ | 7 | -0.196/d | R | OOB |");
        lines[section + 13].Should().BeEmpty();
        lines[section + 14].Should().Be(MinorPointAspectsHeading);
        lines[section + 15].Should().BeEmpty();
        lines[section + 16].Should().Be("| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |");
        lines[section + 17].Should().Be("|--------|--------|--------|-----|-------|");
        lines[section + 18].Should().Be("| Ceres | sqr | ASC | 0°12′ | Sp |");
        lines.Skip(section + 18).Take(5).Should().AllSatisfy(l => l.Should().StartWith("| "));
        lines[section + 23].Should().BeEmpty();
        lines[section + 24].Should().Be("## Essential Dignities");
    }

    /// <summary>With the switch off nothing of the five prints: no heading and no row (R9).</summary>
    [Fact]
    public void ToMarkdown_SwitchOff_PrintsNoSection()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput());
        result.MinorPoints.Should().BeEmpty();

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain(MinorPointsHeading);
        markdown.Should().NotContain("| Ceres |");
        markdown.Should().NotContain("Lilith");
    }

    /// <summary>
    /// A horary chart never carries the five, whatever the switch (R8; scoping decision 2: the engine computes
    /// nothing for EventType "Horary"), so its export prints no section.
    /// </summary>
    [Fact]
    public void ToMarkdown_Horary_PrintsNoSection_EvenWithTheSwitchOn()
    {
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Horary with the switch on",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Horary",
            HoraryQuestion = "Will the five print?",
            IncludeMinorPoints = true,
        });
        result.MinorPoints.Should().BeEmpty("the engine computes nothing for a horary chart");

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain(MinorPointsHeading);
        markdown.Should().NotContain("| Ceres |");
    }

    /// <summary>
    /// An embedder that passes <c>minorPoints: false</c> gets no section however the chart was cast (R8): the
    /// primary-direction, medical, electional, ingress, lunation and great-conjunction exports pass it.
    /// </summary>
    [Fact]
    public void ToMarkdown_MinorPointsFalse_PrintsNoSection_WhateverTheChartCarries()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));
        result.MinorPoints.Should().HaveCount(5, "the chart carries the five");

        string markdown = ChartMarkdownFormatter.ToMarkdown(result, minorPoints: false);

        markdown.Should().NotContain(MinorPointsHeading);
        markdown.Should().NotContain("| Ceres |");
        markdown.Should().NotContain("Lilith");
    }

    /// <summary>Five points with no aspect within 2° print the none line in place of the aspects table.</summary>
    [Fact]
    public void ToMarkdown_FivePointsAndNoAspects_PrintsTheNoneLine()
    {
        ChartResult cast = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));
        ChartResult result = WithMinorPoints(cast, cast.MinorPoints, []);

        string markdown = ChartMarkdownFormatter.ToMarkdown(result);
        List<string> lines = Lines(markdown);

        int heading = lines.IndexOf(MinorPointAspectsHeading);
        heading.Should().BeGreaterThan(lines.IndexOf(MinorPointsHeading));
        lines[heading + 1].Should().BeEmpty();
        lines[heading + 2].Should().Be("No aspects within 2°00′.");
        lines[heading + 3].Should().BeEmpty();
        lines[heading + 4].Should().Be("## Essential Dignities");
        lines.Count(l => l == "| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |").Should().Be(1, "only the chart's own aspects table prints");
    }

    /// <summary>
    /// A row prints its own orb, never the definition's: a minor-point aspect's <see cref="AspectResult.Aspect"/>
    /// is the chart's definition, whose orb is 8° for a conjunction, while the row's orb is the separation found
    /// at the 2° limit (the engine report's note). A constructed conjunction at 1.5° prints 1°30′.
    /// </summary>
    [Fact]
    public void ToMarkdown_ConstructedConjunction_PrintsTheRowsOrb_NeverTheDefinitions()
    {
        ChartResult cast = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));
        PlanetPosition ceres = cast.MinorPoints[0];
        AspectResult conjunction = new()
        {
            Body1Name = ceres.Name,
            Body1Longitude = ceres.Longitude,
            Body2Name = "Sun",
            Body2Longitude = (ceres.Longitude + 1.5) % 360.0,
            Aspect = AspectDefinition.All.Single(d => d.Type == AspectType.Conjunction),
            Orb = 1.5,
            IsApplying = true,
        };
        conjunction.Aspect.Orb.Should().Be(8.0, "the definition's orb is the chart's own");
        ChartResult result = WithMinorPoints(cast, cast.MinorPoints, [conjunction]);

        List<string> lines = Lines(ChartMarkdownFormatter.ToMarkdown(result));

        int heading = lines.IndexOf(MinorPointAspectsHeading);
        lines[heading + 4].Should().Be("| Ceres | cnj | Sun | 1°30′ | Ap |");
        lines.Should().NotContain(l => l.StartsWith("| Ceres | cnj | Sun | 8°00′", StringComparison.Ordinal));
    }

    #endregion
}
