using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for finding the exact moments when a celestial body enters a zodiac sign
/// and optionally casting full charts at those moments.
/// </summary>
public interface IIngressService
{
    /// <summary>
    /// Find all ingresses of a planet within a date range.
    /// Returns timing only (no full charts) for efficiency.
    /// </summary>
    /// <param name="planet">The planet to track (typically Sun).</param>
    /// <param name="startYear">Start year.</param>
    /// <param name="endYear">End year.</param>
    /// <param name="cardinalOnly">If true, only return cardinal ingresses (0° Aries/Cancer/Libra/Capricorn). Only meaningful for the Sun.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IngressSeries"/> containing all found ingresses, chronologically ordered.</returns>
    IngressSeries FindIngresses(
        CelestialBodyId planet,
        int startYear,
        int endYear,
        bool cardinalOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find and cast a full chart at a specific ingress.
    /// </summary>
    /// <param name="planet">The planet.</param>
    /// <param name="sign">The sign being entered (e.g., ZodiacSign.Aries).</param>
    /// <param name="year">The year to search for this ingress.</param>
    /// <param name="latitude">Chart location latitude.</param>
    /// <param name="longitude">Chart location longitude.</param>
    /// <param name="locationName">Location name.</param>
    /// <param name="timeZone">IANA timezone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IngressResult"/> with the full chart populated.</returns>
    IngressResult CalculateIngress(
        CelestialBodyId planet,
        ZodiacSign sign,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find and cast full charts for all four cardinal Sun ingresses in a given year.
    /// This is the standard mundane astrology "quarterly ingress" set.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="latitude">Chart location latitude.</param>
    /// <param name="longitude">Chart location longitude.</param>
    /// <param name="locationName">Location name (typically a national capital).</param>
    /// <param name="timeZone">IANA timezone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Four <see cref="IngressResult"/> instances with full charts, one per cardinal ingress.</returns>
    IReadOnlyList<IngressResult> CalculateCardinalIngresses(
        int year,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default);
}
