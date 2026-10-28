using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for calculating planetary return charts for any traditional planet
/// (Mercury, Venus, Mars, Jupiter, Saturn).
/// </summary>
public interface IPlanetaryReturnService
{
    /// <summary>
    /// Calculate a planetary return chart by ordinal return number: the Nth passage of the planet
    /// over its natal longitude after birth, cast at the passage's first pass, with the passage's
    /// other passes listed (ruling T-D6).
    /// </summary>
    /// <param name="natalChart">The natal chart containing the reference planet longitude.</param>
    /// <param name="planet">The planet to compute the return for (Mercury through Saturn).</param>
    /// <param name="returnNumber">
    /// The ordinal return number (1-based): a count of passages from the birth instant, for every
    /// planet. For Saturn: 1 = first return (~age 29), 2 = second return (~age 58). For Mars:
    /// 1 = first return (~age 2). Mercury and Venus return about once a year.
    /// </param>
    /// <param name="latitude">Latitude of the return location.</param>
    /// <param name="longitude">Longitude of the return location.</param>
    /// <param name="locationName">Human-readable return location name.</param>
    /// <param name="timeZone">IANA timezone for the return location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PlanetaryReturnResult"/> with the return chart and metadata.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no such passage occurs before the ephemeris files end; the message names the
    /// last passage found.
    /// </exception>
    PlanetaryReturnResult Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate a planetary return chart for a specific target year: the first passage of the
    /// planet over its natal longitude with a pass, any of its passes, whose UT date lies in the
    /// calendar year, cast at the passage's first pass.
    /// </summary>
    /// <param name="natalChart">The natal chart containing the reference planet longitude.</param>
    /// <param name="planet">The planet to compute the return for (Mercury through Saturn).</param>
    /// <param name="year">The calendar year (UT) one of the return's passes falls in.</param>
    /// <param name="latitude">Latitude of the return location.</param>
    /// <param name="longitude">Longitude of the return location.</param>
    /// <param name="locationName">Human-readable return location name.</param>
    /// <param name="timeZone">IANA timezone for the return location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PlanetaryReturnResult"/> with the return chart and metadata.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no passage has a pass in the year; the message names the previous and the next
    /// passage's first-pass dates.
    /// </exception>
    PlanetaryReturnResult CalculateForYear(
        ChartResult natalChart,
        CelestialBodyId planet,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);
}
