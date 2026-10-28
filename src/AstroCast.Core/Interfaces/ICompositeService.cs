using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for composite chart calculation.
/// Produces a single synthesised chart from two source charts by computing
/// the short-arc midpoint of each corresponding planet pair (Mercury and Venus
/// nearer the composite Sun) and casting the houses for the midpoint of the two
/// Midheavens at the mean of the two birth latitudes, in the house system both
/// charts share, else Placidus (rulings S-D2 and S-D3, Q1; branch L3, 1.3.4). The
/// composite does not depend on which chart is entered first.
/// </summary>
public interface ICompositeService
{
    /// <summary>
    /// Calculate a composite chart from two source chart results.
    /// </summary>
    /// <param name="chartA">First source chart.</param>
    /// <param name="chartB">Second source chart.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CompositeResult"/> containing the composite chart and source references.</returns>
    /// <remarks>
    /// Until 1.3.4 the call took a reference latitude and longitude for the houses, which the app set to
    /// Person A's birthplace, in Person A's house system, so A &amp; B and B &amp; A differed (Y2-3, Y2-4);
    /// the parameters are gone and the reference latitude is always the mean of the two birth latitudes.
    /// </remarks>
    CompositeResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        CancellationToken cancellationToken = default);
}
