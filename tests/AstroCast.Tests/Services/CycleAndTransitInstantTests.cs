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
/// The cycle timeline and the transit longitudes read the Julian Day in hand (branch R1). The cycle
/// timeline took its start from the return chart's <see cref="ChartResult.UtcDateTime"/>, and the transit
/// engine the longitude beside an entry, exit or station from the printed instant, each read back through
/// the date's calendar switch: before 1582-10-15 a Julian date, so the cycle's days from start were 8
/// days short in 1302 and the longitudes were the body's 8 days later in 1301.
/// </summary>
/// <remarks>
/// Every expected value is Tier B: direct <c>swe_calc_ut</c> on the shipped files
/// (<c>r1-instants/truth/expected.py</c>, <c>expected3.py</c>). An entry or exit is where the transiting
/// body stands at the natal target plus the aspect, less or more the orb (Jupiter's square 1.5°,
/// Neptune's conjunction 1.5°); a station is where its speed in longitude changes sign.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class CycleAndTransitInstantTests
{
    /// <summary>A longitude the app prints beside an instant to the whole second is the body's to this (degrees).</summary>
    private const double LongitudeTolerance = 0.0005;

    /// <summary>Days from start are printed to the hundredth of a day.</summary>
    private const double DaysTolerance = 0.006;

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public CycleAndTransitInstantTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    #region The cycle timeline

    /// <summary>
    /// N1's Mars cycle #1 counts its days from the return itself, Julian 1302-02-23 11:46:30 UT: the
    /// squares and the opposition (its first pass, branch F: Mars crosses the opposition point three
    /// times, on Julian 1302-11-23, 1303-03-31 and 1303-05-02) at 122.57, 273.51 and 593.97 days
    /// (Tier B), where they were 114.57, 424.93 and 585.97 before R1 (1.3.3) and the opposition was
    /// its final pass, 432.93, until branch F (T2-4); and N5's cycle #1, after the switch, as before
    /// (guard).
    /// </summary>
    /// <param name="natalKey">N1 (Julian 1300-03-15 12:00 UT) or N5 (2000-01-01 12:00 UT).</param>
    /// <param name="expected">Tier B's days from the first return to each phase and to the next return.</param>
    [Theory]
    [InlineData("N1", new[] { 0.0, 122.5651, 273.5078, 593.9743, 709.9541 })]
    [InlineData("N5", new[] { 0.0, 125.6433, 263.2971, 403.9109, 553.3397 })]
    public void MarsCycle_Should_CountItsDaysFromTheReturnItself(string natalKey, double[] expected)
    {
        PlanetaryCycleTimeline timeline = new PlanetaryCycleTimelineService(
                _fixture.EphemerisService, new PlanetaryReturnService(_fixture.EphemerisService, _engine, _logger), _logger)
            .Calculate(Natal(natalKey), CelestialBodyId.Mars, 1);

        timeline.PhasePoints.Select(p => p.DaysFromStart).Should().HaveCount(5).And.SatisfyRespectively(
            d => d.Should().BeApproximately(expected[0], DaysTolerance),
            d => d.Should().BeApproximately(expected[1], DaysTolerance),
            d => d.Should().BeApproximately(expected[2], DaysTolerance),
            d => d.Should().BeApproximately(expected[3], DaysTolerance),
            d => d.Should().BeApproximately(expected[4], DaysTolerance));
        timeline.CycleDurationDays.Should().BeApproximately(expected[4], 0.0001);
    }

    #endregion

    #region The transit longitudes

    /// <summary>
    /// In N1's transits of March 1301, the longitude printed beside Jupiter's entry into and exit from
    /// orb of the square to natal Pluto is Jupiter's at that instant: 49.290010° at Julian 1301-02-19
    /// 11:33:45 UT and 52.290010° at Julian 1301-03-10 03:00:57 UT (Tier B). It was Jupiter's 8 days
    /// later, 50.495° and 53.763°.
    /// </summary>
    [Fact]
    public void TransitEntryAndExitBeforeTheSwitch_Should_PrintTheBodysLongitudeThen()
    {
        TransitResult result = Transits(Natal("N1"), new DateOnly(1301, 3, 1), new DateOnly(1301, 3, 31));

        ActiveTransit transit = result.ActiveTransits.Single(
            t => t.TransitPlanet == TransitBody.Jupiter && t.AspectName == "Square" && t.NatalTarget == "Pluto");
        transit.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Entry).TransitLongitude
            .Should().BeApproximately(49.290010182241375, LongitudeTolerance);
        transit.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Exit).TransitLongitude
            .Should().BeApproximately(52.29001018231118, LongitudeTolerance);
    }

    /// <summary>
    /// The longitude printed beside Mars's station direct of March 1301 is Mars's at the station, Julian
    /// 1301-03-11 20:19:02 UT: Leo 8°14′ (Tier B). It was Mars's 8 days later, Leo 8°37′.
    /// </summary>
    [Fact]
    public void TransitStationBeforeTheSwitch_Should_PrintTheBodysLongitudeThen()
    {
        TransitResult result = Transits(Natal("N1"), new DateOnly(1301, 3, 1), new DateOnly(1301, 3, 31));

        TransitStation station = result.Stations.Single(s => s.Planet == TransitBody.Mars);
        station.StationType.Should().Be("Direct");
        (station.Sign, station.Degrees, station.Minutes).Should().Be(("Leo", 8, 14));
    }

    /// <summary>
    /// Guard: after the switch the longitudes are as before. In N1's transits of March 2026, Neptune's
    /// entry into orb of the conjunction to the natal Sun prints 1.290306° (Tier B), and Jupiter's station
    /// direct of 2026-03-11 03:29:51 UT, Cancer 15°05′.
    /// </summary>
    [Fact]
    public void TransitLongitudesAfterTheSwitch_Should_PrintTheBodysLongitudeThen()
    {
        TransitResult result = Transits(Natal("N1"), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        ActiveTransit transit = result.ActiveTransits.Single(
            t => t.TransitPlanet == TransitBody.Neptune && t.AspectName == "Conjunction" && t.NatalTarget == "Sun");
        transit.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Entry).TransitLongitude
            .Should().BeApproximately(1.2903061997013685, LongitudeTolerance);
        TransitStation station = result.Stations.Single(s => s.Planet == TransitBody.Jupiter);
        (station.Sign, station.Degrees, station.Minutes).Should().Be(("Cancer", 15, 5));
    }

    /// <summary>
    /// A transit range across October 1582 steps the calendar in force (branch R3, 1.3.4; R-D7): typed
    /// 1582-09-25 to 1582-10-25 is 21 days, and N1's transits hold exactly two stations, Jupiter direct at
    /// Julian 1582-09-28 17:03:53 UT (Aquarius 12°15′) and Neptune retrograde at Julian 1582-10-23
    /// 21:38:46 UT (Cancer 20°22′), no ingress, and the midpoint 1582-10-15 (Tier B, r3_truth.out). Until
    /// 1.3.4 the ten typed days 1582-10-05 to 10-14 were read as Julian and scanned twice, and the
    /// midpoint was 1582-10-10, a date that never occurred.
    /// </summary>
    [Fact]
    public void TransitsAcrossTheSwitch_Should_StepTheCalendarInForce()
    {
        TransitResult result = Transits(Natal("N1"), new DateOnly(1582, 9, 25), new DateOnly(1582, 10, 25));

        result.Stations.Should().HaveCount(2);
        TransitStation jupiter = result.Stations.Single(s => s.Planet == TransitBody.Jupiter);
        jupiter.StationType.Should().Be("Direct");
        CalendarDateFormatter.Format(jupiter.Date, "yyyy-MM-dd").Should().Be("1582-09-28");
        (jupiter.Sign, jupiter.Degrees, jupiter.Minutes).Should().Be(("Aquarius", 12, 15));
        TransitStation neptune = result.Stations.Single(s => s.Planet == TransitBody.Neptune);
        neptune.StationType.Should().Be("Retrograde");
        CalendarDateFormatter.Format(neptune.Date, "yyyy-MM-dd").Should().Be("1582-10-23");
        (neptune.Sign, neptune.Degrees, neptune.Minutes).Should().Be(("Cancer", 20, 22));
        result.Ingresses.Should().BeEmpty();
        result.MidpointDate.Should().Be(new DateOnly(1582, 10, 15));
    }

    /// <summary>
    /// A transit range over a Julian leap day: typed 1400-02-28 to 1400-03-02 is proleptic 1400-03-08 to
    /// 03-11, its midpoint 1400-03-09, Julian 1400-02-29, which heads the house overlays. Since branch
    /// Modes (1.4.0) the progressions and solar arcs are taken at the start, Julian 1400-02-28: N1's age
    /// at that day's noon is 99.956194 years, progressed JD 2196056.956194, the progressed instant Julian
    /// 1300-06-23 10:56:55 UT (Tier B, n1_start_truth.py; at the midpoint it was 11:00:52, r3_truth2.out).
    /// Until 1.3.4 the headings said 1400-03-01, typed arithmetic skipping the leap day.
    /// </summary>
    [Fact]
    public void TransitsOverAJulianLeapDay_Should_HeadTheMidpointWithIt()
    {
        string md = TransitMarkdownFormatter.ToMarkdown(Transits(Natal("N1"), new DateOnly(1400, 2, 28), new DateOnly(1400, 3, 2)));

        md.Should().Contain("## Transit House Overlays (at 1400-02-29)");
        md.Should().Contain("## Secondary Progressions");
        md.Should().Contain("### Progressed Positions (at report start, 1400-02-28)");
        md.Should().Contain("## Solar Arc Directions");
        md.Should().Contain("### Directed Positions (at report start, 1400-02-28)");
        md.Should().Contain("**Progressed Date:** 1300-06-23 10:56 UT");
    }

    /// <summary>A typed bound in 1582-10-05 to 1582-10-14 never occurred and the transits are refused.</summary>
    [Fact]
    public void TransitsFromABoundInTheTenDays_Should_BeRefused()
    {
        Action act = () => Transits(Natal("N1"), new DateOnly(1582, 10, 10), new DateOnly(1582, 10, 20));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*1582-10-10 does not exist*");
    }

    #endregion

    #region Helpers

    /// <summary>The transits to a natal chart over a range of dates.</summary>
    private TransitResult Transits(ChartResult natal, DateOnly start, DateOnly end) =>
        new TransitService(_fixture.EphemerisService, _logger).Calculate(new TransitInput
        {
            NatalChart = natal,
            ReportStartDate = start,
            ReportEndDate = end,
        });

    /// <summary>
    /// A synthetic natal chart at Greenwich, read under D21 at UT: N1 Julian 1300-03-15 12:00, N5
    /// 2000-01-01 12:00.
    /// </summary>
    /// <param name="key">N1 or N5.</param>
    /// <returns>The natal chart.</returns>
    private ChartResult Natal(string key)
    {
        DateTime reading = key switch
        {
            "N1" => new DateTime(1300, 3, 15, 12, 0, 0),
            "N5" => new DateTime(2000, 1, 1, 12, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "N1 or N5."),
        };
        return _engine.Calculate(new ChartInput
        {
            Name = key,
            LocalDateTime = reading,
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4769,
            Longitude = 0.0,
            LocationName = "Greenwich",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
    }

    #endregion
}
