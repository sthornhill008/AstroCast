namespace AstroCast.Core.Models;

/// <summary>
/// Static definition of an Arabic Part (Lot) formula. Used by the
/// <see cref="AstroCast.Core.Services.ArabicPartService"/> to calculate
/// each Part from chart data.
/// </summary>
/// <remarks>
/// The formula is: <c>Base + FactorA − FactorB</c>.
/// The base is usually "Asc" (Ascendant) but can be "MC" (Midheaven).
/// Factor names are resolved to ecliptic longitudes at runtime.
/// When <see cref="ReversesAtNight"/> is true, FactorA and FactorB are
/// swapped for night charts.
/// </remarks>
public sealed class ArabicPartDefinition
{
    /// <summary>Display name (e.g., "Part of Fortune", "Lot of Eros").</summary>
    public required string Name { get; init; }

    /// <summary>Group: "Arabic" or "Hermetic".</summary>
    public required string Group { get; init; }

    /// <summary>
    /// The base point for the formula: "Asc" (default) or "MC".
    /// </summary>
    public string Base { get; init; } = "Asc";

    /// <summary>
    /// First additive factor name. Resolved to a longitude at runtime.
    /// Valid values: planet names, "Desc", "Fortune", "Spirit", "19Ari", "Cusp8".
    /// </summary>
    public required string FactorA { get; init; }

    /// <summary>
    /// Second (subtractive) factor name. Resolved to a longitude at runtime.
    /// </summary>
    public required string FactorB { get; init; }

    /// <summary>
    /// True if Factor_A and Factor_B are swapped for night charts.
    /// Most traditional Parts reverse; some (Death, Marriage, Vocation) do not.
    /// </summary>
    public required bool ReversesAtNight { get; init; }

    /// <summary>
    /// The domain or life area this Part addresses.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Computation order priority. Parts with lower order are computed first.
    /// Fortune and Spirit must be computed before Hermetic Lots that reference them.
    /// Default: 10. Fortune and Spirit: 1.
    /// </summary>
    public int ComputeOrder { get; init; } = 10;
}
