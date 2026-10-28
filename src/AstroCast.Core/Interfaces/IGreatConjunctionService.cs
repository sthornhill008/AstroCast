using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for finding Jupiter-Saturn great conjunctions within a date range,
/// with element/mutation cycle analysis and optional chart casting.
/// </summary>
public interface IGreatConjunctionService
{
    /// <summary>
    /// Find all Jupiter-Saturn conjunctions within a date range.
    /// </summary>
    /// <param name="startYear">Start year of the scan range.</param>
    /// <param name="endYear">End year of the scan range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GreatConjunctionSeries"/> with all conjunctions found.</returns>
    GreatConjunctionSeries FindConjunctions(
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the great conjunction nearest to a specific year.
    /// </summary>
    /// <param name="year">The target year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="GreatConjunctionResult"/> nearest to mid-year.</returns>
    GreatConjunctionResult FindNearest(
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate a full chart at a great conjunction moment for a given location.
    /// Used for mundane astrology (casting the conjunction chart for a national capital).
    /// </summary>
    /// <param name="conjunction">The conjunction whose moment to cast.</param>
    /// <param name="latitude">Geographic latitude.</param>
    /// <param name="longitude">Geographic longitude.</param>
    /// <param name="locationName">Human-readable location name.</param>
    /// <param name="timeZone">IANA timezone identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A full <see cref="ChartResult"/> at the conjunction moment.</returns>
    ChartResult CastConjunctionChart(
        GreatConjunctionResult conjunction,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);
}
