using AstroCast.Core.Models.Transit;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates transit-to-natal aspects, house overlays, midpoint activations,
/// stations, and ingresses over a date range.
/// </summary>
public interface ITransitService
{
    /// <summary>
    /// Calculates a complete transit report for the given natal chart
    /// over the specified date range.
    /// </summary>
    /// <param name="input">Transit calculation parameters.</param>
    /// <param name="cancellationToken">Token to cancel the long-running calculation.</param>
    /// <returns>Complete transit result with all sections.</returns>
    TransitResult Calculate(TransitInput input, CancellationToken cancellationToken = default);
}
