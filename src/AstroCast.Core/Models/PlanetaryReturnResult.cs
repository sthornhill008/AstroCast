using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Complete planetary return chart result for any traditional planet
/// (Mercury, Venus, Mars, Jupiter, Saturn). Contains the return chart,
/// natal planet reference, return metadata, and the return's ordinal
/// number in the native's life.
/// </summary>
/// <remarks>
/// <para>
/// A planetary return is a passage of the transiting planet over its exact natal ecliptic
/// longitude; its instant is the passage's first pass, and when a retrograde loop lies over the
/// degree the passage has three passes, the retrograde pass and the direct pass after it listed
/// in <see cref="Passes"/> (ruling T-D6, branch F, 1.3.4). As seen from the Earth, Mercury and
/// Venus return to the natal degree about once a year, Mars about every two years (sidereal
/// period 687 days), Jupiter about every 12 years (4,333 days) and Saturn about every 29.5
/// years (10,759 days). <see cref="ReturnNumber"/> counts passages from the birth instant.
/// </para>
/// <para>
/// Saturn returns at ages ~29, ~58, ~87 are among the most significant
/// life-transition markers in astrology. Mars returns are used for
/// physical vitality and medical timing.
/// </para>
/// </remarks>
public sealed class PlanetaryReturnResult
{
    /// <summary>The natal chart used as reference.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>The return chart (full chart at the exact return moment).</summary>
    public required ChartResult ReturnChart { get; init; }

    /// <summary>The planet whose return this is.</summary>
    public required CelestialBodyId Planet { get; init; }

    /// <summary>The planet's name (e.g., "Saturn", "Mars").</summary>
    public required string PlanetName { get; init; }

    /// <summary>The natal planet's ecliptic longitude.</summary>
    public required double NatalLongitude { get; init; }

    /// <summary>The transiting planet's longitude at the exact return moment.</summary>
    public required double ReturnLongitude { get; init; }

    /// <summary>
    /// The ordinal number of this return in the native's life (1-based): the number of the
    /// passage, counted from the birth instant. For Saturn: 1st return ≈ age 29, 2nd ≈ age 58,
    /// 3rd ≈ age 87.
    /// </summary>
    public required int ReturnNumber { get; init; }

    /// <summary>
    /// The native's age at the return: whole years from the birth instant to the return's instant,
    /// both UTC.
    /// </summary>
    public required int AgeAtReturn { get; init; }

    /// <summary>
    /// The passage's passes in order, as UTC instants: the first is the return's own, the instant
    /// the chart is cast at; a passage with a retrograde loop over the degree has three, the
    /// retrograde pass and the direct pass after it following the first (branch F, 1.3.4, ruling
    /// T-D6).
    /// </summary>
    public required IReadOnlyList<DateTime> Passes { get; init; }

    /// <summary>Location where the return chart was cast.</summary>
    public required string ReturnLocationName { get; init; }

    /// <summary>Latitude of the return location.</summary>
    public required double ReturnLatitude { get; init; }

    /// <summary>Geographic longitude of the return location.</summary>
    public required double ReturnLongitude_Geo { get; init; }

    /// <summary>
    /// Precision of the return time in arc-seconds of ecliptic longitude.
    /// </summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
