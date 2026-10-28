using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates secondary progressions using the day-for-a-year method.
/// </summary>
public interface IProgressionService
{
    /// <summary>
    /// Calculates complete progression data for a natal chart at a reference day: the report's start
    /// since branch Modes (1.4.0), its midpoint before.
    /// </summary>
    /// <param name="natalChart">The natal <see cref="ChartResult"/>.</param>
    /// <param name="reportMidpointDate">
    /// The reference day, a proleptic day (the transit scan's start; branch R3, 1.3.4), never a typed date.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>Complete <see cref="ProgressionResult"/>.</returns>
    ProgressionResult Calculate(ChartResult natalChart, DateOnly reportMidpointDate, CancellationToken cancellationToken = default);
}
