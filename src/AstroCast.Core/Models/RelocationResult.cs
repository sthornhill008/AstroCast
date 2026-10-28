using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Complete relocation analysis result containing the natal chart, the
/// relocated chart, and a comparison of house placements.
/// </summary>
/// <remarks>
/// <para>
/// The natal and relocated charts share identical planetary positions
/// (same signs, same degrees, same aspects) because they represent the
/// same moment in time. Only the house cusps, Ascendant, Midheaven, and
/// all house-dependent data differ.
/// </para>
/// <para>
/// The comparison data highlights which planets changed houses, which
/// became angular (gaining prominence), and which left angles (losing
/// prominence). This is the primary interpretive focus of relocation
/// astrology.
/// </para>
/// </remarks>
public sealed class RelocationResult
{
    /// <summary>The original natal chart at the birth location.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>The relocated chart at the target location.</summary>
    public required ChartResult RelocatedChart { get; init; }

    /// <summary>The relocation target location details.</summary>
    public required RelocationInput RelocationTarget { get; init; }

    /// <summary>
    /// Planets whose house or angularity changed between the natal and
    /// relocated charts; an angularity change with the same house number is
    /// a row with equal houses. Ordered by significance: planets that became
    /// angular first, then planets that left angles, then by name.
    /// </summary>
    public required IReadOnlyList<HouseChange> HouseChanges { get; init; }

    /// <summary>
    /// The natal Ascendant sign.
    /// </summary>
    public required ZodiacSign NatalAscendantSign { get; init; }

    /// <summary>
    /// The relocated Ascendant sign.
    /// </summary>
    public required ZodiacSign RelocatedAscendantSign { get; init; }

    /// <summary>
    /// True if the Ascendant sign changed between natal and relocated charts.
    /// A sign change means the chart ruler changes — the most significant
    /// relocation effect.
    /// </summary>
    public bool AscendantSignChanged =>
        NatalAscendantSign != RelocatedAscendantSign;

    /// <summary>
    /// The natal Midheaven sign.
    /// </summary>
    public required ZodiacSign NatalMidheavenSign { get; init; }

    /// <summary>
    /// The relocated Midheaven sign.
    /// </summary>
    public required ZodiacSign RelocatedMidheavenSign { get; init; }

    /// <summary>
    /// True if the Midheaven sign changed between natal and relocated charts.
    /// </summary>
    public bool MidheavenSignChanged =>
        NatalMidheavenSign != RelocatedMidheavenSign;

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this analysis was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
