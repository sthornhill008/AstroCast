using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Primary directions of a natal chart (branch Primary, 1.5.0): Placidus semi-arcs in zodiaco, direct and
/// traditional converse, the promissors and significators of rules R4 and R5, dated by the key of rule R6.
/// </summary>
public interface IPrimaryDirectionService
{
    /// <summary>
    /// Finds every primary direction of the chart exact between birth and the settings' age limit, with each hit's
    /// sensitivity to the birth time.
    /// </summary>
    /// <param name="natalChart">The natal chart, with its positions' equatorial coordinates, its houses and its Part of Fortune.</param>
    /// <param name="settings">The key, the significators, the age limit and the birth-time uncertainty.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The hits in time order, with the count of directions left out.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the chart's positions carry no right ascension or declination, or it has no houses, obliquity or
    /// Part of Fortune.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    PrimaryDirectionResult Calculate(
        ChartResult natalChart,
        PrimaryDirectionSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the primary directions of <see cref="Calculate"/> whose instant's UT date lies in a period.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="settings">The settings of <see cref="Calculate"/>; a hit past the age limit is never in the period.</param>
    /// <param name="start">The period's first day, a UT date.</param>
    /// <param name="end">The period's last day, a UT date, on or after <paramref name="start"/>.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The hits whose instant's UT date lies in [<paramref name="start"/>, <paramref name="end"/>], in time order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    /// <exception cref="ArgumentException">Thrown as <see cref="Calculate"/> throws it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    IReadOnlyList<PrimaryDirectionHit> FindHits(
        ChartResult natalChart,
        PrimaryDirectionSettings settings,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}
