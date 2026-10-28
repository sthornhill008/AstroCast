using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ReturnChartFinder"/> verifying the shared utility that
/// finds exact return moments for both Sun and Moon returns.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ReturnChartFinderTests
{
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture.
    /// </summary>
    public ReturnChartFinderTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    #region SignedLongitudeDelta Tests

    [Fact]
    public void SignedLongitudeDelta_SamePosition_ReturnsZero()
    {
        double delta = ReturnChartFinder.SignedLongitudeDelta(100.0, 100.0);
        delta.Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void SignedLongitudeDelta_CrossZero_ReturnsCorrectSign()
    {
        // 5° current, 355° target → current is 10° ahead (crossing 0°)
        double delta = ReturnChartFinder.SignedLongitudeDelta(5.0, 355.0);
        delta.Should().BeApproximately(10.0, 0.0001,
            "5° is 10° ahead of 355° crossing 0°");
    }

    #endregion

    #region Sun Return Tests

    [Fact]
    public void FindReturnJulianDays_SunOneYear_FindsOneReturn()
    {
        // Arrange — scan 2024-12-01 to 2025-12-31 (13 months) for Sun returns to ~280° (Jan 1 natal Sun)
        double jdStart = _ephemeris.DateToJulianDay(2024, 12, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2025, 12, 31, 23.99);

        // Get approximate natal Sun longitude (10° Capricorn ≈ 280°)
        Core.Models.PlanetPosition sunJ2000 = _ephemeris.CalculatePositionLongitudeOnly(
            _ephemeris.DateToJulianDay(2000, 1, 1, 12.0), CelestialBodyId.Sun);

        // Act
        List<double> results = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Sun, sunJ2000.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: true);

        // Assert
        results.Should().HaveCount(1,
            "the window holds two returns, 2024-12-31 13:32 and 2025-12-31 19:17 UT, and the daily scan's last sample, 2025-12-31 00:00, falls before the second (T0-1)");
    }

    [Fact]
    public void FindReturnJulianDays_FilterConjunction_RejectsOpposition()
    {
        // Arrange — scan a full year with filter=true
        double jdStart = _ephemeris.DateToJulianDay(2024, 12, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2025, 12, 31, 23.99);

        Core.Models.PlanetPosition sunJ2000 = _ephemeris.CalculatePositionLongitudeOnly(
            _ephemeris.DateToJulianDay(2000, 1, 1, 12.0), CelestialBodyId.Sun);

        // Act — with filter, should find only 1 (conjunction, not opposition)
        List<double> filteredResults = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Sun, sunJ2000.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: true);

        // Act — without filter, should find 2 (conjunction + opposition)
        List<double> unfilteredResults = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Sun, sunJ2000.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: false);

        // Assert
        filteredResults.Should().HaveCount(1,
            "filter should reject the opposition crossing");
        unfilteredResults.Should().HaveCountGreaterThanOrEqualTo(2,
            "without filter, both conjunction and opposition are found");
    }

    #endregion

    #region Moon Return Tests

    [Fact]
    public void FindReturnJulianDays_MoonOneMonth_FindsOneReturn()
    {
        // Arrange — scan ~30 days for Moon returns with conjunction filter
        double jdStart = _ephemeris.DateToJulianDay(2025, 3, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2025, 3, 30, 23.99);

        // Get natal Moon longitude
        Core.Models.PlanetPosition moonNatal = _ephemeris.CalculatePositionLongitudeOnly(
            _ephemeris.DateToJulianDay(2000, 1, 1, 12.0), CelestialBodyId.Moon);

        // Act
        List<double> results = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, moonNatal.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: true);

        // Assert — ~30 days contains exactly 1 Moon cycle (~27.3 days)
        results.Should().HaveCount(1,
            "a ~30-day window should contain exactly 1 lunar return");
    }

    [Fact]
    public void FindReturnJulianDays_MoonOneYear_FindsApprox13()
    {
        // Arrange — scan a full year for Moon returns with conjunction filter
        double jdStart = _ephemeris.DateToJulianDay(2025, 1, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2025, 12, 31, 23.99);

        Core.Models.PlanetPosition moonNatal = _ephemeris.CalculatePositionLongitudeOnly(
            _ephemeris.DateToJulianDay(2000, 1, 1, 12.0), CelestialBodyId.Moon);

        // Act
        List<double> results = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, moonNatal.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: true);

        // Assert — 365 days / 27.3 days ≈ 13.4, so expect 12–14
        results.Should().HaveCountGreaterThanOrEqualTo(12)
            .And.HaveCountLessThanOrEqualTo(14,
                "a full year should contain approximately 13 lunar returns");
    }

    [Fact]
    public void FindReturnJulianDays_NoFilter_IncludesAllCrossings()
    {
        // Arrange — Moon with no filter should find every sign change
        // (both conjunction and opposition crossings)
        double jdStart = _ephemeris.DateToJulianDay(2025, 1, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2025, 3, 31, 23.99);

        Core.Models.PlanetPosition moonNatal = _ephemeris.CalculatePositionLongitudeOnly(
            _ephemeris.DateToJulianDay(2000, 1, 1, 12.0), CelestialBodyId.Moon);

        // Act — without filter, includes both conjunction + opposition crossings
        List<double> unfilteredResults = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, moonNatal.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: false);

        // Act — with filter, only genuine returns
        List<double> filteredResults = ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Moon, moonNatal.Longitude,
            jdStart, jdEnd, filterConjunctionOnly: true);

        // Assert — unfiltered should find approximately double the filtered count
        unfilteredResults.Should().HaveCountGreaterThan(filteredResults.Count,
            "without filter, opposition crossings are also included");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="ReturnChartFinder.FindReturnJulianDays"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before the daily-step scan begins.
    /// </summary>
    [Fact]
    public void FindReturnJulianDays_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        double jdStart = _ephemeris.DateToJulianDay(2000, 1, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2050, 12, 31, 23.99);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => ReturnChartFinder.FindReturnJulianDays(
            _ephemeris, CelestialBodyId.Sun, 280.0,
            jdStart, jdEnd, filterConjunctionOnly: true, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the daily-step scan immediately");
    }

    /// <summary>
    /// Verifies that <see cref="ReturnChartFinder.FindAspectJulianDays"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before the aspect-scan begins.
    /// </summary>
    [Fact]
    public void FindAspectJulianDays_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        double jdStart = _ephemeris.DateToJulianDay(2000, 1, 1, 0.0);
        double jdEnd = _ephemeris.DateToJulianDay(2050, 12, 31, 23.99);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => ReturnChartFinder.FindAspectJulianDays(
            _ephemeris, CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            0.0, jdStart, jdEnd, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the aspect scan immediately");
    }

    #endregion
}
