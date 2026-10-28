using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Configuration;

/// <summary>
/// Configuration options for aspect orb values. Provides default orbs
/// matching the standard <see cref="AspectDefinition.All"/> values,
/// with the ability to create custom configurations.
/// </summary>
/// <remarks>
/// The default orbs follow Reinhold Ebertin's cosmobiology method:
/// <list type="bullet">
///   <item><description>Conjunction (0°): 8° orb</description></item>
///   <item><description>Opposition (180°): 8° orb</description></item>
///   <item><description>Trine (120°): 7° orb</description></item>
///   <item><description>Square (90°): 7° orb</description></item>
///   <item><description>Sextile (60°): 5° orb</description></item>
///   <item><description>Semi-square (45°): 2° orb</description></item>
///   <item><description>Sesquiquadrate (135°): 2° orb</description></item>
///   <item><description>Semi-sextile (30°): 2° orb</description></item>
///   <item><description>Quincunx (150°): 2° orb</description></item>
/// </list>
/// </remarks>
public sealed class AspectOrbOptions
{
    /// <summary>
    /// Orb values keyed by <see cref="AspectType"/>.
    /// </summary>
    public Dictionary<AspectType, double> Orbs { get; init; } = new()
    {
        [AspectType.Conjunction] = 8.0,
        [AspectType.Opposition] = 8.0,
        [AspectType.Trine] = 7.0,
        [AspectType.Square] = 7.0,
        [AspectType.Sextile] = 5.0,
        [AspectType.SemiSquare] = 2.0,
        [AspectType.Sesquiquadrate] = 2.0,
        [AspectType.SemiSextile] = 2.0,
        [AspectType.Quincunx] = 2.0,
    };

    /// <summary>
    /// Returns the default instance with standard Ebertin orbs.
    /// </summary>
    public static AspectOrbOptions Default { get; } = new();
}
