using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Engine-level tests for station classification. Each truth-table case is one
/// row of the speed-sign × speed-trend table, computed through
/// <see cref="ChartEngine.Calculate"/> on the real ephemeris so the assertions
/// hold for the same code path the exports use. Further tests exercise
/// <see cref="StationFinder"/> directly, on the real ephemeris and on a
/// synthetic one.
/// </summary>
/// <remarks>
/// <para>
/// The four Venus moments are anchored to the 2026 Venus stations: station
/// retrograde 2026-10-03 07:15:54 UT at 218.4912° and station direct
/// 2026-11-14 00:27:28 UT at 202.8637°. Row 1 is the Venus-station sample moment
/// itself (2026-10-01 10:56:30 UT). Reference values were measured through the
/// Core <c>EphemerisService</c> with an independent bisection and agree with an
/// external pyswisseph computation to within 3 s.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class StationFinderTests
{
    private static readonly DateTime VenusStationRetrogradeUtc = new(2026, 10, 3, 7, 15, 54, DateTimeKind.Utc);
    private static readonly DateTime VenusStationDirectUtc = new(2026, 11, 14, 0, 27, 28, DateTimeKind.Utc);
    private const double VenusStationRetrogradeLongitude = 218.4912;
    private const double VenusStationDirectLongitude = 202.8637;

    /// <summary>One arc-second in degrees.</summary>
    private const double ArcSecond = 1.0 / 3600.0;

    private static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds(2);

    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public StationFinderTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Builds an Event chart at the given UT instant. The zone is <c>Etc/UTC</c>
    /// so the local wall-clock time is the UT time, and the location is the
    /// sample chart's (it does not affect planetary speeds).
    /// </summary>
    private ChartResult CalculateAtUt(DateTime utc) =>
        _engine.Calculate(new ChartInput
        {
            Name = "Station truth-table case",
            LocalDateTime = utc,
            TimeZone = "Etc/UTC",
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio",
            EventType = "Event",
        });

    private static StationInfo VenusStation(ChartResult chart) =>
        chart.Stations.Should().ContainSingle(s => s.PlanetName == "Venus",
            "Venus must be flagged as stationing at this moment").Subject;

    private double JulianDayOf(DateTime utc) =>
        _ephemeris.DateToJulianDay(utc.Year, utc.Month, utc.Day,
            utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0);

    #region Truth table — one case per row, through ChartEngine.Calculate

    [Fact]
    public void Row1_PositiveSpeedFalling_ShouldBeStationRetrograde()
    {
        // The Venus-station sample moment: Venus +0.0705°/day and decelerating toward
        // the 2026-10-03 station retrograde, 1.85 d ahead.
        ChartResult chart = CalculateAtUt(new DateTime(2026, 10, 1, 10, 56, 30));

        StationInfo venus = VenusStation(chart);

        venus.DailySpeed.Should().BePositive();
        venus.StationType.Should().Be("Station Retrograde");
        venus.Phase.Should().Be(StationPhase.Approaching);
        venus.DaysToStation.Should().BeApproximately(1.85, 0.01);
        venus.StationTimeUtc.Should().BeCloseTo(VenusStationRetrogradeUtc, TimeTolerance);
        venus.StationLongitude.Should().BeApproximately(VenusStationRetrogradeLongitude, ArcSecond);
    }

    [Fact]
    public void Row2_PositiveSpeedRising_ShouldBeStationDirect()
    {
        // 1.5 d after the 2026-11-14 station direct: Venus direct and accelerating.
        ChartResult chart = CalculateAtUt(new DateTime(2026, 11, 15, 12, 27, 28));

        StationInfo venus = VenusStation(chart);

        venus.DailySpeed.Should().BePositive();
        venus.StationType.Should().Be("Station Direct");
        venus.Phase.Should().Be(StationPhase.Separating);
        venus.DaysToStation.Should().BeApproximately(-1.50, 0.01);
        venus.StationTimeUtc.Should().BeCloseTo(VenusStationDirectUtc, TimeTolerance);
        venus.StationLongitude.Should().BeApproximately(VenusStationDirectLongitude, ArcSecond);
    }

    [Fact]
    public void Row3_NegativeSpeedFalling_ShouldBeStationDirect()
    {
        // 1.5 d before the 2026-11-14 station direct: Venus retrograde and slowing.
        ChartResult chart = CalculateAtUt(new DateTime(2026, 11, 12, 12, 27, 28));

        StationInfo venus = VenusStation(chart);

        venus.DailySpeed.Should().BeNegative();
        venus.StationType.Should().Be("Station Direct");
        venus.Phase.Should().Be(StationPhase.Approaching);
        venus.DaysToStation.Should().BeApproximately(1.50, 0.01);
        venus.StationTimeUtc.Should().BeCloseTo(VenusStationDirectUtc, TimeTolerance);
        venus.StationLongitude.Should().BeApproximately(VenusStationDirectLongitude, ArcSecond);
    }

    [Fact]
    public void Row4_NegativeSpeedRising_ShouldBeStationRetrograde()
    {
        // 1 d after the 2026-10-03 station retrograde: Venus retrograde and accelerating.
        ChartResult chart = CalculateAtUt(new DateTime(2026, 10, 4, 7, 15, 54));

        StationInfo venus = VenusStation(chart);

        venus.DailySpeed.Should().BeNegative();
        venus.StationType.Should().Be("Station Retrograde");
        venus.Phase.Should().Be(StationPhase.Separating);
        venus.DaysToStation.Should().BeApproximately(-1.00, 0.01);
        venus.StationTimeUtc.Should().BeCloseTo(VenusStationRetrogradeUtc, TimeTolerance);
        venus.StationLongitude.Should().BeApproximately(VenusStationRetrogradeLongitude, ArcSecond);
    }

    #endregion

    #region Exact station times, through StationFinder on the real ephemeris

    [Fact]
    public void FindNearestStation_VenusOctober2026_ShouldBeStationRetrogradeAt071554Ut()
    {
        double jd = JulianDayOf(new DateTime(2026, 10, 1, 10, 56, 30));

        StationSearchResult result = StationFinder.FindNearestStation(_ephemeris, CelestialBodyId.Venus, jd);

        result.IsRetrogradeStation.Should().BeTrue();
        result.StationTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.StationTimeUtc.Should().BeCloseTo(VenusStationRetrogradeUtc, TimeTolerance);
        result.StationLongitude.Should().BeApproximately(VenusStationRetrogradeLongitude, ArcSecond);
        result.Acceleration.Should().BeNegative();
    }

    [Fact]
    public void FindNearestStation_VenusNovember2026_ShouldBeStationDirectAt002728Ut()
    {
        double jd = JulianDayOf(new DateTime(2026, 11, 12, 12, 27, 28));

        StationSearchResult result = StationFinder.FindNearestStation(_ephemeris, CelestialBodyId.Venus, jd);

        result.IsRetrogradeStation.Should().BeFalse();
        result.StationTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.StationTimeUtc.Should().BeCloseTo(VenusStationDirectUtc, TimeTolerance);
        result.StationLongitude.Should().BeApproximately(VenusStationDirectLongitude, ArcSecond);
        result.Acceleration.Should().BePositive();
    }

    [Fact]
    public void FindNearestStation_SpeedAtReference_ShouldEqualChartSpeedExactly()
    {
        // The finder samples through the same swe_calc_ut call and flags that
        // produce a chart's speeds, so its sample at the chart instant must be
        // bit-identical to the chart's SpeedInLongitude — not merely close.
        ChartResult chart = CalculateAtUt(new DateTime(2026, 10, 1, 10, 56, 30));
        PlanetPosition venus = chart.Positions.Single(p => p.Body == CelestialBodyId.Venus);

        StationSearchResult result = StationFinder.FindNearestStation(
            _ephemeris, CelestialBodyId.Venus, chart.JulianDay);

        result.SpeedAtReference.Should().Be(venus.SpeedInLongitude);
    }

    #endregion

    #region Synthetic ephemeris — cases the real sky cannot be made to produce

    /// <summary>2026-10-01 12:00 UT — an exactly representable Julian Day.</summary>
    private const double SyntheticReferenceJd = 2461315.0;

    [Fact]
    public void FindNearestStation_ZeroSpeedAtReference_ShouldReportStationNow()
    {
        // Mars stopped exactly at the reference instant with its speed falling
        // through zero: the station is that instant and it is retrograde.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Mars, speedAtReference: 0.0, accelerationPerDay: -0.01);

        StationSearchResult result = StationFinder.FindNearestStation(
            ephemeris, CelestialBodyId.Mars, SyntheticReferenceJd);

        result.SpeedAtReference.Should().Be(0.0);
        result.Acceleration.Should().BeApproximately(-0.01, 1e-12);
        result.IsRetrogradeStation.Should().BeTrue();
        result.StationJulianDay.Should().Be(SyntheticReferenceJd);
        result.DaysToStation.Should().Be(0.0);
        result.Phase.Should().Be(StationPhase.Separating, "a station at the instant itself is not ahead of it");
        result.StationTimeUtc.Should().Be(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void FindNearestStation_ZeroSpeedAtReferenceAccelerating_ShouldBeStationDirectNow()
    {
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Mars, speedAtReference: 0.0, accelerationPerDay: +0.01);

        StationSearchResult result = StationFinder.FindNearestStation(
            ephemeris, CelestialBodyId.Mars, SyntheticReferenceJd);

        result.IsRetrogradeStation.Should().BeFalse();
        result.StationJulianDay.Should().Be(SyntheticReferenceJd);
        result.DaysToStation.Should().Be(0.0);
    }

    [Fact]
    public void FindNearestStation_StationBeyondThirtyDays_ShouldThrow()
    {
        // Speed +0.05 falling by 0.001/day reaches zero 50 days ahead — outside
        // the window. No fallback to the sign of the speed is permitted.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Mercury, speedAtReference: 0.05, accelerationPerDay: -0.001);

        Action act = () => StationFinder.FindNearestStation(ephemeris, CelestialBodyId.Mercury, SyntheticReferenceJd);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mercury*2461315.000000*30*");
    }

    [Fact]
    public void FindNearestStation_ConstantSpeed_ShouldThrow()
    {
        // A speed that never changes has no sign change in either direction.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Jupiter, speedAtReference: 0.01, accelerationPerDay: 0.0);

        Action act = () => StationFinder.FindNearestStation(ephemeris, CelestialBodyId.Jupiter, SyntheticReferenceJd);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jupiter*2461315.000000*");
    }

    [Fact]
    public void FindNearestStation_NullEphemeris_ShouldThrowArgumentNullException()
    {
        Action act = () => StationFinder.FindNearestStation(null!, CelestialBodyId.Venus, SyntheticReferenceJd);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0.05, -0.025, 0.002, true, StationPhase.Approaching, 2.5)]   // + falling → retrograde ahead
    [InlineData(0.05, +0.025, 0.002, false, StationPhase.Separating, -2.5)]  // + rising  → direct behind
    [InlineData(-0.05, +0.025, -0.002, false, StationPhase.Approaching, 2.5)] // − falling → direct ahead
    [InlineData(-0.05, -0.025, -0.002, true, StationPhase.Separating, -2.5)]  // − rising  → retrograde behind
    public void FindNearestStation_TruthTable_ShouldFollowTrendNotSign(
        double speed, double acceleration, double curvature,
        bool expectRetrograde, StationPhase expectPhase, double expectDays)
    {
        // The curvature puts the crossing at 2.5 d while the Newton seed from
        // the (curvature-free) central difference is 2.0 d, so every row has
        // to bracket the crossing and bisect it rather than land on it.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Venus, speed, acceleration, speedCurvature: curvature);

        StationSearchResult result = StationFinder.FindNearestStation(
            ephemeris, CelestialBodyId.Venus, SyntheticReferenceJd);

        result.Acceleration.Should().BeApproximately(acceleration, 1e-12,
            "the central difference cancels the quadratic term exactly");
        result.IsRetrogradeStation.Should().Be(expectRetrograde);
        result.Phase.Should().Be(expectPhase);
        result.DaysToStation.Should().BeApproximately(expectDays, StationFinder.TimePrecisionDays);
    }

    [Fact]
    public void FindNearestStation_ProbeLandingExactlyOnZeroSpeed_ShouldReturnThatInstant()
    {
        // With a straight-line speed the Newton seed |v/a| = 2 d is the crossing
        // itself: the first probe at exactly ReferenceJd + 2 samples a speed of
        // bit-exactly 0.0 and the finder returns that instant without bisecting.
        // The equality assertions below are deliberately exact — a tolerance
        // would also accept the bisected answer and hide which branch ran.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticReferenceJd)
            .WithBody(CelestialBodyId.Mercury, speedAtReference: 0.05, accelerationPerDay: -0.025);

        StationSearchResult result = StationFinder.FindNearestStation(
            ephemeris, CelestialBodyId.Mercury, SyntheticReferenceJd);

        result.StationJulianDay.Should().Be(SyntheticReferenceJd + 2.0);
        result.DaysToStation.Should().Be(2.0);
        result.IsRetrogradeStation.Should().BeTrue();
        result.Phase.Should().Be(StationPhase.Approaching);
        result.StationTimeUtc.Should().Be(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
    }

    #endregion

    #region Culture independence

    // The finder's messages are built with the invariant culture, so their en-US
    // expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="FindNearestStation_StationBeyondThirtyDays_ShouldThrow"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void FindNearestStation_StationBeyondThirtyDays_ShouldThrow_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        FindNearestStation_StationBeyondThirtyDays_ShouldThrow();
    }

    /// <summary>
    /// <see cref="FindNearestStation_ConstantSpeed_ShouldThrow"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void FindNearestStation_ConstantSpeed_ShouldThrow_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        FindNearestStation_ConstantSpeed_ShouldThrow();
    }

    #endregion
}
