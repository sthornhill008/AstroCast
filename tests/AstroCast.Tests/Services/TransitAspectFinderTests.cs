using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitAspectFinder"/> verifying transit-to-natal aspect
/// detection, lifecycle calculation, orb direction, and angular separation helpers.
/// Uses the real Swiss Ephemeris via the shared test fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitAspectFinderTests
{
    private readonly TransitAspectFinder _finder;
    private readonly TransitEphemerisHelper _helper;
    private readonly EphemerisFixture _fixture;

    /// <summary>Evenly spaced house cusps for deterministic testing.</summary>
    private static readonly List<double> TestHouseCusps =
        Enumerable.Range(0, 12).Select(i => i * 30.0).ToList();

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the finder chain.
    /// </summary>
    public TransitAspectFinderTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
        TransitStationDetector stationDetector = new(_helper);
        _finder = new TransitAspectFinder(_helper, stationDetector);
    }

    #region FindActiveTransits — Aspect Detection

    [Fact]
    public void FindActiveTransits_Saturn_Should_FindAspectToNatalSun()
    {
        // Get Saturn's position at report start and place a synthetic natal Sun there
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        // Place natal Sun 1° ahead of Saturn so Saturn approaches it during the month
        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Sun",
                Symbol = "SU",
                Longitude = (saturnLon + 1.0) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty(
            "Saturn should form a conjunction with a natal target at its own longitude");

        transits.Should().Contain(t => t.AspectName == "Conjunction",
            "the detected aspect should be a conjunction");
    }

    [Fact]
    public void FindActiveTransits_Should_IncludeLifecycleEvents()
    {
        // Use Saturn conjunction from a known position
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Venus",
                Symbol = "VE",
                Longitude = saturnLon,
                HouseNumber = 7,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();
        ActiveTransit transit = transits[0];

        transit.Lifecycle.Should().NotBeEmpty(
            "lifecycle should contain events");

        transit.Lifecycle.Should().Contain(e => e.EventType == TransitLifecycleEventType.Entry,
            "lifecycle should contain an Entry event");

        transit.Lifecycle.Should().Contain(e => e.EventType == TransitLifecycleEventType.Exit,
            "lifecycle should contain an Exit event");

        // Entry should be the first event, Exit the last
        transit.Lifecycle[0].EventType.Should().Be(TransitLifecycleEventType.Entry,
            "Entry should be the first lifecycle event");

        transit.Lifecycle[^1].EventType.Should().Be(TransitLifecycleEventType.Exit,
            "Exit should be the last lifecycle event");
    }

    [Fact]
    public void FindActiveTransits_ExactPass_Should_HavePassNumber()
    {
        // Place natal target slightly ahead of Saturn so it passes through exact
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        // Saturn moves ~0.06°/day; 0.5° ahead should be reached in ~8 days
        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Mercury",
                Symbol = "ME",
                Longitude = (saturnLon + 0.5) % 360.0,
                HouseNumber = 3,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        // Look for an exact pass event
        ActiveTransit transit = transits.First(t => t.AspectName == "Conjunction");
        TransitLifecycleEvent? exactEvent = transit.Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exact);

        exactEvent.Should().NotBeNull(
            "Saturn should have an exact pass when crossing a natal target within the month");

        exactEvent!.PassNumber.Should().BeGreaterThanOrEqualTo(1,
            "exact pass should have a pass number >= 1");
    }

    #endregion

    #region FindActiveTransits — Minor Aspects

    [Fact]
    public void FindActiveTransits_MinorAspects_WhenDisabled_Should_NotIncludeThem()
    {
        // Place natal target at a position forming only a semisquare (45°) with Saturn
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        // Place target 45° away — only semisquare, no major aspect within orb
        double natalLon = (saturnLon + 45.0) % 360.0;

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Mars",
                Symbol = "MA",
                Longitude = natalLon,
                HouseNumber = 4,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().BeEmpty(
            "no major aspects should be found when natal target is 45° away (semisquare only)");
    }

    [Fact]
    public void FindActiveTransits_MinorAspects_WhenEnabled_Should_IncludeThem()
    {
        // Same setup as above but with minor aspects enabled
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);

        // Place target 45° away — should find semisquare
        double natalLon = (saturnLon + 45.0) % 360.0;

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Mars",
                Symbol = "MA",
                Longitude = natalLon,
                HouseNumber = 4,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: true, TestHouseCusps);

        transits.Should().NotBeEmpty(
            "semisquare should be detected when minor aspects are enabled");

        transits.Should().Contain(t => t.AspectName == "Semisquare",
            "the detected minor aspect should be a semisquare");
    }

    #endregion

    #region FindActiveTransits — Orb Direction and Transit House

    [Fact]
    public void FindActiveTransits_OrbDirection_Should_BeApplyingOrSeparating()
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
                Longitude = (saturnLon + 1.0) % 360.0,
                HouseNumber = 1,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        foreach (ActiveTransit transit in transits)
        {
            transit.OrbDirection.Should().BeOneOf("applying", "separating", "exact",
                "OrbDirection should be one of applying, separating, or exact");
        }
    }

    /// <summary>
    /// Saved chart 40, the report from 2026-04-27: transiting Jupiter
    /// is 0°02′ short of a sextile to natal Mercury at the report's start and perfects at
    /// 18:14 UT that day, so it is applying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 1.3.3 the direction compared the orb at 12:00 UT with the orb 24 hours later.
    /// An aspect that perfects in the next 12 hours has passed exact and opened again by
    /// then, so it was printed "Orb at report start: 0°02′, separating" (finding T1-3).
    /// </para>
    /// <para>
    /// Tier B (direct Swiss Ephemeris calls): natal Mercury 48.47382818° at the saved
    /// input (a bare longitude; the chart itself is not cast); Jupiter at 108.43940478° at
    /// 2026-04-27 12:00 UT; orb 0.0344234°, closing at 0.1321°/day by a ±5-second
    /// symmetric difference; exact at 18:14:30.07 UT by bisection.
    /// </para>
    /// </remarks>
    [Fact]
    public void FindActiveTransits_OrbDirection_Should_BeApplying_When_JupiterPerfectsLaterOnReportStartDay()
    {
        DateOnly start = new(2026, 4, 27);
        DateOnly end = new(2026, 6, 1);
        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Mercury", Symbol = "ME", Longitude = 48.47382817880826, HouseNumber = 9 },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Jupiter, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        ActiveTransit sextile = transits.Single(t => t.AspectName == "Sextile");
        sextile.OrbAtStartDegrees.Should().BeApproximately(0.0344234, 1e-6, "the orb Tier B measures at 12:00 UT");
        sextile.OrbDirection.Should().Be("applying", "the sextile perfects at 18:14 UT, after the report's start");
        sextile.Lifecycle.Should().Contain(e => e.EventType == TransitLifecycleEventType.Exact
            && Math.Abs((e.Date - new DateTime(2026, 4, 27, 18, 14, 30, DateTimeKind.Utc)).TotalSeconds) <= 1.0,
            "Tier B puts the exact pass at 18:14:30.07 UT");
    }

    /// <summary>
    /// Saved chart 13, from 2026-01-01: retrograde Jupiter is 0.0375° from a
    /// sextile to natal Mars at 12:00 UT and perfects 6.9 hours later, so it is applying.
    /// </summary>
    /// <remarks>
    /// Tier B: natal Mars 171.25465671° at the saved input (a bare
    /// longitude; the chart itself is not cast); Jupiter at 111.29220587°, moving backward; orb
    /// 0.0375492°, closing at 0.1310°/day; exact at 18:52:23 UT by bisection. The timing
    /// investigation's standard two-year run printed it separating.
    /// </remarks>
    [Fact]
    public void FindActiveTransits_OrbDirection_Should_BeApplying_When_RetrogradeJupiterPerfectsWithinTwelveHours()
    {
        DateOnly start = new(2026, 1, 1);
        DateOnly end = new(2026, 2, 1);
        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Mars", Symbol = "MA", Longitude = 171.254656708657, HouseNumber = 9 },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Jupiter, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        ActiveTransit sextile = transits.Single(t => t.AspectName == "Sextile");
        sextile.OrbAtStartDegrees.Should().BeApproximately(0.0375492, 1e-6, "the orb Tier B measures at 12:00 UT");
        sextile.OrbDirection.Should().Be("applying", "retrograde Jupiter perfects the sextile at 18:52 UT");
    }

    /// <summary>
    /// Saved chart 55, the report from 2026-06-01: transiting Saturn
    /// perfected its square to natal Moon at 01:30 UT, 10.5 hours before the report's
    /// start, so at 12:00 UT it is separating.
    /// </summary>
    /// <remarks>
    /// Tier B: natal Moon 282.24985758° at the saved input (a bare
    /// longitude; the chart itself is not cast); Saturn at 12.28662418°; orb 0.0367666°, opening at 0.0839°/day; exact at
    /// 01:30:38 UT by bisection. Right before 1.3.3 as well — the guard against a fix that
    /// would call everything near exact applying.
    /// </remarks>
    [Fact]
    public void FindActiveTransits_OrbDirection_Should_BeSeparating_When_SaturnPerfectedHoursBeforeReportStart()
    {
        DateOnly start = new(2026, 6, 1);
        DateOnly end = new(2026, 7, 2);
        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Moon", Symbol = "MO", Longitude = 282.249857577705, HouseNumber = 7 },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        ActiveTransit square = transits.Single(t => t.AspectName == "Square");
        square.OrbAtStartDegrees.Should().BeApproximately(0.0367666, 1e-6, "the orb Tier B measures at 12:00 UT");
        square.OrbDirection.Should().Be("separating", "the square perfected at 01:30 UT, before the report's start");
    }

    [Fact]
    public void FindActiveTransits_TransitHouseNumber_Should_BeValid()
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
                Longitude = saturnLon,
                HouseNumber = 2,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        transits.Should().NotBeEmpty();

        foreach (ActiveTransit transit in transits)
        {
            transit.TransitHouseNumber.Should().BeInRange(1, 12,
                "transit house number should be between 1 and 12");
        }
    }

    #endregion

    #region FindActiveTransits — No Aspects

    [Fact]
    public void FindActiveTransits_NoAspectsInRange_Should_ReturnEmpty()
    {
        // Place natal targets far from Saturn's current position
        // Saturn in early Aries (~5°). Target at 160° — too far for any aspect within orb.
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget
            {
                Name = "Neptune",
                Symbol = "NE",
                Longitude = 160.0,
                HouseNumber = 6,
            },
        ];

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: true, TestHouseCusps);

        transits.Should().BeEmpty(
            "no aspects should be found when natal targets are far from transit planet");
    }

    #endregion

    #region IsWithinOrb — Static Helper Tests

    [Fact]
    public void IsWithinOrb_ExactConjunction_Should_ReturnTrue()
    {
        bool result = TransitAspectFinder.IsWithinOrb(
            transitLon: 100.0, natalLon: 100.0, aspectAngle: 0.0, orb: 2.0);

        result.Should().BeTrue(
            "exact conjunction (separation = 0°) should be within a 2° orb");
    }

    [Fact]
    public void IsWithinOrb_OutsideOrb_Should_ReturnFalse()
    {
        bool result = TransitAspectFinder.IsWithinOrb(
            transitLon: 100.0, natalLon: 110.0, aspectAngle: 0.0, orb: 2.0);

        result.Should().BeFalse(
            "10° separation should be outside a 2° conjunction orb");
    }

    [Fact]
    public void IsWithinOrb_Wrap360_Should_HandleCorrectly()
    {
        bool result = TransitAspectFinder.IsWithinOrb(
            transitLon: 359.0, natalLon: 1.0, aspectAngle: 0.0, orb: 2.0);

        result.Should().BeTrue(
            "359° and 1° are 2° apart, which should be within a 2° conjunction orb");
    }

    #endregion

    #region ShortestArc — Static Helper Tests

    [Fact]
    public void ShortestArc_Should_HandleWrap()
    {
        // 1° and 359° are 2° apart (not 358°)
        double arc1 = TransitAspectFinder.ShortestArc(1.0, 359.0);
        arc1.Should().BeApproximately(2.0, 0.001,
            "ShortestArc(1°, 359°) should be 2°");

        // 180° and 0° are 180° apart
        double arc2 = TransitAspectFinder.ShortestArc(180.0, 0.0);
        arc2.Should().BeApproximately(180.0, 0.001,
            "ShortestArc(180°, 0°) should be 180°");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="TransitAspectFinder.FindActiveTransits"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any natal-target iteration runs.
    /// </summary>
    [Fact]
    public void FindActiveTransits_PreCancelledToken_Should_ThrowOperationCanceledException()
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
                Longitude = (saturnLon + 1.0) % 360.0,
                HouseNumber = 1,
            },
        ];

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps,
            cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the natal-target scan loop immediately");
    }

    #endregion

    #region FindActiveTransits — Input Validation (M-3)

    /// <summary>
    /// An inverted range (start after end) is a malformed input; the finder must
    /// reject it rather than silently producing an empty result or running a
    /// negative-day scan.
    /// </summary>
    [Fact]
    public void FindActiveTransits_InvertedRange_ThrowsArgumentException()
    {
        DateOnly start = new(2026, 4, 30);
        DateOnly end = new(2026, 4, 1);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Sun", Symbol = "SU", Longitude = 0.0, HouseNumber = 1 },
        ];

        Action act = () => _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*strictly before*");
    }

    /// <summary>
    /// A 200-year window is far larger than any legitimate transit report and
    /// would scan ~73,000 days; the finder must reject it.
    /// </summary>
    [Fact]
    public void FindActiveTransits_OversizedRange_ThrowsArgumentException()
    {
        DateOnly start = new(1900, 1, 1);
        DateOnly end = new(2100, 1, 1);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Sun", Symbol = "SU", Longitude = 0.0, HouseNumber = 1 },
        ];

        Action act = () => _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, start, end,
            includeMinorAspects: false, TestHouseCusps);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*exceeds 100 years*");
    }

    /// <summary>
    /// Sentinel boundary values (<see cref="DateOnly.MinValue"/>) almost always
    /// indicate an uninitialised variable reaching the API and must be rejected.
    /// </summary>
    [Fact]
    public void FindActiveTransits_MinValueBoundary_ThrowsArgumentException()
    {
        DateOnly end = new(2026, 4, 30);

        List<NatalTarget> natalTargets =
        [
            new NatalTarget { Name = "Sun", Symbol = "SU", Longitude = 0.0, HouseNumber = 1 },
        ];

        Action act = () => _finder.FindActiveTransits(
            TransitBody.Saturn, natalTargets, DateOnly.MinValue, end,
            includeMinorAspects: false, TestHouseCusps);

        act.Should().Throw<ArgumentException>();
    }

    #endregion
    #region Branch D — one passage over one point (T1–T4)

    /// <summary>A lifecycle event as Tier B gives it: type, instant, pass number, station type and flags.</summary>
    /// <param name="Type">The event type.</param>
    /// <param name="Utc">The instant, UT.</param>
    /// <param name="PassNumber">The pass number of an exact pass; null otherwise.</param>
    /// <param name="StationType">"Retrograde" or "Direct" for a station; null otherwise.</param>
    /// <param name="Before">Whether the event is flagged before the report period.</param>
    /// <param name="Beyond">Whether the event is flagged beyond the report period.</param>
    private sealed record ExpectedEvent(
        TransitLifecycleEventType Type,
        DateTime Utc,
        int? PassNumber = null,
        string? StationType = null,
        bool Before = false,
        bool Beyond = false);

    /// <summary>Tolerance on every Tier B instant.</summary>
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    /// <summary>An instant in UT.</summary>
    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    /// <summary>
    /// The J2000 NYC fixture's natal point of the given name as a finder target, as
    /// <see cref="TransitService"/> builds its targets: the position's longitude and house.
    /// </summary>
    /// <param name="chart">The J2000 NYC chart.</param>
    /// <param name="name">The natal body's name ("Mercury", "True Node").</param>
    /// <returns>The target.</returns>
    private static NatalTarget J2000Target(ChartResult chart, string name)
    {
        PlanetPosition position = chart.Positions.Single(p => p.Name == name);
        return new NatalTarget
        {
            Name = name,
            Symbol = name[..2].ToUpperInvariant(),
            Longitude = position.Longitude,
            HouseNumber = position.House ?? 1,
        };
    }

    /// <summary>
    /// Asserts that a lifecycle is exactly the expected events in order, each within five seconds of
    /// its Tier B instant and with the expected pass number, station type and flags.
    /// </summary>
    /// <param name="lifecycle">The transit's lifecycle.</param>
    /// <param name="expected">The expected events, in order.</param>
    private static void AssertLifecycle(IReadOnlyList<TransitLifecycleEvent> lifecycle, params ExpectedEvent[] expected)
    {
        string Describe(IEnumerable<(TransitLifecycleEventType Type, DateTime Utc)> events) =>
            string.Join(", ", events.Select(e => $"{e.Type} {e.Utc:yyyy-MM-dd HH:mm:ss}"));

        lifecycle.Should().HaveCount(expected.Length,
            $"the lifecycle should be [{Describe(expected.Select(e => (e.Type, e.Utc)))}], not [{Describe(lifecycle.Select(e => (e.EventType, e.Date)))}]");

        for (int i = 0; i < expected.Length; i++)
        {
            TransitLifecycleEvent actual = lifecycle[i];
            ExpectedEvent want = expected[i];
            string at = $"of event {i + 1}, {want.Type} {want.Utc:yyyy-MM-dd HH:mm:ss}";
            actual.EventType.Should().Be(want.Type, at);
            actual.Date.Should().BeCloseTo(want.Utc, FiveSeconds, at);
            actual.PassNumber.Should().Be(want.PassNumber, at);
            actual.StationType.Should().Be(want.StationType, at);
            actual.BeforeReportPeriod.Should().Be(want.Before, at);
            actual.BeyondReportPeriod.Should().Be(want.Beyond, at);
        }
    }

    /// <summary>
    /// T1 (R1, R2, R3). Neptune's square to natal Mercury (the point 1°53′ Aries) in a report of
    /// 2026-04-12 to 18 is one transit: one passage over the point with its five passes and its two
    /// in-orb stations, every instant Tier B's to the second. Until branch D the entry was
    /// 2026-02-24 (the walk back stopped after 90 days out of orb), there was one pass, no station,
    /// and the exit was 2026-04-19.
    /// </summary>
    [Fact]
    public void FindActiveTransits_NeptuneSquareNatalMercury_IsOnePassageWithEveryPassAndInOrbStation()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Neptune, [J2000Target(chart, "Mercury")],
            new DateOnly(2026, 4, 12), new DateOnly(2026, 4, 18),
            includeMinorAspects: false, chart.Houses.Cusps);

        ActiveTransit square = transits.Should().ContainSingle(t => t.AspectName == "Square",
            "Neptune is within 1° of the square point on every report day").Subject;

        AssertLifecycle(
            square.Lifecycle,
            new ExpectedEvent(TransitLifecycleEventType.Entry, Utc(2025, 4, 24, 12, 19, 6), Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2025, 6, 2, 8, 14, 12), PassNumber: 1, Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Station, Utc(2025, 7, 4, 21, 33, 5), StationType: "Retrograde", Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2025, 8, 7, 3, 16, 8), PassNumber: 2, Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 3, 23, 16, 3, 22), PassNumber: 3, Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 11, 11, 6, 39, 38), PassNumber: 4, Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Station, Utc(2026, 12, 12, 22, 17, 27), StationType: "Direct", Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2027, 1, 12, 22, 47, 30), PassNumber: 5, Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Exit, Utc(2027, 2, 20, 2, 20, 7), Beyond: true));
    }

    /// <summary>
    /// T2 (R1, R2, R3). Pluto's opposition to natal True Node (the point 3°57′ Aquarius) in the same
    /// report: one passage of nearly three years with three passes and the three stations at which
    /// Pluto is in orb (its retrograde station of 2027-05-08 is out of orb and absent). Until branch
    /// D the entry was 2025-12-22, the exit 2027-02-05, and no station was listed.
    /// </summary>
    [Fact]
    public void FindActiveTransits_PlutoOppositionNatalTrueNode_IsOnePassageWithEveryPassAndInOrbStation()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Pluto, [J2000Target(chart, "True Node")],
            new DateOnly(2026, 4, 12), new DateOnly(2026, 4, 18),
            includeMinorAspects: false, chart.Houses.Cusps);

        ActiveTransit opposition = transits.Should().ContainSingle(t => t.AspectName == "Opposition",
            "Pluto is within 1.5° of the opposition point on every report day").Subject;

        AssertLifecycle(
            opposition.Lifecycle,
            new ExpectedEvent(TransitLifecycleEventType.Entry, Utc(2025, 2, 13, 20, 53, 26), Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Station, Utc(2025, 5, 4, 15, 27, 22), StationType: "Retrograde", Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 2, 9, 4, 31, 41), PassNumber: 1, Before: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 8, 10, 14, 47, 14), PassNumber: 2, Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Station, Utc(2026, 10, 16, 2, 40, 17), StationType: "Direct", Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 12, 17, 19, 7, 57), PassNumber: 3, Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Station, Utc(2027, 10, 18, 3, 52, 30), StationType: "Direct", Beyond: true),
            new ExpectedEvent(TransitLifecycleEventType.Exit, Utc(2027, 12, 12, 19, 59, 3), Beyond: true));
    }

    /// <summary>
    /// T3 (R1, R2). Mars's sextile to natal Sun over 2026-01-01 to 2027-12-31 is two transits, one
    /// over the Pisces point in March 2026 and one over the Scorpio point in September 2027, each
    /// with a single pass of residual at most 0.01°; the Scorpio passage of October 2025 and the
    /// Pisces one of February 2028 have no report day in orb and are not listed. Until branch D the
    /// two were one transit, with a "pass" of about 120° where the signed separation jumped sides.
    /// </summary>
    [Fact]
    public void FindActiveTransits_MarsSextileNatalSun_OverTwoYears_IsTwoTransitsEachWithOnePass()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Mars, [J2000Target(chart, "Sun")],
            new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31),
            includeMinorAspects: false, chart.Houses.Cusps);

        List<ActiveTransit> sextiles = transits
            .Where(t => t.AspectName == "Sextile")
            .OrderBy(t => t.Lifecycle[0].Date)
            .ToList();

        sextiles.Should().HaveCount(2,
            "Mars passes the Pisces sextile point in March 2026 and the Scorpio one in September 2027, each once");

        AssertLifecycle(
            sextiles[0].Lifecycle,
            new ExpectedEvent(TransitLifecycleEventType.Entry, Utc(2026, 3, 14, 11, 42, 50)),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 3, 15, 18, 12, 12), PassNumber: 1),
            new ExpectedEvent(TransitLifecycleEventType.Exit, Utc(2026, 3, 17, 0, 42, 4)));

        AssertLifecycle(
            sextiles[1].Lifecycle,
            new ExpectedEvent(TransitLifecycleEventType.Entry, Utc(2027, 9, 16, 4, 47, 13)),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2027, 9, 17, 16, 23, 19), PassNumber: 1),
            new ExpectedEvent(TransitLifecycleEventType.Exit, Utc(2027, 9, 19, 3, 53, 2)));

        foreach (ActiveTransit sextile in sextiles)
        {
            TransitLifecycleEvent pass = sextile.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Exact);
            pass.ExactOrbDegrees.Should().NotBeNull().And.BeLessThanOrEqualTo(0.01,
                "a pass is a crossing of the transit's own point, bisected to the second");
        }
    }

    /// <summary>
    /// T4 (R2). Mars's sesquiquintile (108°) to natal Venus with minor aspects on, report 2026-03-20
    /// to 31: Mars crosses the 0.75° orb in under two days, and the pass of 2026-03-27 11:08:14 UT
    /// falls between the entry and the first in-orb noon. The scan now starts at the 12:00 UT
    /// sample before the entry instant; until branch D it started at that noon and found no pass.
    /// </summary>
    [Fact]
    public void FindActiveTransits_MarsSesquiquintileNatalVenus_FindsThePassBeforeTheFirstInOrbNoon()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        IReadOnlyList<ActiveTransit> transits = _finder.FindActiveTransits(
            TransitBody.Mars, [J2000Target(chart, "Venus")],
            new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 31),
            includeMinorAspects: true, chart.Houses.Cusps);

        ActiveTransit sesquiquintile = transits.Should().ContainSingle(t => t.AspectName == "Sesquiquintile",
            "Mars is within 0.75° of the 108° point at 12:00 UT on 2026-03-27").Subject;

        AssertLifecycle(
            sesquiquintile.Lifecycle,
            new ExpectedEvent(TransitLifecycleEventType.Entry, Utc(2026, 3, 26, 12, 10, 52)),
            new ExpectedEvent(TransitLifecycleEventType.Exact, Utc(2026, 3, 27, 11, 8, 14), PassNumber: 1),
            new ExpectedEvent(TransitLifecycleEventType.Exit, Utc(2026, 3, 28, 10, 6, 12)));
    }

    #endregion
}
