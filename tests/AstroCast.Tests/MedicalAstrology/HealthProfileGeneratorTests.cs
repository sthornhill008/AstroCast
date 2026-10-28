using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="HealthProfileGenerator"/> service.
/// </summary>
public sealed class HealthProfileGeneratorTests
{
    /// <summary>
    /// Generator produces a non-null HealthProfile with all required fields populated.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ValidChart_ReturnsNonNullProfile()
    {
        ChartResult result = CreateTestChart();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        profile.Should().NotBeNull();
        profile.SubjectName.Should().Be("Test Subject");
        profile.Temperament.Should().NotBeNull();
        profile.SignEmphases.Should().NotBeNull();
        profile.MedicalAspects.Should().NotBeNull();
        profile.SixthHouseAnalysis.Should().NotBeNullOrWhiteSpace();
        profile.EighthHouseAnalysis.Should().NotBeNullOrWhiteSpace();
        profile.TwelfthHouseAnalysis.Should().NotBeNullOrWhiteSpace();
        profile.PlanetsInSixth.Should().NotBeNull();
    }

    /// <summary>
    /// SignEmphases always includes at least the Ascendant sign, Sun sign, and Moon sign.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ValidChart_IncludesAscSunMoonInEmphases()
    {
        ChartResult result = CreateTestChart();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        // Ascendant at 5 deg = Aries, Sun at 130 deg = Leo, Moon at 100 deg = Cancer
        profile.SignEmphases.Should().Contain(e => e.Sign == ZodiacSign.Aries);
        profile.SignEmphases.Should().Contain(e => e.Sign == ZodiacSign.Leo);
        profile.SignEmphases.Should().Contain(e => e.Sign == ZodiacSign.Cancer);
    }

    /// <summary>
    /// MedicalAspects only contain hard aspect types (no trines or sextiles).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ChartWithMixedAspects_OnlyHardAspectsInProfile()
    {
        ChartResult result = CreateTestChartWithAspects();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        foreach (MedicalAspectInfo aspect in profile.MedicalAspects)
        {
            aspect.AspectType.Should().BeOneOf("Conjunction", "Opposition", "Square", "Quincunx");
        }
    }

    /// <summary>
    /// SixthHouseSign corresponds to the correct sign from house cusp data.
    /// Cusps are 0-indexed: Cusps[5] = House 6 cusp.
    /// With Ascendant at 5 deg (Aries), house cusps are at 30-degree intervals,
    /// so Cusps[5] = (5 + 150) = 155 deg = Virgo.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ValidChart_SixthHouseSignCorrect()
    {
        ChartResult result = CreateTestChart();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        // Cusps[5] = (5 + 5*30) = 155 deg -> sign = (155/30) + 1 = 6 = Virgo
        profile.SixthHouseSign.Should().Be(ZodiacSign.Virgo);
    }

    /// <summary>
    /// PlanetsInSixth correctly identifies planets in house 6.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ChartWithPlanetInSixth_PlanetsInSixthPopulated()
    {
        ChartResult result = CreateTestChartWithPlanetInSixth();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        profile.PlanetsInSixth.Should().Contain("Saturn");
    }

    /// <summary>
    /// Null ChartResult throws ArgumentNullException.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_NullResult_ThrowsArgumentNullException()
    {
        Action act = () => HealthProfileGenerator.Generate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Sign emphases include stelliums (signs with 2+ planets).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ChartWithStellium_IncludesStelliumSign()
    {
        // Place 3 planets in Taurus (not Asc/Sun/Moon sign) to create a stellium
        ChartResult result = CreateChartResult(
            ascendant: 5.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),     // Leo
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),    // Cancer
                CreatePosition(CelestialBodyId.Mercury, 40.0, house: 2),  // Taurus
                CreatePosition(CelestialBodyId.Venus, 45.0, house: 2),    // Taurus
                CreatePosition(CelestialBodyId.Mars, 50.0, house: 2),     // Taurus - 3 planets in Taurus
            ]);

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        profile.SignEmphases.Should().Contain(e => e.Sign == ZodiacSign.Taurus);
    }

    /// <summary>
    /// Medical aspects are sorted by significance then by orb.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ChartWithMultipleHardAspects_SortedBySignificanceThenOrb()
    {
        ChartResult result = CreateTestChartWithAspects();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        if (profile.MedicalAspects.Count >= 2)
        {
            // High significance should come before Moderate
            for (int i = 0; i < profile.MedicalAspects.Count - 1; i++)
            {
                MedicalAspectInfo current = profile.MedicalAspects[i];
                MedicalAspectInfo next = profile.MedicalAspects[i + 1];

                int currentOrder = current.Significance == "High" ? 0 : current.Significance == "Moderate" ? 1 : 2;
                int nextOrder = next.Significance == "High" ? 0 : next.Significance == "Moderate" ? 1 : 2;

                if (currentOrder == nextOrder)
                    current.Orb.Should().BeLessThanOrEqualTo(next.Orb);
                else
                    currentOrder.Should().BeLessThanOrEqualTo(nextOrder);
            }
        }
    }

    /// <summary>
    /// House analysis strings contain the sign name and traditional association description.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ValidChart_HouseAnalysisContainsSignAndMeaning()
    {
        ChartResult result = CreateTestChart();

        HealthProfile profile = HealthProfileGenerator.Generate(result);

        profile.SixthHouseAnalysis.Should().Contain("Virgo");
        profile.SixthHouseAnalysis.Should().Contain("daily health");
        profile.EighthHouseAnalysis.Should().Contain("chronic conditions");
        profile.TwelfthHouseAnalysis.Should().Contain("hidden illness");
    }

    #region Test Helpers

    /// <summary>
    /// Creates a standard test chart with known positions.
    /// </summary>
    private static ChartResult CreateTestChart()
    {
        return CreateChartResult(
            ascendant: 5.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),     // Leo
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),    // Cancer
                CreatePosition(CelestialBodyId.Mercury, 120.0, house: 5), // Leo
                CreatePosition(CelestialBodyId.Venus, 140.0, house: 5),   // Leo
                CreatePosition(CelestialBodyId.Mars, 10.0, house: 1),     // Aries
                CreatePosition(CelestialBodyId.Jupiter, 255.0, house: 9), // Sagittarius
                CreatePosition(CelestialBodyId.Saturn, 280.0, house: 10), // Capricorn
            ]);
    }

    /// <summary>
    /// Creates a test chart with mixed aspect types for filtering tests.
    /// </summary>
    private static ChartResult CreateTestChartWithAspects()
    {
        AspectDefinition squareDef = new()
        {
            Type = AspectType.Square, Angle = 90, Orb = 7,
            Category = AspectCategory.Major, Symbol = "[]", Abbreviation = "sqr"
        };
        AspectDefinition trineDef = new()
        {
            Type = AspectType.Trine, Angle = 120, Orb = 7,
            Category = AspectCategory.Major, Symbol = "tri", Abbreviation = "tri"
        };
        AspectDefinition quincunxDef = new()
        {
            Type = AspectType.Quincunx, Angle = 150, Orb = 2,
            Category = AspectCategory.Ebertin, Symbol = "qnx", Abbreviation = "qnx"
        };

        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions =
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),
                CreatePosition(CelestialBodyId.Saturn, 220.0, house: 8),
            ],
            Houses = CreateHouseData(5.0),
            Aspects =
            [
                new AspectResult
                {
                    Body1Name = "Sun", Body1Longitude = 130.0,
                    Body2Name = "Saturn", Body2Longitude = 220.0,
                    Aspect = squareDef, Orb = 0.5, IsApplying = true
                },
                new AspectResult
                {
                    Body1Name = "Sun", Body1Longitude = 130.0,
                    Body2Name = "Moon", Body2Longitude = 100.0,
                    Aspect = trineDef, Orb = 2.0, IsApplying = false
                },
                new AspectResult
                {
                    Body1Name = "Moon", Body1Longitude = 100.0,
                    Body2Name = "Saturn", Body2Longitude = 220.0,
                    Aspect = quincunxDef, Orb = 1.5, IsApplying = true
                },
            ],
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
    /// Creates a test chart with Saturn in the 6th house.
    /// </summary>
    private static ChartResult CreateTestChartWithPlanetInSixth()
    {
        return CreateChartResult(
            ascendant: 5.0,
            positions:
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),
                CreatePosition(CelestialBodyId.Saturn, 165.0, house: 6),  // Saturn in 6th house
            ]);
    }

    private static ChartResult CreateChartResult(double ascendant, IReadOnlyList<PlanetPosition> positions)
    {
        return new ChartResult
        {
            Input = CreateTestInput(),
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions = positions,
            Houses = CreateHouseData(ascendant),
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

    private static ChartInput CreateTestInput() => new()
    {
        Name = "Test Subject",
        LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
        TimeZone = "UTC",
        Latitude = 51.5,
        Longitude = -0.1,
        LocationName = "London"
    };

    private static HouseData CreateHouseData(double ascendant) => new()
    {
        Cusps = Enumerable.Range(0, 12).Select(i => (ascendant + i * 30.0) % 360.0).ToList(),
        Ascendant = ascendant,
        Midheaven = (ascendant + 270.0) % 360.0,
        Armc = 0.0,
        Vertex = 0.0,
    };

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
