using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="StationWindowFinder"/> on the real ephemeris (the 2026
/// Venus stations, measured independently at 2026-10-03 07:15:54 UT and
/// 2026-11-14 00:27:28 UT) and on a synthetic one.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class StationWindowFinderTests
{
    private readonly EphemerisService _ephemeris;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    public StationWindowFinderTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    private double Jd(DateTime utc) =>
        _ephemeris.DateToJulianDay(utc.Year, utc.Month, utc.Day, utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0);

    private static DateTime Utc(int y, int mo, int d, int h, int mi, int s) => new(y, mo, d, h, mi, s, DateTimeKind.Utc);

    [Fact]
    public void FindStations_VenusBetweenSampleChartAndPerfection_ShouldFindTheStationRetrograde()
    {
        double start = Jd(Utc(2026, 10, 1, 10, 56, 30));
        double end = Jd(Utc(2026, 10, 7, 0, 10, 23));

        IReadOnlyList<WindowStation> stations = StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Venus, start, end);

        stations.Should().ContainSingle();
        stations[0].IsRetrogradeStation.Should().BeTrue();
        stations[0].TypeWord.Should().Be("retrograde");
        stations[0].TimeUtc.Should().BeCloseTo(Utc(2026, 10, 3, 7, 15, 54), TimeSpan.FromSeconds(2));
        stations[0].Longitude.Should().BeApproximately(218.4912, 1.0 / 3600.0);
    }

    [Fact]
    public void FindStations_MercuryInTheSameWindow_ShouldFindNone()
    {
        double start = Jd(Utc(2026, 10, 1, 10, 56, 30));
        double end = Jd(Utc(2026, 10, 7, 0, 10, 23));

        StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Mercury, start, end).Should().BeEmpty();
    }

    [Fact]
    public void FindStations_VenusOverAutumn2026_ShouldFindBothStationsInOrder()
    {
        double start = Jd(Utc(2026, 9, 1, 0, 0, 0));
        double end = Jd(Utc(2026, 12, 1, 0, 0, 0));

        IReadOnlyList<WindowStation> stations = StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Venus, start, end);

        stations.Should().HaveCount(2);
        stations[0].IsRetrogradeStation.Should().BeTrue();
        stations[0].TimeUtc.Should().BeCloseTo(Utc(2026, 10, 3, 7, 15, 54), TimeSpan.FromSeconds(2));
        stations[1].IsRetrogradeStation.Should().BeFalse();
        stations[1].TimeUtc.Should().BeCloseTo(Utc(2026, 11, 14, 0, 27, 28), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FindStations_SunAndMoon_ShouldNeverStation()
    {
        double start = Jd(Utc(2026, 1, 1, 0, 0, 0));
        double end = Jd(Utc(2026, 3, 1, 0, 0, 0));

        StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Sun, start, end).Should().BeEmpty();
        StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Moon, start, end).Should().BeEmpty();
    }

    /// <summary>2026-10-01 12:00 UT, an exactly representable Julian Day.</summary>
    private const double T0 = 2461315.0;

    [Fact]
    public void FindStations_Synthetic_ShouldBisectTheCrossingAndReadTheType()
    {
        // v(t) = 0.05 − 0.025 t + 0.002 t²: zero at 2.5 d, going + → −.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Mercury, 0.05, -0.025, speedCurvature: 0.002);

        IReadOnlyList<WindowStation> stations = StationWindowFinder.FindStations(eph, CelestialBodyId.Mercury, T0, T0 + 5.0);

        stations.Should().ContainSingle();
        stations[0].IsRetrogradeStation.Should().BeTrue();
        stations[0].JulianDay.Should().BeApproximately(T0 + 2.5, StationWindowFinder.TimePrecisionDays);
        stations[0].TimeUtc.Should().Be(Utc(2026, 10, 4, 0, 0, 0));
    }

    [Fact]
    public void FindStations_Synthetic_WindowEndingBeforeTheStation_ShouldFindNone()
    {
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Mercury, 0.05, -0.025, speedCurvature: 0.002);

        StationWindowFinder.FindStations(eph, CelestialBodyId.Mercury, T0, T0 + 2.0).Should().BeEmpty();
    }

    [Fact]
    public void FindStations_Synthetic_StoppedAtTheWindowStart_ShouldCountIt()
    {
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Mercury, 0.0, -0.01);

        IReadOnlyList<WindowStation> stations = StationWindowFinder.FindStations(eph, CelestialBodyId.Mercury, T0, T0 + 1.0);

        stations.Should().ContainSingle();
        stations[0].JulianDay.Should().Be(T0);
        stations[0].IsRetrogradeStation.Should().BeTrue();
    }

    [Fact]
    public void FindStations_Synthetic_BodyThatNeverMoves_ShouldFindNoStations()
    {
        // Speed identically zero (a synthetic fixture, never the real sky): no station.
        SyntheticSpeedEphemeris eph = new SyntheticSpeedEphemeris(T0)
            .WithBody(CelestialBodyId.Venus, 0.0, 0.0);

        StationWindowFinder.FindStations(eph, CelestialBodyId.Venus, T0, T0 + 5.0).Should().BeEmpty();
    }

    [Fact]
    public void FindStations_InvertedWindow_ShouldThrow()
    {
        Action act = () => StationWindowFinder.FindStations(_ephemeris, CelestialBodyId.Mercury, T0, T0 - 1.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FindStations_NullEphemeris_ShouldThrow()
    {
        Action act = () => StationWindowFinder.FindStations(null!, CelestialBodyId.Mercury, T0, T0 + 1.0);

        act.Should().Throw<ArgumentNullException>();
    }
}
