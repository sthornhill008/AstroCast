namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// Complete result of secondary progression calculations.
/// </summary>
public sealed class ProgressionResult
{
    /// <summary>The progressed date (natal date + age in days).</summary>
    public required DateTime ProgressedDate { get; init; }

    /// <summary>The progressed Julian Day.</summary>
    public required double ProgressedJulianDay { get; init; }

    /// <summary>
    /// The solar arc value (progressed Sun longitude - natal Sun longitude).
    /// Used both for progressions context and solar arc directions.
    /// </summary>
    public required double SolarArcValue { get; init; }

    /// <summary>Progressed planet and angle positions.</summary>
    public required IReadOnlyList<ProgressedPosition> Positions { get; init; }

    /// <summary>Detailed progressed Moon information.</summary>
    public required ProgressedMoonInfo MoonInfo { get; init; }

    /// <summary>Progressed sign changes within 6 months of report period.</summary>
    public required IReadOnlyList<ProgressedSignChange> SignChanges { get; init; }

    /// <summary>Progressed stations within 1 year of report period.</summary>
    public required IReadOnlyList<ProgressedStation> Stations { get; init; }

    /// <summary>Progressed-to-natal aspects (1° orb, major aspects only).</summary>
    public required IReadOnlyList<ProgressedAspect> Aspects { get; init; }

    /// <summary>
    /// The progressed points' aspects to the natal Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming,
    /// 1.6.0; R7): the same progressed points (the Sun to Mars, the Ascendant and the Midheaven), the five majors
    /// at 1°, applying or separating by the same rule, with the five as targets only. Empty unless the natal chart
    /// carries the five (<see cref="ChartResult.MinorPoints"/>); never in <see cref="Aspects"/>.
    /// </summary>
    public IReadOnlyList<ProgressedAspect> MinorPointAspects { get; init; } = [];
}
