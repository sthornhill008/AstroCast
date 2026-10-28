using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="TemperamentCalculator"/> service.
/// </summary>
public sealed class TemperamentCalculatorTests
{
    /// <summary>
    /// A chart with fire-dominant placements: Ascendant in Aries (5 deg),
    /// Sun in Leo (130 deg), Moon in Sagittarius (260 deg).
    /// Should produce Choleric dominant temperament.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_FireDominantChart_ReturnsCholeric()
    {
        // Ascendant at 5 deg = Aries (Fire), Sun at 130 deg = Leo (Fire), Moon at 260 deg = Sagittarius (Fire)
        ChartResult result = CreateChartResult(
            ascendant: 5.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),     // Leo (Fire)
                CreatePosition(CelestialBodyId.Moon, 260.0, house: 9),    // Sagittarius (Fire)
                CreatePosition(CelestialBodyId.Mars, 10.0, house: 1),     // Aries (Fire) - angular & ASC ruler
                CreatePosition(CelestialBodyId.Jupiter, 255.0, house: 9), // Sagittarius (Fire) - Moon dispositor
                CreatePosition(CelestialBodyId.Mercury, 120.0, house: 5), // Leo (Fire)
                CreatePosition(CelestialBodyId.Venus, 140.0, house: 5),   // Leo (Fire)
                CreatePosition(CelestialBodyId.Saturn, 280.0, house: 10), // Capricorn (Earth) - angular
            ]);

        TemperamentProfile profile = TemperamentCalculator.Calculate(result);

        profile.DominantTemperament.Should().Be(Temperament.Choleric);
        profile.FireScore.Should().BeGreaterThan(profile.EarthScore);
        profile.FireScore.Should().BeGreaterThan(profile.AirScore);
        profile.FireScore.Should().BeGreaterThan(profile.WaterScore);
    }

    /// <summary>
    /// A chart with water-dominant placements: Ascendant in Cancer (100 deg),
    /// Sun in Scorpio (220 deg), Moon in Pisces (345 deg).
    /// Should produce Phlegmatic dominant temperament.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_WaterDominantChart_ReturnsPhlegmatic()
    {
        // Ascendant at 100 deg = Cancer (Water), Sun at 220 deg = Scorpio (Water), Moon at 345 deg = Pisces (Water)
        ChartResult result = CreateChartResult(
            ascendant: 100.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 220.0, house: 5),     // Scorpio (Water)
                CreatePosition(CelestialBodyId.Moon, 345.0, house: 9),    // Pisces (Water)
                CreatePosition(CelestialBodyId.Mars, 105.0, house: 1),    // Cancer (Water) - ASC ruler is Moon
                CreatePosition(CelestialBodyId.Jupiter, 340.0, house: 9), // Pisces (Water) - Moon dispositor
                CreatePosition(CelestialBodyId.Mercury, 210.0, house: 5), // Scorpio (Water)
                CreatePosition(CelestialBodyId.Venus, 350.0, house: 9),   // Pisces (Water)
                CreatePosition(CelestialBodyId.Saturn, 300.0, house: 7),  // Aquarius (Air) - angular
            ]);

        TemperamentProfile profile = TemperamentCalculator.Calculate(result);

        profile.DominantTemperament.Should().Be(Temperament.Phlegmatic);
        profile.WaterScore.Should().BeGreaterThan(profile.FireScore);
    }

    /// <summary>
    /// A chart with mixed elements should have a secondary temperament
    /// different from the dominant.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_MixedChart_SecondaryDiffersFromDominant()
    {
        // Ascendant in Aries (Fire), Sun in Taurus (Earth), Moon in Cancer (Water)
        ChartResult result = CreateChartResult(
            ascendant: 5.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 40.0, house: 2),      // Taurus (Earth)
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),    // Cancer (Water) - angular
                CreatePosition(CelestialBodyId.Mars, 15.0, house: 1),     // Aries (Fire) - ASC ruler, angular
                CreatePosition(CelestialBodyId.Jupiter, 250.0, house: 9), // Sagittarius (Fire)
                CreatePosition(CelestialBodyId.Venus, 200.0, house: 7),   // Libra (Air) - angular
                CreatePosition(CelestialBodyId.Saturn, 60.0, house: 3),   // Gemini (Air)
            ]);

        TemperamentProfile profile = TemperamentCalculator.Calculate(result);

        profile.SecondaryTemperament.Should().NotBe(profile.DominantTemperament);
    }

    /// <summary>
    /// TotalPoints should always be positive for any valid chart.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_AnyChart_TotalPointsPositive()
    {
        ChartResult result = CreateChartResult(
            ascendant: 0.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 30.0, house: 2),
                CreatePosition(CelestialBodyId.Moon, 60.0, house: 3),
            ]);

        TemperamentProfile profile = TemperamentCalculator.Calculate(result);

        profile.TotalPoints.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Null ChartResult throws ArgumentNullException.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NullResult_ThrowsArgumentNullException()
    {
        Action act = () => TemperamentCalculator.Calculate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Angular planets (houses 1, 4, 7, 10) contribute to element scores.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_AngularPlanets_ContributeToScores()
    {
        // Two charts: one with Mars angular (house 1), one with Mars cadent (house 3)
        ChartResult withAngular = CreateChartResult(
            ascendant: 0.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 30.0, house: 2),
                CreatePosition(CelestialBodyId.Moon, 60.0, house: 3),
                CreatePosition(CelestialBodyId.Mars, 5.0, house: 1),  // Angular - Aries (Fire)
            ]);

        ChartResult withoutAngular = CreateChartResult(
            ascendant: 0.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 30.0, house: 2),
                CreatePosition(CelestialBodyId.Moon, 60.0, house: 3),
                CreatePosition(CelestialBodyId.Mars, 5.0, house: 3),  // Cadent - same sign
            ]);

        TemperamentProfile profileAngular = TemperamentCalculator.Calculate(withAngular);
        TemperamentProfile profileCadent = TemperamentCalculator.Calculate(withoutAngular);

        // Angular planet adds 1 point to its sign's element
        profileAngular.TotalPoints.Should().BeGreaterThan(profileCadent.TotalPoints);
    }

    /// <summary>
    /// Empty positions list still produces a valid profile (Ascendant-only scoring).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_EmptyPositions_ProducesValidProfile()
    {
        ChartResult result = CreateChartResult(ascendant: 0.0, positions: []);

        TemperamentProfile profile = TemperamentCalculator.Calculate(result);

        profile.TotalPoints.Should().Be(3); // Only Ascendant sign contributes 3 points
    }

    #region Test Helpers

    /// <summary>
    /// Creates a minimal <see cref="ChartResult"/> with specified Ascendant and positions.
    /// </summary>
    private static ChartResult CreateChartResult(double ascendant, IReadOnlyList<PlanetPosition> positions)
    {
        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test Subject",
                LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
                TimeZone = "UTC",
                Latitude = 51.5,
                Longitude = -0.1,
                LocationName = "London"
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => (ascendant + i * 30.0) % 360.0).ToList(),
                Ascendant = ascendant,
                Midheaven = (ascendant + 270.0) % 360.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = 0.0,
            ResolvedOffsetLabel = "UTC",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a minimal <see cref="PlanetPosition"/> for test purposes.
    /// </summary>
    private static PlanetPosition CreatePosition(CelestialBodyId body, double longitude, int? house = null)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = body.ToString(),
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
        };
    }

    #endregion
}
