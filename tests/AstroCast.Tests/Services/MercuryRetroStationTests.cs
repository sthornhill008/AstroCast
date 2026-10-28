using System.Globalization;

using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the periods <see cref="MercuryRetroDetector.FindRetrogradePeriods"/> returns (branch O1, 1.3.3):
/// every retrograde period that touches the range, with its own stations as start and end, searched
/// outside the range as needed, and nothing read outside the span the shipped ephemeris files cover.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 a retrograde under way at either end of the range was cut to the range: search K100
/// (2026-10-28 to 2026-11-03) printed "Wed, 28 Oct 2026 12:00 AM" and "Wed, 04 Nov 2026 12:00 AM" where
/// the stations are 24 October and 13 November. This is the rule the void-of-course scanner keeps for its
/// periods (branch C).
/// </para>
/// <para>
/// Expected stations are Tier B (<c>o1-bounds/truth/tierb_mercury.py</c>: the sign of Mercury's speed in
/// longitude from direct <c>swe_calc_ut</c> calls, bisected to 1e-9 day), read on the zone's clock with
/// Python <c>zoneinfo</c>. The detector bisects to a cell of 1e-6 day on a lattice fixed in time and takes the
/// station's instant to the second, so a station is within a second of Tier B (31 seconds until the O1 fix-up, when
/// it bisected to 0.0007 day). The library is stateful, so the span tests also run alone in a fresh process.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MercuryRetroStationTests
{
    private const string NewYork = "America/New_York";

    /// <summary>The detector's precision: half its 1e-6-day cell, and the rounding to the second.</summary>
    private static readonly TimeSpan Precision = TimeSpan.FromSeconds(1);

    private readonly EphemerisService _ephemeris;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public MercuryRetroStationTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _ephemeris = fixture.EphemerisService;
    }

    /// <summary>
    /// K100's range, local midnight of 28 October to local midnight of 4 November 2026 in New York, lies
    /// wholly inside one retrograde: its period runs from the station retrograde, 2026-10-24 07:12:43 UT
    /// (03:12:43 EDT), to the station direct, 2026-11-13 15:53:53 UT (10:53:53 EST).
    /// </summary>
    [Fact]
    public void FindRetrogradePeriods_AroundK100_ReturnsThePeriodWithItsOwnStations()
    {
        IReadOnlyList<DisqualifiedPeriod> periods = new MercuryRetroDetector(_ephemeris, _rules).FindRetrogradePeriods(
            LocalMidnight(2026, 10, 28), LocalMidnight(2026, 11, 4), NewYork);

        DisqualifiedPeriod period = periods.Should().ContainSingle().Which;
        period.Reason.Should().Be("Mercury retrograde");
        ShouldBeTierB(period.Start, "2026-10-24 03:12:43", "the period starts at the station retrograde, four days before the range");
        ShouldBeTierB(period.End, "2026-11-13 10:53:53", "the period ends at the station direct, nine days after the range");
    }

    /// <summary>
    /// A range wholly inside the same retrograde, 30 October to 6 November 2026, returns that one period,
    /// with the same two stations.
    /// </summary>
    [Fact]
    public void FindRetrogradePeriods_RangeWhollyInsideARetrograde_ReturnsThatPeriodWithItsStations()
    {
        IReadOnlyList<DisqualifiedPeriod> periods = new MercuryRetroDetector(_ephemeris, _rules).FindRetrogradePeriods(
            LocalMidnight(2026, 10, 30), LocalMidnight(2026, 11, 6), NewYork);

        DisqualifiedPeriod period = periods.Should().ContainSingle().Which;
        ShouldBeTierB(period.Start, "2026-10-24 03:12:43", "Mercury turned retrograde before the range began");
        ShouldBeTierB(period.End, "2026-11-13 10:53:53", "Mercury turns direct after the range ends");
    }

    /// <summary>
    /// A range from two days before the span's first instant (1200-01-08 04:54:45 on the proleptic
    /// Gregorian calendar) to four days after it reads the sky only inside the span. Mercury is retrograde at
    /// the first instant (Tier B: speed −0.484°/day), so its period starts there, the earliest instant the
    /// files show, and ends at its station direct, 1200-01-26 12:31:51 UT.
    /// </summary>
    [Fact]
    public void FindRetrogradePeriods_TouchingTheSpansStart_ReadsOnlyInsideIt_AndStartsThere()
    {
        RecordingEphemeris recorder = new(_ephemeris);

        IReadOnlyList<DisqualifiedPeriod> periods = new MercuryRetroDetector(recorder, _rules).FindRetrogradePeriods(
            EphemerisSpan.FirstJulianDay - 2.0, EphemerisSpan.FirstJulianDay + 4.0, "UTC");

        recorder.Count.Should().BeGreaterThan(0);
        recorder.Earliest.Should().BeGreaterThanOrEqualTo(EphemerisSpan.FirstJulianDay, "the detector never reads before the span");
        recorder.Latest.Should().BeLessThanOrEqualTo(EphemerisSpan.LastJulianDay, "the detector never reads after the span");
        DisqualifiedPeriod period = periods.Should().ContainSingle().Which;
        ShouldBeTierB(period.Start, "1200-01-08 04:54:45", "Mercury is retrograde at the span's first instant, so its period starts there");
        ShouldBeTierB(period.End, "1200-01-26 12:31:51", "the period ends at the station direct, inside the span");
    }

    /// <summary>
    /// A range from four days before the span's last instant (2399-12-31 23:51:20) to two days after it
    /// reads the sky only inside the span. Mercury is direct throughout (Tier B: its last stations are 33.5
    /// and 13.8 days before the last instant), so there is no period.
    /// </summary>
    [Fact]
    public void FindRetrogradePeriods_TouchingTheSpansEnd_ReadsOnlyInsideIt()
    {
        RecordingEphemeris recorder = new(_ephemeris);

        IReadOnlyList<DisqualifiedPeriod> periods = new MercuryRetroDetector(recorder, _rules).FindRetrogradePeriods(
            EphemerisSpan.LastJulianDay - 4.0, EphemerisSpan.LastJulianDay + 2.0, "UTC");

        recorder.Count.Should().BeGreaterThan(0);
        recorder.Earliest.Should().BeGreaterThanOrEqualTo(EphemerisSpan.FirstJulianDay, "the detector never reads before the span");
        recorder.Latest.Should().BeLessThanOrEqualTo(EphemerisSpan.LastJulianDay, "the detector never reads after the span");
        periods.Should().BeEmpty("Mercury is direct for the last four days of the span");
    }

    /// <summary>A range wholly outside the span has no period, reads nothing and does not raise.</summary>
    /// <param name="fromOffset">The range's start, in days from the edge.</param>
    /// <param name="toOffset">The range's end, in days from the edge.</param>
    /// <param name="afterTheSpan">True for an offset from the last instant, false for one from the first.</param>
    [Theory]
    [InlineData(-10.0, -1.0, false)]
    [InlineData(1.0, 10.0, true)]
    public void FindRetrogradePeriods_WhollyOutsideTheSpan_ReturnsNoPeriod_AndReadsNothing(double fromOffset, double toOffset, bool afterTheSpan)
    {
        RecordingEphemeris recorder = new(_ephemeris);
        double edge = afterTheSpan ? EphemerisSpan.LastJulianDay : EphemerisSpan.FirstJulianDay;

        IReadOnlyList<DisqualifiedPeriod> periods = new MercuryRetroDetector(recorder, _rules).FindRetrogradePeriods(
            edge + fromOffset, edge + toOffset, "UTC");

        periods.Should().BeEmpty();
        recorder.Count.Should().Be(0, "nothing inside the span touches the range");
    }

    private double LocalMidnight(int year, int month, int day) =>
        JulianDayUtility.LocalMidnightToJulianDay(_ephemeris, new DateOnly(year, month, day), _rules, NewYork);

    private static void ShouldBeTierB(DateTime actual, string expected, string because)
    {
        DateTime truth = DateTime.ParseExact(expected, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        actual.Should().BeCloseTo(truth, Precision, because);
    }
}
