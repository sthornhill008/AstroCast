using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for analyzing the interpretive context of a chart, including
/// chart ruler, sect, event significator, angular planets, and mutual receptions.
/// </summary>
public interface IChartContextService
{
    /// <summary>
    /// Analyze the chart context from positions, house data, dignities,
    /// and the event type.
    /// </summary>
    /// <param name="positions">All planetary positions in the chart.</param>
    /// <param name="houses">House cusps and angles.</param>
    /// <param name="dignities">Essential dignity data for all planets.</param>
    /// <param name="eventType">The type of event (nullable for birth charts).</param>
    /// <param name="sectFrom">
    /// For a relocated chart, the natal chart's context, whose day or night and Planetary Sect table the
    /// analysis takes in place of the Sun's altitude and the sect service's (S-D6, branch K2, 1.3.4); the
    /// chart ruler, angular planets, mutual receptions and the Ascendant's triplicity rulers are computed as
    /// ever, from the chart's own houses and that sect. <see langword="null"/> for any other chart.
    /// </param>
    /// <returns>Comprehensive chart context analysis.</returns>
    ChartContextInfo Analyze(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        IReadOnlyList<DignityInfo> dignities,
        string? eventType,
        ChartContextInfo? sectFrom = null);
}
