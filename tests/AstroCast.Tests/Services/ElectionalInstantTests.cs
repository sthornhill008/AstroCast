using AstroCast.Core.Enums;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the electional search's comparisons on instants (branch O1, 1.3.3): void-of-course and retrograde periods
/// carry their UTC instants, a sample is disqualified when its instant lies in <c>[StartUtc, EndUtc)</c>, and a
/// range of local dates runs from the first instant of its first date to the first instant of the day after its
/// last. Until 1.3.3 each was compared on the local clock, which in an hour the clocks repeat judged the samples of
/// the other pass by the wrong side of a bound, and a repeated midnight closed the range an hour late.
/// </summary>
/// <remarks>
/// <para>
/// Each case asks for every sample of a few quarter-hours, separation 0, retrograde allowed, so the candidates are
/// exactly the samples admitted. The void bounds are Tier B (the O investigation's void events: direct
/// <c>swe_calc_ut</c>, the closed definition), the readings Python <c>zoneinfo</c> over IANA 2026e
/// (<c>o1-bounds/out/zone-figures.json</c>):
/// </para>
/// <list type="bullet">
///   <item><description>Moon void in Cancer from 2026-10-31 21:59:38.7 UT to 2026-11-01 04:18:11.6 UT: 01:48:12 NDT
///   (first pass) at St. John's, 00:18:12 CDT (first pass) at Havana.</description></item>
///   <item><description>Moon void in Gemini from 2028-11-05 04:16:16.9 UT to 2028-11-06 07:23:59.0 UT: 01:46:17 NDT at
///   St. John's, 00:16:17 CDT at Havana, both first pass.</description></item>
///   <item><description>Moon void from 2028-04-01 16:39:33 UT: 02:39:33 AEST at Sydney, second pass.</description></item>
///   <item><description>Moon void from 2026-10-29 21:43:22 UT: 23:43:22 EET at Cairo, second pass.</description></item>
/// </list>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalInstantTests
{
    private const string StJohns = "America/St_Johns";
    private const string Havana = "America/Havana";

    private readonly EphemerisFixture _fixture;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalInstantTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    #region Periods carry their instants

    /// <summary>The scanner's period carries its Tier B instants, in UTC, beside its local readings.</summary>
    [Fact]
    public void Scan_TheCancerVoidOf1November2026_CarriesItsInstants()
    {
        VoidOfCoursePeriod cancer = new VoidOfCourseScanner(_fixture.EphemerisService, _rules)
            .Scan(LocalMidnight(2026, 10, 31, StJohns), LocalMidnight(2026, 11, 2, StJohns), StJohns)
            .Should().ContainSingle(p => p.MoonSign == "Cancer").Which;

        cancer.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        cancer.EndUtc.Kind.Should().Be(DateTimeKind.Utc);
        cancer.StartUtc.Should().BeCloseTo(new DateTime(2026, 10, 31, 21, 59, 38, 736, DateTimeKind.Utc), TimeSpan.FromSeconds(1.4));
        cancer.EndUtc.Should().BeCloseTo(new DateTime(2026, 11, 1, 4, 18, 11, 649, DateTimeKind.Utc), TimeSpan.FromSeconds(1.4));
        cancer.EndLocal.Should().Be(_rules.ToLocal(cancer.EndUtc, StJohns).LocalDateTime, "the local reading is the instant's, 01:48 NDT, first pass");
    }

    /// <summary>The detector's period carries its stations' instants, in UTC (Tier B 07:12:43 and 15:53:53 UT).</summary>
    [Fact]
    public void FindRetrogradePeriods_AroundK100_CarriesTheStationsInstants()
    {
        DisqualifiedPeriod period = new MercuryRetroDetector(_fixture.EphemerisService, _rules)
            .FindRetrogradePeriods(LocalMidnight(2026, 10, 28, "America/New_York"), LocalMidnight(2026, 11, 4, "America/New_York"), "America/New_York")
            .Should().ContainSingle().Which;

        period.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        period.EndUtc.Kind.Should().Be(DateTimeKind.Utc);
        period.StartUtc.Should().BeCloseTo(new DateTime(2026, 10, 24, 7, 12, 43, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
        period.EndUtc.Should().BeCloseTo(new DateTime(2026, 11, 13, 15, 53, 53, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    /// <summary>The search's void rows carry the scanner's instants.</summary>
    [Fact]
    public void Calculate_VoidRows_CarryTheScannersInstants()
    {
        ElectionalResult result = Search(StJohns, 47.5615, -52.7126, new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 1), new TimeOnly(0, 0), new TimeOnly(23, 45), 3);

        result.VoidOfCoursePeriods.Should().NotBeEmpty();
        foreach (VoidOfCoursePeriod v in result.VoidOfCoursePeriods)
        {
            result.DisqualifiedPeriods.Should().ContainSingle(d => d.Reason == $"Moon void-of-course in {v.MoonSign}" && d.StartUtc == v.StartUtc && d.EndUtc == v.EndUtc,
                "each void row is the scanner's period, its instants included");
        }
    }

    #endregion

    #region A sample is disqualified by its instant

    /// <summary>
    /// St. John's, 1 November 2026, 01:00–01:45: the void ends at 04:18:12 UT, 01:48 NDT in the first pass. The
    /// first pass (03:30–04:15 UT) is void; the second (01:00–01:45 NST, 04:30–05:15 UT) is not, and is offered.
    /// </summary>
    [Fact]
    public void Calculate_StJohns2026_AdmitsTheSecondPassAfterTheVoidEnded()
    {
        ElectionalResult result = Search(StJohns, 47.5615, -52.7126, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), new TimeOnly(1, 0), new TimeOnly(1, 45), 8);

        Instants(result).Should().BeEquivalentTo(Utc(2026, 11, 1, (4, 30), (4, 45), (5, 0), (5, 15)),
            "01:00–01:45 NST follow the void's end at 01:48 NDT, an hour earlier");
    }

    /// <summary>
    /// St. John's, 5 November 2028, 01:00–01:45: the void starts at 04:16:17 UT, 01:46 NDT in the first pass. The
    /// first pass (03:30–04:15 UT) is before it and offered; the second (04:30–05:15 UT) is void and excluded.
    /// </summary>
    [Fact]
    public void Calculate_StJohns2028_ExcludesTheVoidSecondPass()
    {
        ElectionalResult result = Search(StJohns, 47.5615, -52.7126, new DateOnly(2028, 11, 5), new DateOnly(2028, 11, 5), new TimeOnly(1, 0), new TimeOnly(1, 45), 8);

        Instants(result).Should().BeEquivalentTo(Utc(2028, 11, 5, (3, 30), (3, 45), (4, 0), (4, 15)),
            "01:00–01:45 NST follow the void's start at 01:46 NDT");
        result.Candidates.Should().OnlyContain(c => !c.Moon.IsVoidOfCourse);
    }

    /// <summary>
    /// Havana, 4 and 5 November 2028, 00:00–00:15: the void starts at 04:16:17 UT, 00:16 CDT in the first pass of
    /// 5 November. 00:00 and 00:15 CST (05:00, 05:15 UT) are void and excluded.
    /// </summary>
    [Fact]
    public void Calculate_Havana2028_ExcludesTheVoidSecondPass()
    {
        ElectionalResult result = Search(Havana, 23.1136, -82.3666, new DateOnly(2028, 11, 4), new DateOnly(2028, 11, 5), new TimeOnly(0, 0), new TimeOnly(0, 15), 8);

        Instants(result).Should().BeEquivalentTo(
            [.. Utc(2028, 11, 4, (4, 0), (4, 15)), .. Utc(2028, 11, 5, (4, 0), (4, 15))],
            "00:00 and 00:15 CST on 5 November follow the void's start at 00:16 CDT");
    }

    /// <summary>
    /// Sydney, 2 April 2028, 02:45: the void starts at 16:39:33 UT, 02:39 AEST in the second pass. 02:45 AEDT, the
    /// first pass (15:45 UT), is an hour before it and offered; 02:45 AEST (16:45 UT) is void.
    /// </summary>
    [Fact]
    public void Calculate_Sydney2028_AdmitsTheFirstPassBeforeTheVoid()
    {
        ElectionalResult result = Search("Australia/Sydney", -33.8688, 151.2093, new DateOnly(2028, 4, 2), new DateOnly(2028, 4, 2), new TimeOnly(2, 45), new TimeOnly(2, 45), 4);

        Instants(result).Should().BeEquivalentTo(Utc(2028, 4, 1, (15, 45)), "02:45 AEDT precedes the void's start at 02:39 AEST");
    }

    /// <summary>
    /// Cairo, 29 October 2026, 23:45: the void starts at 21:43:22 UT, 23:43 EET in the second pass. 23:45 EEST, the
    /// first pass (20:45 UT), is an hour before it and offered; 23:45 EET (21:45 UT) is void.
    /// </summary>
    [Fact]
    public void Calculate_Cairo2026_AdmitsTheFirstPassBeforeTheVoid()
    {
        ElectionalResult result = Search("Africa/Cairo", 30.0444, 31.2357, new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 29), new TimeOnly(23, 45), new TimeOnly(23, 45), 4);

        Instants(result).Should().BeEquivalentTo(Utc(2026, 10, 29, (20, 45)), "23:45 EEST precedes the void's start at 23:43 EET");
    }

    /// <summary>
    /// The evaluator finds the void period that holds an instant by the instant: 01:00 NST on 5 November 2028
    /// (04:30 UT) lies in the Gemini void that began at 01:46 NDT.
    /// </summary>
    [Fact]
    public void Evaluate_ASecondPassSampleInsideAVoid_FindsItsPeriod()
    {
        IReadOnlyList<VoidOfCoursePeriod> periods = new VoidOfCourseScanner(_fixture.EphemerisService, _rules)
            .Scan(LocalMidnight(2028, 11, 4, StJohns), LocalMidnight(2028, 11, 7, StJohns), StJohns);
        VoidOfCoursePeriod gemini = periods.Should().ContainSingle(p => p.MoonSign == "Gemini").Which;

        MoonCondition moon = new MoonConditionEvaluator(_fixture.EphemerisService, _rules).Evaluate(
            JulianDayUtility.InstantToJulianDay(new DateTime(2028, 11, 5, 4, 30, 0, DateTimeKind.Utc)), periods, StJohns);

        moon.IsVoidOfCourse.Should().BeTrue();
        moon.VoidStartLocal.Should().Be(gemini.StartLocal, "the instant lies in the period, though its reading, 01:00, precedes the period's, 01:46");
        moon.VoidEndLocal.Should().Be(gemini.EndLocal);
    }

    #endregion

    #region A day begins at its first instant

    /// <summary>
    /// Havana, 31 October 2026 alone, 00:00–00:45: the range ends at the first 00:00 of 1 November (04:00 UT, CDT),
    /// so it offers 00:00–00:45 CDT of 31 October and nothing of 1 November.
    /// </summary>
    [Fact]
    public void Calculate_Havana31October2026_OffersNothingOf1November()
    {
        ElectionalResult result = Search(Havana, 23.1136, -82.3666, new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 31), new TimeOnly(0, 0), new TimeOnly(0, 45), 8);

        result.Candidates.Should().OnlyContain(c => c.LocalDateTime.Day == 31, "the range is 31 October only");
        Instants(result).Should().BeEquivalentTo(Utc(2026, 10, 31, (4, 0), (4, 15), (4, 30), (4, 45)));
    }

    /// <summary>
    /// Havana, 1 November 2026, 00:00–00:45: the range starts at the first 00:00 (04:00 UT, CDT). The void ends at
    /// 00:18 CDT, so the first pass offers 00:30 and 00:45 CDT and the second all four quarter-hours, CST.
    /// </summary>
    [Fact]
    public void Calculate_Havana1November2026_StartsAtTheFirstMidnight()
    {
        ElectionalResult result = Search(Havana, 23.1136, -82.3666, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), new TimeOnly(0, 0), new TimeOnly(0, 45), 8);

        Instants(result).Should().BeEquivalentTo(Utc(2026, 11, 1, (4, 30), (4, 45), (5, 0), (5, 15), (5, 30), (5, 45)),
            "the day's first hour, CDT, is searched, and the second pass follows the void's end at 00:18 CDT");
    }

    #endregion

    /// <summary>
    /// An electional range's typed bounds are read once on the calendar in force (branch R3, 1.3.4;
    /// R-D7): a bound in 1582-10-05 to 1582-10-14 never occurred and the search is refused before it scans.
    /// Until 1.3.4 the bound was read as a Julian date in silence.
    /// </summary>
    [Fact]
    public void Calculate_RangeEndInTheTenDays_IsRefused()
    {
        Action act = () => Search("America/New_York", 40.7128, -74.006, new DateOnly(1582, 10, 1), new DateOnly(1582, 10, 10),
            new TimeOnly(0, 0), new TimeOnly(23, 59), 1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*1582-10-10 does not exist*");
    }

    private ElectionalResult Search(string zone, double lat, double lon, DateOnly start, DateOnly end, TimeOnly earliest, TimeOnly latest, int count) =>
        new ElectionalService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _rules).Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Repeated hour",
            RangeStart = start,
            RangeEnd = end,
            Latitude = lat,
            Longitude = lon,
            LocationName = zone,
            TimeZone = zone,
            CandidateCount = count,
            MinimumCandidateSeparationHours = 0.0,
            AvoidMercuryRetrograde = false,
            TimeConstraint = new TimeConstraint { Earliest = earliest, Latest = latest },
        });

    private static IEnumerable<DateTime> Instants(ElectionalResult result) => result.Candidates.Select(c => c.Chart.UtcDateTime);

    private static DateTime[] Utc(int year, int month, int day, params (int Hour, int Minute)[] times) =>
        [.. times.Select(t => new DateTime(year, month, day, t.Hour, t.Minute, 0, DateTimeKind.Utc))];

    private double LocalMidnight(int year, int month, int day, string zone) =>
        JulianDayUtility.LocalMidnightToJulianDay(_fixture.EphemerisService, new DateOnly(year, month, day), _rules, zone);
}
