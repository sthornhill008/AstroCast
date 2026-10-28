using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for calculating lunar return charts — the exact moments when the
/// transiting Moon returns to its natal ecliptic longitude (~27.3 day cycle).
/// </summary>
public interface ILunarReturnService
{
    /// <summary>
    /// Calculate all lunar returns within a date range for a natal chart.
    /// </summary>
    /// <param name="natalChart">The natal chart containing the reference Moon longitude.</param>
    /// <param name="startDate">
    /// Start of the date range: a wall-clock reading on the clock of <paramref name="timeZone"/>,
    /// whatever its <see cref="DateTime.Kind"/>. A date picked on a calendar is that day's
    /// local midnight.
    /// </param>
    /// <param name="endDate">
    /// End of the date range, read the same way: a date picked on a calendar ends the range
    /// at that day's local midnight.
    /// </param>
    /// <param name="latitude">Geographic latitude for the return chart location.</param>
    /// <param name="longitude">Geographic longitude for the return chart location.</param>
    /// <param name="locationName">Human-readable name of the return location.</param>
    /// <param name="timeZone">
    /// IANA timezone identifier for chart display, and the clock the range is read on.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chronologically sorted list of lunar return results with sequence numbers.</returns>
    /// <remarks>
    /// The range's bounds were once read as UT, so in New York it ran from 20:00 (19:00 in
    /// winter) the evening before each picked date: a return in those hours before the start
    /// was listed, and one in those hours before the end was not (T0-2). They are read on the
    /// return chart's own clock, through the app's time-zone data.
    /// </remarks>
    IReadOnlyList<LunarReturnResult> Calculate(
        ChartResult natalChart,
        DateTime startDate,
        DateTime endDate,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the next lunar return on or after a given date.
    /// </summary>
    /// <param name="natalChart">The natal chart containing the reference Moon longitude.</param>
    /// <param name="onOrAfter">Find the first return on or after this UTC date.</param>
    /// <param name="latitude">Geographic latitude for the return chart location.</param>
    /// <param name="longitude">Geographic longitude for the return chart location.</param>
    /// <param name="locationName">Human-readable name of the return location.</param>
    /// <param name="timeZone">IANA timezone identifier for chart display.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next lunar return result.</returns>
    LunarReturnResult CalculateNext(
        ChartResult natalChart,
        DateTime onOrAfter,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);
}
