using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for Placidus house cusp calculation, obliquity retrieval,
/// and planet-to-house assignment. Uses J2000.0 epoch (JD 2451545.0 =
/// 2000-Jan-1 12:00 UT) with New York City coordinates (40.7128°N, 74.006°W).
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class HouseCalculationTests
{
    /// <summary>J2000.0 epoch — Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>New York City latitude (north positive).</summary>
    private const double NycLatitude = 40.7128;

    /// <summary>New York City longitude (east positive; west is negative).</summary>
    private const double NycLongitude = -74.006;

    private readonly EphemerisService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public HouseCalculationTests(EphemerisFixture fixture)
    {
        _service = fixture.EphemerisService;
    }

    #region CalculateHouses

    [Fact]
    public void CalculateHouses_Should_Return12Cusps_When_ValidInput()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        houses.Cusps.Should().HaveCount(12,
            "Placidus house system should produce exactly 12 house cusps");

        foreach (double cusp in houses.Cusps)
        {
            cusp.Should().BeInRange(0.0, 360.0,
                "each house cusp must be a valid ecliptic longitude");
        }
    }

    [Fact]
    public void CalculateHouses_Should_ReturnValidAscendant_When_ValidInput()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        houses.Ascendant.Should().BeInRange(0.0, 360.0,
            "Ascendant must be a valid ecliptic longitude");
        houses.Ascendant.Should().BeApproximately(houses.Cusps[0], 0.001,
            "Ascendant should equal the 1st house cusp");
    }

    [Fact]
    public void CalculateHouses_Should_ReturnValidMidheaven_When_ValidInput()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        houses.Midheaven.Should().BeInRange(0.0, 360.0,
            "Midheaven must be a valid ecliptic longitude");
        houses.Midheaven.Should().BeApproximately(houses.Cusps[9], 0.001,
            "Midheaven should equal the 10th house cusp");
    }

    [Fact]
    public void CalculateHouses_Should_ReturnDescendantOppositeAscendant_When_Calculated()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        double expectedDescendant = (houses.Ascendant + 180.0) % 360.0;
        houses.Descendant.Should().BeApproximately(expectedDescendant, 0.001,
            "Descendant should be exactly opposite the Ascendant");
    }

    [Fact]
    public void CalculateHouses_Should_ReturnICOppositeMC_When_Calculated()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        double expectedIC = (houses.Midheaven + 180.0) % 360.0;
        houses.ImumCoeli.Should().BeApproximately(expectedIC, 0.001,
            "IC should be exactly opposite the Midheaven");
    }

    [Fact]
    public void CalculateHouses_Should_ReturnValidArmc_When_Calculated()
    {
        // Act
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Assert
        houses.Armc.Should().BeInRange(0.0, 360.0,
            "ARMC (sidereal time in degrees) must be in 0–360 range");
    }

    #endregion

    #region GetObliquity

    [Fact]
    public void GetObliquity_Should_ReturnApprox23Degrees_When_J2000()
    {
        // Act
        double obliquity = _service.GetObliquity(J2000);

        // Assert — true obliquity at J2000.0 is approximately 23.439°
        obliquity.Should().BeInRange(23.0, 24.0,
            "true obliquity of the ecliptic at J2000 should be approximately 23.439°");
    }

    #endregion

    #region AssignHouses

    [Fact]
    public void AssignHouses_Should_SetHouseOnAllPlanets_When_ValidInput()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _service.CalculateAllPositions(J2000);
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Act
        HousePlacement.Assign(positions, houses);

        // Assert
        foreach (PlanetPosition position in positions)
        {
            position.House.Should().NotBeNull(
                $"{position.Name} should have a house assignment");
            position.House!.Value.Should().BeInRange(1, 12,
                $"{position.Name} house must be 1–12");
        }
    }

    [Fact]
    public void AssignHouses_Should_PlaceSunNearAscendant_When_NoonUtNYC()
    {
        // Arrange — At J2000.0 (noon UT = 7:00 AM EST), NYC, the Sun is
        // near the eastern horizon (Ascendant), placing it in houses 12, 1, or 2.
        IReadOnlyList<PlanetPosition> positions = _service.CalculateAllPositions(J2000);
        HouseData houses = _service.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Act
        HousePlacement.Assign(positions, houses);

        // Assert
        PlanetPosition sun = positions.First(p => p.Body == CelestialBodyId.Sun);
        sun.House.Should().NotBeNull("Sun should have a house assignment");
        sun.House!.Value.Should().BeOneOf([12, 1, 2],
            "at 7:00 AM local time in NYC, the Sun should be near the Ascendant (houses 12, 1, or 2)");
    }

    #endregion
}
