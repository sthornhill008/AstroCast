using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service that produces a combined chronological timeline of all planetary returns
/// and sign ingresses within a date range — a master timing calendar.
/// </summary>
/// <remarks>
/// <para>
/// Returns require a natal chart to define the target longitudes (the planet's
/// natal position). Ingresses are chart-independent — they record the moment
/// a planet crosses 0° of a zodiac sign.
/// </para>
/// <para>
/// For a 5-year window, expect ~30 return events (mostly Mercury and Venus)
/// and ~10–15 outer planet ingress events. All events are merged into a single
/// chronological timeline.
/// </para>
/// </remarks>
public interface IReturnIngressTimelineService
{
    /// <summary>
    /// Calculate the combined Return &amp; Ingress Timeline.
    /// </summary>
    /// <param name="natalChart">The natal chart (required for returns; null for ingress-only mode).</param>
    /// <param name="startYear">Start year of the scan range.</param>
    /// <param name="endYear">End year of the scan range.</param>
    /// <param name="includeReturns">If true, find planetary returns to natal positions.</param>
    /// <param name="includeIngresses">If true, find sign ingresses for selected planets.</param>
    /// <param name="returnPlanets">Which planets to track for returns. Null = default 5 (Mercury–Saturn).</param>
    /// <param name="ingressPlanets">Which planets to track for ingresses. Null = outer planets only (Jupiter–Pluto + Chiron).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete timeline with all events sorted chronologically.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="includeReturns"/> is true and <paramref name="natalChart"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="startYear"/> is greater than <paramref name="endYear"/>.
    /// </exception>
    ReturnIngressTimeline Calculate(
        ChartResult? natalChart,
        int startYear,
        int endYear,
        bool includeReturns = true,
        bool includeIngresses = true,
        CelestialBodyId[]? returnPlanets = null,
        CelestialBodyId[]? ingressPlanets = null,
        CancellationToken cancellationToken = default);
}
