using System.Globalization;

using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="VoidOfCourseScanner"/> to the 1.3.2 definition (H3, branch C, 1.3.3): the Moon
/// is void of course from the last exact major aspect it makes during its stay in a sign, to the
/// Sun or Mercury through Pluto with every target in motion, until it leaves that sign, and for
/// the whole stay, from its sign entry, when it makes none. Each scan here holds the stays it
/// asserts whole.
/// </summary>
/// <remarks>
/// <para>
/// The expected instants are Tier B: direct <c>swe_calc_ut</c> calls into the shipped library and
/// files, sign changes and exact aspects found on a 0.01-day grid and bisected to 1e-9 day, none of
/// it through AstroCast (<c>c-voc/truth/figures.py</c>). They agree with the timing investigation's
/// own truth for 1900–2050 (check rows 77–79).
/// </para>
/// <para>
/// The scanner reports instants to the second. A start is the last exact aspect as the finder
/// brackets it, at most half a second after the crossing, or the sign entry; an end is the first
/// instant known to lie in the next sign, at most a second after the crossing. Rounded to the
/// nearest second, each is Tier B's instant rounded, or the second after it.
/// </para>
/// <para>
/// Until 1.3.3 a period started at the last hourly sample at which the Moon was within 1° of any
/// major aspect, a stay with no sample in orb had no period, and the end was found to about a
/// minute: the Leo void of 24–25 April 2026 was printed from 20:43 on the 25th, and the Aries
/// stay of March 1965 had none.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class VoidOfCourseDefinitionTests
{
    private const string NewYork = "America/New_York";
    private const string TierBFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly EphemerisService _ephemeris;
    private readonly VoidOfCourseScanner _scanner;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public VoidOfCourseDefinitionTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _ephemeris = fixture.EphemerisService;
        _scanner = new VoidOfCourseScanner(_ephemeris);
    }

    /// <summary>
    /// Three stays in New York, each scanned from local midnight before its entry to local
    /// midnight after its exit. Leo, April 2026 (check row 78): the last aspect is a trine to
    /// Mercury on the 24th; the electional golden printed 20:43–21:04 on the 25th. Cancer,
    /// October 2026 (check row 77): a square to Saturn at 11:08; the daily export printed 11:55.
    /// Gemini, June 2026: a conjunction with the Sun at 22:54 on the 14th.
    /// </summary>
    /// <param name="sign">The sign of the stay.</param>
    /// <param name="scanFrom">The first local day scanned.</param>
    /// <param name="scanTo">The local day whose midnight ends the scan.</param>
    /// <param name="start">Tier B: the last exact aspect, New York time.</param>
    /// <param name="end">Tier B: the sign exit, New York time.</param>
    [Theory]
    [InlineData("Leo", "2026-04-23", "2026-04-27", "2026-04-24 18:20:59.231", "2026-04-25 21:04:26.544")]
    [InlineData("Cancer", "2026-10-02", "2026-10-06", "2026-10-03 11:08:29.259", "2026-10-04 18:54:12.127")]
    [InlineData("Gemini", "2026-06-13", "2026-06-17", "2026-06-14 22:54:10.452", "2026-06-15 08:14:14.034")]
    public void Scan_StartsAtTheLastExactAspect_AndEndsAtTheSignExit(
        string sign, string scanFrom, string scanTo, string start, string end)
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(
            LocalMidnight(scanFrom, NewYork), LocalMidnight(scanTo, NewYork), NewYork);

        VoidOfCoursePeriod period = periods.Should().ContainSingle(p => p.MoonSign == sign).Which;
        ShouldBeTierB(period.StartLocal, start, $"the {sign} void starts at the Moon's last exact aspect in {sign}");
        ShouldBeTierB(period.EndLocal, end, $"the {sign} void ends when the Moon leaves {sign}");
    }

    /// <summary>
    /// Stays in which the Moon makes no exact major aspect are void for the whole stay, from the
    /// sign entry. Aries, March 1965 (check row 79), 55.08 hours, and Gemini, November 1989,
    /// 48.54 hours: until 1.3.3 neither had a period. Four such stays occur in 1900–2050.
    /// </summary>
    /// <param name="sign">The sign of the stay.</param>
    /// <param name="scanFrom">The first UTC day scanned.</param>
    /// <param name="scanTo">The UTC day whose midnight ends the scan.</param>
    /// <param name="entry">Tier B: the sign entry, UTC.</param>
    /// <param name="exit">Tier B: the sign exit, UTC.</param>
    [Theory]
    [InlineData("Aries", "1965-03-04", "1965-03-08", "1965-03-04 18:44:36.648", "1965-03-07 01:49:23.886")]
    [InlineData("Gemini", "1989-11-13", "1989-11-17", "1989-11-13 20:18:32.907", "1989-11-15 20:51:01.373")]
    public void Scan_AStayWithNoExactAspect_IsVoidFromItsSignEntry(
        string sign, string scanFrom, string scanTo, string entry, string exit)
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(UtcMidnight(scanFrom), UtcMidnight(scanTo), "UTC");

        VoidOfCoursePeriod period = periods.Should().ContainSingle(p => p.MoonSign == sign).Which;
        ShouldBeTierB(period.StartLocal, entry, $"the Moon makes no exact major aspect in {sign}, so it is void from its entry");
        ShouldBeTierB(period.EndLocal, exit, $"the {sign} void ends when the Moon leaves {sign}");
    }

    /// <summary>
    /// Every period of January 2026 (UTC; the scan the existing scanner tests use), against Tier B:
    /// thirteen stays, each from its last exact aspect to its sign exit. The first stay, Gemini,
    /// began on 31 December and makes its last aspect inside the scan.
    /// </summary>
    [Fact]
    public void Scan_EveryPeriodOfAMonth_MatchesTierB()
    {
        (string Sign, string Start, string End)[] tierB =
        [
            ("Gemini", "2026-01-02 12:23:38.523", "2026-01-02 13:09:00.594"),
            ("Cancer", "2026-01-04 12:59:15.295", "2026-01-04 13:43:26.219"),
            ("Leo", "2026-01-06 13:04:40.145", "2026-01-06 16:56:45.625"),
            ("Virgo", "2026-01-08 23:22:43.073", "2026-01-09 00:05:47.160"),
            ("Libra", "2026-01-10 17:54:14.760", "2026-01-11 10:55:20.351"),
            ("Scorpio", "2026-01-13 22:58:57.545", "2026-01-13 23:33:48.972"),
            ("Sagittarius", "2026-01-16 11:18:59.649", "2026-01-16 11:47:08.629"),
            ("Capricorn", "2026-01-18 21:56:41.335", "2026-01-18 22:17:56.515"),
            ("Aquarius", "2026-01-21 02:16:29.499", "2026-01-21 06:49:39.555"),
            ("Pisces", "2026-01-23 13:17:16.483", "2026-01-23 13:25:41.718"),
            ("Aries", "2026-01-24 21:36:21.339", "2026-01-25 18:05:19.877"),
            ("Taurus", "2026-01-27 17:57:46.954", "2026-01-27 20:55:00.561"),
            ("Gemini", "2026-01-29 19:56:35.399", "2026-01-29 22:31:39.901"),
        ];

        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(UtcMidnight("2026-01-01"), UtcMidnight("2026-01-31"), "UTC");

        periods.Select(p => p.MoonSign).Should().Equal(tierB.Select(t => t.Sign), "one period per stay, in time order");
        for (int i = 0; i < tierB.Length; i++)
        {
            ShouldBeTierB(periods[i].StartLocal, tierB[i].Start, $"period {i + 1} ({tierB[i].Sign}) starts at its last exact aspect");
            ShouldBeTierB(periods[i].EndLocal, tierB[i].End, $"period {i + 1} ({tierB[i].Sign}) ends at its sign exit");
        }
    }

    /// <summary>
    /// Hourly through January 2026, the scanner's periods and
    /// <see cref="MoonConditionService.GetMoonCondition"/> agree on whether the Moon is void: the
    /// two follow the same definition with the same finder. The scan runs from 28 December to
    /// 4 February, so every stay that touches January lies whole inside it. An instant within two
    /// seconds of a period's edge, where the two may bracket the same crossing a second apart,
    /// would be left out; none of these 744 is.
    /// </summary>
    [Fact]
    public void Scan_AgreesWithMoonConditionService_HourlyThroughAMonth()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = _scanner.Scan(UtcMidnight("2025-12-28"), UtcMidnight("2026-02-04"), "UTC");
        MoonConditionService service = new();
        double january = UtcMidnight("2026-01-01");

        List<string> disagreements = [];
        int nearAnEdge = 0;
        for (int hour = 0; hour < 31 * 24; hour++)
        {
            double jd = january + (hour / 24.0);
            DateTime instant = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);
            if (periods.Any(p => Math.Abs((p.StartLocal - instant).TotalSeconds) <= 2.0 || Math.Abs((p.EndLocal - instant).TotalSeconds) <= 2.0))
            {
                nearAnEdge++;
                continue;
            }

            bool scannerVoid = periods.Any(p => p.StartLocal <= instant && instant < p.EndLocal);
            MoonConditionInfo condition = service.GetMoonCondition(_ephemeris.CalculateAllPositions(jd), [], _ephemeris, jd)
                ?? throw new InvalidOperationException("no Moon condition");
            if (scannerVoid != condition.IsVoidOfCourse)
            {
                disagreements.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{instant:yyyy-MM-dd HH:mm} scanner {scannerVoid}, service {condition.IsVoidOfCourse}"));
            }
        }

        nearAnEdge.Should().Be(0, "no hourly instant of January 2026 lies within two seconds of a period's edge");
        disagreements.Should().BeEmpty("the scanner's periods follow the definition the Moon condition follows");
    }

    /// <summary>Asserts a reported instant is the Tier B instant rounded to the second, or the second after it.</summary>
    /// <param name="reported">The instant the scanner reports.</param>
    /// <param name="tierB">The Tier B instant, <c>yyyy-MM-dd HH:mm:ss.fff</c> on the same clock.</param>
    /// <param name="because">What the instant is.</param>
    internal static void ShouldBeTierB(DateTime reported, string tierB, string because)
    {
        DateTime truth = DateTime.ParseExact(tierB, TierBFormat, CultureInfo.InvariantCulture);
        DateTime rounded = new(truth.Ticks - (truth.Ticks % TimeSpan.TicksPerSecond), truth.Kind);
        if (truth.Ticks % TimeSpan.TicksPerSecond >= TimeSpan.TicksPerSecond / 2)
        {
            rounded = rounded.AddSeconds(1);
        }

        reported.Should().BeOneOf([rounded, rounded.AddSeconds(1)], because + " (Tier B {0})", tierB);
    }

    private double UtcMidnight(string date)
    {
        DateOnly d = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return _ephemeris.DateToJulianDay(d.Year, d.Month, d.Day, 0.0);
    }

    private double LocalMidnight(string date, string zoneId) =>
        JulianDayUtility.LocalMidnightToJulianDay(
            _ephemeris, DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture), _rules, zoneId);
}
