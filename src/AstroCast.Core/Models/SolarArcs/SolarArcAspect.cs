namespace AstroCast.Core.Models.SolarArcs;

/// <summary>
/// An aspect between a solar-arc-directed body and a natal body/angle.
/// </summary>
public sealed class SolarArcAspect
{
    /// <summary>The directed body name (e.g., "SA Sun", "SA Mars").</summary>
    public required string DirectedBody { get; init; }

    /// <summary>Aspect name (e.g., "Conjunction", "Square").</summary>
    public required string AspectName { get; init; }

    /// <summary>Aspect abbreviation (e.g., "cnj", "sqr").</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>The natal target name (e.g., "Venus", "ASC").</summary>
    public required string NatalTarget { get; init; }

    /// <summary>Orb at report midpoint in degrees.</summary>
    public required double OrbDegrees { get; init; }

    /// <summary>
    /// Whether the aspect is applying or separating at report midpoint.
    /// Solar arcs always move forward, so this is determined by whether
    /// the orb is decreasing or increasing.
    /// </summary>
    public required string Direction { get; init; }

    /// <summary>
    /// True if this aspect involves an angle (ASC or MC) as either the
    /// directed body or the natal target. Aspects to angles are
    /// considered especially significant in solar arc work.
    /// </summary>
    public required bool InvolvesAngle { get; init; }

    /// <summary>
    /// True if the aspect is hard: a conjunction, square or opposition, or one of the 45° series, the
    /// semisquare and sesquiquadrate (branch Modes, 1.4.0). Hard aspects are emphasised in solar arc
    /// interpretation.
    /// </summary>
    public required bool IsHardAspect { get; init; }
}
