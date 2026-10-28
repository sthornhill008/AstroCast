namespace AstroCast.Core.Models;

/// <summary>
/// Represents a group of 3 or more chart points whose absolute declinations
/// fall within a span of 2° or less. Clusters reveal hidden structural connections
/// between chart points that share similar distances from the celestial equator.
/// </summary>
/// <remarks>
/// Clusters operate on absolute declination — a point at +22°50' and a point
/// at -22°50' both have |22°50'| and belong to the same cluster zone.
/// This is the same principle that underlies contraparallels.
/// </remarks>
public sealed class DeclinationCluster
{
    /// <summary>
    /// Minimum absolute declination in the cluster zone (degrees).
    /// </summary>
    public required double ZoneMinAbsDeclination { get; init; }

    /// <summary>
    /// Maximum absolute declination in the cluster zone (degrees).
    /// </summary>
    public required double ZoneMaxAbsDeclination { get; init; }

    /// <summary>
    /// The chart points in this cluster, with their signed declinations.
    /// Sorted by absolute declination ascending.
    /// </summary>
    public required IReadOnlyList<DeclinationClusterMember> Members { get; init; }

    /// <summary>
    /// Number of declination aspects (parallels + contraparallels) from the
    /// Declination Aspects table that bind cluster members to each other.
    /// </summary>
    public required int IntraClusterAspectCount { get; init; }
}

/// <summary>
/// A single member of a declination cluster: a chart point and its declination.
/// </summary>
public sealed class DeclinationClusterMember
{
    /// <summary>Name of the chart point (e.g., "Moon", "Jupiter").</summary>
    public required string Name { get; init; }

    /// <summary>Signed declination in decimal degrees (positive = north, negative = south).</summary>
    public required double Declination { get; init; }
}
