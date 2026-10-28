using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Immutable weight profile for the seven electional scoring dimensions.
/// All weights must sum to 1.0 so that the overall score remains on a 0–100 scale.
/// </summary>
/// <param name="Moon">Weight for Moon condition (phase, VOC, Via Combusta, dignity).</param>
/// <param name="Ascendant">Weight for rising sign appropriateness.</param>
/// <param name="Significator">Weight for the significator planet's dignity and condition.</param>
/// <param name="NatalHarmony">Weight for natal chart harmony, the type's own with or without a natal chart; without one the dimension scores 50.</param>
/// <param name="MaleficAvoidance">Weight for Mars/Saturn avoidance of angular houses.</param>
/// <param name="Mercury">Weight for Mercury condition (retrograde, combust, dignity).</param>
/// <param name="PlanetaryHour">Weight for planetary hour ruler alignment.</param>
public sealed record ScoreWeights(
    double Moon,
    double Ascendant,
    double Significator,
    double NatalHarmony,
    double MaleficAvoidance,
    double Mercury,
    double PlanetaryHour);

/// <summary>
/// Provides pre-configured <see cref="ScoreWeights"/> for each <see cref="ElectionType"/>.
/// Each profile emphasises dimensions most relevant to that category of election.
/// All weight sets sum to exactly 1.0.
/// </summary>
public static class ElectionalWeights
{
    /// <summary>
    /// Returns the scoring weight profile for the given election type.
    /// </summary>
    /// <param name="electionType">The type of election being scored.</param>
    /// <returns>A <see cref="ScoreWeights"/> whose components sum to 1.0.</returns>
    public static ScoreWeights GetWeights(ElectionType electionType)
    {
        return electionType switch
        {
            //                                Moon  Asc   Sig   Natal Malf  Merc  Hour
            ElectionType.BusinessLaunch    => new(0.25, 0.10, 0.20, 0.10, 0.15, 0.10, 0.10),
            ElectionType.Contract          => new(0.20, 0.10, 0.15, 0.10, 0.10, 0.25, 0.10),
            ElectionType.Wedding           => new(0.30, 0.10, 0.25, 0.10, 0.10, 0.05, 0.10),
            ElectionType.Surgery           => new(0.25, 0.10, 0.15, 0.05, 0.20, 0.10, 0.15),
            ElectionType.RealEstateListing => new(0.25, 0.10, 0.20, 0.10, 0.15, 0.10, 0.10),
            ElectionType.RealEstateClosing => new(0.20, 0.10, 0.20, 0.10, 0.15, 0.15, 0.10),
            ElectionType.CreativeLaunch    => new(0.25, 0.15, 0.20, 0.10, 0.10, 0.10, 0.10),
            ElectionType.Travel            => new(0.25, 0.10, 0.15, 0.10, 0.15, 0.15, 0.10),
            ElectionType.Other             => new(0.25, 0.10, 0.15, 0.10, 0.15, 0.15, 0.10),
            _ => new(0.25, 0.10, 0.15, 0.10, 0.15, 0.15, 0.10),
        };
    }
}
