namespace AstroCast.Core.Models.SolarArcs;

/// <summary>
/// Complete result of solar arc direction calculations.
/// </summary>
public sealed class SolarArcResult
{
    /// <summary>
    /// The solar arc value in degrees (progressed Sun - natal Sun).
    /// This is the same value stored in ProgressionResult.SolarArcValue.
    /// </summary>
    public required double ArcValue { get; init; }

    /// <summary>
    /// Solar arc formatted as degrees and minutes (e.g., "36°08'").
    /// </summary>
    public required string ArcFormatted { get; init; }

    /// <summary>All directed positions (all natal bodies + angles).</summary>
    public required IReadOnlyList<SolarArcPosition> Positions { get; init; }

    /// <summary>
    /// Solar-arc-to-natal aspects (1° orb; the five majors, the semisquare and the sesquiquadrate).
    /// Sorted: hard aspects first, then by orb ascending.
    /// </summary>
    public required IReadOnlyList<SolarArcAspect> Aspects { get; init; }

    /// <summary>
    /// The directed points' aspects to the natal Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming,
    /// 1.6.0; R7): the same directed points (the twelve bodies, the Ascendant and the Midheaven), the same seven
    /// aspects at 1°, applying or separating by the same rule, sorted as <see cref="Aspects"/> is, with the five
    /// as targets only. Empty unless the natal chart carries the five (<see cref="ChartResult.MinorPoints"/>);
    /// never in <see cref="Aspects"/>, and the five are never directed.
    /// </summary>
    public IReadOnlyList<SolarArcAspect> MinorPointAspects { get; init; } = [];
}
