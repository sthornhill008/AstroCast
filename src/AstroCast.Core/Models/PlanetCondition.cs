namespace AstroCast.Core.Models;

/// <summary>
/// Accidental conditions for a single planet: combustion state (cazimi, combust,
/// under the beams), besiegement, and benefic/malefic influence from conjunctions
/// and applying aspects. Each condition carries a signed score that
/// contributes to an aggregate <see cref="AccidentalScore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Combustion hierarchy</b> (Sun proximity, mutual exclusion):<br/>
///   Cazimi (within 0°17′ = 0.2833°): +5 — planet at the heart of the Sun, greatly strengthened.<br/>
///   Combust (within 8°, excluding cazimi): −5 — overwhelmed by the Sun's light.<br/>
///   Under the Beams (8°–17°): −2 — weakened but not destroyed.
/// </para>
/// <para>
/// <b>Moon exemption:</b> The Moon is never classified as combust or under the beams
/// because the Sun–Moon conjunction (New Moon) is a normal lunation, not a debility.
/// Cazimi still applies to the Moon.
/// </para>
/// <para>
/// <b>Besiegement:</b> A planet is besieged when it is enclosed between Mars and
/// Saturn by zodiacal longitude with no benefic (Jupiter or Venus) intervening.
/// Score: −5.
/// </para>
/// <para>
/// <b>Benefic / malefic influence:</b><br/>
///   Conjunction with Jupiter or Venus: +2 each<br/>
///   Conjunction with Mars or Saturn: −2 each<br/>
///   Applying trine or sextile from Jupiter or Venus: +1, once per flag however many aspects qualify<br/>
///   Applying square or opposition from Mars or Saturn: −1, once per flag however many aspects qualify
/// </para>
/// </remarks>
public sealed class PlanetCondition
{
    /// <summary>Name of the planet this condition applies to.</summary>
    public required string PlanetName { get; init; }

    /// <summary>
    /// True when the planet is within 0°17′ (0.2833°) of the Sun — greatly
    /// fortified ("in the heart of the Sun"). Score: +5.
    /// </summary>
    public required bool IsCazimi { get; init; }

    /// <summary>
    /// True when the planet is within 8° of the Sun but outside the cazimi
    /// zone. The planet's significations are overwhelmed. Score: −5.
    /// The Moon is never combust.
    /// </summary>
    public required bool IsCombust { get; init; }

    /// <summary>
    /// True when the planet is 8°–17° from the Sun. A moderate debility —
    /// weakened but not destroyed. Score: −2.
    /// The Moon is never under the beams.
    /// </summary>
    public required bool IsUnderTheBeams { get; init; }

    /// <summary>
    /// True when the planet is enclosed between Mars and Saturn by zodiacal
    /// longitude with no benefic (Jupiter or Venus) intervening. Score: −5.
    /// </summary>
    public required bool IsBesieged { get; init; }

    /// <summary>
    /// True when the planet is conjunct Jupiter or Venus (benefic influence).
    /// </summary>
    public required bool IsConjunctBenefic { get; init; }

    /// <summary>
    /// True when the planet is conjunct Mars or Saturn (malefic influence).
    /// </summary>
    public required bool IsConjunctMalefic { get; init; }

    /// <summary>
    /// True when the planet has an applying trine or sextile from Jupiter or Venus.
    /// </summary>
    public required bool HasApplyingBeneficAspect { get; init; }

    /// <summary>
    /// True when the planet has an applying square or opposition from Mars or Saturn.
    /// </summary>
    public required bool HasApplyingMaleficAspect { get; init; }

    /// <summary>
    /// Net accidental score from all conditions. Positive values indicate
    /// accidental fortification; negative values indicate accidental debility.
    /// </summary>
    public required int AccidentalScore { get; init; }

    /// <summary>
    /// Human-readable list of active conditions (e.g., "Cazimi", "Combust",
    /// "Besieged", "Conjunct benefic"). Empty when no conditions apply.
    /// </summary>
    public required IReadOnlyList<string> Conditions { get; init; }
}
