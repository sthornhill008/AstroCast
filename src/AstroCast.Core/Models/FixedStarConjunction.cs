namespace AstroCast.Core.Models;

/// <summary>
/// Represents a conjunction between a fixed star and a chart point
/// (planet, angle, node, or Chiron) within the defined orb.
/// </summary>
/// <remarks>
/// Fixed stars are traditionally interpreted only by conjunction.
/// The orb is applied to ecliptic longitude only (not latitude).
/// Produced by <see cref="AstroCast.Core.Interfaces.IFixedStarService"/>.
/// </remarks>
public sealed class FixedStarConjunction
{
    /// <summary>Display name of the fixed star (e.g., "Regulus", "Algol").</summary>
    public required string StarName { get; init; }

    /// <summary>Constellation the star belongs to (e.g., "Leo", "Perseus").</summary>
    public required string Constellation { get; init; }

    /// <summary>Visual magnitude of the star (lower = brighter).</summary>
    public required double Magnitude { get; init; }

    /// <summary>Ecliptic longitude of the star in decimal degrees (0-360, tropical zodiac).</summary>
    public required double StarLongitude { get; init; }

    /// <summary>Name of the chart point the star conjoins (e.g., "Sun", "MC", "True Node").</summary>
    public required string ChartPointName { get; init; }

    /// <summary>Ecliptic longitude of the chart point in decimal degrees (0-360).</summary>
    public required double ChartPointLongitude { get; init; }

    /// <summary>Absolute angular distance between star and chart point in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>Ptolemaic planetary nature from tradition (e.g., "Mars-Jupiter", "Venus-Mercury").</summary>
    public required string Nature { get; init; }

    /// <summary>Brief interpretive keywords from traditional sources.</summary>
    public required string Keywords { get; init; }

    /// <summary>True if this star is one of the four Royal Stars (Aldebaran, Regulus, Antares, Fomalhaut).</summary>
    public required bool IsRoyalStar { get; init; }

    /// <summary>True if this star is one of the 15 traditional Behenian stars.</summary>
    public required bool IsBehenianStar { get; init; }

    /// <summary>
    /// Sort priority of the chart point for output ordering.
    /// Lower values appear first: ASC=0, MC=1, DSC=2, IC=3, Sun=4, Moon=5, etc.
    /// </summary>
    public required int ChartPointSortOrder { get; init; }
}
