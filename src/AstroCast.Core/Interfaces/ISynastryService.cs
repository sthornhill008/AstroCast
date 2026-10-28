using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates synastry data between two natal charts, including inter-chart
/// aspects with synastry-specific orbs and house overlays in both directions.
/// </summary>
public interface ISynastryService
{
    /// <summary>
    /// Calculate complete synastry analysis between two chart results.
    /// </summary>
    /// <param name="personA">Complete natal chart result for Person A.</param>
    /// <param name="personB">Complete natal chart result for Person B.</param>
    /// <param name="relationshipType">The type of relationship being analyzed.</param>
    /// <param name="relationshipNotes">Optional notes about the relationship.</param>
    /// <param name="cancellationToken">
    /// Cooperative cancellation token. Checked at the outer inter-chart-aspect
    /// loops; throws <see cref="OperationCanceledException"/> if cancellation is
    /// requested before the sweep begins or between point-pair iterations.
    /// </param>
    /// <returns>Full synastry result with inter-chart aspects and house overlays.</returns>
    SynastryResult Calculate(
        ChartResult personA,
        ChartResult personB,
        RelationshipType relationshipType = RelationshipType.Unspecified,
        string? relationshipNotes = null,
        CancellationToken cancellationToken = default);
}
