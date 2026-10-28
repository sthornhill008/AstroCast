using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for calculating Ebertin cosmobiology midpoints and their activations
/// on the 45° dial (8th harmonic).
/// </summary>
public interface IMidpointService
{
    /// <summary>
    /// Calculate all midpoints for the given chart points.
    /// </summary>
    /// <param name="points">Named chart points (planets + ASC + MC) with longitudes.</param>
    /// <returns>All unique pair midpoints (n choose 2).</returns>
    IReadOnlyList<Midpoint> CalculateMidpoints(
        IReadOnlyList<(string Name, double Longitude)> points);

    /// <summary>
    /// Detect all midpoint activations: bodies at 0°/45°/90°/135°/180° to midpoints.
    /// </summary>
    /// <param name="midpoints">Previously calculated midpoints.</param>
    /// <param name="points">All chart points that can activate midpoints.</param>
    /// <returns>All activations within orb, sorted by tightest orb first.</returns>
    IReadOnlyList<MidpointActivation> CalculateActivations(
        IReadOnlyList<Midpoint> midpoints,
        IReadOnlyList<(string Name, double Longitude)> points);

    /// <summary>
    /// Convenience method: calculate midpoints and activations in one call.
    /// </summary>
    /// <param name="points">Named chart points (planets + ASC + MC) with longitudes.</param>
    /// <returns>A tuple containing the midpoints and their activations.</returns>
    (IReadOnlyList<Midpoint> Midpoints, IReadOnlyList<MidpointActivation> Activations)
        CalculateAll(IReadOnlyList<(string Name, double Longitude)> points);
}
