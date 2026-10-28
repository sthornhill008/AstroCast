using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests of <see cref="DailyHoroscopeService.BuildDays"/> against values from outside the code:
/// Tier B (direct Swiss Ephemeris with the files), local readings from Python <c>zoneinfo</c>
/// (tzdata 2026.5, 2026e), and pyswisseph 2.10.03 with the Swiss Ephemeris files.
/// </summary>
/// <remarks>
/// The days are built as the heads build them: the natal chart cast by the chart engine, its
/// transits from <see cref="TransitService"/>, the days on the natal zone. Branch E (1.3.3):
/// a day is its own instants, everything is filed by its instant on the local day and printed on
/// the zone's clock, the Moon's sign change is noted at any hour, and each day's orb and phase
/// are the true ones at local noon.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DailyHoroscopeServiceTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public DailyHoroscopeServiceTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    #region A day is its own instants

    /// <summary>
    /// In Havana the clocks go back from 01:00 CDT to 00:00 CST on 2026-11-01, so midnight
    /// occurs twice. The Moon is void in Cancer until it enters Leo at 04:18:11 UT, 00:18 CDT in
    /// the first, repeated hour (pyswisseph; zoneinfo). A report starting that day must keep the
    /// void, with no pad: its day begins at the first midnight (04:00 UT), not at D17's later one
    /// (05:00 UT), and the reading 00:18, which the clocks repeat, names its zone.
    /// </summary>
    [Fact]
    public void BuildDays_Havana_ReportStartingOnTheRepeatedMidnight_Should_KeepTheVoidEndingInTheFirstHour()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HavanaInput(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3)).Days;

        days[0].Date.Should().Be(new DateOnly(2026, 11, 1));
        days[0].VoidOfCoursePeriods.Should().Equal("00:00 – 00:18 CDT (Cancer)");
    }

    /// <summary>
    /// Pacific/Apia skipped 2011-12-30: the clocks went from 2011-12-29 24:00 (UTC−10) to
    /// 2011-12-31 00:00 (UTC+14), so the date has no instant (zoneinfo). The report of
    /// 2011-12-28 to 2012-01-01 has four days, and 2011-12-30 is not one of them.
    /// </summary>
    [Fact]
    public void BuildDays_Apia_ReportAcrossTheSkippedDate_Should_LeaveTheDateOut()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, ApiaInput(), new DateOnly(2011, 12, 28), new DateOnly(2012, 1, 1)).Days;

        days.Select(d => d.Date).Should().Equal(
            new DateOnly(2011, 12, 28), new DateOnly(2011, 12, 29), new DateOnly(2011, 12, 31), new DateOnly(2012, 1, 1));
    }

    #endregion

    #region Passes on their local day, in local time

    /// <summary>
    /// Person A's pass of transiting Saturn square its natal Moon is exact at 2025-02-03
    /// 00:51:11 UT (fixtures/truth/transits_hartwell.py, bisected to the second), which is
    /// 19:51:11 EST on 2025-02-02 (zoneinfo). The pass is filed on 2 February with the time
    /// 19:51, not on its UT date.
    /// </summary>
    [Fact]
    public void BuildDays_Hartwell_SaturnSquareMoonPass_Should_BeFiledOnItsLocalDayInLocalTime()
    {
        (ChartResult natal, _, IReadOnlyList<DailyHoroscopeDay> days) =
            Daily(_fixture, HartwellInput(), new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 4));

        natal.JulianDay.Should().BeApproximately(2432464.1041666665, 1e-8);
        Day(days, 2025, 2, 2).ExactToday.Where(Row("TR Saturn", "sqr", "NA Moon"))
            .Should().ContainSingle().Which.ExactTimes.Should().Equal("19:51");
        Day(days, 2025, 2, 3).ExactToday.Where(Row("TR Saturn", "sqr", "NA Moon")).Should().BeEmpty();
    }

    /// <summary>
    /// Chart 1 (the saved set's, 2026-01-01 to 2027-12-31): on 2026-11-18 transiting Mars opposes
    /// the natal Sun, exact at 17:40:35 UT, 17:40 GMT (Tier B). The transit lifecycle also reports a
    /// sextile, a square and a trine "pass" at that instant, where the signed separation jumps sign
    /// with Mars 120°, 90° and 60° from those aspects (Tier B: no sextile, square or trine pass that
    /// day). They are not passes: only the opposition is Exact.
    /// </summary>
    [Fact]
    public void BuildDays_Chart1_InventedPasses_Should_GiveNoExactRow()
    {
        (ChartResult natal, _, IReadOnlyList<DailyHoroscopeDay> days) =
            Daily(_fixture, Chart1Input(), new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        natal.JulianDay.Should().BeApproximately(2436981.14583333, 1e-8);
        DailyHoroscopeDay day = Day(days, 2026, 11, 18);
        day.ExactToday.Where(Row("TR Mars", "sxt", "NA Sun")).Should().BeEmpty();
        day.ExactToday.Where(Row("TR Mars", "sqr", "NA Sun")).Should().BeEmpty();
        day.ExactToday.Where(Row("TR Mars", "tri", "NA Sun")).Should().BeEmpty();
        day.ExactToday.Where(Row("TR Mars", "opp", "NA Sun"))
            .Should().ContainSingle().Which.ExactTimes.Should().Equal("17:40");
    }

    #endregion

    #region The Moon's sign change at any hour

    /// <summary>
    /// On 2026-10-02 the Moon enters Cancer at 19:54:04 UT (pyswisseph; Tier B 19:54:04.742),
    /// 15:54 EDT, after local noon (zoneinfo). Until 1.3.3 only a change between local midnight and
    /// noon was noted.
    /// </summary>
    [Fact]
    public void BuildDays_Hartwell_Oct2_Should_NoteTheMoonsSignChangeAfterNoon()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HartwellInput(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3)).Days;

        Day(days, 2026, 10, 2).MoonSignChange.Should().Be("Moon enters Cancer at 15:54");
    }

    /// <summary>
    /// In Havana on 2026-11-01 the Moon enters Leo at 04:18:11 UT (pyswisseph; Tier B 04:18:11.649),
    /// 00:18 CDT, in the first of the two hours whose readings the clocks repeat (zoneinfo): the note
    /// names the zone.
    /// </summary>
    [Fact]
    public void BuildDays_Havana_Nov1_Should_NoteTheSignChangeInTheRepeatedHourWithItsZone()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HavanaInput(), new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 3)).Days;

        Day(days, 2026, 11, 1).MoonSignChange.Should().Be("Moon enters Leo at 00:18 CDT");
    }

    /// <summary>
    /// London before 1583, the dates read on the Julian calendar (D21) and the clock on London's local
    /// mean time, UT−0:01:15. Tier B, Julian 1301-03-06: the Moon squares Neptune at 16:15:46.1 LMT,
    /// its last exact major aspect in Capricorn, and enters Aquarius at 17:03:12.6 LMT; Julian
    /// 1301-03-09: it enters Pisces at 03:13:20.7 LMT. The void period, the note and the day are one
    /// calendar's.
    /// </summary>
    [Fact]
    public void BuildDays_London1301_Should_NoteTheSignChangesOnTheJulianDays()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, LondonInput(), new DateOnly(1301, 3, 5), new DateOnly(1301, 3, 9)).Days;

        DailyHoroscopeDay march6 = Day(days, 1301, 3, 14);
        march6.MoonSignChange.Should().Be("Moon enters Aquarius at 17:03");
        march6.VoidOfCoursePeriods.Should().Equal("16:15 – 17:03 (Capricorn)");
        Day(days, 1301, 3, 17).MoonSignChange.Should().Be("Moon enters Pisces at 03:13");
    }

    /// <summary>
    /// A daily range across October 1582 steps the calendar in force (branch R3, 1.3.4; R-D7): typed
    /// 1582-09-25 to 1582-10-25 in London is 21 days, Julian 1582-09-25 to 10-04 then Gregorian 10-15 to
    /// 10-25, each day's Date its proleptic day and its heading the day on the calendar in force with its
    /// weekday (Tier B, r3_truth.out). Until 1.3.4 the typed days 1582-10-05 to 10-14 were read as Julian:
    /// 31 days, ten of them twice.
    /// </summary>
    [Fact]
    public void BuildDays_London1582_Should_StepTheCalendarInForce()
    {
        (_, TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) =
            Daily(_fixture, LondonInput(), new DateOnly(1582, 9, 25), new DateOnly(1582, 10, 25));

        days.Should().HaveCount(21);
        days[9].Date.Should().Be(new DateOnly(1582, 10, 14));
        days[10].Date.Should().Be(new DateOnly(1582, 10, 15));

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);
        md.Should().Contain("**Report Period:** 1582-09-25 to 1582-10-25 (21 days)");
        md.Should().Contain("### Tuesday, September 25, 1582");
        md.Should().Contain("### Thursday, October 4, 1582");
        md.Should().Contain("### Friday, October 15, 1582");
        md.Should().Contain("### Monday, October 25, 1582");
        for (int d = 5; d <= 14; d++)
        {
            md.Should().NotContain($"October {d}, 1582");
        }
    }

    /// <summary>
    /// A daily range over a Julian leap day: typed 1300-02-27 to 1300-03-02 in London is 5 days, headed
    /// Saturday 27 February to Wednesday 2 March 1300 with Monday 29 February between (Tier B,
    /// r3_truth.out). Until 1.3.4 the typed DateOnly loop had no 29 February: 4 days.
    /// </summary>
    [Fact]
    public void BuildDays_London1300_Should_IncludeTheJulianLeapDay()
    {
        (_, TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) =
            Daily(_fixture, LondonInput(), new DateOnly(1300, 2, 27), new DateOnly(1300, 3, 2));

        days.Should().HaveCount(5);

        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days);
        md.Should().Contain("### Saturday, February 27, 1300");
        md.Should().Contain("### Sunday, February 28, 1300");
        md.Should().Contain("### Monday, February 29, 1300");
        md.Should().Contain("### Tuesday, March 1, 1300");
        md.Should().Contain("### Wednesday, March 2, 1300");
    }

    #endregion

    #region Orb, phase and listing at local noon

    /// <summary>
    /// Person A, 2026-09-29: at 12:00 EDT (16:00 UT) Saturn is at 11.682737° and the natal Sun
    /// at 191.500148°, 0.1826° from the opposition, Saturn retrograde (−0.0781°/day) and
    /// closing: applying (Tier B, fixtures/truth/daily_addendum.py). Until 1.3.3 the row
    /// printed the report-start orb.
    /// </summary>
    [Fact]
    public void BuildDays_Hartwell_Sep29_SaturnOppositeSun_Should_HaveItsNoonOrbAndPhase()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HartwellInput(), new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 30)).Days;

        DailyTransitSnapshot row = Day(days, 2026, 9, 29).ActiveTransits.Where(Row("TR Saturn", "opp", "NA Sun"))
            .Should().ContainSingle().Subject;
        row.Phase.Should().Be("Applying");
        row.OrbDegrees.Should().BeApproximately(0.1826, 0.0005);
    }

    /// <summary>
    /// Person A, 2026-11-02: at 12:00 EST (17:00 UT) Uranus is at 64.613314°, speed
    /// −0.0371°/day, and the natal Mercury at 215.154232°: 0.5409° past the quincunx and
    /// widening: separating (Tier B, fixtures/truth/daily_addendum.py). Until 1.3.3 the row
    /// printed the orb and direction at the report's start.
    /// </summary>
    [Fact]
    public void BuildDays_Hartwell_Nov2_UranusQuincunxMercury_Should_HaveItsNoonOrbAndPhase()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HartwellInput(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3), minorAspects: true).Days;

        DailyTransitSnapshot row = Day(days, 2026, 11, 2).ActiveTransits.Where(Row("TR Uranus", "qnx", "NA Mercury"))
            .Should().ContainSingle().Subject;
        row.Phase.Should().Be("Separating");
        row.OrbDegrees.Should().BeApproximately(0.5409, 0.0005);
    }

    /// <summary>
    /// Person A, 2026-08-30 to 10-30 with the minor aspects: no Active
    /// row's orb exceeds its transit's limit, by the transit orb table (Jupiter and Saturn 2° for a
    /// conjunction or opposition, 1.5° for another major aspect, 1° for a minor one; the other
    /// bodies 1.5°, 1° and 0.75°). Until 1.3.3, 148 of that report's 473 rows exceeded their limit.
    /// </summary>
    [Fact]
    public void BuildDays_Hartwell_SixtyOneDays_Should_ListNoRowBeyondItsOrbLimit()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, HartwellInput(), new DateOnly(2026, 8, 30), new DateOnly(2026, 10, 30), minorAspects: true).Days;

        days.SelectMany(d => d.ActiveTransits).Should().NotBeEmpty()
            .And.OnlyContain(row => row.OrbDegrees <= OrbLimit(row.TransitPlanet, row.Aspect));
    }

    /// <summary>
    /// N1, Julian 1324-12-13 (the fixture's clock is Etc/UTC): the true node, wobbling at its
    /// station, enters a 1° sextile of the natal Moon at 01:28:41 UT and leaves it at 12:59:28
    /// UT the same day (fixtures/truth/daily_addendum.py, bisected to the second). The row is
    /// Active, entering and exiting on the one day. No fixture of this suite shows the same
    /// grazing stay in 2026-2048, so this case keeps N1 (person A was searched 2026-2048 at
    /// every orb edge: none).
    /// </summary>
    /// <remarks>
    /// The typed bounds are Julian dates (ProlepticDay), so the window Julian 1324-12-08 to
    /// 12-14 holds the graze day, whose proleptic date is Gregorian 1324-12-21; at 12:00 UT the
    /// node is 0.9998° from the sextile, inside the orb, so the transit is listed from the
    /// day's own noon sample.
    /// </remarks>
    [Fact]
    public void BuildDays_N1_Julian13241213_NodeSextileMoon_Should_EnterAndLeaveOrbTheSameDay()
    {
        IReadOnlyList<DailyHoroscopeDay> days =
            Daily(_fixture, GoldenFixtures.N1Input(), new DateOnly(1324, 12, 8), new DateOnly(1324, 12, 14)).Days;
        DailyHoroscopeDay day = Day(days, 1324, 12, 21);

        DailyTransitSnapshot row = day.ActiveTransits.Where(Row("TR North Node", "sxt", "NA Moon"))
            .Should().ContainSingle().Subject;
        row.EntryTime.Should().Be("01:28");
        row.ExitTime.Should().Be("12:59");
        day.EnteringToday.Where(Row("TR North Node", "sxt", "NA Moon")).Should().ContainSingle();
        day.ExitingToday.Where(Row("TR North Node", "sxt", "NA Moon")).Should().ContainSingle();
    }

    /// <summary>
    /// The transit orb table, typed here as an input (the app's declared table): Jupiter and Saturn
    /// 2° for a conjunction or opposition, 1.5° for another major aspect, 1° for a minor one; every
    /// other body 1.5°, 1° and 0.75°.
    /// </summary>
    /// <param name="transitPlanet">The row's planet, "TR Saturn".</param>
    /// <param name="aspect">The row's aspect name, lower case, "opposition".</param>
    /// <returns>The orb limit in degrees.</returns>
    private static double OrbLimit(string transitPlanet, string aspect)
    {
        bool slow = transitPlanet is "TR Jupiter" or "TR Saturn";
        if (aspect is "semisextile" or "semisquare" or "sesquiquintile" or "sesquiquadrate" or "quincunx")
            return slow ? 1.0 : 0.75;

        bool wide = aspect is "conjunction" or "opposition";
        return slow ? (wide ? 2.0 : 1.5) : (wide ? 1.5 : 1.0);
    }

    #endregion

    #region Charts and days

    /// <summary>
    /// Person A (invented): 1947-10-05 09:30 at an explicit UTC−5 (14:30 UT, JD
    /// 2432464.1041666665), Harrisburg, Pennsylvania; its days are on America/New_York.
    /// </summary>
    /// <returns>The chart's input.</returns>
    internal static ChartInput HartwellInput() => new()
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UseExplicitOffset = true,
        UtcOffsetHours = -5.0,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>
    /// Chart 1 of <c>timing/out/db-charts.json</c>: 1960-02-16 15:30 Europe/London (GMT, JD
    /// 2436981.14583333), the City of London.
    /// </summary>
    /// <returns>The chart's input.</returns>
    internal static ChartInput Chart1Input() => new()
    {
        Name = "Chart 1 Fixture",
        LocalDateTime = new DateTime(1960, 2, 16, 15, 30, 0),
        TimeZone = "Europe/London",
        Latitude = 51.5156177,
        Longitude = -0.0919983,
        LocationName = "City of London, Greater London, England, United Kingdom",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>A chart in Havana, whose zone repeats midnight each November.</summary>
    /// <returns>The chart's input.</returns>
    internal static ChartInput HavanaInput() => new()
    {
        Name = "Havana Fixture",
        LocalDateTime = new DateTime(1980, 3, 10, 8, 0, 0),
        TimeZone = "America/Havana",
        Latitude = 23.1136,
        Longitude = -82.3666,
        LocationName = "Havana, Cuba",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>A chart in London, whose clock before 1847 is its local mean time.</summary>
    /// <returns>The chart's input.</returns>
    internal static ChartInput LondonInput() => new()
    {
        Name = "London Fixture",
        LocalDateTime = new DateTime(1280, 6, 1, 12, 0, 0),
        TimeZone = "Europe/London",
        Latitude = 51.5072,
        Longitude = -0.1276,
        LocationName = "London, England",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>A chart in Apia, whose zone skipped 2011-12-30.</summary>
    /// <returns>The chart's input.</returns>
    internal static ChartInput ApiaInput() => new()
    {
        Name = "Apia Fixture",
        LocalDateTime = new DateTime(1990, 6, 15, 12, 0, 0),
        TimeZone = "Pacific/Apia",
        Latitude = -13.8333,
        Longitude = -171.7667,
        LocationName = "Apia, Samoa",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>
    /// The daily horoscope's days for a chart, as the heads build them: the chart cast by the
    /// chart engine, its transits for the report (progressions off), the days on its zone.
    /// </summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    /// <param name="natalInput">The natal chart's input.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <param name="minorAspects">Whether the transits include the minor aspects.</param>
    /// <returns>The natal chart, the transit result and the days.</returns>
    internal static (ChartResult Natal, TransitResult Result, IReadOnlyList<DailyHoroscopeDay> Days) Daily(
        EphemerisFixture fixture, ChartInput natalInput, DateOnly start, DateOnly end, bool minorAspects = false)
    {
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(natalInput);
        TransitResult result = new TransitService(fixture.EphemerisService, new LoggerConfiguration().CreateLogger())
            .Calculate(new TransitInput
            {
                NatalChart = natal,
                ReportStartDate = start,
                ReportEndDate = end,
                IncludeMinorAspects = minorAspects,
                IncludeSolarArcs = false,
                IncludeSecondaryProgressions = false,
            });
        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(fixture.EphemerisService).BuildDays(result, natal.Input.TimeZone);
        return (natal, result, days);
    }

    /// <summary>The day of a date.</summary>
    /// <param name="days">The days.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day of the month.</param>
    /// <returns>The one day with that date.</returns>
    internal static DailyHoroscopeDay Day(IReadOnlyList<DailyHoroscopeDay> days, int year, int month, int day) =>
        days.Single(d => d.Date == new DateOnly(year, month, day));

    /// <summary>A predicate matching a transit's snapshot by its printed planet, aspect and target.</summary>
    /// <param name="transitPlanet">"TR Saturn".</param>
    /// <param name="aspectAbbrev">"sqr".</param>
    /// <param name="natalTarget">"NA Moon".</param>
    /// <returns>The predicate.</returns>
    internal static Func<DailyTransitSnapshot, bool> Row(string transitPlanet, string aspectAbbrev, string natalTarget) =>
        s => s.TransitPlanet == transitPlanet && s.AspectAbbrev == aspectAbbrev && s.NatalTarget == natalTarget;

    #endregion
}
