using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="DailyHoroscopeMarkdownFormatter.ToMarkdown"/> verifying
/// document structure, section headings, content inclusion, and edge cases.
/// Uses synthetic test data (no ephemeris dependency) for deterministic assertions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DailyHoroscopeFormatterTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates a minimal ChartResult for testing.
    /// </summary>
    private static ChartResult CreateMinimalChartResult()
    {
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 280.0, 1.017, 10),
            CreatePosition(CelestialBodyId.Moon, "Moon", 112.0, 13.2, 4),
            CreatePosition(CelestialBodyId.Mercury, "Mercury", 265.0, 1.5, 9),
            CreatePosition(CelestialBodyId.Venus, "Venus", 300.0, 1.2, 10),
            CreatePosition(CelestialBodyId.Mars, "Mars", 330.0, 0.6, 11),
            CreatePosition(CelestialBodyId.Jupiter, "Jupiter", 25.0, 0.2, 1),
            CreatePosition(CelestialBodyId.Saturn, "Saturn", 40.0, 0.12, 1),
            CreatePosition(CelestialBodyId.Uranus, "Uranus", 315.0, 0.04, 11),
            CreatePosition(CelestialBodyId.Neptune, "Neptune", 303.0, 0.02, 10),
            CreatePosition(CelestialBodyId.Pluto, "Pluto", 251.0, 0.01, 9),
            CreatePosition(CelestialBodyId.TrueNode, "True Node", 130.0, -0.05, 5),
            CreatePosition(CelestialBodyId.Chiron, "Chiron", 175.0, 0.05, 7),
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

    /// <summary>Creates a minimal PlanetPosition for testing.</summary>
    private static PlanetPosition CreatePosition(
        CelestialBodyId body, string name, double longitude, double speed, int house)
    {
        return new PlanetPosition
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
        };
    }

    /// <summary>Creates a minimal TransitResult for testing.</summary>
    private static TransitResult CreateTestTransitResult(
        bool includeActiveTransits = true)
    {
        ChartResult natalChart = CreateMinimalChartResult();

        Dictionary<TransitBody, TransitPlanetPosition> positions = new()
        {
            [TransitBody.Saturn] = new TransitPlanetPosition
            {
                Longitude = 352.25,
                Sign = "Pisces",
                Degrees = 22,
                Minutes = 15,
                IsRetrograde = false,
                DailyMotion = 0.065,
            },
            [TransitBody.Pluto] = new TransitPlanetPosition
            {
                Longitude = 22.5,
                Sign = "Aquarius",
                Degrees = 22,
                Minutes = 30,
                IsRetrograde = false,
                DailyMotion = 0.01,
            },
            [TransitBody.Jupiter] = new TransitPlanetPosition
            {
                Longitude = 55.5,
                Sign = "Taurus",
                Degrees = 25,
                Minutes = 30,
                IsRetrograde = false,
                DailyMotion = 0.083,
            },
        };

        List<ActiveTransit> activeTransits = includeActiveTransits
            ?
            [
                new ActiveTransit
                {
                    TransitPlanet = TransitBody.Saturn,
                    NatalTarget = "Sun",
                    NatalTargetSymbol = "SU",
                    AspectName = "Conjunction",
                    AspectAbbreviation = "cnj",
                    TransitLongitudeAtStart = 280.5,
                    TransitSignAtStart = "Capricorn",
                    NatalLongitude = 280.0,
                    NatalSign = "Capricorn",
                    NatalHouseNumber = 10,
                    OrbAtStartDegrees = 0.5,
                    OrbDirection = "applying",
                    TransitHouseNumber = 10,
                    Lifecycle =
                    [
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Entry,
                            Date = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exact,
                            Date = new DateTime(2026, 3, 12, 14, 23, 7, DateTimeKind.Utc),
                            PassNumber = 1,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exit,
                            Date = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc),
                        },
                    ],
                },
                new ActiveTransit
                {
                    TransitPlanet = TransitBody.Pluto,
                    NatalTarget = "Moon",
                    NatalTargetSymbol = "MO",
                    AspectName = "Square",
                    AspectAbbreviation = "sqr",
                    TransitLongitudeAtStart = 22.5,
                    TransitSignAtStart = "Aquarius",
                    NatalLongitude = 112.0,
                    NatalSign = "Cancer",
                    NatalHouseNumber = 4,
                    OrbAtStartDegrees = 0.5,
                    OrbDirection = "separating",
                    TransitHouseNumber = 11,
                    Lifecycle =
                    [
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Entry,
                            Date = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc),
                            BeforeReportPeriod = true,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exact,
                            Date = new DateTime(2026, 2, 28, 8, 47, 0, DateTimeKind.Utc),
                            PassNumber = 1,
                            BeforeReportPeriod = true,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exit,
                            Date = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
                        },
                    ],
                },
                new ActiveTransit
                {
                    TransitPlanet = TransitBody.Jupiter,
                    NatalTarget = "MC",
                    NatalTargetSymbol = "MC",
                    AspectName = "Trine",
                    AspectAbbreviation = "tri",
                    TransitLongitudeAtStart = 30.0,
                    TransitSignAtStart = "Taurus",
                    NatalLongitude = 270.0,
                    NatalSign = "Capricorn",
                    NatalHouseNumber = 10,
                    OrbAtStartDegrees = 3.0,
                    OrbDirection = "applying",
                    TransitHouseNumber = 2,
                    Lifecycle =
                    [
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Entry,
                            Date = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc),
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exact,
                            Date = new DateTime(2026, 3, 20, 16, 45, 0, DateTimeKind.Utc),
                            PassNumber = 1,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exit,
                            Date = new DateTime(2026, 4, 5, 12, 0, 0, DateTimeKind.Utc),
                            BeyondReportPeriod = true,
                        },
                    ],
                },
            ]
            : [];

        return new TransitResult
        {
            NatalChart = natalChart,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
            MidpointDate = new DateOnly(2026, 3, 16),
            IncludeMinorAspects = false,
            TransitPositions = positions.AsReadOnly(),
            Stations = new List<TransitStation>().AsReadOnly(),
            Ingresses = new List<TransitIngress>().AsReadOnly(),
            ActiveTransits = activeTransits.AsReadOnly(),
            HouseOverlays = new List<TransitHouseOverlay>().AsReadOnly(),
            MidpointActivations = new List<TransitMidpointActivation>().AsReadOnly(),
            MarsIncluded = false,
        };
    }

    /// <summary>Creates a single-day DailyHoroscopeDay for testing.</summary>
    private static DailyHoroscopeDay CreateTestDay(
        DateOnly date,
        ZodiacSign moonSign = ZodiacSign.Aries,
        string moonPhaseLabel = "Waxing Crescent (mid)",
        List<string>? vocPeriods = null,
        List<DailyTransitSnapshot>? activeTransits = null,
        List<DailyTransitSnapshot>? exactToday = null,
        List<DailyTransitSnapshot>? enteringToday = null,
        List<DailyTransitSnapshot>? exitingToday = null)
    {
        return new DailyHoroscopeDay
        {
            Date = date,
            MoonSign = moonSign,
            MoonLongitude = 15.0,
            MoonPhaseLabel = moonPhaseLabel,
            VoidOfCoursePeriods = vocPeriods ?? [],
            ActiveTransits = activeTransits ?? [],
            ExactToday = exactToday ?? [],
            EnteringToday = enteringToday ?? [],
            ExitingToday = exitingToday ?? [],
        };
    }

    /// <summary>Creates a test transit snapshot.</summary>
    private static DailyTransitSnapshot CreateSnapshot(
        string transitPlanet = "TR Saturn",
        string aspect = "conjunction",
        string aspectAbbrev = "cnj",
        string natalTarget = "NA Sun",
        int natalHouse = 10,
        int transitHouse = 10,
        double orbDegrees = 1.5,
        string phase = "Applying",
        bool isExactToday = false,
        List<string>? exactTimes = null)
    {
        return new DailyTransitSnapshot
        {
            TransitPlanet = transitPlanet,
            Aspect = aspect,
            AspectAbbrev = aspectAbbrev,
            NatalTarget = natalTarget,
            NatalHouse = natalHouse,
            TransitHouse = transitHouse,
            OrbDegrees = orbDegrees,
            Phase = phase,
            IsExactToday = isExactToday,
            ExactTimes = exactTimes ?? [],
        };
    }

    #endregion

    #region ToMarkdown Tests

    [Fact]
    public void ToMarkdown_EmptyDays_ProducesHeaderOnly()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days = [];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("# Daily Horoscope Data");
        md.Should().Contain("**Name:** Test Person");
        md.Should().Contain("## Daily Snapshots");
    }

    [Fact]
    public void ToMarkdown_SingleDay_ContainsDateHeader()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13)),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("### Friday, March 13, 2026");
    }

    [Fact]
    public void ToMarkdown_MoonSignDisplayed()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13), moonSign: ZodiacSign.Cancer,
                moonPhaseLabel: "Full Moon (early)"),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("**Moon:** Cancer (Full Moon (early))");
    }

    [Fact]
    public void ToMarkdown_ExactTransit_AppearsInExactTable()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        DailyTransitSnapshot exactSnapshot = CreateSnapshot(
            isExactToday: true,
            exactTimes: ["14:23:07 UTC"]);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 12),
                exactToday: [exactSnapshot],
                activeTransits: [exactSnapshot]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("**Exact Today:**");
        md.Should().Contain("TR Saturn");
        md.Should().Contain("14:23:07 UTC");
    }

    [Fact]
    public void ToMarkdown_NoExactTransits_OmitsExactTable()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        DailyTransitSnapshot snapshot = CreateSnapshot();
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13),
                activeTransits: [snapshot]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().NotContain("**Exact Today:**");
    }

    /// <summary>
    /// An Active row's orb prints as truncated D°MM′ through one function (branch O-D3, 1.3.4):
    /// 2.35° is 2°21′. Until 1.3.4 the row printed "2°21'" with an apostrophe.
    /// </summary>
    [Fact]
    public void ToMarkdown_ActiveTransit_PrintsTheOrbAsTruncatedDegreesAndMinutes()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13), activeTransits: [CreateSnapshot(orbDegrees: 2.35)]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("| TR Saturn | cnj | NA Sun | 10 | 2\u00b021\u2032 | Applying |");
    }

    [Fact]
    public void ToMarkdown_ActiveTransits_SortedOuterFirst()
    {
        TransitResult result = CreateTestTransitResult();
        DailyTransitSnapshot plutoSnapshot = CreateSnapshot(
            transitPlanet: "TR Pluto", aspect: "square", aspectAbbrev: "sqr",
            natalTarget: "NA Moon", natalHouse: 4, transitHouse: 11);
        DailyTransitSnapshot saturnSnapshot = CreateSnapshot(
            transitPlanet: "TR Saturn", natalTarget: "NA Sun");
        DailyTransitSnapshot jupiterSnapshot = CreateSnapshot(
            transitPlanet: "TR Jupiter", aspect: "trine", aspectAbbrev: "tri",
            natalTarget: "NA MC", transitHouse: 2);

        // Pass in unsorted order
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 10),
                activeTransits: [plutoSnapshot, saturnSnapshot, jupiterSnapshot]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        int plutoIndex = md.IndexOf("TR Pluto", StringComparison.Ordinal);
        int saturnIndex = md.IndexOf("TR Saturn", StringComparison.Ordinal);
        int jupiterIndex = md.IndexOf("TR Jupiter", StringComparison.Ordinal);

        // In the Active table, Pluto should appear before Saturn, Saturn before Jupiter
        plutoIndex.Should().BeLessThan(saturnIndex);
        saturnIndex.Should().BeLessThan(jupiterIndex);
    }

    [Fact]
    public void ToMarkdown_SlowMovingBackdrop_ContainsOuterPlanets()
    {
        TransitResult result = CreateTestTransitResult();

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, []);

        md.Should().Contain("## Slow-Moving Transit Backdrop");
        md.Should().Contain("TR Saturn");
        md.Should().Contain("TR Pluto");
        md.Should().Contain("TR Jupiter");
    }

    [Fact]
    public void ToMarkdown_VocPeriods_FormattedCorrectly()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13),
                vocPeriods: ["14:30 \u2013 18:45 (Aries)"]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("**VoC:** 14:30 \u2013 18:45 (Aries)");
    }

    [Fact]
    public void ToMarkdown_InterpretationContext_IncludedInHeader()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(
            result, [],
            interpretationContext: "Focus on career themes. Client is changing jobs.");

        md.Should().Contain("**Interpretation Context:** Focus on career themes. Client is changing jobs.");
    }

    [Fact]
    public void ToMarkdown_MultipleDays_AllDaysPresent()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days = [];
        for (int i = 13; i <= 19; i++)
        {
            days.Add(CreateTestDay(new DateOnly(2026, 3, i)));
        }

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        // Count ### headers (daily snapshots)
        int headerCount = md.Split("### ").Length - 1;
        headerCount.Should().Be(7);
    }

    [Fact]
    public void ToMarkdown_EnteringExiting_DisplayedWhenPresent()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        DailyTransitSnapshot enteringSnapshot = CreateSnapshot(
            transitPlanet: "TR Mars", aspect: "conjunction", aspectAbbrev: "cnj",
            natalTarget: "NA Venus", natalHouse: 7, transitHouse: 7);
        DailyTransitSnapshot exitingSnapshot = CreateSnapshot(
            transitPlanet: "TR Mercury", aspect: "opposition", aspectAbbrev: "opp",
            natalTarget: "NA Saturn", natalHouse: 1, transitHouse: 7);

        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13),
                enteringToday: [enteringSnapshot],
                exitingToday: [exitingSnapshot]),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().Contain("**Entering orb:**");
        md.Should().Contain("TR Mars cnj NA Venus (H7)");
        md.Should().Contain("**Exiting orb:**");
        md.Should().Contain("TR Mercury opp NA Saturn (H1)");
    }

    [Fact]
    public void ToMarkdown_EnteringExiting_OmittedWhenEmpty()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13)),
        ];

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);

        md.Should().NotContain("**Entering orb:**");
        md.Should().NotContain("**Exiting orb:**");
    }

    /// <summary>
    /// A day with no transit in orb names the bodies the daily searched (E2-3): only Jupiter to Pluto, Chiron, the
    /// North Node and Mars when it is slow are ever computed, so "No transits active today." claimed more than was
    /// looked for.
    /// </summary>
    [Fact]
    public void Daily_NoTransits_NamesTheBodiesSearched()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);
        List<DailyHoroscopeDay> days =
        [
            CreateTestDay(new DateOnly(2026, 3, 13)),
        ];

        List<string> lines = [.. DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days).Split('\n').Select(l => l.TrimEnd('\r'))];

        lines.Should().Contain(
            "No transits of Jupiter, Saturn, Uranus, Neptune, Pluto, Chiron, the North Node or a slow Mars today.");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_SingleDay_ContainsDateHeader"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_SingleDay_ContainsDateHeader_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_SingleDay_ContainsDateHeader();
    }

    #endregion
}
