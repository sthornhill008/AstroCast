using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// An exact aspect between two transiting planets detected during the
/// report period. Represents a mundane event affecting the general sky
/// configuration, not specific to any natal chart.
/// </summary>
/// <remarks>
/// Transit-to-transit aspects are major Ptolemaic aspects only (conjunction,
/// sextile, square, trine, opposition) between the standard transit bodies
/// (Jupiter through Pluto, Chiron). North Node is excluded due to its
/// oscillating motion producing frequent false positives.
/// </remarks>
public sealed class TransitToTransitAspect
{
    /// <summary>The first transiting body (by traditional order: outermost first).</summary>
    public required TransitBody Planet1 { get; init; }

    /// <summary>Display name of the first planet.</summary>
    public required string Planet1Name { get; init; }

    /// <summary>Ecliptic longitude of planet 1 at the exact moment.</summary>
    public required double Planet1Longitude { get; init; }

    /// <summary>Zodiac sign and degree of planet 1 at the exact moment.</summary>
    public required string Planet1Position { get; init; }

    /// <summary>The second transiting body.</summary>
    public required TransitBody Planet2 { get; init; }

    /// <summary>Display name of the second planet.</summary>
    public required string Planet2Name { get; init; }

    /// <summary>Ecliptic longitude of planet 2 at the exact moment.</summary>
    public required double Planet2Longitude { get; init; }

    /// <summary>Zodiac sign and degree of planet 2 at the exact moment.</summary>
    public required string Planet2Position { get; init; }

    /// <summary>The aspect type name (conjunction, square, trine, etc.).</summary>
    public required string AspectName { get; init; }

    /// <summary>Aspect abbreviation (cnj, sqr, tri, etc.).</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>The aspect angle (0, 60, 90, 120, 180).</summary>
    public required double AspectAngle { get; init; }

    /// <summary>UTC date/time of the exact aspect, refined by binary search.</summary>
    public required DateTime ExactDateTimeUtc { get; init; }

    /// <summary>
    /// Residual orb at the exact moment in degrees. Represents the precision
    /// achieved by binary search refinement (typically &lt; 0.001°).
    /// </summary>
    public required double ExactOrbDegrees { get; init; }
}
