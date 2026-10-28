using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins how <see cref="DeclinationAspectService.IdentifyClusters"/> chooses its clusters (branch F7, card D14,
/// 1.3.3): each spans 2° or less. A window starts at the absolute declination s of a point in the pool and holds the
/// pool's points from s to s + 2°, both ends included; the window with the most members is chosen, on a tie the
/// narrower span, then the lower start. A window of three or more becomes a cluster and its points leave the pool,
/// until none is left, and the clusters are returned in the order chosen, largest first.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 every 2° window of three or more was merged with every window sharing a member, so a chain grew
/// without limit: points at 0°, 1°, …, 6° made one cluster 6° wide (foundations F5-2: 31 of 135 clusters in 30
/// saved charts wider than 2°, the widest 4.96°). The expected values are Tier A, the rule applied by hand.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DeclinationClusterSpanTests
{
    private readonly DeclinationAspectService _service = new();

    /// <summary>
    /// Points at 0°, 1°, …, 6°: every window of three spans 2°, so the lowest start wins, then the lowest start
    /// left: {0, 1, 2} and {3, 4, 5}. Until 1.3.3 one cluster 6° wide.
    /// </summary>
    [Fact]
    public void IdentifyClusters_ChainOneDegreeApart_GivesTwoClustersOfTwoDegrees()
    {
        IReadOnlyList<DeclinationCluster> clusters = Clusters(0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0);

        clusters.Should().HaveCount(2);
        clusters[0].Members.Select(m => m.Declination).Should().Equal(0.0, 1.0, 2.0);
        clusters[0].ZoneMinAbsDeclination.Should().Be(0.0);
        clusters[0].ZoneMaxAbsDeclination.Should().Be(2.0);
        clusters[1].Members.Select(m => m.Declination).Should().Equal(3.0, 4.0, 5.0);
        clusters[1].ZoneMinAbsDeclination.Should().Be(3.0);
        clusters[1].ZoneMaxAbsDeclination.Should().Be(5.0);
    }

    /// <summary>
    /// Points at 10°, 11°, 12°, 12.5° and 13.5°: three windows of three tie on members; two span 1.5°, and of
    /// those the lower start wins: {11, 12, 12.5}. Until 1.3.3 one cluster of five.
    /// </summary>
    [Fact]
    public void IdentifyClusters_TieOnMembers_NarrowerSpanThenLowerStartWins()
    {
        IReadOnlyList<DeclinationCluster> clusters = Clusters(10.0, 11.0, 12.0, 12.5, 13.5);

        clusters.Should().ContainSingle();
        clusters[0].Members.Select(m => m.Declination).Should().Equal(11.0, 12.0, 12.5);
        clusters[0].ZoneMinAbsDeclination.Should().Be(11.0);
        clusters[0].ZoneMaxAbsDeclination.Should().Be(12.5);
    }

    /// <summary>
    /// Points at 0°, 1°, 2° and 3°: two windows tie on members and on span, and the lower start wins: {0, 1, 2}.
    /// Until 1.3.3 one cluster of four.
    /// </summary>
    [Fact]
    public void IdentifyClusters_TieOnMembersAndSpan_LowerStartWins()
    {
        IReadOnlyList<DeclinationCluster> clusters = Clusters(0.0, 1.0, 2.0, 3.0);

        clusters.Should().ContainSingle();
        clusters[0].Members.Select(m => m.Declination).Should().Equal(0.0, 1.0, 2.0);
        clusters[0].ZoneMinAbsDeclination.Should().Be(0.0);
        clusters[0].ZoneMaxAbsDeclination.Should().Be(2.0);
    }

    /// <summary>
    /// Points at +1.0°, −1.5°, +2.0° and −3.5°: the window from 1.0 holds three over 1°; the window from 1.5 also
    /// holds three, but spans 2°. One cluster, +1.0°, −1.5° and +2.0°, signed and ordered by absolute declination;
    /// −3.5° is left out. Until 1.3.3 one cluster of four.
    /// </summary>
    [Fact]
    public void IdentifyClusters_MixedSides_KeepsSignedDeclinationsOrderedByAbsoluteValue()
    {
        IReadOnlyList<DeclinationCluster> clusters = Clusters(1.0, -1.5, 2.0, -3.5);

        clusters.Should().ContainSingle();
        clusters[0].Members.Select(m => m.Declination).Should().Equal(1.0, -1.5, 2.0);
        clusters[0].ZoneMinAbsDeclination.Should().Be(1.0);
        clusters[0].ZoneMaxAbsDeclination.Should().Be(2.0);
    }

    /// <summary>
    /// Guard, green before and after: points at 0°, 1°, 2°, 5°, 5.5°, 6° and 6.5° give {5, 5.5, 6, 6.5}, the window
    /// with the most members, then {0, 1, 2}.
    /// </summary>
    [Fact]
    public void IdentifyClusters_TwoSeparateGroups_LargestFirst()
    {
        IReadOnlyList<DeclinationCluster> clusters = Clusters(0.0, 1.0, 2.0, 5.0, 5.5, 6.0, 6.5);

        clusters.Should().HaveCount(2);
        clusters[0].Members.Select(m => m.Declination).Should().Equal(5.0, 5.5, 6.0, 6.5);
        clusters[1].Members.Select(m => m.Declination).Should().Equal(0.0, 1.0, 2.0);
    }

    /// <summary>The clusters of points at the given declinations, named "P1", "P2", … in order.</summary>
    /// <param name="declinations">The declinations, degrees.</param>
    /// <returns>The clusters.</returns>
    private IReadOnlyList<DeclinationCluster> Clusters(params double[] declinations)
    {
        List<PlanetPosition> positions = [.. declinations.Select((d, i) => Position($"P{i + 1}", d))];

        return DeclinationAspectService.IdentifyClusters(positions, _service.Calculate(positions));
    }

    /// <summary>A position with the given name and declination, at a longitude of 0°.</summary>
    /// <param name="name">The point's name.</param>
    /// <param name="declination">The declination, degrees.</param>
    /// <returns>The position.</returns>
    private static PlanetPosition Position(string name, double declination) => new()
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
}
