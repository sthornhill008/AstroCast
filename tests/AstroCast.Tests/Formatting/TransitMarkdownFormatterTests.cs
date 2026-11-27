using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="TransitMarkdownFormatter"/> verifying document structure,
/// section headings, content inclusion, and edge cases.
/// Uses synthetic test data (no ephemeris dependency) for deterministic assertions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitMarkdownFormatterTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates a minimal ChartResult with enough data for the formatter to extract
    /// header information and produce valid natal chart content.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0).</param>
    private static ChartResult CreateMinimalChartResult(bool includeMinorPoints = false)
    {
        // Minimal positions for the natal chart
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
            MinorPoints = includeMinorPoints ? CreateMinorPointPositions() : [],
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

    /// <summary>The five minor points, in R1's order, at J2000 NYC's longitudes to the hundredth (branch AsteroidsTiming, 1.6.0).</summary>
    private static List<PlanetPosition> CreateMinorPointPositions() =>
    [
        CreatePosition(CelestialBodyId.Ceres, "Ceres", 184.45, 0.22, 9),
        CreatePosition(CelestialBodyId.Pallas, "Pallas", 134.04, -0.2, 7),
        CreatePosition(CelestialBodyId.Juno, "Juno", 277.99, 0.37, 1),
        CreatePosition(CelestialBodyId.Vesta, "Vesta", 245.97, 0.4, 11),
        CreatePosition(CelestialBodyId.MeanApogee, "Lilith", 263.46, 0.11, 12),
    ];

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
    /// Creates a complete test TransitResult with known synthetic data.
    /// </summary>
    private static TransitResult CreateTestTransitResult(
        bool includeMinorAspects = false,
        bool includeIngresses = true,
        bool includeActiveTransits = true,
        bool includeMidpointActivations = true)
    {
        ChartResult natalChart = CreateMinimalChartResult();

        // Transit positions
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

        // Stations
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

        // Ingresses
        List<TransitIngress> ingresses = includeIngresses
            ?
            [
                new TransitIngress
                {
                    Planet = TransitBody.Saturn,
                    FromSign = "Pisces",
                    ToSign = "Aries",
                    Date = new DateOnly(2026, 3, 14),
                },
            ]
            : [];

        // Active transits
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
                            Date = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc),
                            BeforeReportPeriod = true,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exact,
                            Date = new DateTime(2026, 3, 12, 14, 23, 0, DateTimeKind.Utc),
                            PassNumber = 1,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exit,
                            Date = new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc),
                            BeyondReportPeriod = true,
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
                            Date = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc),
                        },
                    ],
                },
            ]
            : [];

        // House overlays
        List<TransitHouseOverlay> houseOverlays =
        [
            new TransitHouseOverlay { Planet = TransitBody.Saturn, HouseNumber = 10, IsIntercepted = false },
            new TransitHouseOverlay { Planet = TransitBody.Jupiter, HouseNumber = 2, IsIntercepted = false },
        ];

        // Midpoint activations
        List<TransitMidpointActivation> midpointActivations = includeMidpointActivations
            ?
            [
                new TransitMidpointActivation
                {
                    TransitPlanet = TransitBody.Saturn,
                    MidpointBody1 = "Sun",
                    MidpointBody2 = "Moon",
                    TransitDial45Position = 22.25,
                    NatalMidpointDial45Position = 22.1,
                    OrbAtStartDegrees = 0.15,
                    IsPriority = true,
                    PriorityReason = "Sun/Moon",
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
                            Date = new DateTime(2026, 3, 15, 4, 23, 0, DateTimeKind.Utc),
                            PassNumber = 1,
                        },
                        new TransitLifecycleEvent
                        {
                            EventType = TransitLifecycleEventType.Exit,
                            Date = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc),
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
            IncludeMinorAspects = includeMinorAspects,
            TransitPositions = positions.AsReadOnly(),
            Stations = stations.AsReadOnly(),
            Ingresses = ingresses.AsReadOnly(),
            ActiveTransits = activeTransits.AsReadOnly(),
            HouseOverlays = houseOverlays.AsReadOnly(),
            MidpointActivations = midpointActivations.AsReadOnly(),
            MarsIncluded = false,
        };
    }

    /// <summary>
    /// Creates a TransitResult with an active transit targeting ASC
    /// (for birth time caveat testing).
    /// </summary>
    private static TransitResult CreateTransitResultWithAscTarget()
    {
        TransitResult baseResult = CreateTestTransitResult(
            includeActiveTransits: false, includeMidpointActivations: false);

        List<ActiveTransit> transits =
        [
            new ActiveTransit
            {
                TransitPlanet = TransitBody.Saturn,
                NatalTarget = "ASC",
                NatalTargetSymbol = "ASC",
                AspectName = "Conjunction",
                AspectAbbreviation = "cnj",
                TransitLongitudeAtStart = 1.0,
                TransitSignAtStart = "Aries",
                NatalLongitude = 0.0,
                NatalSign = "Aries",
                NatalHouseNumber = 1,
                OrbAtStartDegrees = 1.0,
                OrbDirection = "applying",
                TransitHouseNumber = 1,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Entry,
                        Date = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc),
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        return new TransitResult
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = baseResult.IncludeMinorAspects,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = baseResult.Ingresses,
            ActiveTransits = transits.AsReadOnly(),
            HouseOverlays = baseResult.HouseOverlays,
            MidpointActivations = [],
            MarsIncluded = false,
        };
    }

    #endregion

    #region Header Tests

    [Fact]
    public void ToMarkdown_Should_ContainHeader()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("# Transit Report");
        markdown.Should().Contain("**Name:**");
        markdown.Should().Contain("**Report Period:**");
        markdown.Should().Contain("**Aspects:**");
    }

    [Fact]
    public void ToMarkdown_MinorAspectsEnabled_Should_ShowInHeader()
    {
        TransitResult result = CreateTestTransitResult(includeMinorAspects: true);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Aspects:** Major + Minor");
    }

    [Fact]
    public void ToMarkdown_MajorAspectsOnly_Should_ShowInHeader()
    {
        TransitResult result = CreateTestTransitResult(includeMinorAspects: false);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Aspects:** Major");
        markdown.Should().NotContain("**Aspects:** Major + Minor");
    }

    #endregion

    #region Natal Chart Section Tests

    [Fact]
    public void ToMarkdown_Should_ContainNatalChartSection()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Natal Chart");
    }

    #endregion

    #region Transiting Planets Section Tests

    [Fact]
    public void ToMarkdown_Should_ContainTransitingPlanetsTable()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Transiting Planets");
        markdown.Should().Contain("| Planet | Sign | Degree |");
    }

    [Fact]
    public void ToMarkdown_Should_ContainStationsSection()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Stations During Report Period");
        markdown.Should().Contain("Retrograde");
    }

    [Fact]
    public void ToMarkdown_Should_ContainIngressesSection()
    {
        TransitResult result = CreateTestTransitResult(includeIngresses: true);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Ingresses During Report Period");
        markdown.Should().Contain("Pisces");
        markdown.Should().Contain("Aries");
    }

    [Fact]
    public void ToMarkdown_Should_OmitIngressesSection_WhenNone()
    {
        TransitResult result = CreateTestTransitResult(includeIngresses: false);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("Ingresses");
    }

    #endregion

    #region Active Transits Section Tests

    [Fact]
    public void ToMarkdown_Should_ContainActiveTransitsSection()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Active Transits");
        markdown.Should().Contain("### TR");
    }

    [Fact]
    public void ToMarkdown_ActiveTransit_Should_ContainLifecycleEvents()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Entry:**");
        markdown.Should().Contain("**Exact pass 1:**");
        markdown.Should().Contain("**Exit:**");
    }

    [Fact]
    public void ToMarkdown_ActiveTransit_Should_ShowBoundaryNotes()
    {
        // The Saturn cnj Sun transit has entry before report period
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("(before report period)");
        markdown.Should().Contain("(beyond report period)");
    }

    [Fact]
    public void ToMarkdown_NoActiveTransits_Should_ShowNoneMessage()
    {
        TransitResult result = CreateTestTransitResult(includeActiveTransits: false);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain(
            "No major outer planet transits are active during this period.");
    }

    #endregion

    #region House Overlays Section Tests

    [Fact]
    public void ToMarkdown_Should_ContainHouseOverlaysTable()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Transit House Overlays");
        markdown.Should().Contain("| Natal House |");
    }

    #endregion

    #region Midpoint Activations Section Tests

    [Fact]
    public void ToMarkdown_Should_ContainMidpointActivationsSection()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Transit Midpoint Activations (45\u00b0 Dial)");
        markdown.Should().Contain("Sun/Moon");
        markdown.Should().Contain("(priority)");
    }

    #endregion

    #region Interpretation Context Tests

    [Fact]
    public void ToMarkdown_WithInterpretationContext_Should_IncludeInHeader()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(
            result,
            birthTimeSource: "Birth certificate",
            interpretationContext: "Focus on career themes. Client is considering a job change.");

        markdown.Should().Contain("**Interpretation Context:** Focus on career themes. Client is considering a job change.");
    }

    [Fact]
    public void ToMarkdown_WithoutInterpretationContext_Should_OmitLine()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("**Interpretation Context:**");
    }

    #endregion

    #region Birth Time Source Tests

    [Fact]
    public void ToMarkdown_UnknownBirthTime_Should_AddCaveats()
    {
        TransitResult result = CreateTransitResultWithAscTarget();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result, birthTimeSource: "Unknown");

        markdown.Should().Contain("approximate due to uncertain birth time");
    }

    #endregion

    #region Progression / Solar Arc Test Data Helpers

    /// <summary>
    /// Creates a synthetic ProgressionResult with known values for formatter testing.
    /// </summary>
    private static ProgressionResult CreateTestProgressionResult(
        bool includeAspects = true,
        bool includeSignChanges = true,
        bool includeStations = true)
    {
        List<ProgressedPosition> positions =
        [
            new ProgressedPosition
            {
                Name = "Sun", NatalLongitude = 280.0, NatalFormattedPosition = "10\u00b000' Capricorn",
                ProgressedLongitude = 306.5, ProgressedSign = "Aquarius", ProgressedDegrees = 6,
                ProgressedMinutes = 30, ProgressedFormattedPosition = "06\u00b030' Aquarius",
                NatalHouse = 10, MovementFromNatal = 26.5, IsRetrograde = false, DirectionCode = "D",
            },
            new ProgressedPosition
            {
                Name = "Moon", NatalLongitude = 112.0, NatalFormattedPosition = "22\u00b000' Cancer",
                ProgressedLongitude = 45.3, ProgressedSign = "Taurus", ProgressedDegrees = 15,
                ProgressedMinutes = 18, ProgressedFormattedPosition = "15\u00b018' Taurus",
                NatalHouse = 2, MovementFromNatal = 293.3, IsRetrograde = false, DirectionCode = "D",
            },
            new ProgressedPosition
            {
                Name = "Mercury", NatalLongitude = 265.0, NatalFormattedPosition = "25\u00b000' Sagittarius",
                ProgressedLongitude = 288.7, ProgressedSign = "Capricorn", ProgressedDegrees = 18,
                ProgressedMinutes = 42, ProgressedFormattedPosition = "18\u00b042' Capricorn",
                NatalHouse = 9, MovementFromNatal = 23.7, IsRetrograde = false, DirectionCode = "D",
            },
            new ProgressedPosition
            {
                Name = "Venus", NatalLongitude = 300.0, NatalFormattedPosition = "00\u00b000' Aquarius",
                ProgressedLongitude = 335.2, ProgressedSign = "Pisces", ProgressedDegrees = 5,
                ProgressedMinutes = 12, ProgressedFormattedPosition = "05\u00b012' Pisces",
                NatalHouse = 11, MovementFromNatal = 35.2, IsRetrograde = false, DirectionCode = "D",
            },
            new ProgressedPosition
            {
                Name = "Mars", NatalLongitude = 330.0, NatalFormattedPosition = "00\u00b000' Pisces",
                ProgressedLongitude = 343.8, ProgressedSign = "Pisces", ProgressedDegrees = 13,
                ProgressedMinutes = 48, ProgressedFormattedPosition = "13\u00b048' Pisces",
                NatalHouse = 11, MovementFromNatal = 13.8, IsRetrograde = false, DirectionCode = "D",
            },
            new ProgressedPosition
            {
                Name = "ASC", NatalLongitude = 0.0, NatalFormattedPosition = "00\u00b000' Aries",
                ProgressedLongitude = 28.5, ProgressedSign = "Aries", ProgressedDegrees = 28,
                ProgressedMinutes = 30, ProgressedFormattedPosition = "28\u00b030' Aries",
                NatalHouse = null, MovementFromNatal = 28.5, IsRetrograde = null, DirectionCode = null,
            },
            new ProgressedPosition
            {
                Name = "MC", NatalLongitude = 270.0, NatalFormattedPosition = "00\u00b000' Capricorn",
                ProgressedLongitude = 296.1, ProgressedSign = "Capricorn", ProgressedDegrees = 26,
                ProgressedMinutes = 6, ProgressedFormattedPosition = "26\u00b006' Capricorn",
                NatalHouse = null, MovementFromNatal = 26.1, IsRetrograde = null, DirectionCode = null,
            },
        ];

        ProgressedMoonInfo moonInfo = new()
        {
            Longitude = 45.3,
            Sign = "Taurus",
            Degrees = 15,
            Minutes = 18,
            NatalHouse = 2,
            PhaseName = "Gibbous",
            PhaseAngle = 158.8,
            NextSignChangeDate = new DateOnly(2026, 8, 15),
            NextSign = "Gemini",
        };

        List<ProgressedAspect> aspects = includeAspects
            ?
            [
                new ProgressedAspect
                {
                    ProgressedBody = "P Moon", AspectName = "Conjunction", AspectAbbreviation = "cnj",
                    NatalTarget = "Venus", OrbDegrees = 0.47, Direction = "applying",
                },
                new ProgressedAspect
                {
                    ProgressedBody = "P Sun", AspectName = "Square", AspectAbbreviation = "sqr",
                    NatalTarget = "Saturn", OrbDegrees = 0.72, Direction = "separating",
                },
            ]
            : [];

        List<ProgressedSignChange> signChanges = includeSignChanges
            ?
            [
                new ProgressedSignChange
                {
                    BodyName = "Mercury", FromSign = "Sagittarius", ToSign = "Capricorn",
                    Date = new DateOnly(2026, 5, 14),
                },
            ]
            : [];

        List<ProgressedStation> stations = includeStations
            ?
            [
                new ProgressedStation
                {
                    BodyName = "Mercury", StationType = "Direct",
                    Date = new DateOnly(2026, 9, 3),
                },
            ]
            : [];

        return new ProgressionResult
        {
            ProgressedDate = new DateTime(2000, 1, 27, 12, 0, 0, DateTimeKind.Utc),
            ProgressedJulianDay = 2451571.0,
            SolarArcValue = 26.5,
            Positions = positions.AsReadOnly(),
            MoonInfo = moonInfo,
            SignChanges = signChanges.AsReadOnly(),
            Stations = stations.AsReadOnly(),
            Aspects = aspects.AsReadOnly(),
        };
    }

    /// <summary>
    /// Creates a synthetic SolarArcResult with known values for formatter testing.
    /// </summary>
    private static SolarArcResult CreateTestSolarArcResult(bool includeAspects = true)
    {
        List<SolarArcPosition> positions =
        [
            new SolarArcPosition
            {
                Name = "Sun", NatalLongitude = 280.0, NatalFormattedPosition = "10\u00b000' Capricorn",
                DirectedLongitude = 306.5, DirectedSign = "Aquarius", DirectedDegrees = 6,
                DirectedMinutes = 30, DirectedFormattedPosition = "06\u00b030' Aquarius",
            },
            new SolarArcPosition
            {
                Name = "Moon", NatalLongitude = 112.0, NatalFormattedPosition = "22\u00b000' Cancer",
                DirectedLongitude = 138.5, DirectedSign = "Leo", DirectedDegrees = 18,
                DirectedMinutes = 30, DirectedFormattedPosition = "18\u00b030' Leo",
            },
        ];

        List<SolarArcAspect> aspects = includeAspects
            ?
            [
                new SolarArcAspect
                {
                    DirectedBody = "SA Mars", AspectName = "Conjunction", AspectAbbreviation = "cnj",
                    NatalTarget = "ASC", OrbDegrees = 0.12, Direction = "applying",
                    InvolvesAngle = true, IsHardAspect = true,
                },
                new SolarArcAspect
                {
                    DirectedBody = "SA Venus", AspectName = "Square", AspectAbbreviation = "sqr",
                    NatalTarget = "Moon", OrbDegrees = 0.33, Direction = "separating",
                    InvolvesAngle = false, IsHardAspect = true,
                },
                new SolarArcAspect
                {
                    DirectedBody = "SA Jupiter", AspectName = "Trine", AspectAbbreviation = "tri",
                    NatalTarget = "Sun", OrbDegrees = 0.55, Direction = "applying",
                    InvolvesAngle = false, IsHardAspect = false,
                },
            ]
            : [];

        return new SolarArcResult
        {
            ArcValue = 26.5,
            ArcFormatted = "26\u00b030'",
            Positions = positions.AsReadOnly(),
            Aspects = aspects.AsReadOnly(),
        };
    }

    /// <summary>
    /// Synthetic dated solar arc hits, deliberately out of time order: SA Sun on the Pluto/ASC midpoint
    /// (2026-03-26), then SA Mercury square the MC (2026-03-06).
    /// </summary>
    private static List<SolarArcHit> CreateTestSolarArcHits() =>
    [
        new SolarArcHit
        {
            DirectedPoint = "Sun", AspectAngle = 45.0, AspectName = "Semisquare", AspectAbbreviation = "ssq",
            Target = "Pluto/ASC", MidpointBody1 = "Pluto", MidpointBody2 = "ASC", Arc = 26.6,
            JulianDayUt = 2461126.29, InstantUtc = new DateTime(2026, 3, 26, 18, 59, 53, DateTimeKind.Utc),
        },
        new SolarArcHit
        {
            DirectedPoint = "Mercury", AspectAngle = 90.0, AspectName = "Square", AspectAbbreviation = "sqr",
            Target = "MC", Arc = 26.55,
            JulianDayUt = 2461105.91, InstantUtc = new DateTime(2026, 3, 6, 9, 47, 12, DateTimeKind.Utc),
        },
    ];

    /// <summary>Synthetic dated progressed hits: P Moon conjunct the natal Moon (2026-03-17).</summary>
    private static List<ProgressedHit> CreateTestProgressedHits() =>
    [
        new ProgressedHit
        {
            ProgressedPoint = "Moon", AspectAngle = 0.0, AspectName = "Conjunction", AspectAbbreviation = "cnj",
            NatalPoint = "Moon", JulianDayUt = 2461117.38, InstantUtc = new DateTime(2026, 3, 17, 21, 0, 52, DateTimeKind.Utc),
        },
    ];

    /// <summary>
    /// Creates a TransitResult with progression and solar arc data included, the dated hits with them
    /// unless asked otherwise.
    /// </summary>
    private static TransitResult CreateTransitResultWithProgressions(
        bool includeProgAspects = true,
        bool includeSignChanges = true,
        bool includeStations = true,
        bool includeSolarArcAspects = true,
        bool includeHits = true,
        bool includeTransits = true)
    {
        TransitResult baseResult = CreateTestTransitResult();

        return new TransitResult
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = baseResult.IncludeMinorAspects,
            IncludesTransits = includeTransits,
            TransitPositions = includeTransits ? baseResult.TransitPositions : new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = includeTransits ? baseResult.Stations : [],
            Ingresses = includeTransits ? baseResult.Ingresses : [],
            ActiveTransits = includeTransits ? baseResult.ActiveTransits : [],
            HouseOverlays = includeTransits ? baseResult.HouseOverlays : [],
            MidpointActivations = includeTransits ? baseResult.MidpointActivations : [],
            MarsIncluded = baseResult.MarsIncluded,
            Progressions = CreateTestProgressionResult(includeProgAspects, includeSignChanges, includeStations),
            SolarArcs = CreateTestSolarArcResult(includeSolarArcAspects),
            SolarArcHits = includeHits ? CreateTestSolarArcHits() : [],
            ProgressedHits = includeHits ? CreateTestProgressedHits() : [],
        };
    }

    #endregion

    #region Section 7: Progressions Tests

    [Fact]
    public void ToMarkdown_WithProgressions_Should_ContainSection7Header()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        List<string> lines = Lines(markdown);
        lines.Should().Contain("## Secondary Progressions");
        lines.Should().Contain("**Method:** Day-for-a-year (secondary)");
        lines.Should().Contain("**Techniques:** Transits, Solar Arcs, Secondary Progressions");
        lines.Should().Contain("### Progressed Positions (at report start, 2026-03-01)");
        lines.Should().NotContain(l => l.StartsWith("## Secondary Progressions (at", StringComparison.Ordinal));
    }

    /// <summary>
    /// The header's Techniques line names the techniques the result holds, in the order Transits, Solar
    /// Arcs, Secondary Progressions (branch Modes, default 3).
    /// </summary>
    [Fact]
    public void ToMarkdown_TechniquesLine_NamesTheOnesRun()
    {
        Lines(TransitMarkdownFormatter.ToMarkdown(CreateTestTransitResult()))
            .Should().Contain("**Techniques:** Transits");
        Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions(includeTransits: false)))
            .Should().Contain("**Techniques:** Solar Arcs, Secondary Progressions");

        TransitResult baseResult = CreateTransitResultWithProgressions(includeTransits: false);
        TransitResult solarArcsOnly = new()
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = false,
            IncludesTransits = false,
            TransitPositions = baseResult.TransitPositions,
            Stations = [],
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
            SolarArcs = baseResult.SolarArcs,
            SolarArcHits = baseResult.SolarArcHits,
        };
        Lines(TransitMarkdownFormatter.ToMarkdown(solarArcsOnly)).Should().Contain("**Techniques:** Solar Arcs");
    }

    /// <summary>
    /// In a report without transits the header's Aspects line names each technique's set, on the line
    /// the transits' set takes, directly before the Techniques line (Steve, 2026-10-17, on ModesPanel's
    /// Finding 1): the solar arcs the five majors with the semisquare and sesquiquadrate, the
    /// progressions the five majors. A report with transits keeps "Major" or "Major + Minor".
    /// </summary>
    /// <param name="solarArcs">Whether the result holds the solar arcs.</param>
    /// <param name="progressions">Whether the result holds the secondary progressions.</param>
    /// <param name="primaryDirections">Whether the result holds the primary directions (branch PrimaryExport, 1.5.0).</param>
    /// <param name="expected">The Aspects line.</param>
    [Theory]
    [InlineData(true, false, false, "**Aspects:** Solar arcs: majors, semisquare, sesquiquadrate")]
    [InlineData(false, true, false, "**Aspects:** Progressions: majors")]
    [InlineData(true, true, false, "**Aspects:** Solar arcs: majors, semisquare, sesquiquadrate; progressions: majors")]
    [InlineData(false, false, true, "**Aspects:** Primary directions: majors")]
    [InlineData(true, false, true, "**Aspects:** Solar arcs: majors, semisquare, sesquiquadrate; primary directions: majors")]
    [InlineData(false, true, true, "**Aspects:** Progressions: majors; primary directions: majors")]
    [InlineData(true, true, true, "**Aspects:** Solar arcs: majors, semisquare, sesquiquadrate; progressions: majors; primary directions: majors")]
    public void ToMarkdown_AspectsLine_NamesEachTechniquesSet(bool solarArcs, bool progressions, bool primaryDirections, string expected)
    {
        TransitResult result = WithTechniques(solarArcs, progressions, primaryDirections);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        lines.Should().ContainSingle(l => l.StartsWith("**Aspects:**", StringComparison.Ordinal))
            .Which.Should().Be(expected);
        lines.IndexOf(expected).Should().Be(lines.FindIndex(l => l.StartsWith("**Techniques:**", StringComparison.Ordinal)) - 1);
    }

    /// <summary>The Techniques line appends Primary Directions after the three (branch PrimaryExport, 1.5.0).</summary>
    [Fact]
    public void ToMarkdown_TechniquesLine_AppendsPrimaryDirections()
    {
        TransitResult result = WithTechniques(solarArcs: true, progressions: true, primaryDirections: true, transits: true);

        Lines(TransitMarkdownFormatter.ToMarkdown(result))
            .Should().Contain("**Techniques:** Transits, Solar Arcs, Secondary Progressions, Primary Directions");
    }

    /// <summary>With the transits the Aspects line is the transits' set, whatever the primary directions.</summary>
    [Fact]
    public void ToMarkdown_WithTransits_AspectsLineUnchangedByDirections()
    {
        TransitResult result = WithTechniques(solarArcs: false, progressions: false, primaryDirections: true, transits: true);

        Lines(TransitMarkdownFormatter.ToMarkdown(result)).Should().Contain("**Aspects:** Major");
    }

    /// <summary>The Primary Directions section prints after the Solar Arc Directions section, with its sub-heading and the period's rows.</summary>
    [Fact]
    public void ToMarkdown_PrimaryDirections_SectionAfterSolarArcs()
    {
        TransitResult result = WithTechniques(solarArcs: true, progressions: false, primaryDirections: true);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        int solarArcs = lines.IndexOf("## Solar Arc Directions");
        int directions = lines.IndexOf("## Primary Directions");
        solarArcs.Should().BePositive();
        directions.Should().BeGreaterThan(solarArcs);
        lines.Should().Contain("### Primary Direction Hits in the Report Period");
        lines.Should().Contain("| 2026-03-26 | 26.23 | D | Midheaven | cnj | Moon | 26\u00b000\u2032 | -3.0 |");
        lines.Should().Contain("# Solar Arc and Primary Directions Report");
    }

    /// <summary>Without a hit in the period the section prints its none sentence.</summary>
    [Fact]
    public void ToMarkdown_PrimaryDirections_NoneSentence()
    {
        TransitResult result = WithTechniques(solarArcs: false, progressions: false, primaryDirections: true, periodHits: false);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        lines.Should().Contain("No primary direction becomes exact in the report period.");
        lines.Should().Contain("# Primary Directions Report");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_Should_ContainProgressedPositionsTable()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Progressed Positions");
        markdown.Should().Contain("| Body | Natal Position | Progressed Position | House | Movement | Dir |");
        markdown.Should().Contain("| Sun |");
        markdown.Should().Contain("| Moon |");
        markdown.Should().Contain("| ASC |");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_Should_ContainProgressedMoonDetail()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Progressed Moon");
        markdown.Should().Contain("Gibbous");
        markdown.Should().Contain("2nd house");
        markdown.Should().Contain("Gemini");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_Should_ContainProgressedAspectsTable()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Progressed-to-Natal Aspects");
        markdown.Should().Contain("P Moon");
        markdown.Should().Contain("cnj");
        markdown.Should().Contain("Venus");
        markdown.Should().Contain("P Sun");
        markdown.Should().Contain("sqr");
        markdown.Should().Contain("Saturn");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_NoAspects_Should_ShowNoneMessage()
    {
        TransitResult result = CreateTransitResultWithProgressions(includeProgAspects: false);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("No major progressed-to-natal aspects within 1\u00b0 orb.");
    }

    /// <summary>
    /// With no progressed-to-natal aspect, the heading and the sentence say that only the major aspects were searched
    /// (E2-3): the search covers conjunction, sextile, square, trine and opposition alone.
    /// </summary>
    [Fact]
    public void ToMarkdown_NoProgressedAspects_SaysMajor()
    {
        TransitResult result = CreateTransitResultWithProgressions(includeProgAspects: false);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        lines.Should().Contain("### Progressed-to-Natal Aspects (major, 1\u00b0 orb, at report start)");
        lines.Should().Contain("No major progressed-to-natal aspects within 1\u00b0 orb.");
    }

    /// <summary>
    /// With no solar-arc-to-natal aspect, the heading and the sentence say what was searched (E2-3): the
    /// majors and the 45\u00b0 series, within 1\u00b0 at the report's start (branch Modes, scoping C3b).
    /// </summary>
    [Fact]
    public void ToMarkdown_NoSolarArcAspects_NamesTheSeries()
    {
        TransitResult result = CreateTransitResultWithProgressions(includeSolarArcAspects: false);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        lines.Should().Contain("### Solar-Arc-to-Natal Aspects (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)");
        lines.Should().Contain("No solar-arc-to-natal aspects within 1\u00b0 orb at the report start.");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_WithSignChanges_Should_ContainTable()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Progressed Sign Changes (within 6 months)");
        markdown.Should().Contain("Mercury");
        markdown.Should().Contain("Sagittarius");
        markdown.Should().Contain("Capricorn");
        markdown.Should().Contain("2026-05-14");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_NoSignChanges_Should_OmitSection()
    {
        TransitResult result = CreateTransitResultWithProgressions(includeSignChanges: false);

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("### Progressed Sign Changes");
    }

    [Fact]
    public void ToMarkdown_WithProgressions_WithStations_Should_ContainTable()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Progressed Stations (within 1 year)");
        markdown.Should().Contain("Mercury");
        markdown.Should().Contain("Direct");
        markdown.Should().Contain("2026-09-03");
    }

    #endregion

    #region Section 8: Solar Arcs Tests

    [Fact]
    public void ToMarkdown_WithSolarArcs_Should_ContainSection8Header()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        List<string> lines = Lines(markdown);
        lines.Should().Contain("## Solar Arc Directions");
        lines.Should().Contain("**Solar Arc:** 26\u00b030' (at report start, 2026-03-01)");
        lines.Should().Contain("### Directed Positions (at report start, 2026-03-01)");
        lines.Should().NotContain(l => l.StartsWith("## Solar Arc Directions (at", StringComparison.Ordinal));
    }

    /// <summary>
    /// The solar arc hits print after the aspects at the start as a table headed Date (UTC), one row per
    /// hit in date order whatever order the result holds them, each row the directed point, the aspect and
    /// the natal target in the export's notation (branch Modes, default 2).
    /// </summary>
    [Fact]
    public void ToMarkdown_SolarArcHits_AreDatedAndSorted()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions()));

        int heading = lines.IndexOf("### Solar Arc Hits in the Report Period");
        heading.Should().BePositive();
        lines[heading + 2].Should().Be("| Date (UTC) | Hit | Aspect |");
        lines[heading + 3].Should().Be("|------------|-----|--------|");
        lines[heading + 4].Should().Be("| 2026-03-06 | SA Mercury sqr NA MC | Square |");
        lines[heading + 5].Should().Be("| 2026-03-26 | SA Sun = NA Pluto/ASC | Semisquare (45\u00b0 dial) |");
        heading.Should().BeGreaterThan(lines.IndexOf("### Solar-Arc-to-Natal Aspects (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)"));
    }

    /// <summary>A hit to a natal midpoint prints as SA point = NA body/body, with the dial harmonic.</summary>
    [Fact]
    public void ToMarkdown_SolarArcMidpointHits_UseTheSlashNotation()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions()));

        lines.Should().ContainSingle(l => l.Contains("= NA Pluto/ASC", StringComparison.Ordinal))
            .Which.Should().Be("| 2026-03-26 | SA Sun = NA Pluto/ASC | Semisquare (45\u00b0 dial) |");
    }

    /// <summary>The progressed hits print as a dated table after the progressed aspects at the start.</summary>
    [Fact]
    public void ToMarkdown_ProgressedHits_AreDated()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions()));

        int heading = lines.IndexOf("### Progressed Hits in the Report Period");
        heading.Should().BePositive();
        lines[heading + 2].Should().Be("| Date (UTC) | Hit | Aspect |");
        lines[heading + 3].Should().Be("|------------|-----|--------|");
        lines[heading + 4].Should().Be("| 2026-03-17 | P Moon cnj NA Moon | Conjunction |");
        heading.Should().BeGreaterThan(lines.IndexOf("### Progressed-to-Natal Aspects (major, 1\u00b0 orb, at report start)"));
        heading.Should().BeLessThan(lines.IndexOf("## Solar Arc Directions"));
    }

    /// <summary>With no hit in the period each hits sub-section says so in a sentence.</summary>
    [Fact]
    public void ToMarkdown_NoHits_SaysNone()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions(includeHits: false)));

        lines.Should().Contain("### Solar Arc Hits in the Report Period");
        lines.Should().Contain("No solar arc hit becomes exact in the report period.");
        lines.Should().Contain("### Progressed Hits in the Report Period");
        lines.Should().Contain("No progressed hit becomes exact in the report period.");
        lines.Should().NotContain("| Date (UTC) | Hit | Aspect |");
    }

    [Fact]
    public void ToMarkdown_WithSolarArcs_Should_ContainDirectedPositionsTable()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Directed Positions");
        markdown.Should().Contain("| Body | Natal Position | Directed Position |");
    }

    [Fact]
    public void ToMarkdown_WithSolarArcs_Should_ContainAspectsWithFlags()
    {
        TransitResult result = CreateTransitResultWithProgressions();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("### Solar-Arc-to-Natal Aspects");
        markdown.Should().Contain("SA Mars");
        markdown.Should().Contain("cnj");
        markdown.Should().Contain("ASC");
        markdown.Should().Contain("angle");
        markdown.Should().Contain("hard");
    }

    /// <summary>
    /// A result without the transit scan prints no transit section and takes its title from the
    /// techniques it holds (branch Modes, 1.4.0 item 2; scoping C1f): solar arcs and progressions without
    /// transits is the Solar Arc and Progressions Report, its natal chart and two technique sections intact.
    /// </summary>
    [Fact]
    public void SolarArcOnly_OmitsTheTransitSections()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions(includeTransits: false)));

        lines[0].Should().Be("# Solar Arc and Progressions Report");
        lines.Should().Contain("**Techniques:** Solar Arcs, Secondary Progressions");
        lines.Should().Contain("## Natal Chart");
        lines.Should().Contain("## Secondary Progressions");
        lines.Should().Contain("## Solar Arc Directions");
        lines.Should().NotContain("## Transiting Planets");
        lines.Should().NotContain("## Active Transits");
        lines.Should().NotContain(l => l.StartsWith("## Transit House Overlays", StringComparison.Ordinal));
        lines.Should().NotContain("## Transit Midpoint Activations (45° Dial)");
        lines.Should().NotContain("## Transit-to-Natal Declination Aspects (±1° orb)");
        lines.Should().NotContain("## Transit-to-Transit Aspects");

        Lines(TransitMarkdownFormatter.ToMarkdown(CreateTestTransitResult()))[0].Should().Be("# Transit Report");
    }

    [Fact]
    public void ToMarkdown_WithoutProgressions_Should_OmitSections7And8()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("## Secondary Progressions");
        markdown.Should().NotContain("## Solar Arc Directions");
        Lines(markdown).Should().Contain("**Techniques:** Transits");
    }

    #endregion

    #region Residual Orb Tests

    /// <summary>
    /// Exact pass lifecycle events with ExactOrbDegrees display the residual orb in parenthetical
    /// format (orb: D°MM′SS″), the seconds kept and truncated, the minute marks the prime and the
    /// double prime (branch O-D3, 1.3.4): 0.00083° is 2.988″, printed 0°00′02″. Until 1.3.4 the
    /// note printed an apostrophe and a quotation mark.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPassWithResidualOrb_DisplaysOrbNote()
    {
        TransitResult baseResult = CreateTestTransitResult(
            includeActiveTransits: false, includeMidpointActivations: false);

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
                        ExactOrbDegrees = 0.00083, // 2.988″, printed truncated as 0°00′02″
                    },
                ],
            },
        ];

        TransitResult result = new()
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = false,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = [],
            ActiveTransits = transits,
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
        };

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("- **Exact pass 1:** 2026-03-12 14:23:00 UTC (orb: 0\u00b000\u203202\u2033)");
    }

    /// <summary>
    /// Exact pass lifecycle events with null ExactOrbDegrees do not show an orb note.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPassWithNullOrb_OmitsOrbNote()
    {
        // The default CreateTestTransitResult has lifecycle events without ExactOrbDegrees
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // The exact pass line should exist but not contain an orb note
        markdown.Should().Contain("Exact pass 1");
        markdown.Should().NotContain("(orb:");
    }

    #endregion

    #region Transit-to-Transit Section Tests

    /// <summary>
    /// Transit-to-transit aspects section is rendered when aspects exist.
    /// </summary>
    [Fact]
    public void ToMarkdown_WithTransitToTransitAspects_RendersSection()
    {
        TransitResult baseResult = CreateTestTransitResult(
            includeActiveTransits: false, includeMidpointActivations: false);

        TransitResult result = new()
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = false,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
            TransitToTransitAspects =
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
            ],
        };

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("## Transit-to-Transit Aspects");
        markdown.Should().Contain("Saturn");
        markdown.Should().Contain("Neptune");
        markdown.Should().Contain("cnj");
        markdown.Should().Contain("2026-03-18 16:45");
    }

    /// <summary>
    /// Transit-to-transit section is omitted when no aspects exist.
    /// </summary>
    [Fact]
    public void ToMarkdown_WithoutTransitToTransitAspects_OmitsSection()
    {
        TransitResult result = CreateTestTransitResult();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        markdown.Should().NotContain("## Transit-to-Transit Aspects");
    }

    #endregion

    #region Degree Within Sign Tests

    /// <summary>
    /// The Transit planet and Natal target lines print the degree within the sign beside the sign, not the absolute
    /// longitude (E2-2, ruling 2026-10-12): "Pluto at 5°22' Aquarius", not "305°22'". The first three cases are Swiss
    /// Ephemeris longitudes computed outside AstroCast (Tier B): the transiting Pluto, Neptune and Chiron at
    /// 2026-04-12 12:00 UT, the transit golden's report start, and the J2000 New York natal true node, Mercury and
    /// Jupiter (2000-01-01 12:00 UT). The last two sit at the sign boundaries (Tier A). Each sign field is set to the
    /// sign the expected text names, and the degree is truncated to the minute.
    /// </summary>
    /// <param name="transitLongitude">The transiting planet's longitude at report start.</param>
    /// <param name="natalLongitude">The natal target's longitude.</param>
    /// <param name="transitText">The expected degree and sign on the Transit planet line.</param>
    /// <param name="natalText">The expected degree and sign on the Natal target line.</param>
    [Theory]
    [InlineData(305.3742, 123.9540, "5°22' Aquarius", "3°57' Leo")]
    [InlineData(2.6287, 271.8893, "2°37' Aries", "1°53' Capricorn")]
    [InlineData(26.3906, 25.2531, "26°23' Aries", "25°15' Aries")]
    [InlineData(30.0, 59.99999999999999, "0°00' Taurus", "29°59' Taurus")]
    [InlineData(330.5, 359.99, "0°30' Pisces", "29°59' Pisces")]
    public void ActiveTransit_PrintsTheDegreeWithinTheSign(
        double transitLongitude, double natalLongitude, string transitText, string natalText)
    {
        TransitResult result = CreateTransitResultWithOneTransit(
            transitLongitude, SignOf(transitText), natalLongitude, SignOf(natalText));

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(result));

        lines.Should().Contain($"- **Transit planet:** Pluto at {transitText} (at report start)");
        lines.Should().Contain($"- **Natal target:** Sun at {natalText} (natal 10th house)");
    }

    /// <summary>
    /// Creates a TransitResult whose only active transit is Pluto conjunct the natal Sun (10th house), at the given
    /// longitudes and signs.
    /// </summary>
    /// <param name="transitLongitude">The transiting Pluto's longitude at report start.</param>
    /// <param name="transitSign">The sign the transit carries for that longitude.</param>
    /// <param name="natalLongitude">The natal Sun's longitude.</param>
    /// <param name="natalSign">The sign the transit carries for the natal Sun.</param>
    /// <returns>The transit result.</returns>
    private static TransitResult CreateTransitResultWithOneTransit(
        double transitLongitude, string transitSign, double natalLongitude, string natalSign)
    {
        TransitResult baseResult = CreateTestTransitResult(
            includeActiveTransits: false, includeMidpointActivations: false);

        List<ActiveTransit> transits =
        [
            new ActiveTransit
            {
                TransitPlanet = TransitBody.Pluto,
                NatalTarget = "Sun",
                NatalTargetSymbol = "SU",
                AspectName = "Conjunction",
                AspectAbbreviation = "cnj",
                TransitLongitudeAtStart = transitLongitude,
                TransitSignAtStart = transitSign,
                NatalLongitude = natalLongitude,
                NatalSign = natalSign,
                NatalHouseNumber = 10,
                OrbAtStartDegrees = 1.0,
                OrbDirection = "applying",
                TransitHouseNumber = 10,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Entry,
                        Date = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

        return new TransitResult
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = baseResult.IncludeMinorAspects,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = baseResult.Ingresses,
            ActiveTransits = transits.AsReadOnly(),
            HouseOverlays = baseResult.HouseOverlays,
            MidpointActivations = [],
            MarsIncluded = false,
        };
    }

    /// <summary>The sign an expected "D°MM' Sign" text names: the word after the space.</summary>
    /// <param name="text">The expected degree and sign.</param>
    /// <returns>The sign's name.</returns>
    private static string SignOf(string text) => text[(text.IndexOf(' ', StringComparison.Ordinal) + 1)..];

    /// <summary>The export's lines, each without its line terminator.</summary>
    /// <param name="markdown">A Markdown export.</param>
    /// <returns>The export's lines, in order.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r', ' '))];

    /// <summary>
    /// A result holding the given techniques: the progressions and solar arcs from
    /// <see cref="CreateTransitResultWithProgressions"/>, the primary directions a synthetic whole-life result
    /// with one hit, the direct Moon conjunct the Midheaven, in the period when asked.
    /// </summary>
    /// <param name="solarArcs">Whether the result holds the solar arcs.</param>
    /// <param name="progressions">Whether the result holds the secondary progressions.</param>
    /// <param name="primaryDirections">Whether the result holds the primary directions.</param>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="periodHits">Whether the one primary direction falls in the period.</param>
    /// <returns>The result.</returns>
    private static TransitResult WithTechniques(
        bool solarArcs, bool progressions, bool primaryDirections, bool transits = false, bool periodHits = true)
    {
        TransitResult baseResult = CreateTransitResultWithProgressions(includeTransits: transits);

        PrimaryDirectionHit hit = new()
        {
            Significator = "Midheaven",
            Promissor = "Moon",
            PromissorKind = PrimaryDirectionPromissorKind.Body,
            Aspect = PrimaryDirectionAspect.Conjunction,
            AspectAngle = 0.0,
            Motion = PrimaryDirectionMotion.Direct,
            Arc = 26.001096,
            Years = 26.234098,
            JulianDayUt = 2461126.5,
            InstantUtc = new DateTime(2026, 3, 26, 21, 3, 58, DateTimeKind.Utc),
            MonthsPerMinute = -2.9539,
        };
        PrimaryDirectionResult? directions = primaryDirections
            ? new PrimaryDirectionResult
            {
                NatalChart = baseResult.NatalChart,
                Settings = PrimaryDirectionSettings.Default,
                Hits = [hit],
                LeftOutCount = 0,
            }
            : null;

        return new TransitResult
        {
            NatalChart = baseResult.NatalChart,
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = false,
            IncludesTransits = transits,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = baseResult.Ingresses,
            ActiveTransits = baseResult.ActiveTransits,
            HouseOverlays = baseResult.HouseOverlays,
            MidpointActivations = baseResult.MidpointActivations,
            MarsIncluded = baseResult.MarsIncluded,
            Progressions = progressions ? baseResult.Progressions : null,
            SolarArcs = solarArcs ? baseResult.SolarArcs : null,
            SolarArcHits = solarArcs ? baseResult.SolarArcHits : [],
            ProgressedHits = progressions ? baseResult.ProgressedHits : [],
            PrimaryDirections = directions,
            PrimaryDirectionHits = directions is not null && periodHits ? [hit] : [],
        };
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_WithProgressions_WithSignChanges_Should_ContainTable"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_WithProgressions_WithSignChanges_Should_ContainTable_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_WithProgressions_WithSignChanges_Should_ContainTable();
    }

    /// <summary>
    /// <see cref="ToMarkdown_WithProgressions_WithStations_Should_ContainTable"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_WithProgressions_WithStations_Should_ContainTable_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_WithProgressions_WithStations_Should_ContainTable();
    }

    /// <summary>
    /// <see cref="ToMarkdown_WithTransitToTransitAspects_RendersSection"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_WithTransitToTransitAspects_RendersSection_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_WithTransitToTransitAspects_RendersSection();
    }

    #endregion

    #region Branch AsteroidsTiming — the five as targets (R7)

    /// <summary>A synthetic transit to the natal Juno: Saturn square, exact in the period.</summary>
    private static List<ActiveTransit> CreateMinorPointTransits() =>
    [
        new ActiveTransit
        {
            TransitPlanet = TransitBody.Saturn,
            NatalTarget = "Juno",
            NatalTargetSymbol = "JUN",
            AspectName = "Square",
            AspectAbbreviation = "sqr",
            TransitLongitudeAtStart = 6.9,
            TransitSignAtStart = "Aries",
            NatalLongitude = 277.99,
            NatalSign = "Capricorn",
            NatalHouseNumber = 1,
            OrbAtStartDegrees = 1.09,
            OrbDirection = "applying",
            TransitHouseNumber = 3,
            Lifecycle =
            [
                new TransitLifecycleEvent
                {
                    EventType = TransitLifecycleEventType.Entry,
                    Date = new DateTime(2026, 3, 1, 8, 14, 0, DateTimeKind.Utc),
                    TransitLongitude = 6.5,
                },
                new TransitLifecycleEvent
                {
                    EventType = TransitLifecycleEventType.Exact,
                    Date = new DateTime(2026, 3, 20, 14, 23, 0, DateTimeKind.Utc),
                    PassNumber = 1,
                    ExactOrbDegrees = 0.0,
                },
                new TransitLifecycleEvent
                {
                    EventType = TransitLifecycleEventType.Exit,
                    Date = new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc),
                    TransitLongitude = 9.5,
                    BeyondReportPeriod = true,
                },
            ],
        },
    ];

    /// <summary>A progression result with the aspects to the five given, everything else the base's.</summary>
    /// <param name="prog">The base.</param>
    /// <param name="minorPointAspects">The aspects to the five.</param>
    /// <returns>The result.</returns>
    private static ProgressionResult WithMinorPointAspects(ProgressionResult prog, IReadOnlyList<ProgressedAspect> minorPointAspects) => new()
    {
        ProgressedDate = prog.ProgressedDate,
        ProgressedJulianDay = prog.ProgressedJulianDay,
        SolarArcValue = prog.SolarArcValue,
        Positions = prog.Positions,
        MoonInfo = prog.MoonInfo,
        SignChanges = prog.SignChanges,
        Stations = prog.Stations,
        Aspects = prog.Aspects,
        MinorPointAspects = minorPointAspects,
    };

    /// <summary>A solar arc result with the aspects to the five given, everything else the base's.</summary>
    /// <param name="sa">The base.</param>
    /// <param name="minorPointAspects">The aspects to the five.</param>
    /// <returns>The result.</returns>
    private static SolarArcResult WithMinorPointAspects(SolarArcResult sa, IReadOnlyList<SolarArcAspect> minorPointAspects) => new()
    {
        ArcValue = sa.ArcValue,
        ArcFormatted = sa.ArcFormatted,
        Positions = sa.Positions,
        Aspects = sa.Aspects,
        MinorPointAspects = minorPointAspects,
    };

    /// <summary>
    /// A result whose natal chart carries the five, from a base with the three techniques: the transit to Juno and
    /// the technique rows to the five when asked (P Venus square Ceres and its hit, SA Pluto conjunct Juno and its
    /// hit), else none of them.
    /// </summary>
    /// <param name="baseResult">The base result, with progressions and solar arcs.</param>
    /// <param name="transitRows">Whether the five have a transit.</param>
    /// <param name="techniqueRows">Whether the five have progressed and directed aspects and hits.</param>
    /// <returns>The result.</returns>
    private static TransitResult WithMinorPoints(TransitResult baseResult, bool transitRows = true, bool techniqueRows = true)
    {
        baseResult.Progressions.Should().NotBeNull();
        baseResult.SolarArcs.Should().NotBeNull();
        ProgressionResult progressions = baseResult.Progressions is ProgressionResult prog ? prog : CreateTestProgressionResult();
        SolarArcResult solarArcs = baseResult.SolarArcs is SolarArcResult sa ? sa : CreateTestSolarArcResult();

        List<ProgressedAspect> progressedAspects = techniqueRows
            ?
            [
                new ProgressedAspect
                {
                    ProgressedBody = "P Venus", AspectName = "Square", AspectAbbreviation = "sqr",
                    NatalTarget = "Ceres", OrbDegrees = 0.8335, Direction = "applying",
                },
            ]
            : [];
        List<SolarArcAspect> solarArcAspects = techniqueRows
            ?
            [
                new SolarArcAspect
                {
                    DirectedBody = "SA Pluto", AspectName = "Conjunction", AspectAbbreviation = "cnj",
                    NatalTarget = "Juno", OrbDegrees = 0.0622, Direction = "applying",
                    InvolvesAngle = false, IsHardAspect = true,
                },
            ]
            : [];
        List<ProgressedHit> progressedHits = techniqueRows
            ?
            [
                new ProgressedHit
                {
                    ProgressedPoint = "Moon", AspectAngle = 60.0, AspectName = "Sextile", AspectAbbreviation = "sxt",
                    NatalPoint = "Juno", JulianDayUt = 2461114.0, InstantUtc = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
                },
            ]
            : [];
        List<SolarArcHit> solarArcHits = techniqueRows
            ?
            [
                new SolarArcHit
                {
                    DirectedPoint = "Pluto", AspectAngle = 0.0, AspectName = "Conjunction", AspectAbbreviation = "cnj",
                    Target = "Juno", Arc = 26.5, JulianDayUt = 2461120.0, InstantUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
                },
            ]
            : [];

        return new TransitResult
        {
            NatalChart = CreateMinimalChartResult(includeMinorPoints: true),
            ReportStartDate = baseResult.ReportStartDate,
            ReportEndDate = baseResult.ReportEndDate,
            MidpointDate = baseResult.MidpointDate,
            IncludeMinorAspects = baseResult.IncludeMinorAspects,
            IncludesTransits = baseResult.IncludesTransits,
            TransitPositions = baseResult.TransitPositions,
            Stations = baseResult.Stations,
            Ingresses = baseResult.Ingresses,
            ActiveTransits = baseResult.ActiveTransits,
            HouseOverlays = baseResult.HouseOverlays,
            MidpointActivations = baseResult.MidpointActivations,
            MarsIncluded = baseResult.MarsIncluded,
            MinorPointTransits = transitRows ? CreateMinorPointTransits() : [],
            Progressions = WithMinorPointAspects(progressions, progressedAspects),
            SolarArcs = WithMinorPointAspects(solarArcs, solarArcAspects),
            SolarArcHits = baseResult.SolarArcHits,
            ProgressedHits = baseResult.ProgressedHits,
            MinorPointProgressedHits = progressedHits,
            MinorPointSolarArcHits = solarArcHits,
        };
    }

    /// <summary>The index of the first line beginning with the text, or -1.</summary>
    /// <param name="lines">The export's lines.</param>
    /// <param name="start">The line's beginning.</param>
    /// <returns>The index.</returns>
    private static int IndexOfLineStarting(List<string> lines, string start) =>
        lines.FindIndex(l => l.StartsWith(start, StringComparison.Ordinal));

    /// <summary>
    /// The section of the transits to the five sits directly after the Active Transits section's rule and before
    /// the house overlays, with its opening line and the transit's block as the Active Transits print one.
    /// </summary>
    [Fact]
    public void ToMarkdown_MinorPointTransits_SectionSitsAfterActiveTransitsAndBeforeTheOverlays()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(WithMinorPoints(CreateTransitResultWithProgressions())));

        int active = lines.IndexOf("## Active Transits");
        int section = lines.IndexOf("## Transits to the Asteroids and Lilith");
        int overlays = IndexOfLineStarting(lines, "## Transit House Overlays");
        active.Should().BePositive();
        section.Should().BeGreaterThan(active);
        overlays.Should().BeGreaterThan(section);
        lines[section - 1].Should().BeEmpty();
        lines[section - 2].Should().Be("---", "the section follows the Active Transits section's rule directly");
        lines[section + 2].Should().Be(
            "Transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee): the five Ptolemaic aspects at each transiting body's major orbs.");
        lines.Should().Contain("### TR SA sqr NA JUN");
        lines.Should().Contain("- **Transit planet:** Saturn at 6\u00b054' Aries (at report start)");
        lines.Should().Contain("- **Natal target:** Juno at 7\u00b059' Capricorn (natal 1st house)");
        lines.Should().Contain("- **Orb at report start:** 1\u00b005\u2032, applying");
        lines.Should().Contain("- **Exact pass 1:** 2026-03-20 14:23:00 UTC (orb: 0\u00b000\u203200\u2033)");
        lines[overlays - 1].Should().BeEmpty();
        lines[overlays - 2].Should().Be("---", "the section closes with its own rule");
    }

    /// <summary>The five's blocks hold no medical line (R8) while the Active Transits keep theirs; the transit house line stays.</summary>
    [Fact]
    public void ToMarkdown_MinorPointTransits_BlocksHoldNoMedicalLine()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(WithMinorPoints(CreateTransitResultWithProgressions())));

        int section = lines.IndexOf("## Transits to the Asteroids and Lilith");
        int overlays = IndexOfLineStarting(lines, "## Transit House Overlays");
        List<string> sectionLines = lines.Skip(section).Take(overlays - section).ToList();

        sectionLines.Should().NotContain(l => l.Contains("Medical", StringComparison.Ordinal));
        sectionLines.Should().Contain("- **Transit house:** 3rd (natal)");
        lines.Take(section).Should().Contain(l => l.StartsWith("- **Medical (transit planet):**", StringComparison.Ordinal),
            "the Active Transits keep their medical lines");
    }

    /// <summary>With the five carried and no transit to them the section prints its none line; without the five there is no section.</summary>
    [Fact]
    public void ToMarkdown_MinorPointTransits_NoneLineAndNoSectionWithoutTheFive()
    {
        List<string> none = Lines(TransitMarkdownFormatter.ToMarkdown(WithMinorPoints(CreateTransitResultWithProgressions(), transitRows: false)));
        string withoutTheFive = TransitMarkdownFormatter.ToMarkdown(CreateTransitResultWithProgressions());

        none.Should().Contain("## Transits to the Asteroids and Lilith");
        none.Should().Contain("No transit to the asteroids or Lilith is active during this period.");
        none.Should().NotContain("### TR SA sqr NA JUN");
        withoutTheFive.Should().NotContain("Asteroids and Lilith");
    }

    /// <summary>
    /// The four technique sub-sections of the five sit directly after their twins, in order, with their rows; the
    /// Techniques and Aspects header lines are unchanged.
    /// </summary>
    [Fact]
    public void ToMarkdown_MinorPointTechniqueSubSections_SitAfterTheirTwins()
    {
        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(WithMinorPoints(CreateTransitResultWithProgressions())));

        int[] order =
        [
            lines.IndexOf("### Progressed-to-Natal Aspects (major, 1\u00b0 orb, at report start)"),
            lines.IndexOf("### Progressed-to-Natal Aspects: Asteroids and Lilith (major, 1\u00b0 orb, at report start)"),
            lines.IndexOf("### Progressed Hits in the Report Period"),
            lines.IndexOf("### Progressed Hits to the Asteroids and Lilith"),
            lines.IndexOf("### Progressed Sign Changes (within 6 months)"),
            lines.IndexOf("### Solar-Arc-to-Natal Aspects (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)"),
            lines.IndexOf("### Solar-Arc-to-Natal Aspects: Asteroids and Lilith (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)"),
            lines.IndexOf("### Solar Arc Hits in the Report Period"),
            lines.IndexOf("### Solar Arc Hits to the Asteroids and Lilith"),
        ];
        order.Should().OnlyContain(i => i > 0);
        order.Should().BeInAscendingOrder();
        lines.Should().Contain("| P Venus | sqr | Ceres | 0\u00b050\u2032 | Applying |");
        lines.Should().Contain("| 2026-03-14 | P Moon sxt NA Juno | Sextile |");
        lines.Should().Contain("| SA Pluto | cnj | Juno | 0\u00b003\u2032 | Applying | hard |");
        lines.Should().Contain("| 2026-03-20 | SA Pluto cnj NA Juno | Conjunction |");
        lines.Should().Contain("**Techniques:** Transits, Solar Arcs, Secondary Progressions");
        lines.Should().Contain("**Aspects:** Major");
    }

    /// <summary>Each of the four sub-sections prints its twin's empty text when it has no row: with no row anywhere, each text twice.</summary>
    [Fact]
    public void ToMarkdown_MinorPointTechniqueSubSections_PrintTheirTwinsEmptyText()
    {
        TransitResult baseResult = CreateTransitResultWithProgressions(includeProgAspects: false, includeSolarArcAspects: false, includeHits: false);

        List<string> lines = Lines(TransitMarkdownFormatter.ToMarkdown(WithMinorPoints(baseResult, techniqueRows: false)));

        lines.Where(l => l == "No major progressed-to-natal aspects within 1\u00b0 orb.").Should().HaveCount(2);
        lines.Where(l => l == "No progressed hit becomes exact in the report period.").Should().HaveCount(2);
        lines.Where(l => l == "No solar-arc-to-natal aspects within 1\u00b0 orb at the report start.").Should().HaveCount(2);
        lines.Where(l => l == "No solar arc hit becomes exact in the report period.").Should().HaveCount(2);
        lines.Should().Contain("### Progressed-to-Natal Aspects: Asteroids and Lilith (major, 1\u00b0 orb, at report start)");
        lines.Should().Contain("### Solar Arc Hits to the Asteroids and Lilith");
    }

    #endregion
}
