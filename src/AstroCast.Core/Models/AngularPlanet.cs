namespace AstroCast.Core.Models;

/// <summary>
/// A planet found within close proximity to one of the four chart angles
/// (ASC, MC, DSC, IC), giving it elevated power and visibility.
/// </summary>
public sealed class AngularPlanet
{
    /// <summary>Planet name (e.g., "Venus", "Mars").</summary>
    public required string PlanetName { get; init; }

    /// <summary>Which angle the planet is near ("ASC", "MC", "DSC", "IC").</summary>
    public required string Angle { get; init; }

    /// <summary>
    /// Distance in degrees from the exact angle cusp. Always positive.
    /// Smaller values indicate a stronger angular placement.
    /// </summary>
    /// <remarks>
    /// Unrounded: each export forms it to the minute itself, and the chart JSON rounds it to 4
    /// decimals as it does every orb. Until 1.3.3 it was stored rounded to 0.01°, so the printed
    /// minute was wrong wherever that rounding crossed one (E1-2).
    /// </remarks>
    public required double Orb { get; init; }

    /// <summary>
    /// True if the planet is a traditional benefic (Venus, Jupiter).
    /// False if malefic (Mars, Saturn) or neutral.
    /// </summary>
    public bool IsBenefic => PlanetName is "Venus" or "Jupiter";

    /// <summary>
    /// True if the planet is a traditional malefic (Mars, Saturn).
    /// </summary>
    public bool IsMalefic => PlanetName is "Mars" or "Saturn";
}
