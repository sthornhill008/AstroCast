using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for the Davison chart (branch Davison, 1.7.0): a real chart cast for the moment halfway
/// between two births, the mean of their instants in UT, at the place halfway between their birthplaces, the
/// mean latitude and the mean longitude by the shorter arc round the globe. The two charts are any two the
/// synastry pair can be; the result is a cast like any other, with true planets, angles and houses.
/// </summary>
public interface IDavisonService
{
    /// <summary>
    /// Cast the Davison chart of two charts.
    /// </summary>
    /// <param name="chartA">The first chart, whose house system the Davison chart takes.</param>
    /// <param name="chartB">The second chart.</param>
    /// <param name="includeMinorPoints">
    /// Whether the chart carries the asteroids and Lilith: the session's switch alone decides, whatever the two
    /// sources carry.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DavisonResult"/> holding the Davison chart, the midpoints and the two sources.</returns>
    DavisonResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        bool includeMinorPoints,
        CancellationToken cancellationToken = default);
}
