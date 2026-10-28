using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for finding New Moon and Full Moon moments with sub-arc-second precision
/// and optionally casting full charts at those moments.
/// </summary>
public interface ILunationService
{
    /// <summary>
    /// Find all New Moons and Full Moons within a date range.
    /// Returns timing only (no full charts).
    /// </summary>
    /// <param name="startYear">Start year.</param>
    /// <param name="endYear">End year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LunationSeries"/> containing all found lunations, chronologically ordered.</returns>
    LunationSeries FindLunations(
        int startYear, int endYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find and cast a full chart for the lunation nearest to a given date.
    /// </summary>
    /// <param name="nearDate">The date to search near.</param>
    /// <param name="newMoon">True to find the nearest New Moon, false for Full Moon.</param>
    /// <param name="latitude">Chart location latitude.</param>
    /// <param name="longitude">Chart location longitude.</param>
    /// <param name="locationName">Location name.</param>
    /// <param name="timeZone">IANA timezone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LunationResult"/> with the full chart populated.</returns>
    LunationResult CalculateLunation(
        DateTime nearDate,
        bool newMoon,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find and cast full charts for all lunations in a given year.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="latitude">Chart location latitude.</param>
    /// <param name="longitude">Chart location longitude.</param>
    /// <param name="locationName">Location name.</param>
    /// <param name="timeZone">IANA timezone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LunationSeries"/> with all charts populated.</returns>
    LunationSeries CalculateLunationsForYear(
        int year,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default);
}
