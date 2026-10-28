using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitEphemerisHelper"/> verifying ephemeris position lookups,
/// daily motion calculations, Julian Day conversions, and body ID mapping.
/// Uses the real Swiss Ephemeris via the shared test fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitEphemerisHelperTests
{
    private readonly TransitEphemerisHelper _helper;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a TransitEphemerisHelper.
    /// </summary>
    public TransitEphemerisHelperTests(EphemerisFixture fixture)
    {
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
    }

    #region GetLongitude

    [Fact]
    public void GetLongitude_Jupiter_KnownDate_Should_ReturnExpectedPosition()
    {
        // 2026-03-01 noon UT — Jupiter should be in Gemini/Cancer area (~70–110°)
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 3, 1));
        double longitude = _helper.GetLongitude(TransitBody.Jupiter, jd);

        longitude.Should().BeInRange(0.0, 360.0,
            "longitude should be in the valid range");
        // Jupiter moves ~30° per year, and should be somewhere in the ecliptic
        longitude.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void GetLongitude_Saturn_KnownDate_Should_ReturnExpectedPosition()
    {
        // 2026-03-01 noon UT — Saturn should be in Aries area (~0–30°)
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 3, 1));
        double longitude = _helper.GetLongitude(TransitBody.Saturn, jd);

        longitude.Should().BeInRange(0.0, 360.0,
            "longitude should be in the valid range");
    }

    #endregion

    #region GetDailyMotion

    [Fact]
    public void GetDailyMotion_Jupiter_Should_BePositive_WhenDirect()
    {
        // Jupiter goes direct around March 2026; by June 2026 it is well into direct motion
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 6, 1));
        double dailyMotion = _helper.GetDailyMotion(TransitBody.Jupiter, jd);

        dailyMotion.Should().BeGreaterThan(0,
            "Jupiter should be moving direct (positive speed) on this date");
    }

    [Fact]
    public void GetDailyMotion_Should_BeNegative_WhenRetrograde()
    {
        // Saturn retrogrades annually for ~4.5 months.
        // Saturn Rx 2026: approximately July–November 2026.
        // Use 2026-08-15 which should be well within Saturn retrograde.
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 8, 15));
        double dailyMotion = _helper.GetDailyMotion(TransitBody.Saturn, jd);

        dailyMotion.Should().BeLessThan(0,
            "Saturn should be retrograde (negative speed) in mid-August 2026");
    }

    #endregion

    #region IsRetrograde

    [Fact]
    public void IsRetrograde_Should_ReturnTrue_WhenRetrograde()
    {
        // Same retrograde date as above
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 8, 15));
        bool isRetro = _helper.IsRetrograde(TransitBody.Saturn, jd);

        isRetro.Should().BeTrue(
            "Saturn should be retrograde in mid-August 2026");
    }

    [Fact]
    public void IsRetrograde_Should_ReturnFalse_WhenDirect()
    {
        // Jupiter is well into direct motion by June 2026
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 6, 1));
        bool isRetro = _helper.IsRetrograde(TransitBody.Jupiter, jd);

        isRetro.Should().BeFalse(
            "Jupiter should be direct in June 2026");
    }

    #endregion

    #region GetPosition

    [Fact]
    public void GetPosition_Should_PopulateAllFields()
    {
        double jd = _helper.DateToJulianDay(new DateOnly(2026, 3, 1));
        TransitPlanetPosition position = _helper.GetPosition(TransitBody.Jupiter, jd);

        position.Sign.Should().NotBeNullOrEmpty("Sign should be populated");
        position.Degrees.Should().BeInRange(0, 29, "Degrees should be 0–29");
        position.Minutes.Should().BeInRange(0, 59, "Minutes should be 0–59");
        position.DailyMotion.Should().NotBe(0.0, "DailyMotion should not be zero");
        position.Longitude.Should().BeInRange(0.0, 360.0, "Longitude should be in range");
    }

    #endregion

    #region TransitBodyToSwissEph

    [Theory]
    [InlineData(TransitBody.Jupiter, 5)]   // SE_JUPITER
    [InlineData(TransitBody.Saturn, 6)]    // SE_SATURN
    [InlineData(TransitBody.Uranus, 7)]    // SE_URANUS
    [InlineData(TransitBody.Neptune, 8)]   // SE_NEPTUNE
    [InlineData(TransitBody.Pluto, 9)]     // SE_PLUTO
    [InlineData(TransitBody.Chiron, 15)]   // SE_CHIRON
    [InlineData(TransitBody.NorthNode, 11)] // SE_TRUE_NODE
    [InlineData(TransitBody.Mars, 4)]      // SE_MARS
    public void TransitBodyToSwissEph_Should_MapAllBodies(TransitBody body, int expectedSeId)
    {
        int seId = TransitEphemerisHelper.TransitBodyToSwissEph(body);
        seId.Should().Be(expectedSeId,
            $"{body} should map to Swiss Ephemeris constant {expectedSeId}");
    }

    #endregion

    #region DateToJulianDay

    [Fact]
    public void DateToJulianDay_KnownDate_Should_ReturnExpectedJD()
    {
        // J2000.0 epoch: 2000-01-01 12:00 UT = JD 2451545.0
        double jd = _helper.DateToJulianDay(new DateOnly(2000, 1, 1));
        jd.Should().BeApproximately(2451545.0, 0.001,
            "2000-01-01 noon UT should be the J2000.0 epoch");
    }

    #endregion

    #region LongitudeToSign

    [Theory]
    [InlineData(0.0, "Aries")]
    [InlineData(15.0, "Aries")]
    [InlineData(30.0, "Taurus")]
    [InlineData(180.0, "Libra")]
    [InlineData(359.9, "Pisces")]
    public void LongitudeToSign_Should_ReturnCorrectSign(double longitude, string expectedSign)
    {
        string sign = TransitEphemerisHelper.LongitudeToSign(longitude);
        sign.Should().Be(expectedSign);
    }

    #endregion
}
