using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Computes the full cycle timeline between two consecutive planetary returns,
/// identifying the four critical phase points (return, opening square,
/// opposition, closing square) and the next return.
/// </summary>
public interface IPlanetaryCycleTimelineService
{
    /// <summary>
    /// Calculate the cycle timeline starting from a specific return number.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="planet">The planet to track.</param>
    /// <param name="returnNumber">The return number that starts this cycle.</param>
    /// <param name="includeSnapshots">If true, cast position snapshots at each phase point.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PlanetaryCycleTimeline"/> with all phase points.</returns>
    PlanetaryCycleTimeline Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the cycle timeline for the cycle active at a given date.
    /// Finds the most recent return before the date and computes the cycle from there.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="planet">The planet to track.</param>
    /// <param name="date">A date within the desired cycle.</param>
    /// <param name="includeSnapshots">If true, cast position snapshots at each phase point.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PlanetaryCycleTimeline"/> with all phase points.</returns>
    PlanetaryCycleTimeline CalculateForDate(
        ChartResult natalChart,
        CelestialBodyId planet,
        DateTime date,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default);
}
