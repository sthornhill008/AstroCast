using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for solar return chart calculation.
/// Finds the exact UTC moment the transiting Sun returns to its natal longitude
/// nearest the native's birthday in a target year and casts a full chart at that moment.
/// </summary>
public interface ISolarReturnService
{
    /// <summary>
    /// Calculate a solar return chart for the specified year: the Sun's return to its
    /// natal longitude nearest the birthday in that year (ruling T-D7).
    /// </summary>
    /// <remarks>
    /// The birthday is 12:00 UT on the birth's local month and day in <paramref name="year"/>,
    /// read on the calendar in force on that date; 29 February in a common year is read as
    /// 28 February. A December native gets that December's return; a native born on
    /// 1 January may get 31 December of the year before, when it is the nearer. The birth
    /// year gives the birth instant itself.
    /// </remarks>
    /// <param name="natalChart">The natal chart containing the reference Sun longitude.</param>
    /// <param name="year">The year whose birthday the return is nearest.</param>
    /// <param name="latitude">Latitude of the return location (affects house cusps).</param>
    /// <param name="longitude">Longitude of the return location (affects house cusps).</param>
    /// <param name="locationName">Human-readable name of the return location.</param>
    /// <param name="timeZone">IANA timezone for the return location (for ChartInput display).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SolarReturnResult"/> containing the return chart and metadata.</returns>
    SolarReturnResult Calculate(
        ChartResult natalChart,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);
}
