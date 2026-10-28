using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Two planets in mutual reception — each occupying a sign where the
/// other has dignity (domicile, exaltation, or triplicity). This creates
/// a cooperative link even if the planets don't form a direct aspect.
/// </summary>
/// <remarks>
/// Reception types in order of strength:
/// <list type="bullet">
///   <item><description><b>Domicile:</b> Each in the other's ruling sign (Strongest).</description></item>
///   <item><description><b>Exaltation:</b> Each in the other's exaltation sign (Strong).</description></item>
///   <item><description><b>Mixed (Domicile-Exaltation):</b> One by domicile, other by exaltation (Strong).</description></item>
///   <item><description><b>Triplicity:</b> Each is triplicity ruler of the other's sign (Moderate).</description></item>
///   <item><description><b>Mixed (Domicile-Triplicity):</b> One by domicile, other by triplicity (Moderate).</description></item>
///   <item><description><b>Mixed (Exaltation-Triplicity):</b> One by exaltation, other by triplicity (Moderate).</description></item>
/// </list>
/// </remarks>
public sealed class MutualReception
{
    /// <summary>First planet in the reception pair.</summary>
    public required string Planet1 { get; init; }

    /// <summary>Sign occupied by the first planet.</summary>
    public required ZodiacSign Planet1Sign { get; init; }

    /// <summary>Second planet in the reception pair.</summary>
    public required string Planet2 { get; init; }

    /// <summary>Sign occupied by the second planet.</summary>
    public required ZodiacSign Planet2Sign { get; init; }

    /// <summary>
    /// The type of mutual reception: "Domicile", "Exaltation", "Triplicity",
    /// "Mixed (Domicile-Exaltation)", "Mixed (Domicile-Triplicity)", or
    /// "Mixed (Exaltation-Triplicity)".
    /// Defaults to "Domicile" for backward compatibility with existing data.
    /// </summary>
    public string ReceptionType { get; init; } = "Domicile";

    /// <summary>
    /// The strength tier of the reception: "Strongest" (domicile),
    /// "Strong" (exaltation or mixed with domicile), "Moderate" (triplicity
    /// or mixed with triplicity).
    /// </summary>
    public string Strength => ReceptionType switch
    {
        "Domicile" => "Strongest",
        "Exaltation" => "Strong",
        "Mixed (Domicile-Exaltation)" => "Strong",
        "Mixed (Domicile-Triplicity)" => "Moderate",
        "Triplicity" => "Moderate",
        "Mixed (Exaltation-Triplicity)" => "Moderate",
        _ => "Unknown",
    };

    /// <summary>
    /// Human-readable notation (e.g., "Mars in Cancer ↔ Moon in Aries (Domicile)").
    /// </summary>
    public string Notation =>
        $"{Planet1} in {Planet1Sign} \u2194 {Planet2} in {Planet2Sign} ({ReceptionType})";
}
