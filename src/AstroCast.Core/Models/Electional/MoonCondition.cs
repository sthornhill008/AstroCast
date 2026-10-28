using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Moon condition snapshot at a candidate time. The Moon is the primary
/// timing instrument in electional astrology.
/// </summary>
public sealed class MoonCondition
{
    /// <summary>Moon ecliptic longitude in degrees.</summary>
    public required double Longitude { get; init; }

    /// <summary>Moon sign name.</summary>
    public required string Sign { get; init; }

    /// <summary>Moon phase classification.</summary>
    public required MoonPhaseType Phase { get; init; }

    /// <summary>
    /// Exact lunation percentage. 0 = New Moon, ~50 = Full Moon.
    /// Based on Sun-Moon angular separation: angle / 360 * 100.
    /// </summary>
    public required double PhasePercent { get; init; }

    /// <summary>
    /// Position within the current lunar phase: "early" (first 15° of the 45° range),
    /// "mid" (middle 15°), or "late" (final 15°, transitioning toward next phase).
    /// </summary>
    public required string PhasePosition { get; init; }

    /// <summary>True if the Moon is void-of-course at this moment.</summary>
    public required bool IsVoidOfCourse { get; init; }

    /// <summary>If void-of-course, the local DateTime when the void began.</summary>
    public DateTime? VoidStartLocal { get; init; }

    /// <summary>If void-of-course, the local DateTime when Moon enters next sign.</summary>
    public DateTime? VoidEndLocal { get; init; }

    /// <summary>
    /// True if Moon is in the Via Combusta (15° Libra – 15° Scorpio,
    /// i.e., ecliptic longitude 195° – 225°).
    /// </summary>
    public required bool IsInViaCombusta { get; init; }

    /// <summary>
    /// The Moon's next applying major aspect to any visible planet.
    /// Null if void-of-course (no applying aspects remain in current sign).
    /// Format: "Moon trine Jupiter (applying, 2°14′)"
    /// </summary>
    public string? NextApplyingAspectDescription { get; init; }

    /// <summary>
    /// Name of the planet in the Moon's next applying major aspect
    /// (e.g., "Jupiter"). Null when no applying aspect was found.
    /// Structured counterpart of <see cref="NextApplyingAspectDescription"/>,
    /// allowing export sites to resolve the canonical orb from the chart's
    /// aspect table instead of re-parsing the description string.
    /// </summary>
    public string? NextApplyingPlanet { get; init; }

    /// <summary>
    /// Aspect type of the Moon's next applying major aspect.
    /// Null when no applying aspect was found.
    /// </summary>
    public AspectType? NextApplyingAspectType { get; init; }

    /// <summary>
    /// Orb in decimal degrees of the Moon's next applying major aspect as
    /// computed by the evaluator. Null when no applying aspect was found.
    /// Display sites should prefer the canonical orb from the chart's aspect
    /// table when the aspect appears there; this value is the fallback for
    /// aspects wider than the table's per-aspect orb limits.
    /// </summary>
    public double? NextApplyingOrb { get; init; }

    /// <summary>Moon daily motion in degrees (from SpeedInLongitude).</summary>
    public required double DailyMotion { get; init; }
}
