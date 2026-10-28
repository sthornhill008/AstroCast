using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Engine-level tests of the horary judgments that depend on timing —
/// perfection, refranation and prohibition — on real charts through
/// <see cref="ChartEngine.Calculate"/>, so the assertions hold for the same
/// path the export uses.
/// </summary>
/// <remarks>
/// <para>
/// The saved horaries are invented questions at Columbus, Ohio, each
/// reproducing the mechanism of a row once taken from the app database
/// (branch Fixtures). Reference times were measured by an independent
/// hourly scan and bisection on pyswisseph longitudes
/// (fixtures/truth/out/horary-*.out): Mercury conjunct Venus 2026-10-07
/// 00:10:23 UT; Mars square Mercury 2026-10-02 09:14:22 UT; Venus square
/// Mars 2026-07-29 07:09:50 UT; Saturn sextile Mars 2026-07-19 18:10:55 UT.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class HoraryPerfectionTimingTests
{
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a ChartEngine.</summary>
    public HoraryPerfectionTimingTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    private static DateTime Utc(int y, int mo, int d, int h, int mi, int s) => new(y, mo, d, h, mi, s, DateTimeKind.Utc);

    private static HoraryPerfectionResult HoraryOf(ChartResult chart) =>
        chart.HoraryPerfection ?? throw new InvalidOperationException("the engine stored no horary analysis");

    /// <summary>Invented horary (Id 96's mechanism): Venus L1, Mercury Lx; a direct conjunction perfecting;
    /// a Venus station that is a note, not a refranation; a Mars prohibition; hostile reception.</summary>
    private ChartResult LakeTrip1001() => _engine.Calculate(new ChartInput
    {
        Name = "Lake trip 1001",
        LocalDateTime = new DateTime(2026, 10, 1, 7, 30, 0),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Is tomorrow a good day to drive to the lake?",
        HoraryCategory = "Travel",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    /// <summary>Invented horary (Id 42's mechanism, Placidus by default): Saturn's sextile to Mars first,
    /// a prohibition on L1.</summary>
    private ChartResult LunchQuestion0716() => _engine.Calculate(new ChartInput
    {
        Name = "Lunch question 0716",
        LocalDateTime = new DateTime(2026, 7, 16, 16, 22, 57),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Will my friend join me for lunch tomorrow?",
        HoraryCategory = "Friends",
    });

    #region Id 96's mechanism — a station that does not prevent; a real prohibition by Mars

    [Fact]
    public void LakeTrip_DirectAspect_ShouldPerfectAtTheMeasuredInstant()
    {
        HoraryPerfectionResult hp = HoraryOf(LakeTrip1001());

        hp.QuerentSignificator.Should().Be("Venus");
        hp.QuesitedSignificator.Should().Be("Mercury");
        hp.IsPerfected.Should().BeTrue();
        hp.PerfectionMechanism.Should().Be("Direct Aspect");
        hp.PerfectionAspect.Should().Be("Conjunction");
        hp.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 10, 7, 0, 10, 23), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void LakeTrip_VenusStationsFirst_ShouldBeANoteNotARefranation()
    {
        HoraryPerfectionResult hp = HoraryOf(LakeTrip1001());

        hp.DenialReasons.Should().NotContain(r => r.Contains("efranation"),
            "Venus turns retrograde before exactness but the conjunction still completes");
        hp.Notes.Should().ContainSingle(n => n.Contains("Venus") && n.Contains("retrograde") && n.Contains("still"));
    }

    [Fact]
    public void LakeTrip_MarsSquaresMercuryFirst_ShouldBeAProhibitionWithItsTime()
    {
        HoraryPerfectionResult hp = HoraryOf(LakeTrip1001());

        string reason = hp.DenialReasons.Should().ContainSingle(r => r.StartsWith("Prohibition by Mars")).Subject;
        reason.Should().Contain("square").And.Contain("Mercury (Lx)");
        reason.Should().NotContain("completes its aspect to Mercury", "Mercury is the body applying to Mars");
        hp.ProhibitionEvents.Should().ContainSingle(p => p.Malefic == "Mars").Which
            .ExactTimeUtc.Should().BeCloseTo(Utc(2026, 10, 2, 9, 14, 0), TimeSpan.FromSeconds(60));
        hp.Judgment.Should().Be("Uncertain");
    }

    #endregion

    #region Id 42's mechanism — a prohibition on the querent's side

    [Fact]
    public void LunchQuestion0716_DirectAspect_ShouldPerfectAtTheMeasuredInstant()
    {
        HoraryPerfectionResult hp = HoraryOf(LunchQuestion0716());

        hp.QuerentSignificator.Should().Be("Mars");
        hp.QuesitedSignificator.Should().Be("Venus");
        hp.IsPerfected.Should().BeTrue();
        hp.PerfectionMechanism.Should().Be("Direct Aspect");
        hp.PerfectionAspect.Should().Be("Square");
        hp.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 7, 29, 7, 9, 0), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void LunchQuestion0716_SaturnSextilesMarsFirst_ShouldBeAProhibitionOnL1()
    {
        HoraryPerfectionResult hp = HoraryOf(LunchQuestion0716());

        string reason = hp.DenialReasons.Should().ContainSingle(r => r.StartsWith("Prohibition by Saturn")).Subject;
        reason.Should().Contain("sextile").And.Contain("Mars (L1)");
        hp.ProhibitionEvents.Should().ContainSingle(p => p.Malefic == "Saturn").Which
            .ExactTimeUtc.Should().BeCloseTo(Utc(2026, 7, 19, 18, 10, 0), TimeSpan.FromSeconds(60));
        hp.Judgment.Should().Be("Uncertain");
    }

    #endregion

    #region Real moments found by the harness search (Richmond, Regiomontanus, UT wall clock)

    private ChartResult RichmondAt(DateTime utcWallClock, string category) => _engine.Calculate(new ChartInput
    {
        Name = $"{category} {utcWallClock:yyyy-MM-dd HH:mm}",
        LocalDateTime = utcWallClock,
        TimeZone = "Etc/UTC",
        Latitude = 37.5385087,
        Longitude = -77.4342800,
        LocationName = "Richmond, Virginia",
        EventType = "Horary",
        HoraryQuestion = "search",
        HoraryCategory = category,
        HouseSystem = HouseSystem.Regiomontanus,
    });

    [Fact]
    public void TrueRefranation_MercuryStationsDirectAndTheSquareFails_ShouldBeDeniedAndJudgedNo()
    {
        // 2026-07-21 06:00 UT, Career: Mercury (L1) applies to a square with Saturn (Lx), orb
        // 1.93°. Mercury stations direct 2026-07-23 22:57 UT; the orb turns around at 1.57° on
        // 2026-07-24 and never reaches exact. Saturn then stations retrograde on 2026-07-26
        // 19:56:30 UT — after the turnaround, but before the square's linear perfection estimate
        // (2026-07-28 22:16:47 UT) — so it is a second refranation. Nothing else perfects and
        // nothing else denies.
        HoraryPerfectionResult hp = HoraryOf(RichmondAt(new DateTime(2026, 7, 21, 6, 0, 0), "Career"));

        hp.QuerentSignificator.Should().Be("Mercury");
        hp.QuesitedSignificator.Should().Be("Saturn");
        hp.IsPerfected.Should().BeFalse("the application turns around before exact");
        hp.PerfectionTimeUtc.Should().BeNull();
        hp.ApplicationClosestOrb.Should().BeApproximately(1.57, 0.05);
        hp.ApplicationFailsTimeUtc.Should().BeCloseTo(Utc(2026, 7, 24, 0, 0, 0), TimeSpan.FromHours(2));
        hp.DenialReasons.Should().HaveCount(2);
        hp.DenialReasons[0].Should().StartWith("Refranation").And.Contain("Mercury (L1)").And.Contain("stations direct");
        hp.DenialReasons[1].Should().Be(
            "Refranation — Saturn (Lx) stations retrograde on 2026-07-26 19:56 (Etc/UTC), after the Mercury–Saturn " +
            "square turns around at 1°34′ on 2026-07-24 00:10 (Etc/UTC) but before its linear perfection estimate of " +
            "2026-07-28 22:16 (Etc/UTC).");
        hp.SignificatorStations.Should().ContainSingle(s => s.Significator == "Mercury" && !s.IsRetrogradeStation).Which
            .TimeUtc.Should().BeCloseTo(Utc(2026, 7, 23, 22, 57, 0), TimeSpan.FromSeconds(60));
        hp.SignificatorStations.Should().ContainSingle(s => s.Significator == "Saturn" && s.IsRetrogradeStation).Which
            .TimeUtc.Should().BeCloseTo(Utc(2026, 7, 26, 19, 56, 30), TimeSpan.FromSeconds(60));
        hp.Judgment.Should().Be("No");
    }

    [Fact]
    public void NoDirectAspect_SignificatorNearStation_ShouldNotBeRefranationAndStayUncertain()
    {
        // 2026-02-24 21:00 UT, Money: Sun (L1) and Mercury (Lx, +0.23°/day, 2 d before its station
        // retrograde) have no applying major aspect. The speed rule alone turned this into "No".
        HoraryPerfectionResult hp = HoraryOf(RichmondAt(new DateTime(2026, 2, 24, 21, 0, 0), "Money"));

        hp.QuerentSignificator.Should().Be("Sun");
        hp.QuesitedSignificator.Should().Be("Mercury");
        hp.IsPerfected.Should().BeFalse();
        hp.DenialReasons.Should().BeEmpty("there is no aspect for a station to refranate");
        hp.Judgment.Should().Be("Uncertain");
        hp.JudgmentNote.Should().Contain("No applying aspect");
    }

    [Fact]
    public void SunAsL1_MarsConjoinsTheSunBeforeTheOppositionPerfects_ShouldBeAProhibition()
    {
        // 2026-01-06 00:00 UT, Travel: Sun (L1) applies to an opposition with Jupiter (Lx),
        // perfecting 2026-01-10 08:42:10 UT. Mars conjoins the Sun 2026-01-09 11:41:13 UT, before
        // that. The old orb-and-speed test could never fire with the Sun as L1.
        HoraryPerfectionResult hp = HoraryOf(RichmondAt(new DateTime(2026, 1, 6, 0, 0, 0), "Travel"));

        hp.QuerentSignificator.Should().Be("Sun");
        hp.QuesitedSignificator.Should().Be("Jupiter");
        hp.IsPerfected.Should().BeTrue();
        hp.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 1, 10, 8, 42, 10), TimeSpan.FromSeconds(60));
        string reason = hp.DenialReasons.Should().ContainSingle(r => r.StartsWith("Prohibition by Mars")).Subject;
        reason.Should().Contain("conjunction").And.Contain("Sun (L1)");
        hp.ProhibitionEvents.Should().ContainSingle(p => p.Malefic == "Mars").Which
            .ExactTimeUtc.Should().BeCloseTo(Utc(2026, 1, 9, 11, 41, 13), TimeSpan.FromSeconds(60));
        hp.Judgment.Should().Be("Uncertain");
    }

    #endregion

    #region Intermediaries — the traditional seven, retrograde ones named (Ids 3's and 93's mechanisms)

    /// <summary>Invented horary (Id 3's mechanism): the Moon and the Sun apply only to Chiron, which
    /// cannot collect; the Moon void of course; judged No.</summary>
    private ChartResult RefundEnquiry0617() => _engine.Calculate(new ChartInput
    {
        Name = "Refund enquiry 0617",
        LocalDateTime = new DateTime(2026, 6, 17, 7, 17, 30),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Will the refund arrive without my chasing it?",
        HoraryCategory = "Money",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    /// <summary>Invented horary (Id 93's mechanism): Jupiter and Mercury both apply to retrograde
    /// Saturn, which collects; judged Yes.</summary>
    private ChartResult DinnerQuestion0816() => _engine.Calculate(new ChartInput
    {
        Name = "Dinner question 0816",
        LocalDateTime = new DateTime(2026, 8, 16, 20, 50, 43),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Will we meet for dinner this month?",
        HoraryCategory = "Relationship",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    [Fact]
    public void RefundEnquiry0617_ChironCannotCollect_ShouldBeNotPerfectedAndJudgedNo()
    {
        // The Moon (L1) and the Sun (Lx) both apply to Chiron, which is slower than either,
        // and to no traditional planet. Chiron is not one of the seven, so there is no
        // collection; with the Moon void of course the judgment is No.
        ChartResult chart = RefundEnquiry0617();
        HoraryPerfectionResult hp = HoraryOf(chart);

        hp.QuerentSignificator.Should().Be("Moon");
        hp.QuesitedSignificator.Should().Be("Sun");
        hp.IsPerfected.Should().BeFalse("Chiron cannot collect the light");
        hp.PerfectionMechanism.Should().BeNull();
        hp.Collector.Should().BeNull();
        hp.DenialReasons.Should().ContainSingle().Which.Should().StartWith("Moon Void of Course");
        hp.Judgment.Should().Be("No");
        ChartMarkdownFormatter.ToMarkdown(chart).Should().NotContain("Collection of Light");
    }

    [Fact]
    public void DinnerQuestion0816_RetrogradeSaturnCollects_ShouldBeNamedInTheNotes()
    {
        // Jupiter (L1) and Mercury (Lx) both apply to Saturn, retrograde at −0.035°/day. A
        // retrograde planet may collect: the collection stands (the Jupiter–Saturn trine and
        // the Mercury–Saturn opposition both perfect), and the Notes name the retrograde
        // collector.
        HoraryPerfectionResult hp = HoraryOf(DinnerQuestion0816());

        hp.PerfectionMechanism.Should().Be("Collection of Light");
        hp.Collector.Should().Be("Saturn");
        hp.DenialReasons.Should().BeEmpty();
        hp.Notes.Should().ContainSingle().Which.Should().Be("The light is collected by retrograde Saturn.");
        hp.Judgment.Should().Be("Yes");
    }

    #endregion

    #region Perfection before either significator leaves its sign (Id 8's mechanism)

    /// <summary>Invented horary (Id 8's mechanism): a sextile exact only after both significators change
    /// sign, with no reception; Mars in Venus's detriment, hostile from the quesited's side.</summary>
    private ChartResult BrunchQuestion0508() => _engine.Calculate(new ChartInput
    {
        Name = "Brunch question 0508",
        LocalDateTime = new DateTime(2026, 5, 8, 17, 37, 2),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Should I invite my neighbours to brunch?",
        HoraryCategory = "Relationship",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    [Fact]
    public void BrunchQuestion0508_SextilePerfectsAfterBothChangeSign_ShouldBeUncertainWithTheSignNote()
    {
        // Venus (L1) in Gemini applies to a sextile with Mars (Lx) in Aries, exact 2026-05-19
        // 05:37 UT — after Mars enters Taurus (05-18 22:25 UT) and Venus enters Cancer (05-19
        // 01:05 UT). Neither is in the other's domicile or exaltation, so there is no reception
        // to excuse it: the aspect does not perfect, nothing else does, and nothing denies.
        HoraryPerfectionResult hp = HoraryOf(BrunchQuestion0508());

        hp.QuerentSignificator.Should().Be("Venus");
        hp.QuesitedSignificator.Should().Be("Mars");
        hp.IsPerfected.Should().BeFalse("the sextile completes only after both significators change sign");
        hp.PerfectionMechanism.Should().BeNull();
        hp.PerfectionTimeUtc.Should().BeNull();
        hp.Notes.Should().ContainSingle().Which.Should().Be(
            "The Venus–Mars sextile is exact on 2026-05-19 01:37 (America/New_York), only after Mars enters " +
            "Taurus on 2026-05-18 18:25 (America/New_York) and Venus enters Cancer on 2026-05-18 21:05 " +
            "(America/New_York); with no reception between the significators it does not perfect.");
        hp.DenialReasons.Should().BeEmpty();
        hp.Judgment.Should().Be("Uncertain");
        hp.JudgmentNote.Should().StartWith("The applying Venus–Mars sextile perfects only after a significator leaves its sign");
    }

    #endregion

    #region Reception judged in both directions (Ids 8's, 96's and 14's mechanisms)

    /// <summary>Invented horary (Id 14's mechanism): favoured one way and hostile the other, a mixed
    /// reception; judged No.</summary>
    private ChartResult HouseSale0605() => _engine.Calculate(new ChartInput
    {
        Name = "House sale 0605",
        LocalDateTime = new DateTime(2026, 6, 5, 18, 45, 5),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio, United States",
        EventType = "Horary",
        HoraryQuestion = "Will the house sale close?",
        HoraryCategory = "General",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    [Fact]
    public void BrunchQuestion0508_MarsInVenussDetriment_ShouldBeHostileFromTheQuesitedSide()
    {
        // Mars (Lx) in Aries is in Venus's (L1's) detriment, while Venus in Gemini is in neither
        // Mars's dignity nor his debility. Only the L1 → Lx direction used to be tested, so the
        // line read Neutral; the hostility now shows, named from the quesited's side.
        HoraryPerfectionResult hp = HoraryOf(BrunchQuestion0508());

        hp.ReceptionQuality.Should().Be("Hostile");
        hp.ReceptionDetail.Should().Be(
            "Mars is in a sign of Venus's detriment or fall (Aries) — the quesited is averse to the querent.");
        hp.Judgment.Should().Be("Uncertain");
    }

    [Fact]
    public void LakeTrip_MercuryInVenussDetriment_ShouldBeHostileFromTheQuesitedSide()
    {
        // Mercury (Lx) in Scorpio is in Venus's (L1's) detriment; Venus in Libra, her own sign,
        // is in neither Mercury's dignity nor his debility.
        HoraryPerfectionResult hp = HoraryOf(LakeTrip1001());

        hp.ReceptionQuality.Should().Be("Hostile");
        hp.ReceptionDetail.Should().Be(
            "Mercury is in a sign of Venus's detriment or fall (Scorpio) — the quesited is averse to the querent.");
        hp.Judgment.Should().Be("Uncertain");
    }

    [Fact]
    public void HouseSale0605_FavouredOneWayHostileTheOther_ShouldShowMixedTestimony()
    {
        // Mars (L1) in Taurus is in Venus's (Lx's) domicile — favoured — while Venus in Cancer is
        // in Mars's fall — hostile. "Querent favoured" used to win outright; now neither
        // direction outranks the other, and both are shown.
        HoraryPerfectionResult hp = HoraryOf(HouseSale0605());

        hp.QuerentSignificator.Should().Be("Mars");
        hp.QuesitedSignificator.Should().Be("Venus");
        hp.ReceptionQuality.Should().Be("Mixed");
        hp.ReceptionDetail.Should().Be(
            "Mars is in Venus's domicile (Taurus) — querent is in quesited's power; " +
            "Venus is in a sign of Mars's detriment or fall (Cancer) — the quesited is averse to the querent.");
        hp.Judgment.Should().Be("No");
        hp.Notes.Should().NotContain(n => n.StartsWith("Category", StringComparison.Ordinal),
            "General is a recognised category");
    }

    #endregion

    #region A failing application timed to the minute (the horary review's demonstration chart)

    /// <summary>
    /// The horary review's demonstration chart: Richmond, 2026-10-20 03:00
    /// America/New_York, Regiomontanus, Relationship.
    /// </summary>
    private ChartResult RichmondDemonstration() => _engine.Calculate(new ChartInput
    {
        Name = "demo",
        LocalDateTime = new DateTime(2026, 10, 20, 3, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 37.5385087,
        Longitude = -77.43428,
        LocationName = "Richmond, Virginia, United States",
        EventType = "Horary",
        HoraryQuestion = "demonstration chart (synthetic)",
        HoraryCategory = "Relationship",
        HouseSystem = HouseSystem.Regiomontanus,
    });

    [Fact]
    public void FailedApplication_MercurySquareJupiterTurnsAround_ShouldReportTheTurnaroundToTheMinute()
    {
        // Mercury (L1) applies to a square with Jupiter (Lx) and the orb turns around without
        // reaching exact. An independent scan puts the minimum at 2.27° on 2026-10-23 04:40:28 UT
        // (00:40 EDT). The hourly sample nearest it is 05:00 UT (01:00 EDT), which is the time an
        // unrefined turnaround reports. Mercury's station after the turnaround makes the failure a
        // refranation, so the minute is read from its denial reason rather than from a note.
        HoraryPerfectionResult hp = HoraryOf(RichmondDemonstration());

        hp.QuerentSignificator.Should().Be("Mercury");
        hp.QuesitedSignificator.Should().Be("Jupiter");
        hp.ApplyingAspect.Should().Be("Square");
        hp.IsPerfected.Should().BeFalse("the application turns around before exact");
        hp.ApplicationClosestOrb.Should().BeApproximately(2.27, 0.005);
        hp.ApplicationFailsTimeUtc.Should().BeCloseTo(Utc(2026, 10, 23, 4, 40, 28), TimeSpan.FromMinutes(1));
        hp.DenialReasons.Should().ContainSingle(r => r.Contains("turns around at 2°16′ on 2026-10-23 00:40 (America/New_York)"));
    }

    [Fact]
    public void RichmondDemonstration_MercuryStationsBeforeTheLinearEstimate_ShouldBeRefranation()
    {
        // Mercury stations retrograde on 2026-10-24 07:12:43 UT (03:12 EDT), 26½ hours after the
        // square turns around — outside the window between the chart and the turnaround. At the
        // chart's separation and relative speed the square would have perfected on 2026-10-29
        // 03:37 UT (10-28 23:37 EDT), and the station falls before that: the failure is
        // refranation, naming the station, and the turnaround note no longer appears.
        HoraryPerfectionResult hp = HoraryOf(RichmondDemonstration());

        hp.DenialReasons.Should().ContainSingle().Which.Should().Be(
            "Refranation — Mercury (L1) stations retrograde on 2026-10-24 03:12 (America/New_York), after the " +
            "Mercury–Jupiter square turns around at 2°16′ on 2026-10-23 00:40 (America/New_York) but before its " +
            "linear perfection estimate of 2026-10-28 23:37 (America/New_York).");
        hp.Notes.Should().NotContain(n => n.Contains("neither significator stationing"));
        HorarySignificatorStation station = hp.SignificatorStations.Should().ContainSingle().Subject;
        station.Significator.Should().Be("Mercury");
        station.IsRetrogradeStation.Should().BeTrue();
        station.TimeUtc.Should().BeCloseTo(Utc(2026, 10, 24, 7, 12, 43), TimeSpan.FromSeconds(60));
        hp.Judgment.Should().Be("No");
    }

    #endregion

    #region Culture independence

    // The notes and denial reasons are built with the invariant culture, so their
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="TrueRefranation_MercuryStationsDirectAndTheSquareFails_ShouldBeDeniedAndJudgedNo"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TrueRefranation_MercuryStationsDirectAndTheSquareFails_ShouldBeDeniedAndJudgedNo_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TrueRefranation_MercuryStationsDirectAndTheSquareFails_ShouldBeDeniedAndJudgedNo();
    }

    /// <summary>
    /// <see cref="BrunchQuestion0508_SextilePerfectsAfterBothChangeSign_ShouldBeUncertainWithTheSignNote"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void BrunchQuestion0508_SextilePerfectsAfterBothChangeSign_ShouldBeUncertainWithTheSignNote_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        BrunchQuestion0508_SextilePerfectsAfterBothChangeSign_ShouldBeUncertainWithTheSignNote();
    }

    /// <summary>
    /// <see cref="FailedApplication_MercurySquareJupiterTurnsAround_ShouldReportTheTurnaroundToTheMinute"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void FailedApplication_MercurySquareJupiterTurnsAround_ShouldReportTheTurnaroundToTheMinute_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        FailedApplication_MercurySquareJupiterTurnsAround_ShouldReportTheTurnaroundToTheMinute();
    }

    /// <summary>
    /// <see cref="RichmondDemonstration_MercuryStationsBeforeTheLinearEstimate_ShouldBeRefranation"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void RichmondDemonstration_MercuryStationsBeforeTheLinearEstimate_ShouldBeRefranation_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        RichmondDemonstration_MercuryStationsBeforeTheLinearEstimate_ShouldBeRefranation();
    }

    #endregion
}
