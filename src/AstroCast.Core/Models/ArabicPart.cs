using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// A calculated Arabic Part (Lot) — a sensitive point derived from three
/// chart factors using the formula <c>Base + Factor_A − Factor_B</c>.
/// Most Parts use the Ascendant as the base; some use the MC.
/// Day/night charts reverse Factor_A and Factor_B for most Lots.
/// </summary>
/// <remarks>
/// <para>
/// Arabic Parts are not physical celestial bodies — they are mathematical
/// points on the ecliptic. They are assigned to houses using geometric
/// cusp-range checking (no ecliptic latitude needed).
/// </para>
/// <para>
/// Each Part belongs to a group: "Arabic" for the traditional 8 Parts,
/// "Hermetic" for the 7 Hellenistic Lots. The Lot of Fortune and Lot of
/// Spirit appear in both groups (they are the same calculation) and are
/// included in each group for completeness with their respective labels.
/// </para>
/// </remarks>
public sealed class ArabicPart
{
    /// <summary>Display name of the Part (e.g., "Part of Fortune", "Lot of Eros").</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Group: "Arabic" for the traditional 8 Parts, "Hermetic" for the
    /// 7 Hellenistic Lots.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// Ecliptic longitude of the Part in decimal degrees (0–360), the full value: the sign, the degrees and minutes,
    /// the ruler and the printed position all read it. The chart JSON rounds it to 4 decimal places, as it does every
    /// angular field.
    /// </summary>
    /// <remarks>
    /// Until 1.3.3 it was rounded to 0.0001° (finding F8-2), so the display truncated a rounded value, and just
    /// before a sign's end the sign read the next sign while the ruler, taken from the unrounded value, did not (F8-3).
    /// </remarks>
    public required double Longitude { get; init; }

    /// <summary>The zodiac sign the Part occupies.</summary>
    public ZodiacSign Sign => (ZodiacSign)((int)(((Longitude % 360.0) + 360.0) % 360.0 / 30.0) + 1);

    /// <summary>Degrees within the current sign (0–29).</summary>
    public int SignDegrees => (int)(((Longitude % 360.0) + 360.0) % 360.0 % 30.0);

    /// <summary>Arc-minutes within the current degree (0–59).</summary>
    public int SignMinutes => (int)((((Longitude % 360.0) + 360.0) % 360.0 % 1.0) * 60.0);

    /// <summary>
    /// House number (1–12) the Part occupies, determined by geometric
    /// cusp-range checking. Null if house assignment failed.
    /// </summary>
    public int? House { get; init; }

    /// <summary>
    /// Human-readable description of the formula used to calculate this Part
    /// (e.g., "Asc + Moon − Sun").
    /// </summary>
    public required string Formula { get; init; }

    /// <summary>
    /// The domain or life area this Part describes
    /// (e.g., "General wellbeing, material fortune").
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// The traditional ruler of this Part — the planet that rules the
    /// zodiac sign the Part occupies. Null if not computed.
    /// </summary>
    public string? Ruler { get; init; }
}
