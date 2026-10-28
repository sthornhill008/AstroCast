using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for computing astrocartography (ACG) lines.
/// Determines the geographic locations where each natal planet is angular
/// (on the Ascendant, Descendant, Midheaven, or IC).
/// </summary>
public interface IAstrocartographyService
{
    /// <summary>
    /// Computes all astrocartography lines for a natal chart.
    /// Returns up to 48 lines (12 bodies x 4 line types).
    /// </summary>
    /// <param name="natalChart">The computed natal chart.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete ACG result with all angular lines.</returns>
    AcgResult Calculate(ChartResult natalChart, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds which ACG lines pass near a specific geographic location: every line whose
    /// nearest point is within <paramref name="orbKilometres"/> of it, by great-circle
    /// distance on a sphere of radius <see cref="AstrocartographyService.EarthRadiusKilometres"/>.
    /// </summary>
    /// <param name="acgResult">Pre-computed ACG result.</param>
    /// <param name="latitude">Geographic latitude of the query location.</param>
    /// <param name="longitude">Geographic longitude of the query location.</param>
    /// <param name="locationName">Display name for the location.</param>
    /// <param name="orbKilometres">
    /// Maximum distance in kilometres to include (default
    /// <see cref="AstrocartographyService.DefaultCrossingOrbKilometres"/>, 334 km).
    /// </param>
    /// <returns>Crossing report listing nearby lines sorted by distance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="acgResult"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="locationName"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="orbKilometres"/> is not positive.</exception>
    AcgCrossingReport GetCrossings(
        AcgResult acgResult,
        double latitude, double longitude,
        string locationName,
        double orbKilometres = AstrocartographyService.DefaultCrossingOrbKilometres);
}
