using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Analyses accidental conditions for all planets in a chart: combustion
/// (cazimi, combust, under the beams), besiegement, and benefic/malefic
/// influence from conjunctions and applying aspects.
/// </summary>
public interface IPlanetConditionService
{
    /// <summary>
    /// Evaluate accidental conditions for every planet in the chart.
    /// </summary>
    /// <param name="positions">All planetary positions in the chart.</param>
    /// <param name="aspects">All detected aspects in the chart.</param>
    /// <returns>
    /// A list of <see cref="PlanetCondition"/> objects, one per planet that
    /// has at least one active condition. Planets with no conditions are omitted.
    /// </returns>
    IReadOnlyList<PlanetCondition> Analyze(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects);
}
