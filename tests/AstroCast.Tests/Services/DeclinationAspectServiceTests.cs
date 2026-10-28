using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="DeclinationAspectService"/> verifying detection of
/// declination parallels and contraparallels between planetary positions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeclinationAspectServiceTests
{
    private readonly DeclinationAspectService _service = new();

    /// <summary>
    /// Creates a minimal <see cref="PlanetPosition"/> with the specified
    /// name and declination for testing.
    /// </summary>
    private static PlanetPosition MakePosition(
        string name, CelestialBodyId body, double? declination) => new()
    {
        Body = body,
        Name = name,
        Longitude = 0.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        Declination = declination,
    };

    #region Parallel Tests

    [Fact]
    public void Calculate_ExactParallel_Detected()
    {
        // Arrange — two bodies at identical declination
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 23.0),
            MakePosition("Moon", CelestialBodyId.Moon, 23.0),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().ContainSingle(a => a.IsParallel,
            "identical declinations should produce one parallel");
        aspects[0].Orb.Should().BeApproximately(0.0, 0.0001,
            "exact parallel should have zero orb");
    }

    [Fact]
    public void Calculate_ParallelWithinOrb_Detected()
    {
        // Arrange — two bodies within 1° of each other
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
            MakePosition("Mars", CelestialBodyId.Mars, 20.8),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().ContainSingle(a => a.IsParallel,
            "declinations within 1° should produce a parallel");
        aspects[0].Orb.Should().BeApproximately(0.8, 0.0001);
    }

    [Fact]
    public void Calculate_ParallelBeyondOrb_NotDetected()
    {
        // Arrange — two bodies more than 1° apart
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
            MakePosition("Mars", CelestialBodyId.Mars, 21.5),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().NotContain(a => a.IsParallel,
            "declinations more than 1° apart should not produce a parallel");
    }

    #endregion

    #region Contraparallel Tests

    [Fact]
    public void Calculate_ExactContraparallel_Detected()
    {
        // Arrange — two bodies at equal but opposite declinations
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 23.0),
            MakePosition("Moon", CelestialBodyId.Moon, -23.0),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().ContainSingle(a => !a.IsParallel,
            "opposite declinations should produce one contraparallel");
        DeclinationAspect cpar = aspects.First(a => !a.IsParallel);
        cpar.Orb.Should().BeApproximately(0.0, 0.0001,
            "exact contraparallel should have zero orb");
    }

    [Fact]
    public void Calculate_ContraparallelWithinOrb_Detected()
    {
        // Arrange — |dec1 + dec2| = |15 + (-15.7)| = 0.7
        List<PlanetPosition> positions =
        [
            MakePosition("Venus", CelestialBodyId.Venus, 15.0),
            MakePosition("Saturn", CelestialBodyId.Saturn, -15.7),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().ContainSingle(a => !a.IsParallel,
            "opposite declinations within 1° should produce a contraparallel");
        aspects.First(a => !a.IsParallel).Orb.Should().BeApproximately(0.7, 0.0001);
    }

    [Fact]
    public void Calculate_ContraparallelBeyondOrb_NotDetected()
    {
        // Arrange — |dec1 + dec2| = |15 + (-17)| = 2 > 1°
        List<PlanetPosition> positions =
        [
            MakePosition("Venus", CelestialBodyId.Venus, 15.0),
            MakePosition("Saturn", CelestialBodyId.Saturn, -17.0),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().NotContain(a => !a.IsParallel,
            "opposite declinations more than 1° apart should not produce a contraparallel");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Calculate_NullDeclination_SkipsBody()
    {
        // Arrange — one body has null declination
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 23.0),
            MakePosition("Moon", CelestialBodyId.Moon, null),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().BeEmpty("bodies without declination should be skipped");
    }

    [Fact]
    public void Calculate_EmptyPositions_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate([]);

        // Assert
        aspects.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_SortedByOrb_TightestFirst()
    {
        // Arrange — create positions that produce aspects with different orbs
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
            MakePosition("Moon", CelestialBodyId.Moon, 20.9),    // parallel, orb 0.9
            MakePosition("Mars", CelestialBodyId.Mars, 20.3),    // parallel with Sun, orb 0.3
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Assert
        aspects.Should().HaveCountGreaterThan(1, "multiple parallels expected");
        for (int i = 1; i < aspects.Count; i++)
        {
            aspects[i].Orb.Should().BeGreaterThanOrEqualTo(aspects[i - 1].Orb,
                "aspects should be sorted by orb ascending");
        }
    }

    #endregion

    #region Display Properties

    [Fact]
    public void Parallel_HasCorrectDisplayProperties()
    {
        // Arrange
        DeclinationAspect parallel = new()
        {
            Body1Name = "Sun",
            Body1Declination = 23.0,
            Body2Name = "Moon",
            Body2Declination = 23.0,
            IsParallel = true,
            Orb = 0.0,
        };

        // Assert
        parallel.AspectName.Should().Be("Parallel");
        parallel.Abbreviation.Should().Be("Par");
    }

    [Fact]
    public void Contraparallel_HasCorrectDisplayProperties()
    {
        // Arrange
        DeclinationAspect contraparallel = new()
        {
            Body1Name = "Sun",
            Body1Declination = 23.0,
            Body2Name = "Moon",
            Body2Declination = -23.0,
            IsParallel = false,
            Orb = 0.0,
        };

        // Assert
        contraparallel.AspectName.Should().Be("Contraparallel");
        contraparallel.Abbreviation.Should().Be("CPar");
    }

    #endregion
}
