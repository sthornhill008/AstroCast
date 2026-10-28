using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests verifying the sub-minute UTC time precision for transit exact passes.
/// Covers ephemeris helper round-trip, binary search convergence, edge cases,
/// DatePart backward compatibility, and Markdown format output.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitTimePrecisionTests
{
    private readonly TransitEphemerisHelper _helper;
    private readonly TransitAspectFinder _finder;
    private readonly TransitMidpointScanner _scanner;

    /// <summary>Evenly spaced house cusps for deterministic testing.</summary>
    private static readonly List<double> TestHouseCusps =
        Enumerable.Range(0, 12).Select(i => i * 30.0).ToList();

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    public TransitTimePrecisionTests(EphemerisFixture fixture)
    {
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
        TransitStationDetector stationDetector = new(_helper);
        _finder = new TransitAspectFinder(_helper, stationDetector);
        _scanner = new TransitMidpointScanner(_helper);
    }

    #region Ephemeris Helper Round-Trip

    /// <summary>
    /// Verifies DateTime → Julian Day → DateTime round-trip preserves time
    /// to within 1 second. This confirms that DateTimeToJulianDay and
    /// JulianDayToDateTime are consistent inverse operations.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 15, 14, 23, 0)]
    [InlineData(2026, 1, 1, 0, 0, 0)]
    [InlineData(2026, 6, 21, 12, 0, 0)]
    [InlineData(2026, 12, 31, 23, 59, 30)]
    [InlineData(2000, 1, 1, 12, 0, 0)]
    public void DateTimeToJulianDay_RoundTrip_Should_MatchWithinOneSecond(
        int year, int month, int day, int hour, int minute, int second)
    {
        DateTime original = new(year, month, day, hour, minute, second, DateTimeKind.Utc);

        double jd = _helper.DateTimeToJulianDay(original);
        DateTime restored = _helper.JulianDayToDateTime(jd);

        TimeSpan difference = (original - restored).Duration();
        difference.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(1),
            $"round-trip {original:yyyy-MM-dd HH:mm:ss} → JD {jd:F6} → {restored:yyyy-MM-dd HH:mm:ss} " +
            "should preserve time to within 1 second");
    }

    /// <summary>
    /// Verifies DateTimeToJulianDay throws for non-UTC DateTime values.
    /// </summary>
    [Fact]
    public void DateTimeToJulianDay_NonUtc_Should_ThrowArgumentException()
    {
        DateTime localTime = new(2026, 3, 15, 14, 0, 0, DateTimeKind.Local);

        Action act = () => _helper.DateTimeToJulianDay(localTime);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*UTC*");
    }

    /// <summary>
    /// Verifies J2000.0 epoch round-trip through the DateTime overload.
    /// J2000.0 = 2000-01-01 12:00:00 UTC = JD 2451545.0
    /// </summary>
    [Fact]
    public void DateTimeToJulianDay_J2000Epoch_Should_ReturnExpectedJD()
    {
        DateTime j2000 = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        double jd = _helper.DateTimeToJulianDay(j2000);

        jd.Should().BeApproximately(2451545.0, 0.0001,
            "J2000.0 epoch should map to JD 2451545.0");
    }

    #endregion

    #region Binary Search Convergence — Aspect Finder

    /// <summary>
    /// Verifies that exact pass detection produces a DateTime with a non-noon
    /// time component, confirming the binary search refinement is active.
    /// Uses Saturn conjunction to a synthetic natal target.
    /// </summary>
    [Fact]
    public void FindActiveTransits_ExactPass_Should_HaveSubDayPrecision()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        // Place natal target slightly ahead so Saturn crosses during the month
        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        TransitLifecycleEvent? exactEvent = transits[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        exactEvent.Should().NotBeNull("Saturn should have an exact pass");

        // Verify the time is NOT exactly noon (would indicate no refinement occurred)
        DateTime exactTime = exactEvent!.Date;
        exactTime.Kind.Should().Be(DateTimeKind.Utc,
            "exact pass time should be UTC");

        bool isExactlyNoon = exactTime.Hour == 12 && exactTime.Minute == 0 && exactTime.Second == 0;
        isExactlyNoon.Should().BeFalse(
            "exact pass time should have sub-day precision from binary search, " +
            $"but got {exactTime:HH:mm:ss} which is exactly noon");
    }

    /// <summary>
    /// Verifies that entry/exit events have sub-minute UTC precision from binary
    /// search refinement (no longer noon UTC).
    /// </summary>
    [Fact]
    public void FindActiveTransits_EntryExit_Should_HaveSubMinutePrecision()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Moon",
                Symbol = "MO",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 4,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        TransitLifecycleEvent entryEvent = transits[0].Lifecycle
            .First(e => e.EventType == TransitLifecycleEventType.Entry);
        TransitLifecycleEvent exitEvent = transits[0].Lifecycle
            .First(e => e.EventType == TransitLifecycleEventType.Exit);

        entryEvent.Date.Kind.Should().Be(DateTimeKind.Utc, "entry time should be UTC");
        exitEvent.Date.Kind.Should().Be(DateTimeKind.Utc, "exit time should be UTC");

        // At least one of entry/exit should NOT be exactly noon
        // (binary search should refine away from the noon placeholder)
        bool entryIsNoon = entryEvent.Date.Hour == 12 && entryEvent.Date.Minute == 0 && entryEvent.Date.Second == 0;
        bool exitIsNoon = exitEvent.Date.Hour == 12 && exitEvent.Date.Minute == 0 && exitEvent.Date.Second == 0;
        (entryIsNoon && exitIsNoon).Should().BeFalse(
            "entry/exit times should have sub-minute precision, not both be noon UTC");
    }

    /// <summary>
    /// Verifies that entry time precedes the first exact pass.
    /// </summary>
    [Fact]
    public void FindActiveTransits_EntryTime_PrecedesFirstExactPass()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        TransitLifecycleEvent? entryEvent = transits[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Entry);
        TransitLifecycleEvent? exactEvent = transits[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        if (entryEvent is not null && exactEvent is not null)
        {
            entryEvent.Date.Should().BeBefore(exactEvent.Date,
                "entry should precede the first exact pass");
        }
    }

    /// <summary>
    /// Verifies that exit time follows the last exact pass.
    /// </summary>
    [Fact]
    public void FindActiveTransits_ExitTime_FollowsLastExactPass()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        TransitLifecycleEvent? exitEvent = transits[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exit);
        TransitLifecycleEvent? lastExact = transits[0].Lifecycle
            .LastOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        if (exitEvent is not null && lastExact is not null)
        {
            exitEvent.Date.Should().BeAfter(lastExact.Date,
                "exit should follow the last exact pass");
        }
    }

    /// <summary>
    /// Verifies that station DateTimeUtc is populated when stations are detected.
    /// </summary>
    [Fact]
    public void FindStations_DateTimeUtc_IsPopulated()
    {
        // Saturn has a station in 2025
        TransitStationDetector detector = new(_helper);

        IReadOnlyList<TransitStation> stations = detector.FindStations(
            TransitBody.Saturn, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        stations.Should().NotBeEmpty("Saturn should have at least one station in 2025");

        TransitStation station = stations[0];
        station.DateTimeUtc.Should().NotBeNull("station DateTimeUtc should be populated");
        station.DateTimeUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);

        // Refined time should fall on the same day as the Date property
        DateOnly refinedDate = DateOnly.FromDateTime(station.DateTimeUtc.Value);
        int dayDiff = Math.Abs(refinedDate.DayNumber - station.Date.DayNumber);
        dayDiff.Should().BeLessThanOrEqualTo(1,
            "refined station time should be within 1 day of the day-level station date");
    }

    #endregion

    #region Binary Search Convergence — Midpoint Scanner

    /// <summary>
    /// Verifies that midpoint exact pass detection produces a DateTime with
    /// sub-day precision, confirming binary search refinement is active.
    /// </summary>
    [Fact]
    public void FindMidpointActivations_ExactPass_Should_HaveSubDayPrecision()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        // Place midpoint slightly ahead on the dial
        double targetDial45 = (saturnDial45 + 0.05) % 45.0;

        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Sun",
                Body2 = "Moon",
                Dial45Position = targetDial45,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().NotBeEmpty(
            "Saturn should activate a midpoint 0.05° ahead on the dial");

        TransitLifecycleEvent? exactEvent = activations[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        exactEvent.Should().NotBeNull(
            "midpoint activation should have an exact pass");

        DateTime exactTime = exactEvent!.Date;
        exactTime.Kind.Should().Be(DateTimeKind.Utc,
            "midpoint exact pass time should be UTC");

        bool isExactlyNoon = exactTime.Hour == 12 && exactTime.Minute == 0 && exactTime.Second == 0;
        isExactlyNoon.Should().BeFalse(
            "midpoint exact pass should have sub-day precision from binary search, " +
            $"but got {exactTime:HH:mm:ss} which is exactly noon");
    }

    #endregion

    #region Edge Case — 0° Aries Wraparound

    /// <summary>
    /// Verifies that aspect detection works correctly across the 0° Aries point
    /// (360°/0° boundary). Transit at ~359° forming conjunction with natal at ~1°.
    /// </summary>
    [Fact]
    public void FindActiveTransits_AcrossAriesPoint_Should_DetectConjunction()
    {
        // Use a wider date range to allow any slow-moving planet to cross 0° Aries
        DateOnly start = new(2025, 5, 1);
        DateOnly end = new(2025, 5, 31);

        // Saturn enters Aries in May 2025 — crossing the 0° point
        // Get Saturn's position mid-month
        double jdMid = _helper.DateToJulianDay(new DateOnly(2025, 5, 15));
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jdMid);

        // Place natal target near Saturn (within 2° of its mid-month position)
        // to ensure a conjunction is detected
        double natalLon = saturnLon;

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Venus",
                Symbol = "VE",
                Longitude = natalLon,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty(
            "Saturn should form a conjunction with a natal target at its own longitude");

        transits[0].AspectName.Should().Be("Conjunction");
    }

    /// <summary>
    /// Verifies ShortestArc handles the 360°/0° boundary correctly.
    /// This is the foundation of aspect detection across the Aries point.
    /// </summary>
    [Theory]
    [InlineData(359.5, 0.5, 1.0)]
    [InlineData(1.0, 359.0, 2.0)]
    [InlineData(0.0, 180.0, 180.0)]
    [InlineData(350.0, 10.0, 20.0)]
    public void ShortestArc_AriesWrap_Should_ReturnCorrectSeparation(
        double lon1, double lon2, double expected)
    {
        double arc = TransitAspectFinder.ShortestArc(lon1, lon2);

        arc.Should().BeApproximately(expected, 0.001,
            $"ShortestArc({lon1}°, {lon2}°) should be {expected}°");
    }

    #endregion

    #region DatePart Backward Compatibility

    /// <summary>
    /// Verifies that DatePart on lifecycle events returns the correct date
    /// portion, maintaining backward compatibility with code that previously
    /// used the DateOnly Date property.
    /// </summary>
    [Fact]
    public void ExactPassEvent_DatePart_Should_MatchDatePortionOfDateTime()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Jupiter",
                Symbol = "JU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 9,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        foreach (TransitLifecycleEvent evt in transits[0].Lifecycle)
        {
            DateOnly expectedDatePart = DateOnly.FromDateTime(evt.Date);
            evt.DatePart.Should().Be(expectedDatePart,
                $"DatePart should match the date portion of Date for {evt.EventType} event");
        }
    }

    /// <summary>
    /// Verifies that entry events have DatePart that can be compared
    /// for sorting and display — regression test for the DateOnly→DateTime migration.
    /// </summary>
    [Fact]
    public void LifecycleEvents_DatePart_Should_SupportDayNumberComparison()
    {
        TransitLifecycleEvent entry = new()
        {
            EventType = TransitLifecycleEventType.Entry,
            Date = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc),
        };

        TransitLifecycleEvent exact = new()
        {
            EventType = TransitLifecycleEventType.Exact,
            Date = new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Utc),
            PassNumber = 1,
        };

        TransitLifecycleEvent exit = new()
        {
            EventType = TransitLifecycleEventType.Exit,
            Date = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc),
        };

        int entryToExact = exact.DatePart.DayNumber - entry.DatePart.DayNumber;
        int exactToExit = exit.DatePart.DayNumber - exact.DatePart.DayNumber;

        entryToExact.Should().Be(10, "exact should be 10 days after entry");
        exactToExit.Should().Be(10, "exit should be 10 days after exact");
    }

    #endregion

    #region Markdown Format Verification

    /// <summary>
    /// Verifies that exact pass events in Markdown output include UTC time
    /// in the format "HH:mm:ss UTC", while entry/exit events show date only.
    /// </summary>
    [Fact]
    public void ToMarkdown_ExactPass_Should_ShowUtcTime()
    {
        TransitResult result = CreateTransitResultWithKnownTimes();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Exact pass should include the time "14:23:47 UTC"
        markdown.Should().Contain("14:23:47 UTC",
            "exact pass event should display UTC time in HH:mm:ss format");

        // Exact pass should include the full format "2026-03-12 14:23:47 UTC"
        markdown.Should().Contain("2026-03-12 14:23:47 UTC",
            "exact pass event should display full date + time with seconds");
    }

    /// <summary>
    /// Verifies that entry events in Markdown output show full UTC time
    /// (sub-minute precision from binary search refinement).
    /// </summary>
    [Fact]
    public void ToMarkdown_EntryEvent_Should_ShowUtcTime()
    {
        TransitResult result = CreateTransitResultWithKnownTimes();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Entry at noon UTC (test data uses noon) — should show full time
        markdown.Should().Contain("**Entry:** 2026-02-15 12:00",
            "entry event should display date with UTC time");
    }

    /// <summary>
    /// Verifies that exit events in Markdown output show full UTC time
    /// (sub-minute precision from binary search refinement).
    /// </summary>
    [Fact]
    public void ToMarkdown_ExitEvent_Should_ShowUtcTime()
    {
        TransitResult result = CreateTransitResultWithKnownTimes();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Exit at noon UTC (test data uses noon) — should show full time
        markdown.Should().Contain("**Exit:** 2026-04-08 12:00",
            "exit event should display date with UTC time");
    }

    /// <summary>
    /// Verifies that midpoint exact events in Markdown output include UTC time.
    /// </summary>
    [Fact]
    public void ToMarkdown_MidpointExact_Should_ShowUtcTime()
    {
        TransitResult result = CreateTransitResultWithKnownTimes();

        string markdown = TransitMarkdownFormatter.ToMarkdown(result);

        // Midpoint exact should include time "04:23:12 UTC"
        markdown.Should().Contain("04:23:12 UTC",
            "midpoint exact event should display UTC time with seconds");
    }

    #endregion

    #region Test Data Helpers

    /// <summary>
    /// Creates a TransitResult with known lifecycle times for formatter testing.
    /// Entry/exit use noon UTC, exact passes use specific times.
    /// </summary>
    private static TransitResult CreateTransitResultWithKnownTimes()
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
        };

        List<ActiveTransit> activeTransits =
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
                        Date = new DateTime(2026, 3, 12, 14, 23, 47, DateTimeKind.Utc),
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
        ];

        List<TransitMidpointActivation> midpointActivations =
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
                        Date = new DateTime(2026, 3, 15, 4, 23, 12, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exit,
                        Date = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ];

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
            MidpointActivations = midpointActivations.AsReadOnly(),
            MarsIncluded = false,
        };
    }

    /// <summary>
    /// Creates a minimal ChartResult with enough data for the formatter.
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

    #endregion

    #region Residual Orb Tests

    /// <summary>
    /// Verifies that exact pass events from transit-to-natal aspects have
    /// ExactOrbDegrees populated with a value less than 0.01° (confirming
    /// the binary search achieved at least ~36 arc-second precision).
    /// </summary>
    [Fact]
    public void FindActiveTransits_ExactPass_Should_HaveResidualOrbPopulated()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        TransitLifecycleEvent? exactEvent = transits[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        exactEvent.Should().NotBeNull("Saturn should have an exact pass");
        exactEvent!.ExactOrbDegrees.Should().NotBeNull(
            "exact pass events should have ExactOrbDegrees populated");
        exactEvent.ExactOrbDegrees!.Value.Should().BeLessThan(0.01,
            "binary search should achieve at least ~36 arc-second precision");
    }

    /// <summary>
    /// Verifies that non-exact lifecycle events (Entry, Exit, Station) have
    /// ExactOrbDegrees as null.
    /// </summary>
    [Fact]
    public void FindActiveTransits_NonExactEvents_Should_HaveNullExactOrb()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        IEnumerable<TransitLifecycleEvent> nonExactEvents = transits[0].Lifecycle
            .Where(e => e.EventType != TransitLifecycleEventType.Exact);

        foreach (TransitLifecycleEvent evt in nonExactEvents)
        {
            evt.ExactOrbDegrees.Should().BeNull(
                $"{evt.EventType} events should not have ExactOrbDegrees");
        }
    }

    /// <summary>
    /// Verifies that midpoint exact pass events have ExactOrbDegrees populated
    /// with a value less than 0.01°.
    /// </summary>
    [Fact]
    public void FindMidpointActivations_ExactPass_Should_HaveResidualOrbPopulated()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = saturnLon % 45.0;

        // Place natal midpoint slightly ahead on the 45° dial
        double targetDial45 = (saturnDial45 + 0.3) % 45.0;

        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Sun",
                Body2 = "Moon",
                Dial45Position = targetDial45,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().NotBeEmpty();

        TransitLifecycleEvent? exactEvent = activations[0].Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        exactEvent.Should().NotBeNull("midpoint activation should have an exact pass");
        exactEvent!.ExactOrbDegrees.Should().NotBeNull(
            "midpoint exact pass events should have ExactOrbDegrees populated");
        exactEvent.ExactOrbDegrees!.Value.Should().BeLessThan(0.01,
            "binary search should achieve at least ~36 arc-second precision for midpoints");
    }

    #endregion

    #region Culture independence

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ToMarkdown_ExactPass_Should_ShowUtcTime"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_ExactPass_Should_ShowUtcTime_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_ExactPass_Should_ShowUtcTime();
    }

    /// <summary>
    /// <see cref="ToMarkdown_EntryEvent_Should_ShowUtcTime"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_EntryEvent_Should_ShowUtcTime_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_EntryEvent_Should_ShowUtcTime();
    }

    /// <summary>
    /// <see cref="ToMarkdown_ExitEvent_Should_ShowUtcTime"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ToMarkdown_ExitEvent_Should_ShowUtcTime_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ToMarkdown_ExitEvent_Should_ShowUtcTime();
    }

    #endregion
}
