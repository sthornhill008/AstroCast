using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ReturnChartFinder.FindAspectJulianDays"/> verifying the
/// two-body aspect finder for conjunctions, oppositions, and other aspects.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TwoBodyAspectFinderTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture.
    /// </summary>
    public TwoBodyAspectFinderTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    #region Jupiter-Saturn Conjunction Tests

    [Fact]
    public void FindAspectJulianDays_JupiterSaturnConjunction2020_Found()
    {
        // The 2020 great conjunction occurred on December 21, 2020
        double jdStart = _fixture.EphemerisService.DateToJulianDay(2020, 1, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2021, 1, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            0.0, // conjunction
            jdStart, jdEnd);

        results.Should().HaveCountGreaterThanOrEqualTo(1,
            "the 2020 great conjunction should be found");

        // Verify the date is near December 21, 2020
        (int year, int month, int day, double _) = _fixture.EphemerisService.JulianDayToDate(results[0]);
        year.Should().Be(2020);
        month.Should().Be(12);
        day.Should().BeInRange(20, 22);
    }

    [Fact]
    public void FindAspectJulianDays_JupiterSaturn_MultipleConjunctionsIn40Years()
    {
        double jdStart = _fixture.EphemerisService.DateToJulianDay(1980, 1, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2025, 1, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            0.0,
            jdStart, jdEnd);

        // Should find at least 2 conjunctions: ~1981, ~2000, ~2020
        results.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void FindAspectJulianDays_JupiterSaturnConjunction_PrecisionUnder1Degree()
    {
        double jdStart = _fixture.EphemerisService.DateToJulianDay(2020, 1, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2021, 1, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            0.0,
            jdStart, jdEnd);

        results.Should().NotBeEmpty();

        // Check precision at the found moment
        double jd = results[0];
        double jupLon = _fixture.EphemerisService.CalculatePositionLongitudeOnly(
            jd, CelestialBodyId.Jupiter).Longitude;
        double satLon = _fixture.EphemerisService.CalculatePositionLongitudeOnly(
            jd, CelestialBodyId.Saturn).Longitude;
        double separation = Math.Abs(ReturnChartFinder.SignedLongitudeDelta(jupLon, satLon));

        separation.Should().BeLessThan(0.1,
            "Jupiter-Saturn separation should be < 0.1° at found conjunction");
    }

    #endregion

    #region Jupiter-Saturn Opposition Tests

    [Fact]
    public void FindAspectJulianDays_JupiterSaturnOpposition_FoundInKnownWindow()
    {
        // Jupiter-Saturn oppositions occur roughly 10 years after each conjunction
        double jdStart = _fixture.EphemerisService.DateToJulianDay(2010, 1, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2012, 1, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            180.0, // opposition
            jdStart, jdEnd);

        results.Should().HaveCountGreaterThanOrEqualTo(1,
            "a Jupiter-Saturn opposition should be found ~2010");
    }

    #endregion

    #region Empty Result Tests

    [Fact]
    public void FindAspectJulianDays_NoConjunctionInNarrowWindow_ReturnsEmpty()
    {
        // Search a window too narrow to contain a conjunction
        double jdStart = _fixture.EphemerisService.DateToJulianDay(2023, 6, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2023, 7, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            0.0,
            jdStart, jdEnd);

        results.Should().BeEmpty();
    }

    #endregion

    #region Mars-Jupiter Conjunction Tests

    [Fact]
    public void FindAspectJulianDays_MarsJupiterConjunction_Found()
    {
        // Mars-Jupiter conjunctions occur roughly every 2 years
        double jdStart = _fixture.EphemerisService.DateToJulianDay(2024, 1, 1, 0.0);
        double jdEnd = _fixture.EphemerisService.DateToJulianDay(2026, 1, 1, 0.0);

        List<double> results = ReturnChartFinder.FindAspectJulianDays(
            _fixture.EphemerisService,
            CelestialBodyId.Mars,
            CelestialBodyId.Jupiter,
            0.0,
            jdStart, jdEnd);

        results.Should().HaveCountGreaterThanOrEqualTo(1,
            "at least one Mars-Jupiter conjunction should be found in 2 years");
    }

    #endregion
}
