using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="DeclinationAspectService.FindCrossChartDeclinationAspects"/>
/// verifying cross-chart parallel and contraparallel detection between two separate
/// charts. Covers detection, orb boundaries, sorting, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CrossChartDeclinationAspectTests
{
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

    #region Parallel Detection

    [Fact]
    public void FindCrossChart_ParallelWithinOrb_Detected()
    {
        // Arrange — Chart A Sun at +20.0°, Chart B Mars at +20.5°
        // Parallel orb = |20.0 - 20.5| = 0.5° (within ±1°)
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Mars", CelestialBodyId.Mars, 20.5),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().ContainSingle(a => a.IsParallel,
            "same-sign declinations within 1° should produce a cross-chart parallel");
        DeclinationAspect par = aspects.First(a => a.IsParallel);
        par.Body1Name.Should().Be("Sun");
        par.Body2Name.Should().Be("Mars");
        par.Orb.Should().BeApproximately(0.5, 0.0001);
    }

    #endregion

    #region Contraparallel Detection

    [Fact]
    public void FindCrossChart_ContraparallelWithinOrb_Detected()
    {
        // Arrange — Chart A Venus at +15.0°, Chart B Saturn at -15.3°
        // Contraparallel orb = |15.0 + (-15.3)| = 0.3° (within ±1°)
        List<PlanetPosition> chartA =
        [
            MakePosition("Venus", CelestialBodyId.Venus, 15.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Saturn", CelestialBodyId.Saturn, -15.3),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().ContainSingle(a => !a.IsParallel,
            "opposite-sign declinations within 1° should produce a cross-chart contraparallel");
        DeclinationAspect cpar = aspects.First(a => !a.IsParallel);
        cpar.Body1Name.Should().Be("Venus");
        cpar.Body2Name.Should().Be("Saturn");
        cpar.Orb.Should().BeApproximately(0.3, 0.0001);
    }

    #endregion

    #region Orb Boundaries

    [Fact]
    public void FindCrossChart_BeyondOrb_NotDetected()
    {
        // Arrange — Chart A Sun at +20.0°, Chart B Mars at +21.5°
        // Parallel orb = |20.0 - 21.5| = 1.5° (exceeds ±1°)
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Mars", CelestialBodyId.Mars, 21.5),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().NotContain(a => a.IsParallel,
            "declinations more than 1° apart should not produce a parallel");
    }

    [Fact]
    public void FindCrossChart_ExactlyAtOrbBoundary_StillDetected()
    {
        // Arrange — Chart A Sun at +20.0°, Chart B Moon at +21.0°
        // Parallel orb = |20.0 - 21.0| = 1.0° (exactly at boundary, inclusive)
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Moon", CelestialBodyId.Moon, 21.0),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().ContainSingle(a => a.IsParallel,
            "exactly 1.0° should be within the ±1° threshold (inclusive)");
        aspects.First(a => a.IsParallel).Orb.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void FindCrossChart_JustBeyondOrbBoundary_Rejected()
    {
        // Arrange — Chart A Sun at +20.0°, Chart B Moon at +21.01°
        // Parallel orb = |20.0 - 21.01| = 1.01° (just beyond threshold)
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Moon", CelestialBodyId.Moon, 21.01),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().NotContain(a => a.IsParallel,
            "1.01° should exceed the ±1° threshold");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FindCrossChart_EmptyCharts_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects([], []);

        // Assert
        aspects.Should().BeEmpty("empty charts should produce no aspects");
    }

    [Fact]
    public void FindCrossChart_NullDeclination_SkipsBody()
    {
        // Arrange — one body in Chart A has null declination
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, null),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Moon", CelestialBodyId.Moon, 20.0),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().BeEmpty("bodies without declination should be skipped");
    }

    #endregion

    #region Sorting

    [Fact]
    public void FindCrossChart_SortedByOrb_TightestFirst()
    {
        // Arrange — multiple aspects with different orbs
        List<PlanetPosition> chartA =
        [
            MakePosition("Sun", CelestialBodyId.Sun, 20.0),
            MakePosition("Moon", CelestialBodyId.Moon, 15.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Mars", CelestialBodyId.Mars, 20.8),   // parallel with Sun, orb 0.8
            MakePosition("Venus", CelestialBodyId.Venus, 15.2), // parallel with Moon, orb 0.2
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().HaveCountGreaterThan(1, "multiple aspects expected");
        for (int i = 1; i < aspects.Count; i++)
        {
            aspects[i].Orb.Should().BeGreaterThanOrEqualTo(aspects[i - 1].Orb,
                "aspects should be sorted by orb ascending (tightest first)");
        }
    }

    #endregion

    #region Body Name Mapping

    [Fact]
    public void FindCrossChart_PreservesBodyNames_CorrectChartOrder()
    {
        // Arrange — verify Body1 = chart A, Body2 = chart B
        List<PlanetPosition> chartA =
        [
            MakePosition("Jupiter", CelestialBodyId.Jupiter, 10.0),
        ];

        List<PlanetPosition> chartB =
        [
            MakePosition("Saturn", CelestialBodyId.Saturn, 10.3),
        ];

        // Act
        IReadOnlyList<DeclinationAspect> aspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(chartA, chartB);

        // Assert
        aspects.Should().ContainSingle();
        aspects[0].Body1Name.Should().Be("Jupiter",
            "Body1 should be from chart A");
        aspects[0].Body2Name.Should().Be("Saturn",
            "Body2 should be from chart B");
    }

    #endregion
}
