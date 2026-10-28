using AstroCast.Core.Models.Electional;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Orchestrates electional chart calculation: scans a date range,
/// evaluates candidates, and returns ranked election windows.
/// </summary>
public interface IElectionalService
{
    /// <summary>
    /// Calculates the best election windows for the given input.
    /// </summary>
    /// <param name="input">Election parameters including date range,
    /// location, natal chart, and constraints.</param>
    /// <param name="cancellationToken">Token to cancel the long-running calculation.</param>
    /// <returns>Complete electional result with ranked candidates.
    /// The candidate list is empty when the configured constraints eliminate
    /// every viable moment in the date range.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="ElectionalInput.TimeConstraint"/> wraps midnight
    /// (earliest later than latest), which the candidate sampler does not support;
    /// or when the natal chart's house system has no houses at the election's place
    /// (Placidus or Koch beyond the polar circles), since every candidate is cast in
    /// that system.
    /// </exception>
    ElectionalResult Calculate(ElectionalInput input, CancellationToken cancellationToken = default);
}
