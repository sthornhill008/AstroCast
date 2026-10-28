using AstroCast.Core.Formatting;

namespace AstroCast.Core.Models;

/// <summary>
/// Represents the midpoint between two chart points on the ecliptic.
/// The near midpoint (shorter arc between the two bodies) is used per convention.
/// </summary>
public sealed class Midpoint
{
    /// <summary>Name of the first body/point.</summary>
    public required string Body1Name { get; init; }

    /// <summary>Ecliptic longitude of the first body/point (degrees).</summary>
    public required double Body1Longitude { get; init; }

    /// <summary>Name of the second body/point.</summary>
    public required string Body2Name { get; init; }

    /// <summary>Ecliptic longitude of the second body/point (degrees).</summary>
    public required double Body2Longitude { get; init; }

    /// <summary>Ecliptic longitude of the near midpoint (degrees, 0–360).</summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Formatted notation: "Sun/Moon" (per COSI convention).
    /// </summary>
    public string Notation => $"{Body1Name}/{Body2Name}";

    /// <summary>Formatted midpoint position: DD°MM′SS″ Sign.</summary>
    public string FormattedPosition => PositionFormatter.FormatLongitude(Longitude);
}
