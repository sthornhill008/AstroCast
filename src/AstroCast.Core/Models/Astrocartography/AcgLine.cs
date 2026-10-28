using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Astrocartography;

/// <summary>
/// A single astrocartography line representing where a planet is angular
/// (MC, IC, ASC, or DSC) across the globe.
/// </summary>
/// <remarks>
/// The line is in mundo: it marks where the body itself is on the meridian or the
/// horizon. MC/IC lines are straight vertical lines with 2 coordinate points, at
/// 85 degrees south and north. ASC/DSC lines are curves with a point at every whole
/// degree of latitude from -85 to +85 at which the body rises and sets; where the body
/// stops rising and setting, at |latitude| = 90 degrees - |declination|, the line ends,
/// and each end is a point of it.
/// </remarks>
public sealed class AcgLine
{
    /// <summary>The celestial body this line belongs to.</summary>
    public required CelestialBodyId Body { get; init; }

    /// <summary>Display name of the celestial body (e.g., "Sun", "Jupiter").</summary>
    public required string BodyName { get; init; }

    /// <summary>The type of angular line (MC, IC, ASC, DSC).</summary>
    public required AcgLineType LineType { get; init; }

    /// <summary>
    /// Ordered coordinate points defining the line path, by increasing latitude.
    /// MC/IC: 2 points (vertical line from -85 to +85 degrees).
    /// ASC/DSC: one point per whole degree of latitude from -85 to +85 at which the body
    /// rises and sets (171 for a body within 5 degrees of the equator), plus the two ends
    /// of a line that stops short of 85 degrees.
    /// </summary>
    public required IReadOnlyList<AcgCoordinate> Coordinates { get; init; }

    /// <summary>
    /// The ecliptic longitude of the planet (degrees, 0-360).
    /// </summary>
    public required double PlanetLongitude { get; init; }

    /// <summary>
    /// The ecliptic latitude of the planet (degrees).
    /// </summary>
    public required double PlanetLatitude { get; init; }
}
