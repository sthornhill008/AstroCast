using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="AspectPerfectionFinder"/>: first-approach following
/// and first-exact-aspect search, on the real ephemeris at moments whose
/// ground truth was measured by an independent scan-and-bisect, and on a
/// synthetic ephemeris for the shapes the real sky cannot be made to produce.
/// </summary>
/// <remarks>
/// Real reference moments (independent bisection on engine longitudes):
/// Mercury conjunct Venus 2026-10-07 00:10:23 UT (Venus stations retrograde
/// inside the window, 2026-10-03 07:15:54 UT); Venus square Mars 2026-07-29
/// 07:09 UT; Moon sextile Venus 2026-04-23 18:52 UT; Mars square Mercury
/// 2026-10-02 09:14 UT; Saturn sextile Mars 2026-07-19 18:10 UT.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class AspectPerfectionFinderTests
{
    private readonly EphemerisService _ephemeris;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    public AspectPerfectionFinderTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    private double Jd(DateTime utc) =>
        _ephemeris.DateToJulianDay(utc.Year, utc.Month, utc.Day, utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0);

    private static DateTime Utc(int y, int mo, int d, int h, int mi, int s) => new(y, mo, d, h, mi, s, DateTimeKind.Utc);

    #region SignedTarget

    [Theory]
    [InlineData(60.0, 100.0, 50.0, 60.0)]    // A 50° ahead of B → +60 side
    [InlineData(60.0, 50.0, 100.0, -60.0)]   // A behind → −60 side
    [InlineData(90.0, 10.0, 300.0, 90.0)]    // A ahead across the 0° wrap (+70)
    [InlineData(0.0, 50.0, 100.0, 0.0)]      // conjunction: unsigned
    [InlineData(180.0, 50.0, 100.0, 180.0)]  // opposition: unsigned
    public void SignedTarget_ShouldFollowThePairsOrientation(double angle, double lonA, double lonB, double expected)
    {
        AspectPerfectionFinder.SignedTarget(angle, lonA, lonB).Should().Be(expected);
    }

    #endregion

    #region FollowFirstApproach — real ephemeris

    [Fact]
    public void FollowFirstApproach_MercuryVenusConjunction_ShouldPerfectDespiteVenusStation()
    {
        // The Venus-station sample instant (a bare value). Venus stations retrograde 1.85 d later, yet the
        // conjunction still perfects on this approach because Mercury is the faster body.
        double start = Jd(Utc(2026, 10, 1, 10, 56, 30));

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
            _ephemeris, CelestialBodyId.Mercury, CelestialBodyId.Venus, 0.0, start);

        approach.Outcome.Should().Be(AspectApproachOutcome.Perfects);
        approach.Perfects.Should().BeTrue();
        approach.OrbAtStart.Should().BeApproximately(7.1446, 0.001);
        approach.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 10, 7, 0, 10, 23), TimeSpan.FromSeconds(2));
        approach.ClosestOrb.Should().Be(0.0);
    }

    [Fact]
    public void FollowFirstApproach_VenusMarsSquare_ShouldPerfect()
    {
        // Id 42's instant (a bare value): Mars L1, Venus Lx; Venus applies to the square from behind.
        double start = Jd(Utc(2026, 7, 16, 20, 22, 57));
        double lonMars = _ephemeris.CalculatePositionLongitudeOnly(start, CelestialBodyId.Mars).Longitude;
        double lonVenus = _ephemeris.CalculatePositionLongitudeOnly(start, CelestialBodyId.Venus).Longitude;
        double target = AspectPerfectionFinder.SignedTarget(90.0, lonMars, lonVenus);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
            _ephemeris, CelestialBodyId.Mars, CelestialBodyId.Venus, target, start);

        approach.Outcome.Should().Be(AspectApproachOutcome.Perfects);
        approach.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 7, 29, 7, 9, 0), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void FollowFirstApproach_MoonVenusSextile_ShouldPerfectWithinHours()
    {
        // Id 90: a Moon-involving pair closing at ~12.7°/day; the hourly step must not miss it.
        double start = Jd(Utc(2026, 4, 23, 15, 4, 35));
        double lonMoon = _ephemeris.CalculatePositionLongitudeOnly(start, CelestialBodyId.Moon).Longitude;
        double lonVenus = _ephemeris.CalculatePositionLongitudeOnly(start, CelestialBodyId.Venus).Longitude;
        double target = AspectPerfectionFinder.SignedTarget(60.0, lonMoon, lonVenus);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
            _ephemeris, CelestialBodyId.Moon, CelestialBodyId.Venus, target, start);

        approach.Outcome.Should().Be(AspectApproachOutcome.Perfects);
        approach.PerfectionTimeUtc.Should().BeCloseTo(Utc(2026, 4, 23, 18, 52, 0), TimeSpan.FromSeconds(60));
        (approach.PerfectionJulianDay!.Value - start).Should().BeLessThan(0.2);
    }

    [Fact]
    public void FollowFirstApproach_SeparatingPair_ShouldTurnAroundAtOnce()
    {
        // Following the Mercury–Venus conjunction *backwards in orientation* — i.e. after
        // perfection, from a point where the pair is already separating — fails at once.
        double start = Jd(Utc(2026, 10, 8, 0, 0, 0)); // the day after the 10-07 conjunction

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
            _ephemeris, CelestialBodyId.Mercury, CelestialBodyId.Venus, 0.0, start);

        approach.Outcome.Should().Be(AspectApproachOutcome.TurnsAround);
        approach.ClosestOrb.Should().BeApproximately(approach.OrbAtStart, 0.05);
        approach.ClosestJulianDay.Should().Be(start);
    }

    #endregion

    #region FindFirstExactMajorAspect — real ephemeris

    [Fact]
    public void FindFirstExactMajorAspect_MarsToMercury_BeforeTheConjunction_ShouldBeTheSquare()
    {
        double start = Jd(Utc(2026, 10, 1, 10, 56, 30));
        double end = Jd(Utc(2026, 10, 7, 0, 10, 23));

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
            _ephemeris, CelestialBodyId.Mars, CelestialBodyId.Mercury, start, end);

        hit.Should().NotBeNull();
        hit!.AspectType.Should().Be(AspectType.Square);
        hit.TimeUtc.Should().BeCloseTo(Utc(2026, 10, 2, 9, 14, 0), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void FindFirstExactMajorAspect_SaturnToMars_BeforeTheSquare_ShouldBeTheSextile()
    {
        double start = Jd(Utc(2026, 7, 16, 20, 22, 57));
        double end = Jd(Utc(2026, 7, 29, 7, 9, 0));

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
            _ephemeris, CelestialBodyId.Saturn, CelestialBodyId.Mars, start, end);

        hit.Should().NotBeNull();
        hit!.AspectType.Should().Be(AspectType.Sextile);
        hit.TimeUtc.Should().BeCloseTo(Utc(2026, 7, 19, 18, 10, 0), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void FindFirstExactMajorAspect_NoneInWindow_ShouldReturnNull()
    {
        // Saturn and Venus make no exact Ptolemaic aspect between the Id 90 chart and its perfection (3.8 h).
        double start = Jd(Utc(2026, 4, 23, 15, 4, 35));
        double end = Jd(Utc(2026, 4, 23, 18, 52, 0));

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
            _ephemeris, CelestialBodyId.Saturn, CelestialBodyId.Venus, start, end);

        hit.Should().BeNull();
    }

    #endregion

    #region Synthetic ephemeris — shapes the real sky cannot be made to produce

    /// <summary>2026-10-01 12:00 UT, an exactly representable Julian Day.</summary>
    private const double T0 = 2461315.0;

    /// <summary>
    /// Body A starts at 100°, moving +1°/day and decelerating 0.5°/day², so it
    /// reverses at +2 d having reached 101°. Body B is fixed at the given
    /// longitude. Following the conjunction (target 0) then perfects, turns
    /// around or touches depending on where B sits.
    /// </summary>
    private static SyntheticSpeedEphemeris Reversing(double longitudeB) =>
        new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Mars, speedAtReference: 1.0, accelerationPerDay: -0.5, longitudeAtReference: 100.0)
            .WithBody(CelestialBodyId.Jupiter, speedAtReference: 0.0, accelerationPerDay: 0.0, longitudeAtReference: longitudeB);

    [Fact]
    public void FollowFirstApproach_Synthetic_ShouldPerfectAtTheAnalyticRoot()
    {
        // lon_A(t) = 100 + t − 0.25 t² reaches 100.8 at t = (1 − √0.2)/0.5 = 1.105573 d.
        SyntheticSpeedEphemeris eph = Reversing(100.8);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(eph, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        approach.Outcome.Should().Be(AspectApproachOutcome.Perfects);
        approach.PerfectionJulianDay.Should().BeApproximately(T0 + 1.105573, AspectPerfectionFinder.TimePrecisionDays);
        approach.OrbAtStart.Should().BeApproximately(0.8, 1e-12);
    }

    [Fact]
    public void FollowFirstApproach_Synthetic_Turnaround_ShouldReportTheClosestOrbAndItsTime()
    {
        // B at 101.5: A gets within 0.5° at t = 2 d and then falls back.
        SyntheticSpeedEphemeris eph = Reversing(101.5);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(eph, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        approach.Outcome.Should().Be(AspectApproachOutcome.TurnsAround);
        approach.Perfects.Should().BeFalse();
        approach.ClosestOrb.Should().BeApproximately(0.5, 1e-9);
        approach.ClosestJulianDay.Should().BeApproximately(T0 + 2.0, AspectPerfectionFinder.SampleStepDays);
        approach.PerfectionJulianDay.Should().BeNull();
    }

    [Fact]
    public void FollowFirstApproach_Synthetic_Tangency_ShouldBeAFailedApplication()
    {
        // B at 101.05: the orb dips to 0.05° without ever crossing zero.
        SyntheticSpeedEphemeris eph = Reversing(101.05);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(eph, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        approach.Outcome.Should().Be(AspectApproachOutcome.TurnsAround, "a tangency is never a perfection");
        approach.ClosestOrb.Should().BeApproximately(0.05, 1e-9);
        approach.ClosestJulianDay.Should().BeApproximately(T0 + 2.0, AspectPerfectionFinder.SampleStepDays);
    }

    [Fact]
    public void FollowFirstApproach_Synthetic_StillApplyingAtTheCap_ShouldReportCapped()
    {
        // A closes on B at 0.001°/day from 5° away: 5,000 days to exact.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Mars, speedAtReference: 0.001, accelerationPerDay: 0.0, longitudeAtReference: 100.0)
            .WithBody(CelestialBodyId.Jupiter, speedAtReference: 0.0, accelerationPerDay: 0.0, longitudeAtReference: 105.0);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(eph, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        approach.Outcome.Should().Be(AspectApproachOutcome.Capped);
        approach.PerfectionJulianDay.Should().BeNull();
        approach.ClosestOrb.Should().BeApproximately(5.0 - 0.001 * AspectPerfectionFinder.MaxFollowDays, 0.01);
    }

    [Fact]
    public void FollowFirstApproach_Synthetic_ExactAtStart_ShouldPerfectAtTheStart()
    {
        SyntheticSpeedEphemeris eph = Reversing(100.0);

        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(eph, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        approach.Outcome.Should().Be(AspectApproachOutcome.Perfects);
        approach.PerfectionJulianDay.Should().Be(T0);
    }

    [Fact]
    public void FindFirstExactMajorAspect_Synthetic_ShouldPickTheEarliestOfSeveralAngles()
    {
        // A at 0° moving +10°/day, B fixed at 95°: A is 95° behind B, so it reaches the
        // square (90° behind) at 0.5 d, the sextile (60° behind) at 3.5 d and the
        // conjunction at 9.5 d. The earliest wins.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Moon, speedAtReference: 10.0, accelerationPerDay: 0.0, longitudeAtReference: 0.0)
            .WithBody(CelestialBodyId.Saturn, speedAtReference: 0.0, accelerationPerDay: 0.0, longitudeAtReference: 95.0);

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(eph, CelestialBodyId.Moon, CelestialBodyId.Saturn, T0, T0 + 20.0);

        hit.Should().NotBeNull();
        hit!.AspectType.Should().Be(AspectType.Square);
        hit.SignedTargetAngle.Should().Be(-90.0, "A is behind B when the square is exact");
        hit.JulianDay.Should().BeApproximately(T0 + 0.5, AspectPerfectionFinder.TimePrecisionDays);
    }

    [Fact]
    public void FindFirstExactMajorAspect_Synthetic_WindowStartingAfterTheSquare_ShouldFindTheSextile()
    {
        // Same motion, window opened at 1 d: the square (0.5 d) is behind, the sextile (3.5 d) is next.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Moon, speedAtReference: 10.0, accelerationPerDay: 0.0, longitudeAtReference: 0.0)
            .WithBody(CelestialBodyId.Saturn, speedAtReference: 0.0, accelerationPerDay: 0.0, longitudeAtReference: 95.0);

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(eph, CelestialBodyId.Moon, CelestialBodyId.Saturn, T0 + 1.0, T0 + 20.0);

        hit.Should().NotBeNull();
        hit!.AspectType.Should().Be(AspectType.Sextile);
        hit.SignedTargetAngle.Should().Be(-60.0);
        hit.JulianDay.Should().BeApproximately(T0 + 3.5, AspectPerfectionFinder.TimePrecisionDays);
    }

    [Fact]
    public void FindFirstExactMajorAspect_Synthetic_ExactAtTheWindowStart_ShouldNotCount()
    {
        // A sits exactly on the sextile at T0 and moves away: nothing strictly after T0 within 2 d.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Moon, speedAtReference: 10.0, accelerationPerDay: 0.0, longitudeAtReference: 35.0)
            .WithBody(CelestialBodyId.Saturn, speedAtReference: 0.0, accelerationPerDay: 0.0, longitudeAtReference: 95.0);

        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(eph, CelestialBodyId.Moon, CelestialBodyId.Saturn, T0, T0 + 2.0);

        hit.Should().BeNull();
    }

    [Fact]
    public void FollowFirstApproach_NullEphemeris_ShouldThrow()
    {
        Action act = () => AspectPerfectionFinder.FollowFirstApproach(null!, CelestialBodyId.Mars, CelestialBodyId.Jupiter, 0.0, T0);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FindFirstExactMajorAspect_InvertedWindow_ShouldThrow()
    {
        Action act = () => AspectPerfectionFinder.FindFirstExactMajorAspect(_ephemeris, CelestialBodyId.Mars, CelestialBodyId.Jupiter, T0, T0 - 1.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0, AspectType.Conjunction)]
    [InlineData(60.0, AspectType.Sextile)]
    [InlineData(90.0, AspectType.Square)]
    [InlineData(120.0, AspectType.Trine)]
    [InlineData(180.0, AspectType.Opposition)]
    public void AspectTypeFor_ShouldMapThePtolemaicAngles(double angle, AspectType expected)
    {
        AspectPerfectionFinder.AspectTypeFor(angle).Should().Be(expected);
    }

    #endregion
}
