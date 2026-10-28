using AstroCast.Core.Enums;

namespace AstroCast.Core.Configuration;

/// <summary>
/// Configuration options for paran (paranatellonta) calculation.
/// Controls which celestial bodies participate and the time orb
/// used to detect angular simultaneities.
/// </summary>
/// <remarks>
/// The default orb of 4.0 minutes (~1° of right ascension) follows
/// Bernadette Brady's methodology. Tighter orbs (2 minutes) yield
/// fewer but more potent parans; wider orbs (8 minutes) capture
/// subtler connections.
/// </remarks>
public sealed class ParanOptions
{
    /// <summary>
    /// Time orb in minutes for paran detection.
    /// </summary>
    /// <value>Default: 4.0 minutes (~1° of right ascension).</value>
    public double OrbMinutes { get; init; } = 4.0;

    /// <summary>
    /// Celestial bodies to include in the paran calculation.
    /// </summary>
    public IReadOnlyList<CelestialBodyId> Bodies { get; init; } =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Moon,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
        CelestialBodyId.TrueNode,
        CelestialBodyId.Chiron,
    ];

    /// <summary>
    /// Optional fixed star names to include in paran detection.
    /// Names must match Swiss Ephemeris sefstars.txt entries.
    /// </summary>
    public IReadOnlyList<string> FixedStars { get; init; } = [];

    /// <summary>
    /// Whether to exclude paran events where both bodies cross the same angle.
    /// </summary>
    /// <value>Default: <c>true</c>.</value>
    public bool ExcludeSameAngle { get; init; } = true;

    /// <summary>Returns the default instance with standard Brady orb and full body set.</summary>
    public static ParanOptions Default { get; } = new();
}
