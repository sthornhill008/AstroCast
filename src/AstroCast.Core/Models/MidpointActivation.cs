using System.Globalization;

namespace AstroCast.Core.Models;

/// <summary>
/// Represents a planet (or point) activating a midpoint on the 45° dial.
/// Notation: A/B = C (per Ebertin's COSI).
/// </summary>
/// <remarks>
/// An activation occurs when a chart point lies at 0°, 45°, 90°, 135°,
/// or 180° from the midpoint axis, within the applicable orb.
/// </remarks>
public sealed class MidpointActivation
{
    /// <summary>The midpoint being activated.</summary>
    public required Midpoint Midpoint { get; init; }

    /// <summary>Name of the activating body/point.</summary>
    public required string ActivatorName { get; init; }

    /// <summary>Ecliptic longitude of the activating body/point.</summary>
    public required double ActivatorLongitude { get; init; }

    /// <summary>
    /// The specific harmonic angle (0, 45, 90, 135, or 180) at which
    /// the activator contacts the midpoint axis.
    /// </summary>
    public required int HarmonicAngle { get; init; }

    /// <summary>The orb of the activation in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>
    /// COSI notation: "Sun/Moon = Mars" or "Sun/Moon = Mars (90°)".
    /// </summary>
    public string Notation => HarmonicAngle == 0
        ? $"{Midpoint.Notation} = {ActivatorName}"
        : string.Create(CultureInfo.InvariantCulture, $"{Midpoint.Notation} = {ActivatorName} ({HarmonicAngle}°)");
}
