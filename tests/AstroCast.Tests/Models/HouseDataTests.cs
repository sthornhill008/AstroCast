using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="HouseData"/> computed properties and static formatting.
/// Tests the Descendant/IC derived angles and the longitude formatting method.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HouseDataTests
{
    /// <summary>
    /// Creates a minimal <see cref="HouseData"/> for testing computed properties.
    /// </summary>
    /// <param name="ascendant">Ascendant longitude in degrees.</param>
    /// <param name="midheaven">Midheaven longitude in degrees.</param>
    /// <returns>A HouseData instance with the specified angles.</returns>
    private static HouseData CreateHouseData(double ascendant = 0.0, double midheaven = 0.0)
    {
        return new HouseData
        {
            Cusps = new List<double> { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 },
            Ascendant = ascendant,
            Midheaven = midheaven,
            Armc = 0.0,
            Vertex = 0.0,
        };
    }

    #region Descendant

    [Fact]
    public void Descendant_Should_BeOppositeAscendant_When_AscIs90()
    {
        // Arrange
        HouseData data = CreateHouseData(ascendant: 90.0);

        // Assert
        data.Descendant.Should().Be(270.0,
            "Descendant of ASC 90° should be 270°");
    }

    [Fact]
    public void Descendant_Should_WrapAround_When_AscIs200()
    {
        // Arrange — 200 + 180 = 380, mod 360 = 20
        HouseData data = CreateHouseData(ascendant: 200.0);

        // Assert
        data.Descendant.Should().Be(20.0,
            "Descendant of ASC 200° should wrap to 20°");
    }

    #endregion

    #region Imum Coeli

    [Fact]
    public void ImumCoeli_Should_BeOppositeMidheaven_When_McIs350()
    {
        // Arrange — 350 + 180 = 530, mod 360 = 170
        HouseData data = CreateHouseData(midheaven: 350.0);

        // Assert
        data.ImumCoeli.Should().Be(170.0,
            "IC of MC 350° should be 170°");
    }

    [Fact]
    public void ImumCoeli_Should_Be180_When_McIs0()
    {
        // Arrange
        HouseData data = CreateHouseData(midheaven: 0.0);

        // Assert
        data.ImumCoeli.Should().Be(180.0,
            "IC of MC 0° should be 180°");
    }

    #endregion

    #region FormatLongitude

    [Fact]
    public void FormatLongitude_Should_FormatCorrectly_When_GivenZeroDegrees()
    {
        // Act
        string result = PositionFormatter.FormatLongitude(0.0);

        // Assert
        result.Should().Be("00°00′00″ Aries",
            "0.0° should format as the start of Aries");
    }

    [Fact]
    public void FormatLongitude_Should_FormatCorrectly_When_GivenCapricornLongitude()
    {
        // Arrange — 280.37° = 10°22′ in Capricorn (270°–300°)
        string result = PositionFormatter.FormatLongitude(280.37);

        // Assert
        result.Should().StartWith("10°",
            "280.37° - 270° = 10.37° within Capricorn");
        result.Should().Contain("Capricorn",
            "280.37° falls in Capricorn");
    }

    [Fact]
    public void FormatLongitude_Should_FormatCorrectly_When_GivenPiscesLongitude()
    {
        // Arrange — 359.99° is near the end of Pisces (330°–360°)
        string result = PositionFormatter.FormatLongitude(359.99);

        // Assert
        result.Should().Contain("Pisces",
            "359.99° falls in Pisces");
    }

    #endregion
}
