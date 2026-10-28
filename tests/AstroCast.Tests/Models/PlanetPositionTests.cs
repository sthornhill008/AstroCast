using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="PlanetPosition"/> computed properties:
/// sign derivation, formatted position, retrograde detection, and boundary cases.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlanetPositionTests
{
    /// <summary>
    /// Creates a <see cref="PlanetPosition"/> with the given longitude and speed
    /// for testing computed properties.
    /// </summary>
    private static PlanetPosition CreatePosition(
        double longitude, double speedInLongitude = 1.0)
    {
        return new PlanetPosition
        {
            Body = CelestialBodyId.Sun,
            Name = "Sun",
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speedInLongitude,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
    }

    #region Formatted Position

    [Fact]
    public void FormattedPosition_Should_FormatCorrectly_When_GivenLongitude()
    {
        // Arrange — 15.396389° ≈ 15°23′47″ Aries
        PlanetPosition position = CreatePosition(15.396389);

        // Assert
        PositionFormatter.FormatPosition(position).Should().StartWith("15°23′",
            "15.396389° should format to 15°23′ within Aries");
        position.Sign.Should().Be(ZodiacSign.Aries,
            "15.396389° is within Aries (0°–30°)");
    }

    #endregion

    #region Retrograde Detection

    [Fact]
    public void IsRetrograde_Should_ReturnTrue_When_SpeedIsNegative()
    {
        // Arrange
        PlanetPosition position = CreatePosition(100.0, speedInLongitude: -0.5);

        // Assert
        position.IsRetrograde.Should().BeTrue(
            "negative speed in longitude indicates retrograde motion");
        position.DirectionCode.Should().Be("R",
            "retrograde bodies should have direction code 'R'");
    }

    [Fact]
    public void IsRetrograde_Should_ReturnFalse_When_SpeedIsPositive()
    {
        // Arrange
        PlanetPosition position = CreatePosition(100.0, speedInLongitude: 1.0);

        // Assert
        position.IsRetrograde.Should().BeFalse(
            "positive speed in longitude indicates direct motion");
        position.DirectionCode.Should().Be("D",
            "direct-motion bodies should have direction code 'D'");
    }

    #endregion

    #region Sign Derivation at Boundaries

    [Theory]
    [InlineData(0.0, ZodiacSign.Aries)]
    [InlineData(30.0, ZodiacSign.Taurus)]
    [InlineData(60.0, ZodiacSign.Gemini)]
    [InlineData(90.0, ZodiacSign.Cancer)]
    [InlineData(120.0, ZodiacSign.Leo)]
    [InlineData(150.0, ZodiacSign.Virgo)]
    [InlineData(180.0, ZodiacSign.Libra)]
    [InlineData(210.0, ZodiacSign.Scorpio)]
    [InlineData(240.0, ZodiacSign.Sagittarius)]
    [InlineData(270.0, ZodiacSign.Capricorn)]
    [InlineData(300.0, ZodiacSign.Aquarius)]
    [InlineData(330.0, ZodiacSign.Pisces)]
    [InlineData(359.99, ZodiacSign.Pisces)]
    [InlineData(360.0, ZodiacSign.Aries)]
    public void Sign_Should_ReturnCorrectSign_When_LongitudeAtBoundary(
        double longitude, ZodiacSign expectedSign)
    {
        // Arrange
        PlanetPosition position = CreatePosition(longitude);

        // Assert
        position.Sign.Should().Be(expectedSign,
            $"longitude {longitude}° should map to {expectedSign}");
    }

    #endregion

    #region Sign Degrees

    [Fact]
    public void SignDegrees_Should_ReturnDegreesWithinSign_When_LongitudeGiven()
    {
        // Arrange — 280.46° = 10°28′ within Capricorn (270°–300°)
        PlanetPosition position = CreatePosition(280.46);

        // Assert
        position.SignDegrees.Should().Be(10,
            "280.46° - 270° (Capricorn start) = 10.46°, integer part is 10");
        position.Sign.Should().Be(ZodiacSign.Capricorn);
    }

    #endregion

    #region Longitude Normalisation (360.0 boundary)

    [Fact]
    public void Sign_Should_ReturnAries_When_LongitudeIsExactly360()
    {
        // Arrange — 360.0° wraps to 0.0° Aries
        PlanetPosition position = CreatePosition(360.0);

        // Assert
        position.Sign.Should().Be(ZodiacSign.Aries,
            "360.0° should normalise to 0° Aries, not throw or return sign 13");
    }

    [Fact]
    public void SignDegrees_Should_ReturnZero_When_LongitudeIsExactly360()
    {
        // Arrange — 360.0° wraps to 0° within Aries
        PlanetPosition position = CreatePosition(360.0);

        // Assert
        position.SignDegrees.Should().Be(0,
            "360.0° % 30 = 0, so sign degrees should be 0");
    }

    [Fact]
    public void Sign_Should_ReturnPisces_When_LongitudeIs359Point999()
    {
        // Arrange — just below 360° should still be Pisces
        PlanetPosition position = CreatePosition(359.999);

        // Assert
        position.Sign.Should().Be(ZodiacSign.Pisces,
            "359.999° is still within Pisces (330°–360°)");
    }

    #endregion
}
