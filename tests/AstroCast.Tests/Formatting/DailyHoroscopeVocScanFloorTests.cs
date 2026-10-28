using System.Globalization;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Integration tests pinning the void-of-course scan floor used by
/// <see cref="DailyHoroscopeService.BuildDays"/>: the report's
/// first local day must be scanned from its own local midnight, and any period
/// overlapping that day must be reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these tests pin.</b> The scan floor was
/// <c>DateOnlyToNoonJd(ReportStartDate) - 0.5</c>, which is 00:00 <b>UT</b> on
/// the report's first day, while every surrounding line -- the per-day loop, the
/// overlap filter, and the printed clock times -- works in local time. The floor
/// was the only value in the method not converted through the target zone.
/// </para>
/// <para>
/// <b>Two symptoms, one cause.</b> East of Greenwich the floor lands <i>after</i>
/// local midnight (09:00 in Tokyo, 11:00 in Sydney), so the first hours of day 1
/// are never scanned and the day reads "VoC: None". West of Greenwich the floor
/// lands <i>before</i> local midnight (19:00 or 20:00 the previous evening),
/// which over-covers the day boundary but supplies only a four- or five-hour
/// look-back -- too short to find the last aspect of a period already in
/// progress at local midnight, so those days also read "VoC: None". Every
/// pre-existing test used <c>America/New_York</c> and a range whose first day
/// happened to hold no such period, so neither symptom was caught.
/// </para>
/// <para>
/// Since 1.3.3 the scan reads exactly the report's own instants, from the first
/// instant of its first local day to the first instant after its last, with no
/// pad: the scanner returns every period that touches its range with its true
/// start and end (branch C).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DailyHoroscopeVocScanFloorTests
{
    private readonly EphemerisService _ephemeris;
    private readonly TransitService _transitService;
    private readonly ChartResult _natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the natal chart and
    /// transit service the daily horoscope is derived from.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public DailyHoroscopeVocScanFloorTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(
            _ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_ephemeris, logger), new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()),
            new AntisciaService(), new StationService(), new ParanService(_ephemeris, logger), logger);

        _natalChart = engine.Calculate(new ChartInput
        {
            Name = "J2000.0 Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        });

        _transitService = new TransitService(_ephemeris, logger);
    }

    #region East of Greenwich: the first local hours of day 1

    /// <summary>
    /// Tokyo is UTC+9, so the UT floor sat at 09:00 local on the report's first
    /// day and the first nine hours were never scanned. On 2026-03-05 the Moon
    /// is void in Virgo until it enters Libra at 03:56 local (from 23:53 the
    /// evening before), a void that ends well inside those nine hours. Before
    /// the fix the day reported no void periods at all.
    /// </summary>
    /// <remarks>
    /// The assertion is on the period's <i>end</i>, not its start. When the test
    /// was written the end was a sign ingress located by binary search to about a
    /// minute, independent of the scan floor, while the start was the last sampled
    /// aspect on a one-hour grid anchored to the floor, so moving the floor moved
    /// the reported start by up to an hour; asserting the start would have pinned
    /// an artefact of the sampling grid rather than the defect. Since 1.3.3 both
    /// are event times, independent of the floor.
    /// </remarks>
    [Fact]
    public void BuildDailyData_TokyoFirstDay_Should_ReportVoidPeriodInsideTheFirstLocalHours()
    {
        const string ZoneId = "Asia/Tokyo";
        DateOnly reportStart = new(2026, 3, 5);

        DailyHoroscopeDay day1 = FirstDay(reportStart, reportStart.AddDays(2), ZoneId);

        // 09:00 local is exactly where the UT floor sat for a UTC+9 zone.
        TimeSpan utFloorLocalTime = TimeSpan.FromHours(9);

        day1.VoidOfCoursePeriods.Should().NotBeEmpty(
            "the Moon is void in Virgo until it enters Libra at 03:56 local on {0:yyyy-MM-dd} in {1}, "
            + "and a void period inside the first hours of the report's first local day must be reported",
            reportStart, ZoneId);

        day1.VoidOfCoursePeriods.Should().Contain(
            entry => ParseEndTime(entry) < utFloorLocalTime,
            "a void period ending before {0:hh\\:mm} local -- the point the UT scan floor sat at for a "
            + "UTC+9 zone -- must be visible; day 1 reported [{1}]",
            utFloorLocalTime,
            string.Join(" | ", day1.VoidOfCoursePeriods));
    }

    #endregion

    #region West of Greenwich: a period already in progress at local midnight

    /// <summary>
    /// New York is UTC-4 in May, so the UT floor sat at 20:00 local on the
    /// previous evening -- earlier than local midnight, but only a four-hour
    /// look-back. On 2026-05-22 the Moon is void in Leo for the whole calendar
    /// day, having made its last aspect well before that window opened, so the
    /// scanner found no last aspect and emitted no period: the day reported
    /// "VoC: None" while the Moon was void from midnight to midnight.
    /// </summary>
    /// <remarks>
    /// Both printed times are clamps rather than computed values -- the period
    /// starts before this day and ends after it, so the formatter substitutes
    /// "00:00" and "23:59". That made the expected string independent of the
    /// aspect-sampling grid the scanner used until 1.3.3, and so of exactly how
    /// far the fixed floor looks back.
    /// </remarks>
    [Fact]
    public void BuildDailyData_NewYorkFirstDay_Should_ReportVoidPeriodAlreadyInProgressAtLocalMidnight()
    {
        const string ZoneId = "America/New_York";
        DateOnly reportStart = new(2026, 5, 22);

        DailyHoroscopeDay day1 = FirstDay(reportStart, reportStart.AddDays(2), ZoneId);

        day1.VoidOfCoursePeriods.Should().Contain(
            "00:00 – 23:59 (Leo)",
            "the Moon is void in Leo for the whole of {0:yyyy-MM-dd} in {1}, so the day must report a "
            + "full-day void rather than none; the period began before local midnight, which the UT "
            + "floor's four-hour look-back was too short to reach. Day 1 reported [{2}]",
            reportStart, ZoneId,
            string.Join(" | ", day1.VoidOfCoursePeriods));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds the daily horoscope data for a report range and returns its first day.
    /// </summary>
    /// <param name="reportStart">First local calendar day of the report.</param>
    /// <param name="reportEnd">Last local calendar day of the report.</param>
    /// <param name="zoneId">IANA timezone the report is rendered for.</param>
    /// <returns>The first day's data.</returns>
    private DailyHoroscopeDay FirstDay(DateOnly reportStart, DateOnly reportEnd, string zoneId)
    {
        TransitResult result = _transitService.Calculate(new TransitInput
        {
            NatalChart = _natalChart,
            ReportStartDate = reportStart,
            ReportEndDate = reportEnd,
            IncludeMinorAspects = false,
        });

        VoidOfCourseScanner scanner = new(_ephemeris);
        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, scanner).BuildDays(result, zoneId);

        days.Should().NotBeEmpty("the report range covers at least one day");
        return days[0];
    }

    /// <summary>
    /// Extracts the end time from a formatted void-of-course entry of the form
    /// <c>"HH:mm – HH:mm (Sign)"</c>.
    /// </summary>
    /// <param name="entry">A formatted entry.</param>
    /// <returns>The end time as a <see cref="TimeSpan"/> past local midnight.</returns>
    private static TimeSpan ParseEndTime(string entry)
    {
        // "01:36 – 03:56 (Virgo)" -> "03:56"
        int dash = entry.IndexOf('–', StringComparison.Ordinal);
        string tail = entry[(dash + 1)..].Trim();
        string endTime = tail[..tail.IndexOf(' ', StringComparison.Ordinal)];

        return TimeSpan.ParseExact(endTime, "hh\\:mm", CultureInfo.InvariantCulture);
    }

    #endregion
}
