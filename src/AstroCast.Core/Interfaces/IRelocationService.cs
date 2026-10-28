using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates a relocation chart by recasting a natal chart at a different
/// geographic location while preserving the exact UTC moment.
/// </summary>
public interface IRelocationService
{
    /// <summary>
    /// Calculate a relocation chart from a natal chart result and a target location.
    /// </summary>
    /// <param name="natalChart">The complete natal chart result at the birth location.</param>
    /// <param name="target">The relocation target location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A relocation result containing the natal chart, the relocated chart,
    /// and a comparison of house placements.
    /// </returns>
    RelocationResult Calculate(
        ChartResult natalChart,
        RelocationInput target,
        CancellationToken cancellationToken = default);
}
