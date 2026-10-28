using System.Reflection;

using AstroCast.Core.Enums;
using AstroCast.Core.Exceptions;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EphemerisService"/> verifying planetary position
/// calculations against known astronomical reference data.
/// </summary>
/// <remarks>
/// These tests require the Swiss Ephemeris DLL and ephemeris data files to be
/// present in the test output directory. The J2000.0 epoch (JD 2451545.0 =
/// 2000-Jan-1 12:00 UT) is used as the primary reference point.
/// Uses the shared "Ephemeris" collection fixture to avoid multiple swe_close() calls.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EphemerisServiceTests
{
    /// <summary>J2000.0 epoch — Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    private readonly EphemerisService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public EphemerisServiceTests(EphemerisFixture fixture)
    {
        _service = fixture.EphemerisService;
    }

    #region Sun at J2000.0

    [Fact]
    public void CalculatePosition_Should_ReturnSunAtCapricorn_When_J2000()
    {
        // Act
        PlanetPosition sun = _service.CalculatePosition(J2000, CelestialBodyId.Sun);

        // Assert — Sun at J2000.0 is approximately 280.46° ecliptic longitude (Capricorn)
        sun.Longitude.Should().BeInRange(280.0, 281.0,
            "Sun's ecliptic longitude at J2000.0 should be approximately 280.46°");
        sun.Sign.Should().Be(ZodiacSign.Capricorn,
            "Sun at ~280° is in Capricorn (270°–300°)");
        sun.SpeedInLongitude.Should().BePositive(
            "Sun should have direct (positive) motion");
        sun.SpeedInLongitude.Should().BeInRange(0.9, 1.1,
            "Sun's daily speed should be approximately 1°/day");
    }

    #endregion

    #region Moon at J2000.0

    [Fact]
    public void CalculatePosition_Should_ReturnMoonPosition_When_J2000()
    {
        // Act
        PlanetPosition moon = _service.CalculatePosition(J2000, CelestialBodyId.Moon);

        // Assert — Moon's longitude must be in valid range
        moon.Longitude.Should().BeInRange(0.0, 360.0,
            "Moon's longitude must be in valid ecliptic range");
        moon.SpeedInLongitude.Should().BeInRange(11.0, 15.0,
            "Moon's daily speed should be in the typical range of 11–15°/day");
    }

    #endregion

    #region All Bodies

    [Fact]
    public void CalculateAllPositions_Should_Return12Bodies_When_J2000()
    {
        // Act
        IReadOnlyList<PlanetPosition> positions = _service.CalculateAllPositions(J2000);

        // Assert
        positions.Should().HaveCount(12,
            "should calculate all 12 configured celestial bodies");

        foreach (PlanetPosition position in positions)
        {
            position.Longitude.Should().BeInRange(0.0, 360.0,
                $"{position.Name} longitude must be in valid ecliptic range");
            position.Name.Should().NotBeNullOrWhiteSpace(
                "every body should have a name");
        }
    }

    #endregion

    #region Equatorial Coordinates

    [Fact]
    public void CalculatePosition_Should_ReturnEquatorialCoordinates_When_Calculated()
    {
        // Act
        PlanetPosition sun = _service.CalculatePosition(J2000, CelestialBodyId.Sun);

        // Assert
        sun.RightAscension.Should().NotBeNull(
            "equatorial coordinates should be calculated");
        sun.Declination.Should().NotBeNull(
            "declination should be calculated");
        sun.RightAscension!.Value.Should().BeInRange(0.0, 360.0,
            "right ascension must be in valid range");
        sun.Declination!.Value.Should().BeInRange(-90.0, 90.0,
            "declination must be in valid range");
    }

    #endregion

    #region Julian Day Conversion

    [Fact]
    public void DateToJulianDay_Should_ReturnCorrectJD_When_J2000Epoch()
    {
        // Act — 2000-Jan-1 12:00 UT = J2000.0
        double jd = _service.DateToJulianDay(2000, 1, 1, 12.0);

        // Assert
        jd.Should().Be(J2000,
            "2000-Jan-1 12:00 UT should equal JD 2451545.0 exactly");
    }

    [Fact]
    public void JulianDayToDate_Should_ReturnCorrectDate_When_J2000Epoch()
    {
        // Act
        (int year, int month, int day, double hour) = _service.JulianDayToDate(J2000);

        // Assert
        year.Should().Be(2000);
        month.Should().Be(1);
        day.Should().Be(1);
        hour.Should().BeApproximately(12.0, 0.001,
            "hour should be approximately 12.0 (noon UT)");
    }

    #endregion

    #region Retrograde Detection

    [Fact]
    public void CalculatePosition_Should_DetectRetrograde_When_PlanetIsRetrograde()
    {
        // Arrange — JD 2451580.0 ≈ 2000-Feb-5, Mercury may or may not be retrograde.
        // This test verifies the property logic is consistent with speed sign.
        const double testJd = 2451580.0;

        // Act
        PlanetPosition mercury = _service.CalculatePosition(testJd, CelestialBodyId.Mercury);

        // Assert — IsRetrograde must match the sign of SpeedInLongitude
        if (mercury.SpeedInLongitude < 0)
        {
            mercury.IsRetrograde.Should().BeTrue(
                "negative speed means retrograde motion");
            mercury.DirectionCode.Should().Be("R");
        }
        else
        {
            mercury.IsRetrograde.Should().BeFalse(
                "positive speed means direct motion");
            mercury.DirectionCode.Should().Be("D");
        }
    }

    #endregion

    #region Version

    [Fact]
    public void Version_Should_ReturnNonEmptyString_When_Accessed()
    {
        // Act
        string version = _service.Version;

        // Assert
        version.Should().NotBeNullOrWhiteSpace(
            "Swiss Ephemeris should return a version string");
        version.Should().Contain("2.10",
            "should be Swiss Ephemeris version 2.10.x");
    }

    #endregion

    #region Thread Safety (CRIT-001)

    /// <summary>
    /// Smoke test: 10 concurrent <see cref="EphemerisService.CalculatePosition"/>
    /// calls should complete without exceptions. The <see cref="SemaphoreSlim"/>
    /// serialises access, preventing native memory corruption.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CalculatePosition_Should_NotThrow_When_CalledConcurrently()
    {
        // Arrange — 10 concurrent tasks for the Sun at sequential Julian Days
        Task[] tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(() => _service.CalculatePosition(
                J2000 + i, CelestialBodyId.Sun))).ToArray();

        // Act & Assert — all should complete without exception
        Func<Task> act = () => Task.WhenAll(tasks);
        await act.Should().NotThrowAsync(
            "concurrent access should be serialised by the internal SemaphoreSlim");
    }

    #endregion

    #region House Calculation Return Value (CRIT-002)

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculateHouses"/> returns valid
    /// house cusps for a standard mid-latitude location (London, 51.5°N).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CalculateHouses_Should_ReturnValidCusps_When_StandardLatitude()
    {
        // Arrange — London, 2000-Jan-1 12:00 UT
        const double latitude = 51.5;
        const double longitude = 0.0;

        // Act
        HouseData houses = _service.CalculateHouses(J2000, latitude, longitude);

        // Assert — all 12 cusps should be in valid range
        houses.Cusps.Should().HaveCount(12,
            "Placidus should return 12 house cusps");
        foreach (double cusp in houses.Cusps)
        {
            cusp.Should().BeInRange(0.0, 360.0,
                "each house cusp must be a valid ecliptic longitude");
        }

        houses.Ascendant.Should().BeInRange(0.0, 360.0,
            "Ascendant must be a valid longitude");
        houses.Midheaven.Should().BeInRange(0.0, 360.0,
            "Midheaven must be a valid longitude");
    }

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculateHouses"/> at an extreme
    /// latitude (70°N, Troms&#248;) either succeeds with valid cusps or throws
    /// <see cref="EphemerisCalculationException"/>. The Swiss Ephemeris may silently
    /// fall back to Porphyry houses at extreme latitudes, so both outcomes are valid.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CalculateHouses_Should_HandleExtremeLatitude_When_70DegreesNorth()
    {
        // Arrange — Tromsø (70°N), 2024-Jun-21 (summer solstice)
        const double latitude = 70.0;
        const double longitude = 19.0;
        double summerSolsticeJd = _service.DateToJulianDay(2024, 6, 21, 12.0);

        // Act — may succeed (with fallback) or throw
        try
        {
            HouseData houses = _service.CalculateHouses(summerSolsticeJd, latitude, longitude);

            // If it succeeds, cusps should still be valid
            houses.Cusps.Should().HaveCount(12,
                "even at extreme latitudes, 12 cusps should be returned");
            foreach (double cusp in houses.Cusps)
            {
                cusp.Should().BeInRange(0.0, 360.0,
                    "house cusps must be valid even at extreme latitudes");
            }
        }
        catch (EphemerisCalculationException ex)
        {
            // If Placidus fails, the exception message should mention the latitude
            ex.Message.Should().Contain("latitude",
                "error message should mention the latitude constraint");
        }
    }

    #endregion

    #region Circumpolar Sunrise/Sunset (CRIT-003)

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculateSunriseSunset"/> throws
    /// <see cref="EphemerisCalculationException"/> for circumpolar conditions
    /// (midnight sun at 70°N on the summer solstice).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CalculateSunriseSunset_Should_ThrowEphemerisCalculationException_When_CircumpolarConditions()
    {
        // Arrange — Tromsø (70°N), 2024-Jun-21 (summer solstice — midnight sun)
        const double latitude = 70.0;
        const double longitude = 19.0;
        double summerSolsticeJd = _service.DateToJulianDay(2024, 6, 21, 12.0);

        // Act & Assert — no sunrise/sunset during midnight sun.
        // The Swiss Ephemeris may return < 0 (error) or 1 (event not found) for
        // circumpolar conditions. Either path throws EphemerisCalculationException.
        Action act = () => _service.CalculateSunriseSunset(
            summerSolsticeJd, latitude, longitude);

        act.Should().Throw<EphemerisCalculationException>(
            "circumpolar conditions must throw rather than return invalid JD values");
    }

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculateSunriseSunset"/> succeeds
    /// at 70°N on the equinox, when sunrise and sunset do occur.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CalculateSunriseSunset_Should_Succeed_When_EquinoxAt70DegreesNorth()
    {
        // Arrange — Tromsø (70°N), 2024-Mar-20 (spring equinox — sunrise occurs)
        const double latitude = 70.0;
        const double longitude = 19.0;
        double equinoxJd = _service.DateToJulianDay(2024, 3, 20, 12.0);

        // Act — should succeed at the equinox
        (double sunriseJd, double sunsetJd) = _service.CalculateSunriseSunset(
            equinoxJd, latitude, longitude);

        // Assert — both should be valid JDs near the search date.
        // Note: swe_rise_trans returns the NEXT occurrence from the given JD,
        // so sunset (same afternoon) may precede next sunrise (next morning).
        sunriseJd.Should().BeGreaterThan(0.0,
            "sunrise JD should be a valid positive Julian Day");
        sunsetJd.Should().BeGreaterThan(0.0,
            "sunset JD should be a valid positive Julian Day");
        Math.Abs(sunriseJd - equinoxJd).Should().BeLessThan(1.5,
            "sunrise should be within ~1.5 days of the search date");
        Math.Abs(sunsetJd - equinoxJd).Should().BeLessThan(1.5,
            "sunset should be within ~1.5 days of the search date");
    }

    #endregion

    #region Warning Logging on Distant Dates (HIGH-012)

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculatePosition"/> succeeds for
    /// dates outside the .se1 ephemeris file range (year 3000). The Swiss Ephemeris
    /// silently falls back to the Moshier analytical ephemeris and logs a warning.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CalculatePosition_Should_Succeed_When_DateOutsideEphemerisRange()
    {
        // Arrange — year 3000, well outside the .se1 file range
        double distantJd = _service.DateToJulianDay(3000, 1, 1, 12.0);

        // Act — should succeed via Moshier fallback (warning logged but not asserted)
        PlanetPosition sun = _service.CalculatePosition(distantJd, CelestialBodyId.Sun);

        // Assert — position should still be valid
        sun.Longitude.Should().BeInRange(0.0, 360.0,
            "Sun longitude should be valid even with Moshier fallback");
        sun.SpeedInLongitude.Should().BeInRange(0.5, 1.2,
            "Sun speed should be approximately 1°/day");
    }

    #endregion

    #region Longitude-Only Calculation (HIGH-004)

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculatePositionLongitudeOnly"/>
    /// returns the same longitude as the full <see cref="EphemerisService.CalculatePosition"/>
    /// for the Sun at J2000, and that equatorial coordinates are null.
    /// </summary>
    [Fact]
    public void CalculatePositionLongitudeOnly_Should_MatchFullPositionLongitude_When_Sun()
    {
        // Arrange
        PlanetPosition fullPosition = _service.CalculatePosition(J2000, CelestialBodyId.Sun);

        // Act
        PlanetPosition lonOnly = _service.CalculatePositionLongitudeOnly(J2000, CelestialBodyId.Sun);

        // Assert — longitude and speed must match the full calculation
        lonOnly.Longitude.Should().BeApproximately(fullPosition.Longitude, 1e-8,
            "longitude-only calculation must match full calculation for the Sun");
        lonOnly.SpeedInLongitude.Should().BeApproximately(fullPosition.SpeedInLongitude, 1e-8,
            "speed in longitude must match the full calculation");

        // Equatorial coordinates must be null (not computed)
        lonOnly.RightAscension.Should().BeNull(
            "longitude-only calculation should not compute right ascension");
        lonOnly.Declination.Should().BeNull(
            "longitude-only calculation should not compute declination");
    }

    /// <summary>
    /// Verifies that <see cref="EphemerisService.CalculatePositionLongitudeOnly"/>
    /// returns the same longitude as the full <see cref="EphemerisService.CalculatePosition"/>
    /// for the Moon at J2000, and that equatorial coordinates are null.
    /// </summary>
    [Fact]
    public void CalculatePositionLongitudeOnly_Should_MatchFullPositionLongitude_When_Moon()
    {
        // Arrange
        PlanetPosition fullPosition = _service.CalculatePosition(J2000, CelestialBodyId.Moon);

        // Act
        PlanetPosition lonOnly = _service.CalculatePositionLongitudeOnly(J2000, CelestialBodyId.Moon);

        // Assert — longitude and speed must match the full calculation
        lonOnly.Longitude.Should().BeApproximately(fullPosition.Longitude, 1e-8,
            "longitude-only calculation must match full calculation for the Moon");
        lonOnly.SpeedInLongitude.Should().BeApproximately(fullPosition.SpeedInLongitude, 1e-8,
            "speed in longitude must match the full calculation");

        // Equatorial coordinates must be null (not computed)
        lonOnly.RightAscension.Should().BeNull(
            "longitude-only calculation should not compute right ascension");
        lonOnly.Declination.Should().BeNull(
            "longitude-only calculation should not compute declination");
    }

    #endregion

    #region Pre-1582 Calendar Auto-Detection

    [Fact]
    public void DateToJulianDay_Should_UseJulianCalendar_When_DateBefore1582()
    {
        // Arrange — 1066-Oct-14 12:00 (Battle of Hastings) is before the Gregorian reform.
        // The auto-detection should switch to Julian calendar transparently.
        // Known reference: 1066-Oct-14 12:00 Julian = JD 2110701.0
        double expectedJd = 2110701.0;

        // Act
        double jd = _service.DateToJulianDay(1066, 10, 14, 12.0);

        // Assert
        jd.Should().BeApproximately(expectedJd, 0.5,
            "pre-1582 date should auto-detect Julian calendar and return correct JD");
    }

    #endregion

    #region Disposal Guard (H-3)

    /// <summary>
    /// Verifies that every public member of <see cref="EphemerisService"/>
    /// honours the <c>_disposed</c> guard by throwing
    /// <see cref="ObjectDisposedException"/> once the instance has been disposed.
    /// </summary>
    /// <remarks>
    /// The test flips the private <c>_disposed</c> field directly via reflection
    /// instead of calling <see cref="EphemerisService.Dispose"/>, because the real
    /// <c>Dispose()</c> calls <c>swe_close()</c> on the process-wide Swiss
    /// Ephemeris state — which the shared <see cref="EphemerisFixture"/>
    /// deliberately limits to a single invocation at test-run end. Flipping the
    /// flag back inside the finally block restores the shared fixture to a
    /// usable state for later tests in the collection.
    /// </remarks>
    [Fact]
    public void PublicMembers_AfterDispose_ThrowObjectDisposedException()
    {
        FieldInfo disposedField = typeof(EphemerisService).GetField(
            "_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!;
        disposedField.Should().NotBeNull(
            "EphemerisService must expose a _disposed field as the source of truth " +
            "for ObjectDisposedException.ThrowIf (H-3)");

        disposedField.SetValue(_service, true);
        try
        {
            Action calculatePosition = () =>
                _service.CalculatePosition(J2000, CelestialBodyId.Sun);
            Action calculateHouses = () =>
                _service.CalculateHouses(J2000, 51.5, -0.12);
            Action dateToJulianDay = () =>
                _service.DateToJulianDay(2025, 1, 1, 0.0);
            Action readVersion = () => { _ = _service.Version; };

            calculatePosition.Should().Throw<ObjectDisposedException>(
                "CalculatePosition must honour the _disposed guard (H-3)");
            calculateHouses.Should().Throw<ObjectDisposedException>(
                "CalculateHouses must honour the _disposed guard (H-3)");
            dateToJulianDay.Should().Throw<ObjectDisposedException>(
                "DateToJulianDay must honour the _disposed guard (H-3)");
            readVersion.Should().Throw<ObjectDisposedException>(
                "the Version property getter must honour the _disposed guard (H-3)");
        }
        finally
        {
            // Restore the flag so later tests in the "Ephemeris" collection see
            // an operational service. Without this restoration every subsequent
            // ephemeris test in the suite would fail with ObjectDisposedException.
            disposedField.SetValue(_service, false);
        }
    }

    /// <summary>
    /// Verifies that a second call to <see cref="EphemerisService.Dispose"/> is a
    /// no-op — the private <c>_disposed</c> flag gates the second entry so
    /// <c>swe_close()</c> is only invoked once, preventing the native-library
    /// double-close crash that the shared fixture explicitly guards against.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_SecondCallIsNoOp()
    {
        FieldInfo disposedField = typeof(EphemerisService).GetField(
            "_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Pretend the fixture's shared service is already disposed so that both
        // Dispose() calls in this test fall into the guarded no-op branch —
        // neither will actually invoke swe_close() or dispose the lock.
        disposedField.SetValue(_service, true);
        try
        {
            Action firstDispose = () => _service.Dispose();
            Action secondDispose = () => _service.Dispose();

            firstDispose.Should().NotThrow(
                "Dispose() must short-circuit when _disposed is already true");
            secondDispose.Should().NotThrow(
                "Dispose() must remain idempotent — the second call stays a no-op");
        }
        finally
        {
            disposedField.SetValue(_service, false);
        }
    }

    #endregion
    #region Fixed stars (branch Primary, 1.5.0)

    /// <summary>
    /// A star's position carries its ecliptic latitude beside the longitude the longitude-only member gives:
    /// Regulus at J2000, 149.829041° and +0.464813° (Tier B, pyswisseph over the shipped sefstars.txt).
    /// </summary>
    [Fact]
    public void CalculateFixedStarPosition_Regulus_CarriesItsLatitude()
    {
        (double longitude, double latitude) = _service.CalculateFixedStarPosition("Regulus", J2000);

        longitude.Should().BeApproximately(149.829041, 1e-6);
        longitude.Should().Be(_service.CalculateFixedStarLongitude("Regulus", J2000));
        latitude.Should().BeApproximately(0.464813, 1e-6);
    }

    #endregion

    #region Minor points (branch Asteroids, 1.6.0)

    /// <summary>
    /// The five minor points are computed as the twelve bodies are, by the same call with its ecliptic and
    /// equatorial places, and named by the library for the four asteroids and "Lilith" for the mean apogee (R1),
    /// where the library says "mean Apogee".
    /// </summary>
    /// <param name="body">The point's body.</param>
    /// <param name="name">Its name on every list.</param>
    [Theory]
    [InlineData(CelestialBodyId.Ceres, "Ceres")]
    [InlineData(CelestialBodyId.Pallas, "Pallas")]
    [InlineData(CelestialBodyId.Juno, "Juno")]
    [InlineData(CelestialBodyId.Vesta, "Vesta")]
    [InlineData(CelestialBodyId.MeanApogee, "Lilith")]
    public void CalculatePosition_MinorPoint_IsNamed(CelestialBodyId body, string name)
    {
        PlanetPosition position = _service.CalculatePosition(J2000, body);

        position.Body.Should().Be(body);
        position.Name.Should().Be(name);
        position.Longitude.Should().BeInRange(0.0, 360.0);
        position.RightAscension.Should().NotBeNull("the equatorial call is made for the five as for the twelve");
        position.Declination.Should().NotBeNull("the equatorial call is made for the five as for the twelve");
    }

    #endregion
}
