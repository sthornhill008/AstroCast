using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Moon condition analysis for an event chart. Contains phase, Void of Course
/// status, first applying aspect, and Via Combusta detection.
/// </summary>
/// <remarks>
/// <para>
/// Phase is calculated from the Sun-Moon angular separation (Moon longitude
/// minus Sun longitude, normalised to 0-360). The eight 45-degree segments
/// correspond to the eight traditional Moon phases.
/// </para>
/// <para>
/// Void of Course (VoC) means the Moon will not perfect a major Ptolemaic
/// aspect (conjunction, opposition, trine, square, sextile) to any visible
/// planet (Sun through Pluto) before leaving its current sign. For a chart
/// cast at an instant the Moon and every target are followed in the
/// ephemeris up to the Moon's sign exit (<c>MoonConditionService</c>), so a
/// target that runs ahead or comes to meet the Moon is where it really is
/// when the aspect perfects. Positions with no motion of their own, such as
/// a composite chart's midpoints, are scanned with every target held at its
/// given longitude.
/// </para>
/// <para>
/// Via Combusta is the region from 15° Libra to 15° Scorpio (195°-225°
/// ecliptic longitude). The Moon in this region is traditionally considered
/// weakened.
/// </para>
/// </remarks>
public sealed class MoonConditionInfo
{
    /// <summary>The Moon phase classification.</summary>
    public required MoonPhaseType Phase { get; init; }

    /// <summary>
    /// Sun-Moon angular separation in degrees (0-360).
    /// Calculated as (Moon longitude - Sun longitude + 360) % 360.
    /// </summary>
    public required double PhaseAngle { get; init; }

    /// <summary>
    /// True if the Moon is waxing (phase angle 0-180, light increasing).
    /// </summary>
    public bool IsWaxing => PhaseAngle < 180.0;

    /// <summary>
    /// True if the Moon is void of course — no major Ptolemaic aspect
    /// to any visible planet (Sun through Pluto) will perfect before
    /// the Moon changes sign.
    /// </summary>
    public required bool IsVoidOfCourse { get; init; }

    /// <summary>
    /// Name of the first planet the Moon applies to by major Ptolemaic aspect,
    /// or null if the Moon is void of course.
    /// </summary>
    public string? FirstApplyingPlanet { get; init; }

    /// <summary>
    /// The aspect type of the Moon's first applying Ptolemaic aspect,
    /// or null if the Moon is void of course.
    /// </summary>
    public AspectType? FirstApplyingAspectType { get; init; }

    /// <summary>
    /// The separation (in degrees) of the Moon's first applying Ptolemaic
    /// aspect from exact at the chart instant, or null if the Moon is void of
    /// course.
    /// </summary>
    /// <remarks>
    /// With every target held still this is also the arc the Moon travels to
    /// perfect the aspect. With the targets moving the Moon travels more or
    /// less than this, as the target runs ahead of it or comes to meet it.
    /// </remarks>
    public double? FirstApplyingOrb { get; init; }

    /// <summary>
    /// Position within the current lunar phase: "early" (first 15° of the 45° range),
    /// "mid" (middle 15°), or "late" (final 15°, transitioning toward next phase).
    /// Calculated from <see cref="PhaseAngle"/> using the formula: phaseAngle % 45.
    /// Early corresponds to the phase's defining theme at full strength; late indicates
    /// the next phase's quality is emerging.
    /// </summary>
    public required string PhasePosition { get; init; }

    /// <summary>
    /// True if the Moon is in Via Combusta (15° Libra to 15° Scorpio,
    /// ecliptic longitude 195°-225°).
    /// </summary>
    public required bool IsViaCombusta { get; init; }

    /// <summary>The zodiac sign the Moon currently occupies.</summary>
    public required ZodiacSign MoonSign { get; init; }

    /// <summary>The Moon's ecliptic longitude in decimal degrees (0-360).</summary>
    public required double MoonLongitude { get; init; }

    /// <summary>
    /// Human-readable label for the Moon phase
    /// (e.g., "New Moon", "Waxing Crescent", "Full Moon").
    /// </summary>
    public string PhaseLabel => Phase switch
    {
        MoonPhaseType.NewMoon => "New Moon",
        MoonPhaseType.WaxingCrescent => "Waxing Crescent",
        MoonPhaseType.FirstQuarter => "First Quarter",
        MoonPhaseType.WaxingGibbous => "Waxing Gibbous",
        MoonPhaseType.FullMoon => "Full Moon",
        MoonPhaseType.WaningGibbous => "Waning Gibbous",
        MoonPhaseType.LastQuarter => "Last Quarter",
        MoonPhaseType.WaningCrescent => "Waning Crescent",
        _ => Phase.ToString(),
    };
}
