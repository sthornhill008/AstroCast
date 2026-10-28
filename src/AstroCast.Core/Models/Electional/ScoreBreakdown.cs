namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Detailed breakdown of an electional score across seven dimensions.
/// Each dimension is scored 0–100, and the <see cref="OverallScore"/>
/// is a weighted sum using the <see cref="Weights"/> profile.
/// </summary>
public sealed class ScoreBreakdown
{
    /// <summary>Moon condition score (phase, VOC, Via Combusta, dignity, applying aspects).</summary>
    public required double MoonScore { get; init; }

    /// <summary>Rising sign appropriateness score.</summary>
    public required double AscendantScore { get; init; }

    /// <summary>Significator planet dignity and condition score.</summary>
    public required double SignificatorScore { get; init; }

    /// <summary>Natal chart harmony score (neutral 50 if no natal chart provided).</summary>
    public required double NatalHarmonyScore { get; init; }

    /// <summary>Malefic avoidance score (Mars/Saturn not angular).</summary>
    public required double MaleficAvoidanceScore { get; init; }

    /// <summary>Mercury condition score (retrograde, combust, dignity).</summary>
    public required double MercuryScore { get; init; }

    /// <summary>Planetary hour alignment score.</summary>
    public required double PlanetaryHourScore { get; init; }

    /// <summary>The weight profile used for this scoring.</summary>
    public required ScoreWeights Weights { get; init; }

    /// <summary>The election type that was scored.</summary>
    public required string ElectionTypeName { get; init; }

    /// <summary>
    /// The overall weighted score (0–100). Computed as the weighted sum
    /// of all seven dimension scores using the <see cref="Weights"/> profile.
    /// </summary>
    public double OverallScore =>
        MoonScore * Weights.Moon +
        AscendantScore * Weights.Ascendant +
        SignificatorScore * Weights.Significator +
        NatalHarmonyScore * Weights.NatalHarmony +
        MaleficAvoidanceScore * Weights.MaleficAvoidance +
        MercuryScore * Weights.Mercury +
        PlanetaryHourScore * Weights.PlanetaryHour;
}
