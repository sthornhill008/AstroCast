using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests pinning the contract the Electional date pickers advertise:
/// every returned candidate falls on a local calendar date inside the requested
/// <see cref="ElectionalInput.RangeStart"/>..<see cref="ElectionalInput.RangeEnd"/>
/// range.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these tests pin.</b> <c>ElectionalService.Calculate</c> converted
/// both range bounds with <c>IEphemerisService.DateToJulianDay(y, m, d, 0.0)</c>.
/// That overload documents its hour argument as <b>UT</b>, but
/// <see cref="ElectionalInput.RangeStart"/> and <see cref="ElectionalInput.RangeEnd"/>
/// are local calendar dates chosen in a date picker. The scanned window was
/// therefore displaced by the location's UTC offset: in <c>America/New_York</c>
/// it ran from 20:00 local on the day <i>before</i> <c>RangeStart</c> to 20:00
/// local on <c>RangeEnd</c>, rather than from local midnight to local midnight.
/// </para>
/// <para>
/// <b>Why the request count matters.</b> The displacement was always present but
/// only becomes observable once the candidate pool reaches far enough from the
/// score peak to select a moment inside the four displaced hours. Measured on
/// both Windows and macOS, that happens at a request of 7 for the Richmond
/// window and 6 for the NYC window. Requesting fewer would let a broken engine
/// pass. The separation filter incidentally caps the damage at a single
/// out-of-range candidate -- the displaced zone is four hours wide, so all
/// samples in it fall inside one 6-hour separation width and at most one can
/// survive -- which is why the assertion below must not be a count.
/// </para>
/// <para>
/// <b>Why the assertion is on the local calendar date.</b> The user picked
/// calendar dates, and the export prints calendar dates. Asserting on the
/// instant, or on the Julian Day, would restate the engine's own arithmetic
/// rather than the promise made to the user.
/// </para>
/// <para>
/// <b>Why the DST windows are here, and why they use constraints rather than
/// scores.</b> A range spanning a transition has a different UTC offset at each
/// end, so resolving one offset and applying it to both bounds displaces the
/// window <i>and</i> resizes it by an hour. An earlier draft of these tests
/// simply requested seven candidates over such a range and asserted they all
/// landed in range -- and passed against the unfixed engine, because those
/// windows happened to hold no high-scoring moment inside their displaced hours.
/// A guard that has never failed would not catch someone later collapsing the
/// conversion back to a single offset.
/// </para>
/// <para>
/// The two DST tests below therefore decide reachability with
/// <see cref="ElectionalInput.TimeConstraint"/> and
/// <see cref="ElectionalInput.AllowedDays"/> instead of with scoring. Those
/// filters run before any scoring, so whether a candidate can exist is settled
/// by the boundary arithmetic alone and not by the state of the sky. Each names,
/// in its own remarks, the three end-bound positions it distinguishes.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalRangeBoundaryTests
{
    /// <summary>Richmond, VA latitude (Doctor Visit live-run location).</summary>
    private const double RichmondLatitude = 37.5385;

    /// <summary>Richmond, VA longitude.</summary>
    private const double RichmondLongitude = -77.4343;

    /// <summary>New York City latitude (golden-fixture election location).</summary>
    private const double NycLatitude = 40.7128;

    /// <summary>New York City longitude.</summary>
    private const double NycLongitude = -74.006;

    /// <summary>IANA timezone shared by both election locations.</summary>
    private const string TimeZone = "America/New_York";

    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;
    private readonly ElectionalService _service;

    /// <summary>Cached natal chart shared across tests in this class.</summary>
    private static ChartResult? s_natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the full service graph.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ElectionalRangeBoundaryTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(
            _ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_ephemeris, logger), new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()),
            new AntisciaService(), new StationService(), new ParanService(_ephemeris, logger), logger);
        _service = new ElectionalService(_ephemeris, _chartEngine);
    }

    #region Every candidate falls inside the requested local date range

    /// <summary>
    /// The Doctor Visit window at the request count where the displaced scan
    /// first reaches outside the range. Measured returning a candidate dated
    /// 2026-06-10 -- one day before RangeStart -- at 20:00 local.
    /// </summary>
    [Fact]
    public void Calculate_RichmondSevenRequested_Should_ReturnOnlyCandidatesInsideRequestedRange()
    {
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 14),
            Latitude = RichmondLatitude,
            Longitude = RichmondLongitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            CandidateCount = 7,
        };

        AssertAllCandidatesInsideRange(_service.Calculate(input), input);
    }

    /// <summary>
    /// The J2000 NYC business-launch window at its own threshold count.
    /// Measured returning a candidate dated 2026-04-19 at 20:00 local.
    /// </summary>
    [Fact]
    public void Calculate_NycSixRequested_Should_ReturnOnlyCandidatesInsideRequestedRange()
    {
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 6,
        };

        AssertAllCandidatesInsideRange(_service.Calculate(input), input);
    }

    /// <summary>
    /// The NYC window across the request counts either side of its threshold.
    /// No count may produce a candidate dated outside the requested range.
    /// </summary>
    /// <param name="requested">Number of candidate windows to request.</param>
    /// <remarks>
    /// <para>
    /// A single request count pins a single point on the curve. This sweeps the
    /// counts at which the displaced scan first reached outside the range, so a
    /// fix that repairs one count and not its neighbours cannot pass.
    /// </para>
    /// <para>
    /// Counts 7 and above are also what expose the <i>labelling</i> half of the
    /// defect. Once the scan window begins at local midnight, the first sample of
    /// the range is an exact midnight instant -- and a candidate's reported date
    /// comes from decomposing a Julian Day whose fractional hour is a few
    /// microseconds short. Truncating that decomposition moved the label back
    /// across the date boundary, so the first sample of a 20-25 April request
    /// printed as 19 April. The window arithmetic was right and the printed date
    /// was still wrong, which is precisely what a user would report as the bug
    /// being unfixed.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Calculate_NycAcrossRequestCounts_Should_NeverReturnACandidateOutsideTheRange(int requested)
    {
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = requested,
        };

        AssertAllCandidatesInsideRange(_service.Calculate(input), input);
    }

    #endregion

    #region RangeEnd covers the whole of its local day

    /// <summary>
    /// A single-day range restricted to the late evening. This pins the
    /// <see cref="ElectionalInput.RangeEnd"/> semantics without depending on
    /// the sky being favourable: the time constraint, not the scoring, decides
    /// which local hours can appear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before the fix the scan ran <c>[20:00 local on RangeStart-1, 20:00 local
    /// on RangeEnd)</c>. The requested 21:00-23:30 band on <c>RangeEnd</c> lay
    /// entirely past the end bound and was unreachable, while the same band on
    /// the day <i>before</i> <c>RangeStart</c> lay inside the window and was
    /// reachable. A one-day request therefore answered with moments from a day
    /// the user never asked about, and could not answer with any moment from the
    /// day they did.
    /// </para>
    /// <para>
    /// Both halves are asserted. That every candidate is in range catches the
    /// displacement; that at least one lands on <c>RangeEnd</c> catches an
    /// end bound that stops at local midnight <i>of</i> <c>RangeEnd</c> rather
    /// than at the end of it -- an off-by-one-day that the in-range assertion
    /// alone would accept, since returning nothing at all also satisfies it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculate_SingleDayRangeLateEvening_Should_ReachTheLastLocalHoursOfRangeEnd()
    {
        DateOnly theDay = new(2026, 6, 12);

        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Late-evening appointment on a single day.",
            RangeStart = theDay,
            RangeEnd = theDay,
            Latitude = RichmondLatitude,
            Longitude = RichmondLongitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            CandidateCount = 2,
            TimeConstraint = new TimeConstraint
            {
                Earliest = new TimeOnly(21, 0),
                Latest = new TimeOnly(23, 30),
            },
        };

        ElectionalResult result = _service.Calculate(input);

        AssertAllCandidatesInsideRange(result, input);

        result.Candidates.Should().Contain(
            c => DateOnly.FromDateTime(c.LocalDateTime) == theDay,
            "the requested range is the single day {0:yyyy-MM-dd} and the requested hours are 21:00-23:30 "
            + "local, so at least one candidate must fall in the last local hours of that day; "
            + "delivered [{1}]",
            theDay,
            string.Join(", ", result.Candidates.Select(
                c => c.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))));
    }

    #endregion

    #region Ranges spanning a DST transition

    /// <summary>
    /// A range spanning the 2026 US fall-back transition (Sunday 2026-11-01,
    /// when 02:00 EDT becomes 01:00 EST), asking only for the last forty-five
    /// minutes of the final local day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the case that discriminates a per-bound conversion from a single
    /// resolved offset applied to both bounds. The range opens in EDT (UTC-4)
    /// and closes in EST (UTC-5), so its two bounds need different offsets. The
    /// end bound falls at one of three places depending on the implementation:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>UT hour 0.0</b> (the defect): 2026-11-03 19:00 local.
    /// </description></item>
    /// <item><description>
    /// <b>One offset for both bounds</b>, resolved on RangeStart's date and so
    /// EDT: 2026-11-03 23:00 local -- an hour short.
    /// </description></item>
    /// <item><description>
    /// <b>Each bound on its own date</b> (correct): 2026-11-04 00:00 local.
    /// </description></item>
    /// </list>
    /// <para>
    /// A 23:15-23:45 request is unreachable under the first two and reachable
    /// only under the third. <c>AllowedDays</c> restricts the range's five days
    /// to its single Tuesday, 2026-11-03, so no other day can supply a candidate
    /// and mask the result. Mercury-retrograde avoidance is switched off so that
    /// only the boundary arithmetic, not the state of the sky, decides whether a
    /// candidate exists.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculate_FallBackRangeLastLocalHour_Should_ReachTheFinalMinutesOfRangeEnd()
    {
        DateOnly rangeEnd = new(2026, 11, 3);

        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Late slot on the last day of a fall-back range.",
            RangeStart = new DateOnly(2026, 10, 30),
            RangeEnd = rangeEnd,
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 1,
            AvoidMercuryRetrograde = false,
            AllowedDays = [DayOfWeek.Tuesday],
            TimeConstraint = new TimeConstraint
            {
                Earliest = new TimeOnly(23, 15),
                Latest = new TimeOnly(23, 45),
            },
        };

        ElectionalResult result = _service.Calculate(input);

        AssertAllCandidatesInsideRange(result, input);

        result.Candidates.Should().NotBeEmpty(
            "2026-11-03 is the only Tuesday in the requested range and 23:15-23:45 local lies "
            + "inside it, so the scan must reach the final minutes of RangeEnd; an empty result "
            + "means the end bound stopped early -- at 19:00 if it was built in UT, or at 23:00 "
            + "if one offset was resolved once and applied to both ends of a range that crosses "
            + "a DST transition");
    }

    /// <summary>
    /// A range spanning the 2026 US spring-forward transition (Sunday
    /// 2026-03-08), asking only for a weekday the range does not contain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the start-bound counterpart of the fall-back test. The defect
    /// never truncates the start -- it moves it <i>earlier</i>, to the previous
    /// evening -- so the symptom to catch is a candidate arriving from outside
    /// the range, and the assertion is that nothing is returned at all.
    /// </para>
    /// <para>
    /// The requested range 2026-03-07..2026-03-11 runs Saturday to Wednesday and
    /// so contains no Friday. The nearest Friday is 2026-03-06, the evening
    /// immediately before RangeStart. That evening is reachable under the defect
    /// (the scan began at 2026-03-06 19:00 local) and still reachable under a
    /// single offset resolved on RangeEnd's date, which is EDT and puts the start
    /// at 2026-03-06 23:00. Only converting RangeStart on its own date excludes
    /// 2026-03-06 entirely.
    /// </para>
    /// <para>
    /// <b>Why the band is 23:15-23:45 rather than the whole evening.</b> The Moon
    /// is void of course in Libra across 2026-03-06 until 23:01 local, and void
    /// samples are disqualified before scoring. An earlier draft of this test
    /// asked for 20:15-23:45 on 2026-03-05 and passed against the unfixed engine
    /// for exactly that reason: every out-of-range sample it reached had already
    /// been thrown away as void, so the test proved nothing. The band here sits
    /// wholly after that void ends, which is what makes the out-of-range samples
    /// survive to be selected -- and therefore what makes the assertion bite.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculate_SpringForwardRangeWithNoMatchingWeekday_Should_ReturnNoCandidates()
    {
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Friday-only request over a range containing no Friday.",
            RangeStart = new DateOnly(2026, 3, 7),
            RangeEnd = new DateOnly(2026, 3, 11),
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
            AvoidMercuryRetrograde = false,
            AllowedDays = [DayOfWeek.Friday],
            TimeConstraint = new TimeConstraint
            {
                Earliest = new TimeOnly(23, 15),
                Latest = new TimeOnly(23, 45),
            },
        };

        ElectionalResult result = _service.Calculate(input);

        result.Candidates.Should().BeEmpty(
            "2026-03-07..2026-03-11 runs Saturday to Wednesday and contains no Friday, so a "
            + "Friday-only request has nothing to return; any candidate here was scanned from "
            + "outside the requested range -- 2026-03-06 is the Friday evening a start bound "
            + "built in UT, or resolved at RangeEnd's offset, reaches back into. Delivered [{0}]",
            string.Join(", ", result.Candidates.Select(
                c => c.LocalDateTime.ToString("yyyy-MM-dd HH:mm dddd", CultureInfo.InvariantCulture))));
    }

    #endregion

    #region Assertion helper

    /// <summary>
    /// Asserts every candidate's local calendar date lies within the requested
    /// inclusive range.
    /// </summary>
    /// <param name="result">The calculated electional result.</param>
    /// <param name="input">The input whose range the result must respect.</param>
    /// <remarks>
    /// Failures name the offending candidate and the whole delivered set, because
    /// the useful diagnosis is which moment escaped and by how far -- and a
    /// FluentAssertions collection dump of a <c>CandidateWindow</c> would bury it
    /// under a complete <c>ChartResult</c>.
    /// </remarks>
    private static void AssertAllCandidatesInsideRange(ElectionalResult result, ElectionalInput input)
    {
        string delivered = string.Join(", ", result.Candidates.Select(
            c => c.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));

        foreach (CandidateWindow candidate in result.Candidates)
        {
            DateOnly candidateDate = DateOnly.FromDateTime(candidate.LocalDateTime);

            candidateDate.Should().BeOnOrAfter(input.RangeStart,
                "candidate {0:yyyy-MM-dd HH:mm} must not predate the requested RangeStart of {1:yyyy-MM-dd} "
                + "-- the scan window is a local calendar range, not a UT one (delivered: {2})",
                candidate.LocalDateTime, input.RangeStart, delivered);

            candidateDate.Should().BeOnOrBefore(input.RangeEnd,
                "candidate {0:yyyy-MM-dd HH:mm} must not postdate the requested RangeEnd of {1:yyyy-MM-dd} "
                + "-- the scan window is a local calendar range, not a UT one (delivered: {2})",
                candidate.LocalDateTime, input.RangeEnd, delivered);
        }
    }

    #endregion

    #region Natal fixture

    /// <summary>
    /// Calculates (once) and caches the J2000 NYC natal chart, the same
    /// reference chart the electional goldens are anchored to.
    /// </summary>
    /// <returns>The cached natal chart.</returns>
    private ChartResult GetNatalChart()
    {
        return s_natalChart ??= _chartEngine.Calculate(new ChartInput
        {
            Name = "J2000 NYC Fixture",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
    }

    #endregion
}
