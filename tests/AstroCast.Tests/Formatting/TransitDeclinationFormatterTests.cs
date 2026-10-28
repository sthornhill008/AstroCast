using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for the transit-to-natal declination aspects section in
/// <see cref="TransitMarkdownFormatter"/>. Uses synthetic test data
/// (no ephemeris dependency) to verify section rendering, parallel/contraparallel
/// formatting, chronological ordering, and suppression when no aspects exist.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitDeclinationFormatterTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates a minimal <see cref="ChartResult"/> with enough data for the
    /// formatter to produce valid output.
    /// </summary>
    private static ChartResult CreateMinimalChartResult()
    {
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 280.0, 1.017, 10, +22.917),
            CreatePosition(CelestialBodyId.Moon, "Moon", 112.0, 13.2, 4, -15.5),
            CreatePosition(CelestialBodyId.Mercury, "Mercury", 265.0, 1.5, 9, +23.0),
            CreatePosition(CelestialBodyId.Venus, "Venus", 300.0, 1.2, 10, -18.3),
            CreatePosition(CelestialBodyId.Mars, "Mars", 330.0, 0.6, 11, +10.2),
            CreatePosition(CelestialBodyId.Jupiter, "Jupiter", 25.0, 0.2, 1, +5.0),
            CreatePosition(CelestialBodyId.Saturn, "Saturn", 40.0, 0.12, 1, +12.4),
            CreatePosition(CelestialBodyId.Uranus, "Uranus", 315.0, 0.04, 11, -22.0),
            CreatePosition(CelestialBodyId.Neptune, "Neptune", 303.0, 0.02, 10, -8.6),
            CreatePosition(CelestialBodyId.Pluto, "Pluto", 251.0, 0.01, 9, +22.833),
            CreatePosition(CelestialBodyId.TrueNode, "True Node", 130.0, -0.05, 5, null),
            CreatePosition(CelestialBodyId.Chiron, "Chiron", 175.0, 0.05, 7, +3.5),
        ];

        List<double> cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList();

        HouseData houses = new()
        {
            Cusps = cusps,
            Ascendant = 0.0,
            Midheaven = 270.0,
            Armc = 270.0,
            Vertex = 180.0,
        };

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test Person",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
                EventType = "Birth",
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            Positions = positions,
            Houses = houses,
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Creates a minimal <see cref="PlanetPosition"/> for testing.</summary>
    private static PlanetPosition CreatePosition(
        CelestialBodyId body, string name, double longitude, double speed, int house, double? declination) =>
        new()
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
            Declination = declination,
        };

    /// <summary>
    /// Creates a <see cref="TransitResult"/> with the specified transit declination aspects.
    /// </summary>
    private static TransitResult CreateTransitResult(
        IReadOnlyList<TransitDeclinationAspect>? transitDeclinationAspects = null)
    {
        ChartResult natalChart = CreateMinimalChartResult();

        Dictionary<TransitBody, TransitPlanetPosition> positions = new()
        {
            [TransitBody.Jupiter] = new TransitPlanetPosition
            {
                Longitude = 55.5,
                Sign = "Taurus",
                Degrees = 25,
                Minutes = 30,
                IsRetrograde = false,
                DailyMotion = 0.083,
            },
            [TransitBody.Saturn] = new TransitPlanetPosition
            {
                Longitude = 352.25,
                Sign = "Pisces",
                Degrees = 22,
                Minutes = 15,
                IsRetrograde = false,
                DailyMotion = 0.065,
            },
        };

        return new TransitResult
        {
            NatalChart = natalChart,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
            MidpointDate = new DateOnly(2026, 3, 16),
            IncludeMinorAspects = false,
            TransitPositions = positions.AsReadOnly(),
            Stations = Array.Empty<TransitStation>(),
            Ingresses = Array.Empty<TransitIngress>(),
            ActiveTransits = Array.Empty<ActiveTransit>(),
            HouseOverlays = Array.Empty<TransitHouseOverlay>(),
            MidpointActivations = Array.Empty<TransitMidpointActivation>(),
            MarsIncluded = false,
            TransitDeclinationAspects = transitDeclinationAspects ?? [],
        };
    }

    #endregion

    #region Section Emission

    [Fact]
    public void ToMarkdown_NoTransitDeclinationAspects_SectionOmitted()
    {
        // Arrange — no transit declination aspects
        TransitResult result = CreateTransitResult(transitDeclinationAspects: []);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().NotContain("Transit-to-Natal Declination Aspects",
            "section should be suppressed when no aspects exist");
    }

    [Fact]
    public void ToMarkdown_WithTransitDeclinationAspects_SectionEmitted()
    {
        // Arrange
        List<TransitDeclinationAspect> aspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Jupiter",
                TransitDeclination = +22.833,
                IsParallel = true,
                NatalPlanetName = "Sun",
                NatalDeclination = +22.917,
                Orb = 0.083,
                Date = new DateOnly(2026, 3, 10),
            },
        ];

        TransitResult result = CreateTransitResult(transitDeclinationAspects: aspects);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("## Transit-to-Natal Declination Aspects (±1° orb)");
        markdown.Should().Contain("| Transit | Tr Dec | Aspect | Natal | Na Dec | Orb | Date |");
    }

    #endregion

    #region Parallel and Contraparallel Rendering

    [Fact]
    public void ToMarkdown_ParallelAspect_ShowsParAbbreviation()
    {
        // Arrange — transit Jupiter at Dec +22°50' vs natal Sun at Dec +22°55'
        // Same-sign declinations within ±1° = parallel (orb 0°05')
        List<TransitDeclinationAspect> aspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Jupiter",
                TransitDeclination = +22.833,   // +22°50'
                IsParallel = true,
                NatalPlanetName = "Sun",
                NatalDeclination = +22.917,     // +22°55'
                Orb = 0.083,                    // 0°05'
                Date = new DateOnly(2026, 3, 12),
            },
        ];

        TransitResult result = CreateTransitResult(transitDeclinationAspects: aspects);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Par", "parallel aspect should show Par abbreviation");
        markdown.Should().Contain("TR Jupiter");
        markdown.Should().Contain("NA Sun");
        markdown.Should().Contain("2026-03-12");
    }

    [Fact]
    public void ToMarkdown_ContraparallelAspect_ShowsCParAbbreviation()
    {
        // Arrange — transit Jupiter at Dec +22°50' vs natal Uranus at Dec -22°55'
        // Opposite-sign declinations within ±1° = contraparallel (orb 0°05')
        List<TransitDeclinationAspect> aspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Jupiter",
                TransitDeclination = +22.833,    // +22°50'
                IsParallel = false,
                NatalPlanetName = "Uranus",
                NatalDeclination = -22.917,      // -22°55'
                Orb = 0.083,                     // 0°05'
                Date = new DateOnly(2026, 3, 15),
            },
        ];

        TransitResult result = CreateTransitResult(transitDeclinationAspects: aspects);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("CPar", "contraparallel aspect should show CPar abbreviation");
        markdown.Should().Contain("TR Jupiter");
        markdown.Should().Contain("NA Uranus");
    }

    #endregion

    #region Chronological Ordering

    [Fact]
    public void ToMarkdown_MultipleAspects_DisplayedChronologically()
    {
        // Arrange — aspects provided in chronological order (as the service returns them)
        List<TransitDeclinationAspect> aspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Saturn",
                TransitDeclination = +12.5,
                IsParallel = true,
                NatalPlanetName = "Saturn",
                NatalDeclination = +12.4,
                Orb = 0.1,
                Date = new DateOnly(2026, 3, 5),
            },
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Jupiter",
                TransitDeclination = +22.833,
                IsParallel = true,
                NatalPlanetName = "Sun",
                NatalDeclination = +22.917,
                Orb = 0.083,
                Date = new DateOnly(2026, 3, 12),
            },
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Jupiter",
                TransitDeclination = +22.900,
                IsParallel = false,
                NatalPlanetName = "Uranus",
                NatalDeclination = -22.0,
                Orb = 0.900,
                Date = new DateOnly(2026, 3, 25),
            },
        ];

        TransitResult result = CreateTransitResult(transitDeclinationAspects: aspects);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert — verify dates appear in chronological order
        int indexFirst = markdown.IndexOf("2026-03-05");
        int indexSecond = markdown.IndexOf("2026-03-12");
        int indexThird = markdown.IndexOf("2026-03-25");

        indexFirst.Should().BeGreaterThan(-1);
        indexSecond.Should().BeGreaterThan(indexFirst,
            "second aspect date should appear after first");
        indexThird.Should().BeGreaterThan(indexSecond,
            "third aspect date should appear after second");
    }

    #endregion

    #region Orb Formatting

    [Fact]
    public void ToMarkdown_OrbFormattedAsDegreeMinute()
    {
        // Arrange — orb of 0.5° = 0°30′ (branch O-D3, 1.3.4: the prime, through one function)
        List<TransitDeclinationAspect> aspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Saturn",
                TransitDeclination = +12.5,
                IsParallel = true,
                NatalPlanetName = "Saturn",
                NatalDeclination = +12.0,
                Orb = 0.5,
                Date = new DateOnly(2026, 3, 10),
            },
        ];

        TransitResult result = CreateTransitResult(transitDeclinationAspects: aspects);

        // Act
        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Assert — orb should be formatted as 0°30′
        markdown.Should().Contain("0°30′");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_ParallelAspect_ShowsParAbbreviation"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_ParallelAspect_ShowsParAbbreviation_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_ParallelAspect_ShowsParAbbreviation();
    }

    /// <summary>
    /// <see cref="ToMarkdown_MultipleAspects_DisplayedChronologically"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_MultipleAspects_DisplayedChronologically_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_MultipleAspects_DisplayedChronologically();
    }

    #endregion
}
