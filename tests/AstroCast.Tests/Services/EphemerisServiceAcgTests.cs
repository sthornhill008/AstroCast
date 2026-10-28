using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for the astrocartography-related ephemeris extensions:
/// <see cref="EphemerisService.EclipticToEquatorial"/> and
/// <see cref="EphemerisService.GetSiderealTimeDegrees"/>.
/// </summary>
/// <remarks>
/// Uses the shared "Ephemeris" collection fixture. Tests verify that the
/// wrappers around <c>swe_cotrans</c> and <c>swe_sidtime</c> produce expected
/// values at well-known astronomical reference points (equinoxes, solstices).
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EphemerisServiceAcgTests
{
    /// <summary>J2000.0 epoch - Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>True obliquity of the ecliptic at J2000.0 (degrees).</summary>
    /// <remarks>
    /// IAU 2006 value at J2000.0 is approximately 23.4392911 degrees.
    /// Swiss Ephemeris returns the true obliquity (with nutation) which
    /// varies slightly from mean obliquity by up to ~9 arc-seconds.
    /// </remarks>
    private const double ObliquityJ2000 = 23.4393;

    private readonly EphemerisService _service;

    public EphemerisServiceAcgTests(EphemerisFixture fixture)
    {
        _service = fixture.EphemerisService;
    }

    #region EclipticToEquatorial

    [Fact]
    public void EclipticToEquatorial_VernalEquinox_ReturnsZeroRA()
    {
        // Act - Ecliptic (0 degrees, 0 degrees) is the vernal equinox point
        double obliquity = _service.GetObliquity(J2000);
        (double ra, double dec) = _service.EclipticToEquatorial(0.0, 0.0, obliquity);

        // Assert - vernal equinox: RA = 0 and Dec = 0 exactly
        ra.Should().BeApproximately(0.0, 0.001,
            "ecliptic longitude 0 degrees corresponds to RA 0 degrees at the vernal equinox");
        dec.Should().BeApproximately(0.0, 0.001,
            "ecliptic latitude 0 degrees at the vernal equinox has declination 0");
    }

    [Fact]
    public void EclipticToEquatorial_SummerSolstice_ReturnsRA90()
    {
        // Act - Ecliptic (90 degrees, 0 degrees) is the summer solstice point
        double obliquity = _service.GetObliquity(J2000);
        (double ra, double dec) = _service.EclipticToEquatorial(90.0, 0.0, obliquity);

        // Assert - summer solstice: RA = 90 degrees and Dec = +obliquity
        ra.Should().BeApproximately(90.0, 0.01,
            "ecliptic longitude 90 degrees corresponds to RA 90 degrees at the summer solstice");
        dec.Should().BeApproximately(obliquity, 0.01,
            "at the summer solstice the Sun reaches its maximum declination equal to the obliquity");
    }

    [Fact]
    public void EclipticToEquatorial_WinterSolstice_ReturnsRA270()
    {
        // Act - Ecliptic (270 degrees, 0 degrees) is the winter solstice point
        double obliquity = _service.GetObliquity(J2000);
        (double ra, double dec) = _service.EclipticToEquatorial(270.0, 0.0, obliquity);

        // Assert - winter solstice: RA = 270 degrees and Dec = -obliquity
        ra.Should().BeApproximately(270.0, 0.01,
            "ecliptic longitude 270 degrees corresponds to RA 270 degrees at the winter solstice");
        dec.Should().BeApproximately(-obliquity, 0.01,
            "at the winter solstice the Sun reaches its minimum declination equal to -obliquity");
    }

    [Fact]
    public void EclipticToEquatorial_ReturnsRAInValidRange()
    {
        // Act - Try several ecliptic longitudes and verify RA is normalised to 0-360
        double obliquity = _service.GetObliquity(J2000);

        foreach (double lon in new[] { 0.0, 45.0, 90.0, 135.0, 180.0, 225.0, 270.0, 315.0 })
        {
            (double ra, double _) = _service.EclipticToEquatorial(lon, 0.0, obliquity);

            ra.Should().BeGreaterThanOrEqualTo(0.0,
                $"RA for ecliptic longitude {lon} should be >= 0 degrees");
            ra.Should().BeLessThan(360.0,
                $"RA for ecliptic longitude {lon} should be < 360 degrees");
        }
    }

    #endregion

    #region GetSiderealTimeDegrees

    [Fact]
    public void GetSiderealTimeDegrees_ReturnsPositiveValue()
    {
        // Act
        double gast = _service.GetSiderealTimeDegrees(J2000);

        // Assert - sidereal time is in the range [0, 360)
        gast.Should().BeGreaterThanOrEqualTo(0.0,
            "sidereal time in degrees should be non-negative");
        gast.Should().BeLessThan(360.0,
            "sidereal time in degrees should be less than 360");
    }

    [Fact]
    public void GetSiderealTimeDegrees_DifferentDays_ReturnsDifferentValues()
    {
        // Act - Sidereal time on two consecutive solar days
        double gast0 = _service.GetSiderealTimeDegrees(J2000);
        double gast1 = _service.GetSiderealTimeDegrees(J2000 + 1.0);

        // Assert - sidereal time advances by ~360.986 degrees over one solar day
        // (difference should be approximately 0.986 degrees after mod 360, i.e., the extra ~1 degree
        // from the sidereal vs solar day difference)
        double diff = ((gast1 - gast0) % 360.0 + 360.0) % 360.0;

        diff.Should().BeGreaterThan(0.5,
            "sidereal time advances by ~360.986 degrees per solar day; mod 360 yields ~0.986 degrees");
        diff.Should().BeLessThan(1.5,
            "the daily sidereal-solar difference should be approximately 1 degree (~0.986 degrees)");
    }

    [Fact]
    public void GetSiderealTimeDegrees_AtJ2000_MatchesExpectedValue()
    {
        // J2000.0 sidereal time at Greenwich: approximately 18:41:50.548 UT1
        // which is 18.6973745... hours = 280.46062 degrees (GMST at J2000)
        // GAST (Greenwich Apparent Sidereal Time) differs from GMST by the equation of the equinoxes,
        // which is typically less than 1 arc-second (< 0.0003 degrees).

        // Act
        double gast = _service.GetSiderealTimeDegrees(J2000);

        // Assert - expected GAST at J2000 is approximately 280.46 degrees
        gast.Should().BeInRange(280.0, 281.0,
            "GAST at J2000.0 should be approximately 280.46 degrees");
    }

    #endregion
}
