using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates synodic cycles for any two-planet pair: finds every conjunction, groups
/// consecutive conjunctions into one cycle until the pair has passed through an opposition,
/// so a multi-pass (triple) conjunction counts as one cycle beginning at its first pass, and
/// computes the synodic period between cycles' first passes. A pair of two of the Sun,
/// Mercury and Venus, which are never in opposition, has a cycle per conjunction.
/// </summary>
public interface ISynodicCycleService
{
    /// <summary>
    /// Find all conjunctions of two bodies within a year range and compute synodic periods.
    /// </summary>
    /// <param name="bodyA">First celestial body.</param>
    /// <param name="bodyB">Second celestial body (must differ from <paramref name="bodyA"/>).</param>
    /// <param name="startYear">Start year of the search period.</param>
    /// <param name="endYear">End year of the search period (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="SynodicCycleSeries"/> containing all conjunctions found in the
    /// period with synodic period statistics. Bodies are automatically ordered so the
    /// faster-moving body is Body A.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bodyA"/> equals <paramref name="bodyB"/>, or when
    /// <paramref name="startYear"/> is greater than <paramref name="endYear"/>.
    /// </exception>
    SynodicCycleSeries FindConjunctions(
        CelestialBodyId bodyA,
        CelestialBodyId bodyB,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default);
}
