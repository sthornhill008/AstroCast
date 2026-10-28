using AstroCast.Core.Exceptions;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EphemerisService.CalculateSunriseSunset"/>
/// verifying sunrise/sunset calculations against known astronomical expectations.
/// </summary>
/// <remarks>
/// These tests require the Swiss Ephemeris DLL and ephemeris data files to be
/// present in the test output directory. Uses the shared "Ephemeris" collection
/// fixture to avoid multiple swe_close() calls.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SunriseSunsetTests
{
    private readonly EphemerisService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public SunriseSunsetTests(EphemerisFixture fixture)
    {
        _service = fixture.EphemerisService;
    }

    #region London Spring Equinox

    [Fact]
    public void CalculateSunriseSunset_KnownDate_London_Should_ReturnReasonableValues()
    {
        // Arrange — 2026-03-21 (spring equinox), London (51.5°N, 0.0°W)
        double jd = _service.DateToJulianDay(2026, 3, 21, 0.0);

        // Act
        (double sunriseJd, double sunsetJd) = _service.CalculateSunriseSunset(jd, 51.5, 0.0);

        // Assert — sunrise should be before sunset
        sunriseJd.Should().BeLessThan(sunsetJd,
            "sunrise must occur before sunset");

        // Convert sunrise JD to UTC hours
        (int _, int _, int _, double sunriseHour) = _service.JulianDayToDate(sunriseJd);
        sunriseHour.Should().BeInRange(5.0, 7.0,
            "London spring equinox sunrise should be between 05:00–07:00 UTC");

        // Convert sunset JD to UTC hours
        (int _, int _, int _, double sunsetHour) = _service.JulianDayToDate(sunsetJd);
        sunsetHour.Should().BeInRange(17.0, 19.0,
            "London spring equinox sunset should be between 17:00–19:00 UTC");
    }

    #endregion

    #region New York Summer Solstice

    [Fact]
    public void CalculateSunriseSunset_KnownDate_NewYork_Should_ReturnReasonableValues()
    {
        // Arrange — 2026-06-21 (summer solstice), New York (40.7°N, -74.0°W)
        // Start at 06:00 UTC (02:00 AM EDT) — after previous sunset, before sunrise.
        // swe_rise_trans finds the NEXT event, so starting at 0:00 UTC would find the
        // previous local day's sunset (~0:30 UTC) before finding sunrise (~9:25 UTC).
        double jd = _service.DateToJulianDay(2026, 6, 21, 6.0);

        // Act
        (double sunriseJd, double sunsetJd) = _service.CalculateSunriseSunset(jd, 40.7, -74.0);

        // Assert — sunrise should be before sunset
        sunriseJd.Should().BeLessThan(sunsetJd,
            "sunrise must occur before sunset");

        // Convert sunrise JD to UTC hours — NYC sunrise ~5:25 AM EDT = ~9:25 UTC
        (int _, int _, int _, double sunriseHour) = _service.JulianDayToDate(sunriseJd);
        sunriseHour.Should().BeInRange(9.0, 11.0,
            "New York summer solstice sunrise should be between 09:00–11:00 UTC (5–7 AM EDT)");

        // Convert sunset to date — NYC sunset ~8:31 PM EDT = ~0:31 UTC next day
        (int _, int _, int sunsetDay, double sunsetHour) = _service.JulianDayToDate(sunsetJd);

        // Sunset at ~0:31 UTC next day means hour ~0.5 on day 22
        sunsetDay.Should().Be(22,
            "New York summer solstice sunset should fall on the next UTC day");
        sunsetHour.Should().BeInRange(0.0, 2.0,
            "New York summer solstice sunset should be between 00:00–02:00 UTC next day (8–10 PM EDT)");
    }

    #endregion

    #region Sunrise Before Sunset

    [Fact]
    public void CalculateSunriseSunset_SunriseBeforeSunset_Always()
    {
        // Arrange — use a mid-latitude location and a random date
        // 2026-01-15, latitude 45°N (temperate zone), longitude 10°E
        double jd = _service.DateToJulianDay(2026, 1, 15, 0.0);

        // Act
        (double sunriseJd, double sunsetJd) = _service.CalculateSunriseSunset(jd, 45.0, 10.0);

        // Assert
        sunriseJd.Should().BeLessThan(sunsetJd,
            "sunrise must always precede sunset at normal latitudes");
    }

    #endregion

    #region Day Length Comparison

    [Fact]
    public void CalculateSunriseSunset_WinterSolstice_Should_HaveShorterDay()
    {
        // Arrange — compare day lengths at 51.5°N for summer vs winter solstice
        double summerJd = _service.DateToJulianDay(2026, 6, 21, 0.0);
        double winterJd = _service.DateToJulianDay(2026, 12, 21, 0.0);

        // Act
        (double summerRise, double summerSet) = _service.CalculateSunriseSunset(summerJd, 51.5, 0.0);
        (double winterRise, double winterSet) = _service.CalculateSunriseSunset(winterJd, 51.5, 0.0);

        double summerDayLength = summerSet - summerRise;
        double winterDayLength = winterSet - winterRise;

        // Assert — summer day must be longer than winter day
        summerDayLength.Should().BeGreaterThan(winterDayLength,
            "summer solstice day length should be longer than winter solstice at 51.5°N");

        // Summer day at London should be roughly 16+ hours (~0.67 JD days)
        double summerHours = summerDayLength * 24.0;
        summerHours.Should().BeInRange(14.0, 18.0,
            "London summer solstice day should be approximately 16 hours");

        // Winter day at London should be roughly 8 hours (~0.33 JD days)
        double winterHours = winterDayLength * 24.0;
        winterHours.Should().BeInRange(6.0, 10.0,
            "London winter solstice day should be approximately 8 hours");
    }

    #endregion

    #region No Sunrise or Sunset (F9-3)

    // swe_rise_trans reports a body that does not rise or set by returning -2, with
    // "rise or set not found for planet 0" (swecl.c:4353 documents it, :4685 returns it).
    // Both instants below were probed directly on the shipped libswe.dylib: -2 for the
    // rise and for the set.

    /// <summary>
    /// Tromsø at the June solstice (2026-06-21 10:00 UT, 69.6492° N, 18.9553° E): the Sun
    /// does not set. The service throws <see cref="SunDoesNotRiseOrSetException"/>, which is
    /// an <see cref="EphemerisCalculationException"/>, so every existing catch still catches it.
    /// </summary>
    [Fact]
    public void CalculateSunriseSunset_Should_ThrowSunDoesNotRiseOrSet_When_TheSunDoesNotSet()
    {
        double jd = _service.DateToJulianDay(2026, 6, 21, 10.0);

        Action act = () => _service.CalculateSunriseSunset(jd, 69.6492, 18.9553);

        act.Should().Throw<SunDoesNotRiseOrSetException>()
            .Which.Should().BeAssignableTo<EphemerisCalculationException>();
    }

    /// <summary>
    /// Longyearbyen at chart 17's instant (1980-11-16 18:00 UT, 78.2232° N, 15.6267° E):
    /// the polar night, the Sun does not rise.
    /// </summary>
    [Fact]
    public void CalculateSunriseSunset_Should_ThrowSunDoesNotRiseOrSet_When_TheSunDoesNotRise()
    {
        double jd = _service.DateToJulianDay(1980, 11, 16, 18.0);

        Action act = () => _service.CalculateSunriseSunset(jd, 78.2232, 15.6267);

        act.Should().Throw<SunDoesNotRiseOrSetException>();
    }

    #endregion
}
