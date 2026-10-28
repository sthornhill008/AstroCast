using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using static AstroCast.Tests.Services.VoidOfCourseDefinitionTests;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the range <see cref="VoidOfCourseScanner.Scan"/> answers for (branch C, 1.3.3): every
/// void period that touches the range, end after its start and start before its end, each with
/// its true start and end, in time order, once each; and nothing read outside the span the
/// shipped ephemeris files cover.
/// </summary>
/// <remarks>
/// <para>
/// A stay under way at the range's start is searched from its own sign entry, and one under way
/// at its end to its own sign exit, so no caller pads or looks back. Until 1.3.3 a period that
/// straddled either end of the range was dropped: the daily horoscope padded its scan by three
/// days for that reason, and the electional search, which did not, never disqualified a void
/// already under way at the range's start.
/// </para>
/// <para>
/// The span (<see cref="EphemerisSpan"/>): a range partly outside is cut to it and a range wholly
/// outside has no period, neither raising. A stay that begins before its first instant is searched
/// from there, and is void from there if it makes no aspect after it; a stay whose exit lies after
/// its last instant has no period, its last aspect being unknowable. The library is stateful, so
/// the span tests also run alone in a fresh process.
/// </para>
/// <para>
/// Expected instants are Tier B (<c>c-voc/truth/figures.py</c> and <c>span_edges.py</c>: direct
/// <c>swe_calc_ut</c> calls, bisected to 1e-9 day, the edge ones reading only inside the span),
/// asserted as in <see cref="VoidOfCourseDefinitionTests"/>.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class VoidOfCourseRangeTests
{
    private const string NewYork = "America/New_York";

    private readonly EphemerisFixture _fixture;
    private readonly EphemerisService _ephemeris;
    private readonly VoidOfCourseScanner _scanner;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public VoidOfCourseRangeTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _ephemeris = fixture.EphemerisService;
        _scanner = new VoidOfCourseScanner(_ephemeris);
    }

    /// <summary>
    /// A scan from 2026-04-25 00:00 New York starts inside the Leo void, which began at the
    /// Moon's trine to Mercury at 18:20:59 on the 24th: the period is reported with that start.
    /// </summary>
    [Fact]
    public void Scan_StartingInsideAPeriod_ReturnsItWithItsTrueStart()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(
            LocalJd("2026-04-25", 0), LocalJd("2026-04-26", 0), NewYork);

        VoidOfCoursePeriod leo = periods.Should().ContainSingle().Which;
        leo.MoonSign.Should().Be("Leo");
        ShouldBeTierB(leo.StartLocal, "2026-04-24 18:20:59.231", "the Leo void began before the range, at the Moon's last aspect in Leo");
        ShouldBeTierB(leo.EndLocal, "2026-04-25 21:04:26.544", "the Leo void ends when the Moon leaves Leo");
    }

    /// <summary>
    /// The Doctor Visit golden's range, 2026-06-11 to 2026-06-15 00:00 New York, ends inside the
    /// Gemini void (the Moon's conjunction with the Sun at 22:54:10 on the 14th, to its exit at
    /// 08:14:14 on the 15th): the period is reported with its true end, after the three stays the
    /// range holds.
    /// </summary>
    [Fact]
    public void Scan_EndingInsideAPeriod_ReturnsItWithItsTrueEnd()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(
            LocalJd("2026-06-11", 0), LocalJd("2026-06-15", 0), NewYork);

        periods.Select(p => p.MoonSign).Should().Equal(["Aries", "Taurus", "Gemini"], "every period touching the range, in time order");
        ShouldBeTierB(periods[0].StartLocal, "2026-06-11 04:22:14.284", "the Aries void starts at the Moon's square to Venus");
        ShouldBeTierB(periods[1].StartLocal, "2026-06-13 03:30:12.003", "the Taurus void starts at the Moon's sextile to Jupiter");
        ShouldBeTierB(periods[2].StartLocal, "2026-06-14 22:54:10.452", "the Gemini void starts at the Moon's conjunction with the Sun");
        ShouldBeTierB(periods[2].EndLocal, "2026-06-15 08:14:14.034", "the Gemini void runs past the range to the Moon's exit from Gemini");
    }

    /// <summary>A scan wholly inside the Leo void of 24–25 April 2026 returns that period, whole.</summary>
    [Fact]
    public void Scan_WhollyInsideAPeriod_ReturnsThatPeriod()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(
            LocalJd("2026-04-25", 6), LocalJd("2026-04-25", 12), NewYork);

        VoidOfCoursePeriod leo = periods.Should().ContainSingle().Which;
        leo.MoonSign.Should().Be("Leo");
        ShouldBeTierB(leo.StartLocal, "2026-04-24 18:20:59.231", "the period keeps its true start");
        ShouldBeTierB(leo.EndLocal, "2026-04-25 21:04:26.544", "the period keeps its true end");
    }

    /// <summary>
    /// 2026-04-24 00:00 to 12:00 New York lies between the Cancer void, which ended at 15:40 on the
    /// 23rd, and the Leo void, which began at 18:20 on the 24th: no period touches it.
    /// </summary>
    [Fact]
    public void Scan_InsideNoPeriod_ReturnsNone()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(
            LocalJd("2026-04-24", 0), LocalJd("2026-04-24", 12), NewYork);

        periods.Should().BeEmpty("the Moon is in Leo and has aspects left to make all morning");
    }

    /// <summary>
    /// Fifty windows across 2026 (UTC, a fixed seed, a quarter-hour to a week long, starting on a
    /// whole minute): each scan returns exactly the year's periods that touch the window, start
    /// and end to the second. A stay is computed the same whichever scan reaches it.
    /// </summary>
    [Fact]
    public void Scan_AnyWindow_EqualsTheYearsPeriodsThatTouchIt()
    {
        double yearStart = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double yearEnd = _ephemeris.DateToJulianDay(2027, 1, 1, 0.0);
        IReadOnlyList<VoidOfCoursePeriod> year = _scanner.Scan(yearStart, yearEnd, "UTC");
        Random random = new(20261009);

        for (int i = 0; i < 50; i++)
        {
            double fromJd = yearStart + (random.Next(0, 358 * 1440) / 1440.0);
            double toJd = fromJd + (random.Next(15, 7 * 1440) / 1440.0);
            DateTime from = JulianDayUtility.ToDateTimeUtc(_ephemeris, fromJd);
            DateTime to = JulianDayUtility.ToDateTimeUtc(_ephemeris, toJd);

            IReadOnlyList<VoidOfCoursePeriod> window = _scanner.Scan(fromJd, toJd, "UTC");

            window.Select(Key).Should().Equal(
                year.Where(p => p.EndLocal > from && p.StartLocal < to).Select(Key),
                "window {0} ({1:yyyy-MM-dd HH:mm} to {2:yyyy-MM-dd HH:mm} UTC) touches those periods of the year", i, from, to);
        }
    }

    /// <summary>
    /// The known issue "ElectionalService void-of-course look-back": a search from 2026-04-25 in New
    /// York starts inside the Leo void, so every sample from local midnight to the Moon's exit at
    /// 21:04 is disqualified. Until 1.3.3 the scanner dropped that period and the search offered
    /// moments in it.
    /// </summary>
    [Fact]
    public void ElectionalSearch_StartingInsideAVoid_DisqualifiesItsSamples()
    {
        ElectionalService service = new(_ephemeris, GoldenFixtures.CreateChartEngine(_fixture));
        ElectionalResult result = service.Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 25),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            TimeZone = NewYork,
            CandidateCount = 3,
        });

        VoidOfCoursePeriod leo = result.VoidOfCoursePeriods.Should().ContainSingle().Which;
        ShouldBeTierB(leo.StartLocal, "2026-04-24 18:20:59.231", "the search sees the Leo void that began before its range");
        ShouldBeTierB(leo.EndLocal, "2026-04-25 21:04:26.544", "the Leo void ends when the Moon leaves Leo");
        result.DisqualifiedPeriods.Should().Contain(d => d.Start == leo.StartLocal && d.End == leo.EndLocal,
            "the void disqualifies the moments it covers");
        result.Candidates.Should().NotBeEmpty("the evening after the Moon enters Virgo is open");
        result.Candidates.Should().OnlyContain(c => c.LocalDateTime >= leo.EndLocal,
            "no moment from local midnight to 21:04, while the Moon is void in Leo, may be offered");
    }

    /// <summary>
    /// A scan from two days before the span's first instant to four days after it reads the sky only
    /// inside the span. The Moon is in Gemini at the first instant (1200-01-08 04:54:45 on the
    /// proleptic Gregorian calendar) and makes no exact aspect from there to its exit at 13:45:18,
    /// so it is void from the first instant; Cancer's void follows. Leo's begins after the range.
    /// </summary>
    [Fact]
    public void Scan_TouchingTheSpansStart_ReadsOnlyInsideIt_AndStartsThereWhenNoAspectFollows()
    {
        RecordingEphemeris recorder = new(_ephemeris);

        IReadOnlyList<VoidOfCoursePeriod> periods = new VoidOfCourseScanner(recorder).Scan(
            EphemerisSpan.FirstJulianDay - 2.0, EphemerisSpan.FirstJulianDay + 4.0, "UTC");

        recorder.Count.Should().BeGreaterThan(0);
        recorder.Earliest.Should().BeGreaterThanOrEqualTo(EphemerisSpan.FirstJulianDay, "the scanner never reads before the span");
        recorder.Latest.Should().BeLessThanOrEqualTo(EphemerisSpan.LastJulianDay, "the scanner never reads after the span");
        periods.Select(p => p.MoonSign).Should().Equal(["Gemini", "Cancer"]);
        ShouldBeTierB(periods[0].StartLocal, "1200-01-08 04:54:45.319", "Gemini makes no aspect after the span's first instant, so it is void from there");
        ShouldBeTierB(periods[0].EndLocal, "1200-01-08 13:45:18.097", "the Gemini void ends at the Moon's exit");
        ShouldBeTierB(periods[1].StartLocal, "1200-01-10 23:06:30.496", "the Cancer void starts at the Moon's opposition to Mercury");
        ShouldBeTierB(periods[1].EndLocal, "1200-01-11 02:41:49.443", "the Cancer void ends at the Moon's exit");
    }

    /// <summary>
    /// A scan from four days before the span's last instant (2399-12-31 23:51:20) to two days after
    /// it reads the sky only inside the span. Capricorn's void ends inside it; the Moon enters
    /// Aquarius at 22:45:44 on the 29th and leaves only on 1 January 2400, after the span, so that
    /// stay's last aspect cannot be known and it has no period.
    /// </summary>
    [Fact]
    public void Scan_TouchingTheSpansEnd_ReadsOnlyInsideIt_AndGivesNoPeriodToAStayLeavingAfterIt()
    {
        RecordingEphemeris recorder = new(_ephemeris);

        IReadOnlyList<VoidOfCoursePeriod> periods = new VoidOfCourseScanner(recorder).Scan(
            EphemerisSpan.LastJulianDay - 4.0, EphemerisSpan.LastJulianDay + 2.0, "UTC");

        recorder.Count.Should().BeGreaterThan(0);
        recorder.Earliest.Should().BeGreaterThanOrEqualTo(EphemerisSpan.FirstJulianDay, "the scanner never reads before the span");
        recorder.Latest.Should().BeLessThanOrEqualTo(EphemerisSpan.LastJulianDay, "the scanner never reads after the span");
        VoidOfCoursePeriod capricorn = periods.Should().ContainSingle().Which;
        capricorn.MoonSign.Should().Be("Capricorn");
        ShouldBeTierB(capricorn.StartLocal, "2399-12-29 22:02:39.492", "the Capricorn void starts at the Moon's sextile to Uranus");
        ShouldBeTierB(capricorn.EndLocal, "2399-12-29 22:45:44.081", "the Capricorn void ends at the Moon's exit, inside the span");
    }

    /// <summary>A range wholly outside the span has no period, reads nothing and does not raise.</summary>
    /// <param name="fromOffset">The range's start, in days from the edge.</param>
    /// <param name="toOffset">The range's end, in days from the edge.</param>
    /// <param name="afterTheSpan">True for an offset from the last instant, false for one from the first.</param>
    [Theory]
    [InlineData(-10.0, -1.0, false)]
    [InlineData(1.0, 10.0, true)]
    public void Scan_WhollyOutsideTheSpan_ReturnsNoPeriod_AndReadsNothing(double fromOffset, double toOffset, bool afterTheSpan)
    {
        RecordingEphemeris recorder = new(_ephemeris);
        double edge = afterTheSpan ? EphemerisSpan.LastJulianDay : EphemerisSpan.FirstJulianDay;

        IReadOnlyList<VoidOfCoursePeriod> periods = new VoidOfCourseScanner(recorder).Scan(edge + fromOffset, edge + toOffset, "UTC");

        periods.Should().BeEmpty();
        recorder.Count.Should().Be(0, "nothing inside the span touches the range");
    }

    private static (DateTime Start, DateTime End, string Sign) Key(VoidOfCoursePeriod p) => (p.StartLocal, p.EndLocal, p.MoonSign);

    private double LocalJd(string date, int hour) =>
        JulianDayUtility.LocalMidnightToJulianDay(
            _ephemeris, DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture), _rules, NewYork) + (hour / 24.0);
}
