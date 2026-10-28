using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for finding exact moments when two celestial bodies form aspects
/// within a harmonic series. Supports 45° (Ebertin cosmobiology) and 90°
/// (traditional major aspects) harmonic modes.
/// </summary>
/// <remarks>
/// Uses the two-body aspect finder for sub-arc-second precision. The harmonic
/// mode searches multiple target angles in a single operation and returns a
/// complete chronological cycle timeline with waxing/waning phase labels.
/// Does not cast charts — provides timing only.
/// </remarks>
public interface IMundaneAspectService
{
    /// <summary>
    /// Find all exact aspects in the specified harmonic series between two bodies
    /// within a date range.
    /// </summary>
    /// <param name="body1">The first body (typically the slower planet).</param>
    /// <param name="body2">The second body (typically the faster planet).</param>
    /// <param name="harmonicMode">Harmonic mode: "45° Harmonic" or "90° Harmonic".</param>
    /// <param name="startYear">Start year of the scan range.</param>
    /// <param name="endYear">End year of the scan range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="MundaneAspectSeries"/> with all exact aspects found chronologically.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="body1"/> equals <paramref name="body2"/>,
    /// or when <paramref name="harmonicMode"/> is not a recognised mode.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startYear"/> is greater than <paramref name="endYear"/>.
    /// </exception>
    MundaneAspectSeries FindAspects(
        CelestialBodyId body1,
        CelestialBodyId body2,
        string harmonicMode,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default);
}
