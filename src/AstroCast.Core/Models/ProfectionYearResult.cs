namespace AstroCast.Core.Models;

/// <summary>
/// Annual profection analysis for a natal chart at a given age or date.
/// Identifies the profected house, lord of the year, and the lord's
/// natal condition — used to prioritise planetary themes for the year.
/// </summary>
public sealed class ProfectionYearResult
{
    /// <summary>The native's name.</summary>
    public required string Name { get; init; }

    /// <summary>The birth date (from ChartInput.LocalDateTime).</summary>
    public required DateTime BirthDate { get; init; }

    /// <summary>The reference date for this profection (typically today or a birthday).</summary>
    public required DateTime ReferenceDate { get; init; }

    /// <summary>The native's age at the reference date (completed years).</summary>
    public required int Age { get; init; }

    /// <summary>The profection year number within the 12-year cycle (0–11).</summary>
    public required int ProfectionCycleYear { get; init; }

    /// <summary>The profected house number (1–12).</summary>
    public required int ProfectedHouse { get; init; }

    /// <summary>
    /// The profected sign: the Ascendant's sign plus (house − 1) signs, counted in whole signs, not the sign
    /// on a cusp (T-D9).
    /// </summary>
    public required string ProfectedSign { get; init; }

    /// <summary>
    /// The lord of the year — traditional ruler of the profected sign, counted from the Ascendant's sign.
    /// </summary>
    public required string LordOfTheYear { get; init; }

    /// <summary>The house themes activated by the profected house.</summary>
    public required string ActivatedThemes { get; init; }

    /// <summary>The lord's natal sign.</summary>
    public required string LordNatalSign { get; init; }

    /// <summary>The lord's natal house.</summary>
    public required int LordNatalHouse { get; init; }

    /// <summary>The lord's essential dignity label and score.</summary>
    public required string LordDignity { get; init; }

    /// <summary>The lord's essential dignity score.</summary>
    public required int LordDignityScore { get; init; }

    /// <summary>True if the lord is retrograde natally.</summary>
    public required bool LordRetrograde { get; init; }

    /// <summary>Active conditions on the lord (combustion, besiegement, etc.).</summary>
    public required IReadOnlyList<string> LordConditions { get; init; }

    /// <summary>The lord's accidental score from conditions.</summary>
    public required int LordAccidentalScore { get; init; }

    /// <summary>Whether the lord is in sect in the natal chart.</summary>
    public required bool LordInSect { get; init; }

    /// <summary>Natal aspects the lord makes (applying and separating).</summary>
    public required IReadOnlyList<string> LordNatalAspects { get; init; }

    /// <summary>The complete 12-year profection table for context.</summary>
    public required IReadOnlyList<ProfectionTableEntry> ProfectionTable { get; init; }

    /// <summary>A narrative summary of the profection year.</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// A single row in the 12-year profection table showing the house,
/// sign, lord, and age for each year of the cycle.
/// </summary>
public sealed class ProfectionTableEntry
{
    /// <summary>House number (1–12).</summary>
    public required int House { get; init; }

    /// <summary>The sign (house − 1) signs after the Ascendant's, counted in whole signs; not the sign on the cusp.</summary>
    public required string Sign { get; init; }

    /// <summary>Traditional ruler of the sign.</summary>
    public required string Lord { get; init; }

    /// <summary>The age(s) that fall in this profection year, relative to the current cycle.</summary>
    public required string Ages { get; init; }

    /// <summary>True if this is the current profection year.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>House themes.</summary>
    public required string Themes { get; init; }
}
