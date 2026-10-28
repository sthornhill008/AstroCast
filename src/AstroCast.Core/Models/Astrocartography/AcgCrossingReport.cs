using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Astrocartography;

/// <summary>
/// Report of which astrocartography lines pass near a specific geographic location.
/// </summary>
public sealed class AcgCrossingReport
{
    /// <summary>Name of the location (e.g., "London, UK").</summary>
    public required string LocationName { get; init; }

    /// <summary>Geographic latitude of the location.</summary>
    public required double Latitude { get; init; }

    /// <summary>Geographic longitude of the location.</summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// The orb the report was made with: the greatest great-circle distance, in kilometres,
    /// at which a line is listed.
    /// </summary>
    public required double OrbKilometres { get; init; }

    /// <summary>Lines that pass within the specified orb of the location, sorted by distance.</summary>
    public required IReadOnlyList<AcgCrossing> Crossings { get; init; }
}

/// <summary>
/// A single ACG line crossing near a location.
/// </summary>
public sealed class AcgCrossing
{
    /// <summary>The celestial body.</summary>
    public required CelestialBodyId Body { get; init; }

    /// <summary>Display name of the celestial body.</summary>
    public required string BodyName { get; init; }

    /// <summary>The line type (MC, IC, ASC, DSC).</summary>
    public required AcgLineType LineType { get; init; }

    /// <summary>
    /// Distance from the location to the nearest point of the line, in kilometres:
    /// the great-circle distance on a sphere of radius
    /// <see cref="Services.AstrocartographyService.EarthRadiusKilometres"/>, whatever the
    /// line type and the latitude.
    /// </summary>
    public required double DistanceKilometres { get; init; }
}
