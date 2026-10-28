using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="StationService"/>. Verifies station detection
/// for all eight eligible planets, threshold boundaries, station types,
/// proximity calculation, and exclusion of non-stationing bodies.
/// </summary>
/// <remarks>
/// Speeds are supplied through <see cref="SyntheticSpeedEphemeris"/>, whose
/// speed is a straight line in time, so each test can state both the speed at
/// the chart instant and whether |speed| is rising or falling. The station
/// type must follow the trend, never the sign of the speed alone.
/// </remarks>
public sealed class StationServiceTests
{
    /// <summary>2026-10-01 12:00 UT — an exactly representable Julian Day.</summary>
    private const double ReferenceJd = 2461315.0;

    private readonly StationService _service = new();

    #region Mercury Station Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_MercuryBelowThreshold_ShouldDetectStation()
    {
        // Mercury threshold = 0.15, speed 0.10 → stationing
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 0.10));

        result.Should().HaveCount(1);
        result[0].PlanetName.Should().Be("Mercury");
        result[0].IsStationing.Should().BeTrue();
        result[0].StationThreshold.Should().Be(0.15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_MercuryAboveThreshold_ShouldNotDetectStation()
    {
        // Mercury threshold = 0.15, speed 1.2 → normal
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 1.2));

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_MercuryExactlyAtThreshold_ShouldNotDetectStation()
    {
        // Mercury threshold = 0.15, speed exactly 0.15 → NOT stationing (>= threshold)
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 0.15));

        result.Should().BeEmpty();
    }

    #endregion

    #region Station Type Tests — one pair per speed sign, rising and falling

    // Each case gives the speed a small curvature (c = ±0.002°/day³) so that
    // the zero crossing sits at 2.5 d rather than on the finder's Newton seed
    // of 2.0 d: the search must then bracket the crossing and bisect it, which
    // is the path every real-ephemeris station takes. With a straight-line
    // speed the seed lands bit-exactly on the crossing and the finder returns
    // through its exact-zero branch instead (pinned separately in
    // StationFinderTests).

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_PositiveSpeedFalling_ShouldBeStationRetrogradeApproaching()
    {
        // Mercury direct at +0.05 and decelerating: speed reaches zero 2.5 days
        // ahead, going + → −, so the station ahead is retrograde.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 0.05, acceleration: -0.025, curvature: 0.002));

        result.Should().HaveCount(1);
        result[0].DailySpeed.Should().BePositive();
        result[0].StationType.Should().Be("Station Retrograde");
        result[0].Phase.Should().Be(StationPhase.Approaching);
        result[0].DaysToStation.Should().BeApproximately(2.5, 0.001);
        result[0].StationJulianDay.Should().BeApproximately(ReferenceJd + 2.5, 0.001);
        result[0].StationTimeUtc.Should().Be(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_PositiveSpeedRising_ShouldBeStationDirectSeparating()
    {
        // Mercury direct at +0.05 and accelerating: speed was zero 2.5 days
        // ago, going − → +, so the station behind is direct.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 0.05, acceleration: +0.025, curvature: 0.002));

        result.Should().HaveCount(1);
        result[0].DailySpeed.Should().BePositive();
        result[0].StationType.Should().Be("Station Direct");
        result[0].Phase.Should().Be(StationPhase.Separating);
        result[0].DaysToStation.Should().BeApproximately(-2.5, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NegativeSpeedFalling_ShouldBeStationDirectApproaching()
    {
        // Mercury retrograde at −0.05 with |speed| falling: speed reaches zero
        // 2.5 days ahead, going − → +, so the station ahead is direct.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", -0.05, acceleration: +0.025, curvature: -0.002));

        result.Should().HaveCount(1);
        result[0].DailySpeed.Should().BeNegative();
        result[0].StationType.Should().Be("Station Direct");
        result[0].Phase.Should().Be(StationPhase.Approaching);
        result[0].DaysToStation.Should().BeApproximately(2.5, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NegativeSpeedRising_ShouldBeStationRetrogradeSeparating()
    {
        // Mercury retrograde at −0.05 with |speed| rising: speed was zero
        // 2.5 days ago, going + → −, so the station behind is retrograde.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", -0.05, acceleration: -0.025, curvature: -0.002));

        result.Should().HaveCount(1);
        result[0].DailySpeed.Should().BeNegative();
        result[0].StationType.Should().Be("Station Retrograde");
        result[0].Phase.Should().Be(StationPhase.Separating);
        result[0].DaysToStation.Should().BeApproximately(-2.5, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_ZeroSpeedDecelerating_ShouldBeStationRetrogradeNow()
    {
        // Saturn stopped exactly at the chart instant with speed falling through
        // zero (+ → −): the station is now and it is retrograde.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Saturn, "Saturn", 0.0, acceleration: -0.004));

        result.Should().HaveCount(1);
        result[0].StationType.Should().Be("Station Retrograde");
        result[0].StationProximity.Should().Be(0.0);
        result[0].DaysToStation.Should().Be(0.0);
        result[0].StationJulianDay.Should().Be(ReferenceJd);
        result[0].Phase.Should().Be(StationPhase.Separating, "a station at the instant itself is not ahead of it");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_ZeroSpeedAccelerating_ShouldBeStationDirectNow()
    {
        // Saturn stopped exactly at the chart instant with speed rising through
        // zero (− → +): the station is now and it is direct.
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Saturn, "Saturn", 0.0, acceleration: +0.004));

        result.Should().HaveCount(1);
        result[0].StationType.Should().Be("Station Direct");
        result[0].StationProximity.Should().Be(0.0);
        result[0].DaysToStation.Should().Be(0.0);
    }

    #endregion

    #region Proximity Calculation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_HalfThresholdSpeed_ShouldHave50PercentProximity()
    {
        // Saturn threshold = 0.01, speed = 0.005 → proximity = 0.5
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Saturn, "Saturn", 0.005));

        result.Should().HaveCount(1);
        result[0].StationProximity.Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NearZeroSpeed_ShouldHaveNearZeroProximity()
    {
        // Saturn threshold = 0.01, speed = 0.0001 → proximity ≈ 0.01
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Saturn, "Saturn", 0.0001));

        result.Should().HaveCount(1);
        result[0].StationProximity.Should().BeApproximately(0.01, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_RetrogradePlanet_ProximityShouldUseAbsoluteSpeed()
    {
        // Saturn threshold = 0.01, speed = -0.005 → proximity = 0.5 (uses |speed|)
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Saturn, "Saturn", -0.005));

        result.Should().HaveCount(1);
        result[0].StationProximity.Should().BeApproximately(0.5, 0.0001);
        result[0].DailySpeed.Should().BeNegative();
    }

    #endregion

    #region All Planet Threshold Tests

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CelestialBodyId.Venus,   "Venus",   0.12,  0.06)]
    [InlineData(CelestialBodyId.Mars,    "Mars",    0.08,  0.04)]
    [InlineData(CelestialBodyId.Jupiter, "Jupiter", 0.02,  0.01)]
    [InlineData(CelestialBodyId.Uranus,  "Uranus",  0.004, 0.002)]
    [InlineData(CelestialBodyId.Neptune, "Neptune", 0.003, 0.0015)]
    [InlineData(CelestialBodyId.Pluto,   "Pluto",   0.002, 0.001)]
    public void Evaluate_PlanetBelowThreshold_ShouldDetectStation(
        CelestialBodyId body, string name, double threshold, double speed)
    {
        IReadOnlyList<StationInfo> result = Evaluate(Body(body, name, speed));

        result.Should().HaveCount(1);
        result[0].PlanetName.Should().Be(name);
        result[0].IsStationing.Should().BeTrue();
        result[0].StationThreshold.Should().Be(threshold);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CelestialBodyId.Venus,   "Venus",   0.50)]
    [InlineData(CelestialBodyId.Mars,    "Mars",    0.30)]
    [InlineData(CelestialBodyId.Jupiter, "Jupiter", 0.05)]
    [InlineData(CelestialBodyId.Uranus,  "Uranus",  0.01)]
    [InlineData(CelestialBodyId.Neptune, "Neptune", 0.008)]
    [InlineData(CelestialBodyId.Pluto,   "Pluto",   0.005)]
    public void Evaluate_PlanetAboveThreshold_ShouldNotDetectStation(
        CelestialBodyId body, string name, double speed)
    {
        IReadOnlyList<StationInfo> result = Evaluate(Body(body, name, speed));

        result.Should().BeEmpty();
    }

    #endregion

    #region Excluded Bodies Tests

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CelestialBodyId.Sun,      "Sun")]
    [InlineData(CelestialBodyId.Moon,     "Moon")]
    [InlineData(CelestialBodyId.TrueNode, "True Node")]
    [InlineData(CelestialBodyId.Chiron,   "Chiron")]
    public void Evaluate_ExcludedBody_ShouldNeverStation(
        CelestialBodyId body, string name)
    {
        // Even with zero speed, excluded bodies should not appear
        IReadOnlyList<StationInfo> result = Evaluate(Body(body, name, 0.0));

        result.Should().BeEmpty();
    }

    #endregion

    #region Multiple Planets and Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_MultipleStationingPlanets_ShouldReturnAll()
    {
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 0.05),
            Body(CelestialBodyId.Saturn,  "Saturn",  -0.003),
            Body(CelestialBodyId.Jupiter, "Jupiter", 0.10));  // above threshold

        result.Should().HaveCount(2);
        result.Select(s => s.PlanetName).Should().Contain("Mercury");
        result.Select(s => s.PlanetName).Should().Contain("Saturn");
        result.Select(s => s.PlanetName).Should().NotContain("Jupiter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_EmptyPositions_ShouldReturnEmptyList()
    {
        IReadOnlyList<StationInfo> result = _service.Evaluate(
            [], ReferenceJd, new SyntheticSpeedEphemeris(ReferenceJd));

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NullPositions_ShouldThrowArgumentNullException()
    {
        Action act = () => _service.Evaluate(null!, ReferenceJd, new SyntheticSpeedEphemeris(ReferenceJd));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NullEphemeris_ShouldThrowArgumentNullException()
    {
        Action act = () => _service.Evaluate([], ReferenceJd, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_NoStationingPlanets_ShouldReturnEmptyList()
    {
        IReadOnlyList<StationInfo> result = Evaluate(
            Body(CelestialBodyId.Mercury, "Mercury", 1.2),
            Body(CelestialBodyId.Venus,   "Venus",   1.0),
            Body(CelestialBodyId.Mars,    "Mars",    0.5),
            Body(CelestialBodyId.Jupiter, "Jupiter", 0.1),
            Body(CelestialBodyId.Saturn,  "Saturn",  0.05));

        result.Should().BeEmpty();
    }

    #endregion

    #region Summary Property Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Summary_StationingPlanet_ShouldIncludeTypeAndProximity()
    {
        StationInfo info = new()
        {
            PlanetName = "Saturn",
            StationType = "Station Retrograde",
            IsStationing = true,
            DailySpeed = -0.005,
            StationThreshold = 0.01,
            StationProximity = 0.5,
        };

        info.Summary.Should().Contain("Saturn");
        info.Summary.Should().Contain("Station Retrograde");
        info.Summary.Should().Contain("50% of threshold");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Summary_NonStationingPlanet_ShouldSayNotStationing()
    {
        StationInfo info = new()
        {
            PlanetName = "Mars",
            StationType = "None",
            IsStationing = false,
            DailySpeed = 0.5,
            StationThreshold = 0.08,
            StationProximity = null,
        };

        info.Summary.Should().Contain("Not stationing");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0.5874, "59% of threshold")]   // the Venus station sample: 58.74 rounds up, not down to 58
    [InlineData(0.58,   "58% of threshold")]   // 0.58 × 100 = 57.999…: truncation printed 57
    [InlineData(0.29,   "29% of threshold")]   // 0.29 × 100 = 28.999…: truncation printed 28
    [InlineData(0.755,  "76% of threshold")]   // exact midpoint rounds away from zero
    public void Summary_Proximity_ShouldRoundToNearestPercent(double proximity, string expected)
    {
        StationInfo info = new()
        {
            PlanetName = "Venus",
            StationType = "Station Retrograde",
            IsStationing = true,
            DailySpeed = proximity * 0.12,
            StationThreshold = 0.12,
            StationProximity = proximity,
        };

        info.Summary.Should().Contain(expected);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A body's speed at the chart instant and, optionally, the rate at which
    /// that speed changes. When no acceleration is given the body decelerates
    /// toward a station ahead, so any flagged body has a station for the
    /// finder to locate within the search window.
    /// </summary>
    private sealed record BodySpec(
        CelestialBodyId Body, string Name, double Speed, double Acceleration, double Curvature);

    private static BodySpec Body(
        CelestialBodyId body, string name, double speed, double? acceleration = null, double curvature = 0.0) =>
        new(body, name, speed,
            acceleration ?? (speed >= 0.0 ? -(speed + 0.02) / 2.0 : (Math.Abs(speed) + 0.02) / 2.0),
            curvature);

    /// <summary>
    /// Builds the positions and a synthetic ephemeris that agrees with them at
    /// the chart instant, then evaluates them through the service.
    /// </summary>
    private IReadOnlyList<StationInfo> Evaluate(params BodySpec[] specs)
    {
        SyntheticSpeedEphemeris ephemeris = new(ReferenceJd);
        List<PlanetPosition> positions = [];

        foreach (BodySpec spec in specs)
        {
            ephemeris.WithBody(spec.Body, spec.Speed, spec.Acceleration, speedCurvature: spec.Curvature);
            positions.Add(CreatePosition(spec.Body, spec.Name, spec.Speed));
        }

        return _service.Evaluate(positions, ReferenceJd, ephemeris);
    }

    /// <summary>
    /// Create a minimal <see cref="PlanetPosition"/> for station testing.
    /// </summary>
    private static PlanetPosition CreatePosition(
        CelestialBodyId body, string name, double speedInLongitude) => new()
    {
        Body = body,
        Name = name,
        Longitude = 100.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = speedInLongitude,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        House = 1,
    };

    #endregion
}
