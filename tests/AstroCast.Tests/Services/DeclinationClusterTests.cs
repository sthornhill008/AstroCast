using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="DeclinationAspectService.IdentifyClusters"/> verifying
/// identification of declination clusters — groups of 3+ chart points whose
/// absolute declinations fall within a span of 2° or less.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeclinationClusterTests
{
    private readonly DeclinationAspectService _service = new();

    /// <summary>
    /// Creates a minimal <see cref="PlanetPosition"/> with the specified
    /// name and declination for testing.
    /// </summary>
    private static PlanetPosition MakePosition(string name, double? declination) => new()
    {
        Body = CelestialBodyId.Sun,
        Name = name,
        Longitude = 0.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        Declination = declination,
    };

    #region Basic Cluster Detection

    [Fact]
    public void IdentifyClusters_ThreePointsWithinWindow_FormsSingleCluster()
    {
        // Arrange — three points at |Dec| = 22.5°, 23.0°, 23.5° (range 1.0° ≤ 2.0°)
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Jupiter", +23.0),
            MakePosition("Saturn", +23.5),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle("three points within 2° should form one cluster");
        clusters[0].Members.Should().HaveCount(3);
        clusters[0].ZoneMinAbsDeclination.Should().BeApproximately(22.5, 0.001);
        clusters[0].ZoneMaxAbsDeclination.Should().BeApproximately(23.5, 0.001);
    }

    [Fact]
    public void IdentifyClusters_TwoPointsOnly_NoCluster()
    {
        // Arrange — only 2 points (minimum is 3)
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Jupiter", +23.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().BeEmpty("fewer than 3 points should not form a cluster");
    }

    [Fact]
    public void IdentifyClusters_PositiveAndNegativeSameAbsValue_SameCluster()
    {
        // Arrange — +22.5° and -22.5° both have |22.5°|, plus a third point
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Pluto", -22.8),
            MakePosition("Jupiter", +23.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle("opposite-sign declinations with same |Dec| should cluster");
        clusters[0].Members.Should().HaveCount(3);
    }

    #endregion

    #region Overlapping Window Merging

    [Fact]
    public void IdentifyClusters_OverlappingWindows_MergedIntoOneCluster()
    {
        // Arrange — 4 points spanning exactly 2°, the widest span a cluster may have:
        // |22.0|, |23.0|, |23.5|, |24.0| — the one window [22, 24] holds all four, no merging
        List<PlanetPosition> positions =
        [
            MakePosition("A", +22.0),
            MakePosition("B", +23.0),
            MakePosition("C", +23.5),
            MakePosition("D", +24.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle("overlapping windows should merge into one cluster");
        clusters[0].Members.Should().HaveCount(4);
    }

    [Fact]
    public void IdentifyClusters_NonOverlappingGroups_SeparateClusters()
    {
        // Arrange — two separate groups with gap > 2° between them
        // Group 1: |1.0|, |1.5|, |2.0| — Group 2: |22.0|, |22.5|, |23.0|
        List<PlanetPosition> positions =
        [
            MakePosition("A", +1.0),
            MakePosition("B", +1.5),
            MakePosition("C", +2.0),
            MakePosition("D", +22.0),
            MakePosition("E", +22.5),
            MakePosition("F", +23.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().HaveCount(2, "two non-overlapping groups should form two clusters");
    }

    #endregion

    #region Intra-Cluster Aspect Count

    [Fact]
    public void IdentifyClusters_CountsIntraClusterAspects()
    {
        // Arrange — three points close enough for both cluster AND declination aspects
        // Moon +22.5, Jupiter +22.8, Saturn +23.0 — all within ±1° of each other
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Jupiter", +22.8),
            MakePosition("Saturn", +23.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle();
        // Moon-Jupiter (0.3°), Moon-Saturn (0.5°), Jupiter-Saturn (0.2°) = 3 parallels
        clusters[0].IntraClusterAspectCount.Should().Be(3,
            "all three pairs are within the ±1° aspect orb");
    }

    [Fact]
    public void IdentifyClusters_AspectCountExcludesNonClusterMembers()
    {
        // Arrange — cluster of 3 points at ~22° plus an unrelated point at 5°
        // The 5° point might have aspects with itself but not with cluster members
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Jupiter", +22.8),
            MakePosition("Saturn", +23.0),
            MakePosition("Venus", +5.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle();
        // Only aspects between Moon, Jupiter, Saturn count — Venus is excluded
        clusters[0].IntraClusterAspectCount.Should().Be(3);
        clusters[0].Members.Should().HaveCount(3);
        clusters[0].Members.Should().NotContain(m => m.Name == "Venus");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void IdentifyClusters_AllDeclinationsSpreadOut_NoClusters()
    {
        // Arrange — all points more than 2° apart from each other
        List<PlanetPosition> positions =
        [
            MakePosition("Sun", +5.0),
            MakePosition("Moon", +10.0),
            MakePosition("Mars", +15.0),
            MakePosition("Jupiter", +20.0),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().BeEmpty("widely spaced declinations should produce no clusters");
    }

    [Fact]
    public void IdentifyClusters_EmptyPositions_ReturnsEmpty()
    {
        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters([], []);

        // Assert
        clusters.Should().BeEmpty();
    }

    [Fact]
    public void IdentifyClusters_NullDeclinations_Skipped()
    {
        // Arrange — some positions have null declination
        List<PlanetPosition> positions =
        [
            MakePosition("Moon", +22.5),
            MakePosition("Jupiter", null),
            MakePosition("Saturn", +23.0),
            MakePosition("Pluto", +22.8),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().ContainSingle("null-declination bodies should be skipped");
        clusters[0].Members.Should().HaveCount(3);
        clusters[0].Members.Should().NotContain(m => m.Name == "Jupiter");
    }

    [Fact]
    public void IdentifyClusters_SortedByCountDescending()
    {
        // Arrange — a cluster of 4 and a cluster of 3
        List<PlanetPosition> positions =
        [
            MakePosition("A", +1.0),
            MakePosition("B", +1.5),
            MakePosition("C", +2.0),
            MakePosition("D", +22.0),
            MakePosition("E", +22.5),
            MakePosition("F", +23.0),
            MakePosition("G", +23.5),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        // Act
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, aspects);

        // Assert
        clusters.Should().HaveCount(2);
        clusters[0].Members.Count.Should().BeGreaterThanOrEqualTo(clusters[1].Members.Count,
            "clusters should be sorted by member count descending");
    }

    #endregion
}
