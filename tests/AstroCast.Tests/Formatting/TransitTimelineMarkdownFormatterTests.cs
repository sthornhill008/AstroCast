using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="TransitTimelineMarkdownFormatter"/> verifying chronological
/// event ordering, event type inclusion/exclusion, Markdown table structure,
/// residual orb display, and transit-to-transit aspect rows.
/// Uses synthetic test data (no ephemeris dependency) for deterministic assertions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitTimelineMarkdownFormatterTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates a minimal ChartResult for use as the natal chart in transit results.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0).</param>
    private static ChartResult CreateMinimalChartResult(bool includeMinorPoints = false)
    {
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 280.0, 1.017, 10),
            CreatePosition(CelestialBodyId.Moon, "Moon", 112.0, 13.2, 4),
        ];

        List<PlanetPosition> minorPoints = includeMinorPoints
            ?
            [
                CreatePosition(CelestialBodyId.Ceres, "Ceres", 184.45, 0.22, 9),
                CreatePosition(CelestialBodyId.Pallas, "Pallas", 134.04, -0.2, 7),
                CreatePosition(CelestialBodyId.Juno, "Juno", 277.99, 0.37, 1),
                CreatePosition(CelestialBodyId.Vesta, "Vesta", 245.97, 0.4, 11),
                CreatePosition(CelestialBodyId.MeanApogee, "Lilith", 263.46, 0.11, 12),
            ]
            : [];

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
            MinorPoints = minorPoints,
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

    /// <summary>
    /// Creates a TransitResult with configurable event data for timeline testing.
    /// </summary>
    private static TransitResult CreateTransitResult(
        List<ActiveTransit>? activeTransits = null,
        List<TransitStation>? stations = null,
        List<TransitIngress>? ingresses = null,
        List<TransitMidpointActivation>? midpointActivations = null,
        List<TransitToTransitAspect>? transitToTransitAspects = null,
        List<ActiveTransit>? minorPointTransits = null,
        bool includeMinorPoints = false)
    {
        return new TransitResult
        {
            NatalChart = CreateMinimalChartResult(includeMinorPoints),
            MinorPointTransits = minorPointTransits ?? [],
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
            MidpointDate = new DateOnly(2026, 3, 16),
            IncludeMinorAspects = false,
            TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = stations ?? [],
            Ingresses = ingresses ?? [],
            ActiveTransits = activeTransits ?? [],
            HouseOverlays = [],
            MidpointActivations = midpointActivations ?? [],
            TransitToTransitAspects = transitToTransitAspects ?? [],
            MarsIncluded = false,
        };
    }

    #endregion

    #region Header Tests

    /// <summary>
    /// ToMarkdown includes the H1 heading, name, and report period in the header.
    /// </summary>
    [Fact]
    public void ToMarkdown_IncludesHeader_WithNameAndPeriod()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("# Transit Timeline");
        md.Should().Contain("**Name:** Test Person");
        md.Should().Contain("**Report Period:** 2026-03-01 to 2026-03-31");
    }

    /// <summary>
    /// When birthTimeSource is provided, it appears in the header.
    /// </summary>
    [Fact]
    public void ToMarkdown_WithBirthTimeSource_IncludesInHeader()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(
            result, birthTimeSource: "Birth certificate");

        md.Should().Contain("**Birth Time Source:** Birth certificate");
    }

    /// <summary>
    /// When interpretationContext is provided, it appears in the header.
    /// </summary>
    [Fact]
    public void ToMarkdown_WithInterpretationContext_IncludesInHeader()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(
            result, interpretationContext: "Focus on career transits");

        md.Should().Contain("**Context:** Focus on career transits");
    }

    /// <summary>
    /// When neither optional parameter is provided, header omits those fields.
    /// </summary>
    [Fact]
    public void ToMarkdown_WithoutOptionalParams_OmitsBirthTimeAndContext()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().NotContain("**Birth Time Source:**");
        md.Should().NotContain("**Context:**");
    }

    #endregion

    #region Empty Result Tests

    /// <summary>
    /// When no transit events exist, the output shows a "no events" message.
    /// </summary>
    [Fact]
    public void ToMarkdown_NoEvents_ShowsEmptyMessage()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("No transit events during this period.");
        md.Should().NotContain("| Date |");
    }

    #endregion

    #region Exact Pass Extraction Tests

    /// <summary>
    /// Exact pass events from active transits appear in the timeline table
    /// with UTC time, transit label, pass number, and residual orb.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPass_AppearsInTimeline()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        ExactOrbDegrees = 0.00042,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("| Date | Transit | Event | Position | Orb |");
        md.Should().Contain("2026-03-12 14:23 UTC");
        md.Should().Contain("TR Saturn cnj NA Sun");
        md.Should().Contain("Exact pass 1");
    }

    /// <summary>
    /// Exact passes flagged as BeforeReportPeriod or BeyondReportPeriod are excluded
    /// from the Events table (but Entry/Exit events are still included).
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPass_OutOfPeriod_IsExcluded()
    {
        List<ActiveTransit> transits =
        [
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 2, 28, 8, 47, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        BeforeReportPeriod = true,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                        PassNumber = 2,
                        BeyondReportPeriod = true,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // Out-of-period exact passes should not appear in the Events table
        // (they DO appear in Transit Windows with boundary notes)
        int eventsSection = md.IndexOf("## Events");
        eventsSection.Should().BeGreaterThan(-1);
        string eventsTable = md[eventsSection..md.IndexOf("---", eventsSection + 1)];

        eventsTable.Should().NotContain("Exact pass 1",
            "before-period exact passes should not appear in Events table");
        eventsTable.Should().NotContain("Exact pass 2",
            "beyond-period exact passes should not appear in Events table");
    }

    /// <summary>
    /// Entry and Exit lifecycle events are now included in the Events table
    /// (previously they were excluded).
    /// </summary>
    [Fact]
    public void ToMarkdown_EntryExitEvents_AreIncludedInEventsTable()
    {
        List<ActiveTransit> transits =
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
                        Date = new DateTime(2026, 3, 1, 8, 14, 0, DateTimeKind.Utc),
                        TransitLongitude = 277.5,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 25, 11, 22, 0, DateTimeKind.Utc),
                        TransitLongitude = 282.5,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // Entry and Exit should appear in the Events table
        md.Should().Contain("Entry");
        md.Should().Contain("Exit");
        md.Should().Contain("TR Saturn cnj NA Sun");
    }

    #endregion

    #region Station and Ingress Tests

    /// <summary>
    /// Stations and ingresses appear in the timeline with correct descriptions
    /// and empty orb cells.
    /// </summary>
    [Fact]
    public void ToMarkdown_StationsAndIngresses_AppearInTimeline()
    {
        List<TransitStation> stations =
        [
            new TransitStation
            {
                Planet = TransitBody.Saturn,
                StationType = "Retrograde",
                Date = new DateOnly(2026, 3, 20),
                Sign = "Pisces",
                Degrees = 22,
                Minutes = 30,
            },
        ];

        List<TransitIngress> ingresses =
        [
            new TransitIngress
            {
                Planet = TransitBody.Jupiter,
                FromSign = "Taurus",
                ToSign = "Gemini",
                Date = new DateOnly(2026, 3, 10),
            },
        ];

        TransitResult result = CreateTransitResult(stations: stations, ingresses: ingresses);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("Station Retrograde");
        md.Should().Contain("22\u00b030' Pisces");
        md.Should().Contain("Ingress Taurus \u2192 Gemini");
        md.Should().Contain("2026-03-20");
        md.Should().Contain("2026-03-10");
    }

    #endregion

    #region Midpoint Activation Tests

    /// <summary>
    /// Midpoint activation exact passes appear in the timeline.
    /// </summary>
    [Fact]
    public void ToMarkdown_MidpointExactPass_AppearsInTimeline()
    {
        List<TransitMidpointActivation> activations =
        [
            new TransitMidpointActivation
            {
                TransitPlanet = TransitBody.Saturn,
                MidpointBody1 = "Sun",
                MidpointBody2 = "Moon",
                TransitDial45Position = 10.0,
                NatalMidpointDial45Position = 10.5,
                OrbAtStartDegrees = 0.5,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 15, 9, 30, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(midpointActivations: activations);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("TR Saturn = Sun/Moon");
        md.Should().Contain("Midpoint exact");
        md.Should().Contain("2026-03-15 09:30 UTC");
    }

    #endregion

    #region Transit-to-Transit Aspect Tests

    /// <summary>
    /// Transit-to-transit aspects appear in the timeline with correct label format
    /// and residual orb display.
    /// </summary>
    [Fact]
    public void ToMarkdown_TransitToTransitAspect_AppearsInTimeline()
    {
        List<TransitToTransitAspect> aspects =
        [
            new TransitToTransitAspect
            {
                Planet1 = TransitBody.Saturn,
                Planet1Name = "Saturn",
                Planet1Longitude = 5.2,
                Planet1Position = "5\u00b012' Aries",
                Planet2 = TransitBody.Neptune,
                Planet2Name = "Neptune",
                Planet2Longitude = 5.3,
                Planet2Position = "5\u00b018' Aries",
                AspectName = "Conjunction",
                AspectAbbreviation = "cnj",
                AspectAngle = 0.0,
                ExactDateTimeUtc = new DateTime(2026, 3, 18, 16, 45, 0, DateTimeKind.Utc),
                ExactOrbDegrees = 0.00015,
            },
        ];

        TransitResult result = CreateTransitResult(transitToTransitAspects: aspects);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("TR Saturn cnj TR Neptune");
        md.Should().Contain("Transit-to-transit conjunction");
        md.Should().Contain("2026-03-18 16:45 UTC");
    }

    /// <summary>
    /// Transit-to-transit aspects are sorted chronologically alongside other events.
    /// </summary>
    [Fact]
    public void ToMarkdown_TransitToTransitAspect_SortedWithOtherEvents()
    {
        // Exact pass on Mar 12
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        ExactOrbDegrees = 0.001,
                    },
                ],
            },
        ];

        // Transit-to-transit on Mar 18
        List<TransitToTransitAspect> t2tAspects =
        [
            new TransitToTransitAspect
            {
                Planet1 = TransitBody.Saturn,
                Planet1Name = "Saturn",
                Planet1Longitude = 5.2,
                Planet1Position = "5\u00b012' Aries",
                Planet2 = TransitBody.Neptune,
                Planet2Name = "Neptune",
                Planet2Longitude = 5.3,
                Planet2Position = "5\u00b018' Aries",
                AspectName = "Conjunction",
                AspectAbbreviation = "cnj",
                AspectAngle = 0.0,
                ExactDateTimeUtc = new DateTime(2026, 3, 18, 16, 45, 0, DateTimeKind.Utc),
                ExactOrbDegrees = 0.00015,
            },
        ];

        TransitResult result = CreateTransitResult(
            activeTransits: transits, transitToTransitAspects: t2tAspects);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        int natalIndex = md.IndexOf("TR Saturn cnj NA Sun");
        int t2tIndex = md.IndexOf("TR Saturn cnj TR Neptune");

        natalIndex.Should().BeGreaterThan(-1);
        t2tIndex.Should().BeGreaterThan(-1);
        natalIndex.Should().BeLessThan(t2tIndex,
            "natal transit (Mar 12) should come before transit-to-transit (Mar 18)");
    }

    #endregion

    #region Orb Formatting Tests

    /// <summary>
    /// Residual orb is formatted as D°MM′, truncated, in the Orb column for exact passes.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPassWithOrb_DisplaysFormattedOrb()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        ExactOrbDegrees = 0.05, // 0° 03' (0.05 * 60 = 3 minutes)
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // 0.05° = 0°03′ (3.0′, truncated; branch O-D3, 1.3.4)
        md.Should().Contain("0\u00b003\u2032");
    }

    /// <summary>
    /// When ExactOrbDegrees is null, the Orb column is empty for that row.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPassWithNullOrb_DisplaysEmptyOrbCell()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        ExactOrbDegrees = null,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // The row should contain Exact pass 1 with an empty Orb cell
        // (table now has Position column between Event and Orb)
        md.Should().Contain("Exact pass 1");
        // Verify there's a row with empty orb at the end
        md.Should().Contain("|  |", "orb cell should be empty when ExactOrbDegrees is null");
    }

    #endregion

    #region Footer Tests

    /// <summary>
    /// Footer contains the precision note about binary search refinement.
    /// </summary>
    [Fact]
    public void ToMarkdown_Footer_IncludesPrecisionNote()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("binary search");
        md.Should().Contain("~1 arc-second");
        md.Should().Contain("Entry/exit times mark the orb boundary crossing");
    }

    #endregion

    #region Chronological Sorting Tests

    /// <summary>
    /// Events from different sources are merged and sorted chronologically.
    /// </summary>
    [Fact]
    public void ToMarkdown_MixedEvents_SortedChronologically()
    {
        // Ingress on Mar 10
        List<TransitIngress> ingresses =
        [
            new TransitIngress
            {
                Planet = TransitBody.Jupiter,
                FromSign = "Taurus",
                ToSign = "Gemini",
                Date = new DateOnly(2026, 3, 10),
            },
        ];

        // Exact pass on Mar 12
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                ],
            },
        ];

        // Station on Mar 20
        List<TransitStation> stations =
        [
            new TransitStation
            {
                Planet = TransitBody.Saturn,
                StationType = "Retrograde",
                Date = new DateOnly(2026, 3, 20),
                Sign = "Pisces",
                Degrees = 22,
                Minutes = 30,
            },
        ];

        TransitResult result = CreateTransitResult(
            activeTransits: transits, stations: stations, ingresses: ingresses);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // Verify chronological order by checking that the ingress (Mar 10)
        // appears before the exact pass (Mar 12), which appears before the station (Mar 20)
        // Search within the Events table section (after "## Events")
        int eventsSection = md.IndexOf("## Events");
        eventsSection.Should().BeGreaterThan(-1, "Events section should exist");
        string eventsTable = md[eventsSection..];

        int ingressIndex = eventsTable.IndexOf("Ingress Taurus");
        int exactIndex = eventsTable.IndexOf("Exact pass 1");
        int stationIndex = eventsTable.IndexOf("Station Retrograde");

        ingressIndex.Should().BeGreaterThan(-1, "ingress should be in the Events table");
        exactIndex.Should().BeGreaterThan(-1, "exact pass should be in the Events table");
        stationIndex.Should().BeGreaterThan(-1, "station should be in the Events table");
        ingressIndex.Should().BeLessThan(exactIndex, "ingress (Mar 10) should come before exact pass (Mar 12)");
        exactIndex.Should().BeLessThan(stationIndex, "exact pass (Mar 12) should come before station (Mar 20)");
    }

    #endregion

    #region Null Guard Tests

    /// <summary>
    /// ToMarkdown throws ArgumentNullException for null input.
    /// </summary>
    [Fact]
    public void ToMarkdown_NullResult_ThrowsArgumentNullException()
    {
        Action act = () => TransitTimelineMarkdownFormatter.ToMarkdown(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SaveAsync Tests

    /// <summary>
    /// SaveAsync writes a valid Markdown file to disk.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WritesFile()
    {
        TransitResult result = CreateTransitResult();
        string tempPath = Path.Combine(Path.GetTempPath(), $"timeline_test_{Guid.NewGuid()}.md");

        try
        {
            await TransitTimelineMarkdownFormatter.SaveAsync(result, tempPath);

            File.Exists(tempPath).Should().BeTrue();
            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().Contain("# Transit Timeline");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>
    /// SaveAsync passes optional parameters through to the formatter.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithOptionalParams_IncludesInOutput()
    {
        TransitResult result = CreateTransitResult();
        string tempPath = Path.Combine(Path.GetTempPath(), $"timeline_test_{Guid.NewGuid()}.md");

        try
        {
            await TransitTimelineMarkdownFormatter.SaveAsync(
                result, tempPath,
                birthTimeSource: "Rectified",
                interpretationContext: "Career focus");

            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().Contain("**Birth Time Source:** Rectified");
            content.Should().Contain("**Context:** Career focus");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    #endregion

    #region Transit Windows Tests

    /// <summary>
    /// Transit Windows section appears when active transits exist.
    /// </summary>
    [Fact]
    public void ToMarkdown_IncludesTransitWindowsSection()
    {
        List<ActiveTransit> transits =
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
                        Date = new DateTime(2026, 3, 1, 8, 14, 0, DateTimeKind.Utc),
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 15, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                        ExactOrbDegrees = 0.001,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 30, 11, 22, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("## Transit Windows");
        md.Should().Contain("### TR Saturn cnj NA SU");
        md.Should().Contain("**Entry:**");
        md.Should().Contain("**Exact pass 1:**");
        md.Should().Contain("**Exit:**");
        md.Should().Contain("Duration:");
    }

    /// <summary>
    /// Transit Windows section shows duration in days with house numbers.
    /// </summary>
    [Fact]
    public void ToMarkdown_TransitWindow_ShowsDuration()
    {
        List<ActiveTransit> transits =
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
                        Date = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 31, 11, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("Duration: 30 days");
        md.Should().Contain("Natal house: 10th");
        md.Should().Contain("Transit house: 10th");
    }

    /// <summary>
    /// A window of one day reads "Duration: 1 day" (branch ExportText, 1.7.2; found beside finding N12). Until
    /// 1.7.2 it read "1 days".
    /// </summary>
    [Fact]
    public void ToMarkdown_TransitWindowOfOneDay_ReadsOneDay()
    {
        List<ActiveTransit> transits =
        [
            new ActiveTransit
            {
                TransitPlanet = TransitBody.Mars,
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
                        Date = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 2, 11, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("Duration: 1 day |");
    }

    #endregion

    #region Position Column Tests

    /// <summary>
    /// Events table includes Position column header.
    /// </summary>
    [Fact]
    public void ToMarkdown_EventsTable_HasPositionColumn()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("| Position |");
    }

    /// <summary>
    /// Entry row shows position from TransitLongitude.
    /// </summary>
    [Fact]
    public void ToMarkdown_EntryRow_ShowsPosition()
    {
        List<ActiveTransit> transits =
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
                OrbAtStartDegrees = 3.0,
                OrbDirection = "applying",
                TransitHouseNumber = 10,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Entry,
                        Date = new DateTime(2026, 3, 5, 8, 14, 0, DateTimeKind.Utc),
                        TransitLongitude = 277.0, // 7° Capricorn
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // The Position column should contain a formatted longitude
        md.Should().Contain("Capricorn");
        md.Should().Contain("Entry");
    }

    #endregion

    #region Cluster Tests

    /// <summary>
    /// Clusters section appears when events converge within 3 days.
    /// </summary>
    [Fact]
    public void ToMarkdown_ClusterSection_Exists_WhenEventsConverge()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 15, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Entry,
                        Date = new DateTime(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc),
                        TransitLongitude = 277.0,
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
                OrbAtStartDegrees = 0.3,
                OrbDirection = "applying",
                TransitHouseNumber = 11,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 16, 8, 47, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("## Clusters");
        md.Should().Contain("Recommended Event Type B dates:");
    }

    /// <summary>
    /// Clusters section is absent when events are spread far apart.
    /// </summary>
    [Fact]
    public void ToMarkdown_ClusterSection_Absent_WhenNoConvergence()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 5, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 25, 8, 0, 0, DateTimeKind.Utc),
                        PassNumber = 2,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().NotContain("## Clusters");
    }

    /// <summary>
    /// T12 (R9). A cluster is a run of in-period events each within three days of the cluster's first
    /// event, and the first event outside that window starts a new cluster. Exact passes on 1, 3, 5, 7
    /// and 9 April, and nothing else, make two clusters of two, 1–3 and 5–7, and the pass of the 9th,
    /// alone, none. Until branch D each event was measured from the one before it, so the five were
    /// one cluster, a chain.
    /// </summary>
    [Fact]
    public void ToMarkdown_Clusters_AreMeasuredFromTheClustersFirstEvent()
    {
        List<TransitLifecycleEvent> passes = [];
        foreach ((int day, int pass) in new[] { (1, 1), (3, 2), (5, 3), (7, 4), (9, 5) })
        {
            passes.Add(new TransitLifecycleEvent
            {
                EventType = TransitLifecycleEventType.Exact,
                Date = new DateTime(2026, 4, day, 12, 0, 0, DateTimeKind.Utc),
                PassNumber = pass,
            });
        }

        TransitResult result = new()
        {
            NatalChart = CreateMinimalChartResult(),
            ReportStartDate = new DateOnly(2026, 4, 1),
            ReportEndDate = new DateOnly(2026, 4, 30),
            MidpointDate = new DateOnly(2026, 4, 15),
            IncludeMinorAspects = false,
            TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = [],
            Ingresses = [],
            ActiveTransits =
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
                    Lifecycle = passes,
                },
            ],
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
        };

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("### 2026-04-01 to 2026-04-03 \u2014 2 events");
        md.Should().Contain("### 2026-04-05 to 2026-04-07 \u2014 2 events");
        md.Should().NotContainAny("to 2026-04-09", "2026-04-09 to", "5 events");
    }

    #endregion

    #region Declination Aspect Tests

    /// <summary>
    /// Declination aspects appear in the Events table.
    /// </summary>
    [Fact]
    public void ToMarkdown_EventsTable_IncludesDeclinationAspect()
    {
        List<TransitDeclinationAspect> declAspects =
        [
            new TransitDeclinationAspect
            {
                TransitPlanetName = "Pluto",
                TransitDeclination = -23.2,
                IsParallel = false,
                NatalPlanetName = "Moon",
                NatalDeclination = 23.1,
                Orb = 0.1,
                Date = new DateOnly(2026, 3, 15),
            },
        ];

        TransitResult result = CreateTransitResult();
        // Need to create a new result with declination aspects
        TransitResult resultWithDecl = new()
        {
            NatalChart = result.NatalChart,
            ReportStartDate = result.ReportStartDate,
            ReportEndDate = result.ReportEndDate,
            MidpointDate = result.MidpointDate,
            IncludeMinorAspects = false,
            TransitPositions = result.TransitPositions,
            Stations = [],
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            TransitDeclinationAspects = declAspects,
            MarsIncluded = false,
        };

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(resultWithDecl);

        md.Should().Contain("Contraparallel");
        md.Should().Contain("Pluto");
        md.Should().Contain("Moon");
    }

    #endregion

    #region Natal Chart Embedding Tests

    /// <summary>
    /// Timeline output includes a Natal Chart section heading.
    /// </summary>
    [Fact]
    public void ToMarkdown_IncludesNatalChartSection()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("## Natal Chart");
    }

    /// <summary>
    /// The embedded natal chart contains Planetary Positions (the first major
    /// section from ChartMarkdownFormatter).
    /// </summary>
    [Fact]
    public void ToMarkdown_NatalChart_ContainsPlanetaryPositions()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        md.Should().Contain("## Planetary Positions");
    }

    /// <summary>
    /// The natal chart's H1 header is stripped (it would be "# Test Person"
    /// from the test chart input name).
    /// </summary>
    [Fact]
    public void ToMarkdown_NatalChart_DoesNotContainH1Header()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // The H1 from ChartMarkdownFormatter would start with "# Test Person"
        // or "# Natal Chart — Test Person". It should be stripped.
        md.Should().NotContain("# Test Person\n",
            "natal chart H1 header should be stripped when embedded");
    }

    /// <summary>
    /// The natal chart footer ("*Generated by AstroCast...") is stripped.
    /// </summary>
    [Fact]
    public void ToMarkdown_NatalChart_DoesNotContainGeneratedByFooter()
    {
        TransitResult result = CreateTransitResult();

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        // Count occurrences of "Generated by" — the timeline footer doesn't use this
        // phrase, so the natal chart's footer should be the only source.
        // It should be stripped.
        md.Should().NotContain("*Generated by AstroCast",
            "natal chart footer should be stripped when embedded");
    }

    /// <summary>
    /// The natal chart section appears between the header and the Transit Windows
    /// or Events section.
    /// </summary>
    [Fact]
    public void ToMarkdown_NatalChart_AppearsBetweenHeaderAndContent()
    {
        List<ActiveTransit> transits =
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
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 15, 14, 23, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                ],
            },
        ];

        TransitResult result = CreateTransitResult(activeTransits: transits);

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(result);

        int generatedIndex = md.IndexOf("**Generated:**");
        int natalIndex = md.IndexOf("## Natal Chart");
        int windowsIndex = md.IndexOf("## Transit Windows");
        int eventsIndex = md.IndexOf("## Events");

        generatedIndex.Should().BeGreaterThan(-1);
        natalIndex.Should().BeGreaterThan(-1);
        eventsIndex.Should().BeGreaterThan(-1);

        natalIndex.Should().BeGreaterThan(generatedIndex,
            "natal chart should appear after header metadata");

        if (windowsIndex > 0)
        {
            natalIndex.Should().BeLessThan(windowsIndex,
                "natal chart should appear before Transit Windows");
        }
        else
        {
            natalIndex.Should().BeLessThan(eventsIndex,
                "natal chart should appear before Events section");
        }
    }

    #endregion

    #region Entry/Exit Orb Tests

    /// <summary>
    /// The Orb cell of an Entry or Exit row is the transit's orb limit, the separation at the boundary crossing
    /// (E2-4, ruling 2026-10-12), whatever the orb at report start. The expected cells come from the transit orb table
    /// (card T-D15, kept; Tier A): Jupiter and Saturn 2° for a conjunction or opposition and 1.5° for the other majors;
    /// Uranus, Neptune, Pluto, Chiron, the North Node and Mars 1.5° and 1°; minors 1° for Jupiter and Saturn and 0.75°
    /// for the rest. Until 1.3.3 the cell was the orb at report start, empty when that orb was 0.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="aspect">The aspect's name, as the transit carries it.</param>
    /// <param name="orbAtStart">The orb at report start, in degrees.</param>
    /// <param name="eventType">"Entry" or "Exit".</param>
    /// <param name="orbCell">The expected Orb cell.</param>
    [Theory]
    [InlineData(TransitBody.Saturn, "Square", 4.75, "Entry", "1°30′")]
    [InlineData(TransitBody.Saturn, "Conjunction", 0.5, "Exit", "2°00′")]
    [InlineData(TransitBody.Jupiter, "Trine", 0.0, "Entry", "1°30′")]
    [InlineData(TransitBody.Pluto, "Opposition", 1.2667, "Exit", "1°30′")]
    [InlineData(TransitBody.Neptune, "Sextile", 0.9667, "Entry", "1°00′")]
    [InlineData(TransitBody.Uranus, "Semisquare", 0.3, "Entry", "0°45′")]
    public void EntryExit_Orb_IsTheOrbLimit(
        TransitBody body, string aspect, double orbAtStart, string eventType, string orbCell)
    {
        // The abbreviations are the transit aspect table's (TransitAspectFinder).
        Dictionary<string, string> abbreviations = new()
        {
            ["Conjunction"] = "cnj",
            ["Sextile"] = "sxt",
            ["Square"] = "sqr",
            ["Trine"] = "tri",
            ["Opposition"] = "opp",
            ["Semisquare"] = "ssq",
        };
        List<ActiveTransit> transits =
        [
            new ActiveTransit
            {
                TransitPlanet = body,
                NatalTarget = "Sun",
                NatalTargetSymbol = "SU",
                AspectName = aspect,
                AspectAbbreviation = abbreviations[aspect],
                TransitLongitudeAtStart = 100.0,
                TransitSignAtStart = "Cancer",
                NatalLongitude = 280.0,
                NatalSign = "Capricorn",
                NatalHouseNumber = 10,
                OrbAtStartDegrees = orbAtStart,
                OrbDirection = "applying",
                TransitHouseNumber = 4,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = eventType == "Entry" ? TransitLifecycleEventType.Entry : TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 10, 8, 14, 0, DateTimeKind.Utc),
                        TransitLongitude = 100.5, // 10°30′00″ Cancer
                    },
                ],
            },
        ];

        List<string> lines = Lines(TransitTimelineMarkdownFormatter.ToMarkdown(CreateTransitResult(activeTransits: transits)));

        lines.Should().Contain(
            $"| 2026-03-10 08:14 UTC | TR {body} {abbreviations[aspect]} NA Sun | {eventType} | 10°30′00″ Cancer | {orbCell} |");
    }

    /// <summary>The export's lines, each without its line terminator.</summary>
    /// <param name="markdown">A Markdown export.</param>
    /// <returns>The export's lines, in order.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_IncludesHeader_WithNameAndPeriod"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_IncludesHeader_WithNameAndPeriod_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_IncludesHeader_WithNameAndPeriod();
    }

    /// <summary>
    /// <see cref="ToMarkdown_ExactPass_AppearsInTimeline"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_ExactPass_AppearsInTimeline_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_ExactPass_AppearsInTimeline();
    }

    /// <summary>
    /// <see cref="ToMarkdown_StationsAndIngresses_AppearInTimeline"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_StationsAndIngresses_AppearInTimeline_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_StationsAndIngresses_AppearInTimeline();
    }

    /// <summary>
    /// <see cref="ToMarkdown_MidpointExactPass_AppearsInTimeline"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_MidpointExactPass_AppearsInTimeline_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_MidpointExactPass_AppearsInTimeline();
    }

    /// <summary>
    /// <see cref="ToMarkdown_TransitToTransitAspect_AppearsInTimeline"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_TransitToTransitAspect_AppearsInTimeline_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_TransitToTransitAspect_AppearsInTimeline();
    }

    #endregion

    #region Branch AsteroidsTiming — the five as targets (R7)

    /// <summary>A synthetic transit with the lifecycle given.</summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="target">The natal target's name.</param>
    /// <param name="symbol">The natal target's symbol.</param>
    /// <param name="natalLongitude">The natal target's longitude.</param>
    /// <param name="natalSign">The natal target's sign.</param>
    /// <param name="lifecycle">The lifecycle events, in order.</param>
    /// <returns>The transit, a square.</returns>
    private static ActiveTransit Square(
        TransitBody body, string target, string symbol, double natalLongitude, string natalSign, params TransitLifecycleEvent[] lifecycle) => new()
    {
        TransitPlanet = body,
        NatalTarget = target,
        NatalTargetSymbol = symbol,
        AspectName = "Square",
        AspectAbbreviation = "sqr",
        TransitLongitudeAtStart = 6.9,
        TransitSignAtStart = "Aries",
        NatalLongitude = natalLongitude,
        NatalSign = natalSign,
        NatalHouseNumber = 1,
        OrbAtStartDegrees = 1.09,
        OrbDirection = "applying",
        TransitHouseNumber = 3,
        Lifecycle = lifecycle,
    };

    /// <summary>An exact pass at the instant, orb 0.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="passNumber">The pass's number.</param>
    /// <returns>The event.</returns>
    private static TransitLifecycleEvent ExactPass(DateTime instant, int passNumber = 1) => new()
    {
        EventType = TransitLifecycleEventType.Exact,
        Date = instant,
        PassNumber = passNumber,
        ExactOrbDegrees = 0.0,
    };

    /// <summary>An entry at the instant, the body at 6.5°.</summary>
    /// <param name="instant">The instant.</param>
    /// <returns>The event.</returns>
    private static TransitLifecycleEvent Entry(DateTime instant) => new()
    {
        EventType = TransitLifecycleEventType.Entry,
        Date = instant,
        TransitLongitude = 6.5,
    };

    /// <summary>
    /// With the natal carrying the five, the Transit Windows and Events sections of the transits to them sit directly
    /// after their twins, with the twins' columns, and the chart's own Events table holds none of their rows.
    /// </summary>
    [Fact]
    public void ToMarkdown_MinorPointSections_SitAfterTheirTwins()
    {
        List<ActiveTransit> transits =
        [
            Square(TransitBody.Saturn, "Sun", "SU", 280.0, "Capricorn", Entry(new DateTime(2026, 3, 1, 8, 14, 0, DateTimeKind.Utc)), ExactPass(new DateTime(2026, 3, 15, 14, 23, 0, DateTimeKind.Utc))),
        ];
        List<ActiveTransit> five =
        [
            Square(TransitBody.Saturn, "Juno", "JUN", 277.99, "Capricorn", Entry(new DateTime(2026, 3, 2, 8, 14, 0, DateTimeKind.Utc)), ExactPass(new DateTime(2026, 3, 20, 14, 23, 0, DateTimeKind.Utc))),
        ];

        List<string> lines = Lines(TransitTimelineMarkdownFormatter.ToMarkdown(
            CreateTransitResult(activeTransits: transits, minorPointTransits: five, includeMinorPoints: true)));

        int windows = lines.IndexOf("## Transit Windows");
        int windowsFive = lines.IndexOf("## Transit Windows: Asteroids and Lilith");
        int events = lines.IndexOf("## Events");
        int eventsFive = lines.IndexOf("## Events: Asteroids and Lilith");
        new[] { windows, windowsFive, events, eventsFive }.Should().OnlyContain(i => i > 0);
        new[] { windows, windowsFive, events, eventsFive }.Should().BeInAscendingOrder();
        lines.Skip(windowsFive).Take(events - windowsFive).Should().Contain("### TR Saturn sqr NA JUN")
            .And.Contain("- **Exact pass 1:** 2026-03-20 14:23 UTC (0\u00b000\u2032)");
        lines.Skip(events).Take(eventsFive - events).Should().NotContain(l => l.Contains("Juno", StringComparison.Ordinal));
        lines.Skip(eventsFive).Should().Contain("| Date | Transit | Event | Position | Orb |")
            .And.Contain("| 2026-03-02 08:14 UTC | TR Saturn sqr NA Juno | Entry | 06\u00b030\u203200\u2033 Aries | 1\u00b030\u2032 |")
            .And.Contain("| 2026-03-20 14:23 UTC | TR Saturn sqr NA Juno | Exact pass 1 | 07\u00b059\u203224\u2033 Capricorn | 0\u00b000\u2032 |");
    }

    /// <summary>
    /// The Clusters read the chart's own events only (scoping decision 5): one chart event with two events of the
    /// five inside three days makes no cluster, where the same three events on the chart's own transits would.
    /// </summary>
    [Fact]
    public void ToMarkdown_Clusters_CountNoEventOfTheFive()
    {
        ActiveTransit chartTransit = Square(TransitBody.Saturn, "Sun", "SU", 280.0, "Capricorn", ExactPass(new DateTime(2026, 3, 15, 14, 23, 0, DateTimeKind.Utc)));
        ActiveTransit fiveTransit = Square(TransitBody.Pluto, "Juno", "JUN", 277.99, "Capricorn", Entry(new DateTime(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc)), ExactPass(new DateTime(2026, 3, 16, 8, 47, 0, DateTimeKind.Utc)));

        string md = TransitTimelineMarkdownFormatter.ToMarkdown(
            CreateTransitResult(activeTransits: [chartTransit], minorPointTransits: [fiveTransit], includeMinorPoints: true));
        string control = TransitTimelineMarkdownFormatter.ToMarkdown(
            CreateTransitResult(activeTransits: [chartTransit, fiveTransit]));

        md.Should().NotContain("## Clusters");
        md.Should().Contain("## Events: Asteroids and Lilith");
        md.Should().Contain("| 2026-03-16 08:47 UTC | TR Pluto sqr NA Juno | Exact pass 1 |");
        control.Should().Contain("## Clusters", "the same events on the chart's own transits cluster");
    }

    #endregion
}
