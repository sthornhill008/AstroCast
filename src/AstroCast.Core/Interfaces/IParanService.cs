using AstroCast.Core.Configuration;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for paran (paranatellonta) calculations.
/// Detects when two celestial bodies simultaneously occupy angular positions
/// (rising, culminating, setting, anti-culminating) at a specific geographic
/// location within a configured time orb.
/// </summary>
/// <remarks>
/// The implementation uses <c>swe_rise_trans()</c> for all four angular events:
/// rise (<c>SE_CALC_RISE</c>), set (<c>SE_CALC_SET</c>), upper meridian transit
/// (<c>SE_CALC_MTRANSIT</c>), and lower meridian transit (<c>SE_CALC_ITRANSIT</c>).
/// Rise and set events include atmospheric refraction and disc-centre corrections.
/// Meridian transits are purely geometric.
/// </remarks>
public interface IParanService
{
    /// <summary>
    /// Calculate all paran events for a given date and geographic location,
    /// using the default body set and orb.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number (UT) for the start of the calculation window.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <returns>A <see cref="ParanResult"/> containing all angular crossings and detected parans.</returns>
    ParanResult Calculate(double julianDayUt, double latitude, double longitude);

    /// <summary>
    /// Calculate all paran events with custom options.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number (UT) for the start of the calculation window.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="options">Configuration options controlling body set, orb, and filtering.</param>
    /// <returns>A <see cref="ParanResult"/> containing all angular crossings and detected parans.</returns>
    ParanResult Calculate(double julianDayUt, double latitude, double longitude,
                          ParanOptions options);
}
