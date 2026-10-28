using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Essential dignity information for a single planet in a chart.
/// Describes how well the planet can function in its current zodiac sign
/// based on the traditional dignity/debility system.
/// </summary>
/// <remarks>
/// Uses the Ptolemaic (traditional) rulership system. Modern outer planets
/// (Uranus, Neptune, Pluto), Chiron, and True Node have no traditional
/// dignities and are always peregrine. The dispositor is still valid for
/// all planets — it is the traditional ruler of the sign the planet occupies.
/// </remarks>
public sealed class DignityInfo
{
    /// <summary>Name of the planet (e.g., "Sun", "Moon", "Mars").</summary>
    public required string PlanetName { get; init; }

    /// <summary>The zodiac sign the planet occupies.</summary>
    public required ZodiacSign Sign { get; init; }

    /// <summary>
    /// True if the planet is in the sign it rules (domicile).
    /// A domiciled planet is at home and expresses its nature fully.
    /// Score: +5.
    /// </summary>
    public required bool IsDomicile { get; init; }

    /// <summary>
    /// True if the planet is in its sign of exaltation.
    /// An exalted planet is elevated and performs effortlessly.
    /// Score: +4.
    /// </summary>
    public required bool IsExalted { get; init; }

    /// <summary>
    /// True if the planet is in the sign opposite its domicile.
    /// A planet in detriment struggles to express its nature authentically.
    /// Score: -5.
    /// </summary>
    public required bool IsInDetriment { get; init; }

    /// <summary>
    /// True if the planet is in the sign opposite its exaltation.
    /// A planet in fall is weakened and uncomfortable.
    /// Score: -4.
    /// </summary>
    public required bool IsInFall { get; init; }

    /// <summary>
    /// True if the planet has no essential dignity or debility at any level.
    /// A peregrine planet is considered aimless and lacking resources.
    /// </summary>
    public bool IsPeregrine => !IsDomicile && !IsExalted && !IsTriplicityRuler
                            && !IsInOwnDecan && !IsInDetriment && !IsInFall;

    /// <summary>
    /// The planet that rules the sign this planet occupies (the dispositor).
    /// For example, if Mars is in Cancer, the dispositor is the Moon (ruler of Cancer).
    /// </summary>
    public required string Dispositor { get; init; }

    /// <summary>
    /// The Dorothean triplicity ruler of the sign this planet occupies,
    /// selected based on chart sect (day ruler for day charts, night ruler
    /// for night charts). Null if sect has not been determined.
    /// </summary>
    public string? TriplicityRuler { get; init; }

    /// <summary>
    /// True if this planet is the active triplicity ruler of the sign it
    /// occupies. Confers moderate essential dignity (+3 in the Lilly system).
    /// </summary>
    public bool IsTriplicityRuler { get; init; }

    /// <summary>
    /// The Chaldean decan (face) ruler of the 10° segment this planet
    /// occupies. The decan rulers follow the Chaldean order starting from
    /// Mars for the first decan of Aries.
    /// </summary>
    public string? DecanRuler { get; init; }

    /// <summary>
    /// True if this planet is the decan ruler of the 10° segment it
    /// occupies. Confers the weakest essential dignity (+1 in the Lilly system).
    /// </summary>
    public bool IsInOwnDecan { get; init; }

    /// <summary>
    /// The dignity score, the app's own scheme: the first of Domicile +5,
    /// Exaltation +4, Triplicity +3, Decan (Face) +1, Detriment -5 and Fall -4
    /// that applies, else Peregrine 0; terms are not scored. It is not Lilly's
    /// point system, which adds every dignity and debility and scores a
    /// peregrine planet -5.
    /// </summary>
    public int Score
    {
        get
        {
            if (IsDomicile) return 5;
            if (IsExalted) return 4;
            if (IsTriplicityRuler) return 3;
            if (IsInOwnDecan) return 1;
            if (IsInDetriment) return -5;
            if (IsInFall) return -4;
            return 0;
        }
    }

    /// <summary>
    /// Human-readable label for the dignity status.
    /// One of: "Domicile", "Exalted", "Triplicity", "Face",
    /// "Detriment", "Fall", or "Peregrine".
    /// When multiple dignities apply, the highest takes precedence.
    /// </summary>
    public string Label
    {
        get
        {
            if (IsDomicile) return "Domicile";
            if (IsExalted) return "Exalted";
            if (IsTriplicityRuler) return "Triplicity";
            if (IsInOwnDecan) return "Face";
            if (IsInDetriment) return "Detriment";
            if (IsInFall) return "Fall";
            return "Peregrine";
        }
    }
}
